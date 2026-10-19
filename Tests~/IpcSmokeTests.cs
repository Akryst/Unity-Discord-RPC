using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using DiscordRPC.IO;
using DiscordRPC.Logging;
using DiscordRPC.RPC;
using DiscordRPC.Helper;

internal static class IpcSmokeTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void WaitFor(Func<bool> condition, string message)
    {
        Check(SpinWait.SpinUntil(condition, 5000), message);
    }

    private static byte[] Frame(string message)
    {
        using (var memory = new MemoryStream())
        {
            new PipeFrame { Opcode = Opcode.Frame, Data = Encoding.UTF8.GetBytes(message) }.WriteStream(memory);
            return memory.ToArray();
        }
    }

    private static void ExerciseStream(ManagedNamedPipeClient client, Stream server)
    {
        var first = Frame("fragmented");
        server.Write(first, 0, 3);
        Thread.Sleep(50);
        Check(!client.ReadFrame(out _), "A partial header must not produce a frame.");
        server.Write(first, 3, 6);
        Thread.Sleep(50);
        Check(!client.ReadFrame(out _), "A partial payload must not produce a frame.");
        var remainder = first.Skip(9).Concat(Frame("second")).Concat(Frame("")).ToArray();
        server.Write(remainder, 0, remainder.Length);

        var frames = new List<PipeFrame>();
        WaitFor(() =>
        {
            while (client.ReadFrame(out var frame)) frames.Add(frame);
            return frames.Count == 3;
        }, "Expected all fragmented and combined frames.");
        Check(frames[0].Message == "fragmented" && frames[1].Message == "second" && frames[2].Message == "", "Frames changed or arrived out of order.");
        Check(client.WriteFrame(new PipeFrame { Opcode = Opcode.Frame, Data = Encoding.UTF8.GetBytes("reply") }), "Client write failed.");
        var expected = Frame("reply");
        var actual = new byte[expected.Length];
        server.ReadTimeout = 5000;
        int offset = 0;
        while (offset < actual.Length)
        {
            int count = server.Read(actual, offset, actual.Length - offset);
            Check(count > 0, "Unexpected EOF in reply.");
            offset += count;
        }
        Check(actual.SequenceEqual(expected), "Client reply differs from its wire format.");
    }

    private static bool ConnectTo(ManagedNamedPipeClient client, string path)
    {
        bool connected = (bool)typeof(ManagedNamedPipeClient).GetMethod("AttemptConnection", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(client, new object[] { path });
        if (connected)
            typeof(ManagedNamedPipeClient).GetMethod("BeginReadStream", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(client, null);
        return connected;
    }

    private static void UnixTransport()
    {
        string directory = Path.Combine(Path.GetTempPath(), "uerp-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "discord-ipc-9");
        string oldRuntimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        try
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", directory);
            using (var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            using (var client = new ManagedNamedPipeClient())
            {
                listener.Bind(new UnixDomainSocketEndPoint(path));
                listener.Listen(1);
                bool connected = OperatingSystem.IsWindows() ? ConnectTo(client, path) : client.Connect(9);
                Check(connected, "Could not connect to Unix socket.");
                Console.WriteLine("Unix socket connected.");
                Check(client.ConnectedPipe == 9, "Wrong pipe index.");
                using (var server = new NetworkStream(listener.Accept(), true))
                    ExerciseStream(client, server);
                WaitFor(() => !client.IsConnected, "EOF must disconnect the client.");
                Console.WriteLine("Unix framing, writes and EOF passed.");

                connected = OperatingSystem.IsWindows() ? ConnectTo(client, path) : client.Connect(9);
                Check(connected, "Reconnect failed.");
                Console.WriteLine("Unix socket reconnected.");
                using (var server = new NetworkStream(listener.Accept(), true))
                {
                    var invalidHeader = BitConverter.GetBytes((uint)Opcode.Frame).Concat(BitConverter.GetBytes((uint)PipeFrame.MAX_SIZE + 1)).ToArray();
                    server.Write(invalidHeader, 0, invalidHeader.Length);
                    WaitFor(() => !client.IsConnected, "Oversized frames must disconnect.");
                }
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", oldRuntimeDirectory);
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    private static void WindowsTransport()
    {
        string name = "uerp-" + Guid.NewGuid().ToString("N") + "-9";
        using (var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
        using (var client = new ManagedNamedPipeClient())
        {
            var connection = server.WaitForConnectionAsync();
            Console.WriteLine("Connecting Windows named pipe.");
            Check(ConnectTo(client, name), "Windows named-pipe connection failed.");
            Console.WriteLine("Windows named pipe connected.");
            Check(connection.Wait(5000), "Server connection timed out.");
            Console.WriteLine("Windows server accepted.");
            // Named pipes do not expose stream timeouts.
            var first = Frame("windows");
            server.Write(first, 0, first.Length);
            Console.WriteLine("Windows server sent frame.");
            PipeFrame received = default;
            WaitFor(() => client.ReadFrame(out received), "Windows reply timed out.");
            Console.WriteLine("Windows client received frame.");
            Check(received.Message == "windows", "Windows transport corrupted the frame.");
            var buffer = new byte[Frame("reply").Length];
            var read = server.ReadAsync(buffer, 0, buffer.Length);
            Check(client.WriteFrame(new PipeFrame { Opcode = Opcode.Frame, Data = Encoding.UTF8.GetBytes("reply") }), "Windows client write failed.");
            Check(read.Wait(5000) && read.Result == buffer.Length && buffer.SequenceEqual(Frame("reply")), "Windows reply differs.");
            Console.WriteLine("Windows framing and writes passed.");
        }
    }

    private static void Locations()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var directories = (IEnumerable<string>)typeof(PipeLocation).GetMethod("TemporaryDirectories", flags).Invoke(null, null);
        Check(directories.Contains("/tmp") && !directories.Contains("/temp"), "Wrong Unix fallback directory.");
        string oldRuntimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        try
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", "/run/user/1000");
            var paths = ((IEnumerable<string>)typeof(PipeLocation).GetMethod("GetUnixPipes", flags).Invoke(null, new object[] { 9 })).ToArray();
            Check(paths.Contains(Path.Combine("/run/user/1000", "discord-ipc-9")), "Native Discord path missing.");
            Check(paths.Contains(Path.Combine("/run/user/1000", "app/com.discordapp.Discord/", "discord-ipc-9")), "Flatpak path missing.");
            Check(paths.Contains(Path.Combine("/run/user/1000", "snap.discord/", "discord-ipc-9")), "Snap path missing.");
            Check(PipeLocation.GetPipes(9).All(path => path.EndsWith("-9")), "Pipe index exceeded 9.");
        }
        finally { Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", oldRuntimeDirectory); }
    }

    private static void Main()
    {
        UnavailableDiscord();
        Locations();
        Console.WriteLine("IPC locations passed.");
        UnixTransport();
        Console.WriteLine("Unix transport passed.");
        if (OperatingSystem.IsWindows()) WindowsTransport();
        Console.WriteLine("PASS: unavailable Discord retries without repeated warnings/errors, IPC locations, Unix sockets, fragmented/combined frames, writes, EOF, reconnect, oversized frames" + (OperatingSystem.IsWindows() ? ", Windows named pipes." : "."));
    }

    private static void UnavailableDiscord()
    {
        var pipe = new UnavailablePipe();
        var logger = new CountingLogger();
        using (var connection = new RpcConnection("1", 1, -1, pipe))
        {
            connection.Logger = logger;
            typeof(RpcConnection).GetField("delay", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(connection, new BackoffDelay(1, 1));
            Check(connection.AttemptConnection(), "Retry worker did not start.");
            WaitFor(() => Volatile.Read(ref pipe.Attempts) >= 3, "Unavailable Discord must retry.");
        }
        WaitFor(() => pipe.Disposed, "Retry worker did not stop.");
        Check(logger.Warnings == 1 && logger.Errors == 0, "Unavailable Discord must produce one warning and no errors.");
    }

    private sealed class UnavailablePipe : INamedPipeClient
    {
        public int Attempts;
        public volatile bool Disposed;
        public ILogger Logger { get; set; }
        public bool IsConnected => false;
        public int ConnectedPipe => -1;
        public bool Connect(int pipe) { Interlocked.Increment(ref Attempts); return false; }
        public bool ReadFrame(out PipeFrame frame) { frame = default; return false; }
        public bool WriteFrame(PipeFrame frame) => false;
        public void Close() { }
        public void Dispose() { Disposed = true; }
    }

    private sealed class CountingLogger : ILogger
    {
        public int Warnings;
        public int Errors;
        public LogLevel Level { get; set; } = LogLevel.Trace;
        public void Trace(string message, params object[] args) { }
        public void Info(string message, params object[] args) { }
        public void Warning(string message, params object[] args) { Interlocked.Increment(ref Warnings); }
        public void Error(string message, params object[] args) { Interlocked.Increment(ref Errors); }
    }
}
