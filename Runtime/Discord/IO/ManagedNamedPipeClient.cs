using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DiscordRPC.Logging;
using System.IO.Pipes;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace DiscordRPC.IO
{
    /// <summary>
    /// Uses Windows named pipes or Unix domain sockets for Discord IPC.
    /// </summary>
    public sealed class ManagedNamedPipeClient : INamedPipeClient
    {
        /// <summary>
        /// The logger for the Pipe client to use
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Checks if the client is connected
        /// </summary>
        public bool IsConnected
        {
            get
            {
                //This will trigger if the stream is disabled. This should prevent the lock check
                if (_isClosed) return false;
                lock (l_stream)
                {
                    //We cannot be sure its still connected, so lets double check
                    return _stream != null && !_isClosed;
                }
            }
        }

        /// <summary>
        /// The pipe we are currently connected too.
        /// </summary>
        public int ConnectedPipe { get; private set; }

        private Stream _stream;
        private readonly List<byte> _pendingData = new List<byte>();

        private byte[] _buffer = new byte[PipeFrame.MAX_SIZE];

        private Queue<PipeFrame> _framequeue = new Queue<PipeFrame>();
        private object _framequeuelock = new object();

        private volatile bool _isDisposed = false;
        private volatile bool _isClosed = true;

        private object l_stream = new object();

        /// <summary>
        /// Creates a new instance of a Managed NamedPipe client. Doesn't connect to anything yet, just setups the values.
        /// </summary>
        public ManagedNamedPipeClient()
        {
            _buffer = new byte[PipeFrame.MAX_SIZE];
            Logger = new NullLogger();
            _stream = null;
        }

        /// <summary>
        /// Connects to the pipe
        /// </summary>
        /// <param name="pipe"></param>
        /// <returns></returns>
        public bool Connect(int pipe)
        {
            Logger.Trace("ManagedNamedPipeClient.Connection({0})", pipe);

            if (_isDisposed)
                throw new ObjectDisposedException("NamedPipe");

            if (pipe < -1 || pipe > 9)
                throw new ArgumentOutOfRangeException("pipe", "Argument must be between -1 and 9");


            int startPipe = 0;
            if (pipe >= 0)
                startPipe = pipe;

            foreach (var pipename in PipeLocation.GetPipes(startPipe).Where(name => pipe < 0 || name.EndsWith("-" + pipe)))
            {
                if (AttemptConnection(pipename))
                {
                    BeginReadStream();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts a new connection
        /// </summary>
        /// <returns></returns>
        private bool AttemptConnection(string pipename)
        {
            if (_isDisposed)
                throw new ObjectDisposedException("_stream");

            try
            {
                //Create the client
                lock (l_stream)
                {
                    Logger.Info("Attempting to connect to '{0}'", pipename);
                    if (Path.IsPathRooted(pipename))
                    {
                        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                        try
                        {
                            socket.Connect(new UnixSocketEndPoint(pipename));
                            _stream = new NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    }
                    else
                    {
                        var pipeStream = new NamedPipeClientStream(".", pipename, PipeDirection.InOut, PipeOptions.Asynchronous);
                        _stream = pipeStream;
                        pipeStream.Connect(0);
                    }
                    _pendingData.Clear();
                    _buffer = new byte[PipeFrame.MAX_SIZE];
                }

                //Store the value
                Logger.Info("Connected to '{0}'", pipename);
                ConnectedPipe = int.Parse(pipename.Substring(pipename.LastIndexOf('-') + 1));
                _isClosed = false;
            }
            catch (Exception e)
            {
                //Something happened, try again
                //TODO: Log the failure condition
                Logger.Trace("Failed connection to {0}. {1}", pipename, e.Message);
                Close();
            }

            Logger.Trace("Done. Result: {0}", _isClosed);
            return !_isClosed;
        }

        /// <summary>
        /// Starts a read. Can be executed in another thread.
        /// </summary>
        private void BeginReadStream()
        {
            if (_isClosed) return;
            try
            {
                lock (l_stream)
                {
                    //Make sure the stream is valid
                    if (_stream == null || _isClosed) return;

                    Logger.Trace("Begining Read of {0} bytes", _buffer.Length);
                    _stream.BeginRead(_buffer, 0, _buffer.Length, EndReadStream, Tuple.Create(_stream, _buffer));
                }
            }
            catch (ObjectDisposedException)
            {
                Logger.Warning("Attempted to start reading from a disposed pipe");
                return;
            }
            catch (InvalidOperationException)
            {
                //The pipe has been closed
                Logger.Warning("Attempted to start reading from a closed pipe");
                return;
            }
            catch (Exception e)
            {
                Logger.Error("An exception occured while starting to read a stream: {0}", e.Message);
                Logger.Error(e.StackTrace);
            }
        }

        /// <summary>
        /// Ends a read. Can be executed in another thread.
        /// </summary>
        /// <param name="callback"></param>
        private void EndReadStream(IAsyncResult callback)
        {
            var read = (Tuple<Stream, byte[]>)callback.AsyncState;
            lock (l_stream)
            {
                try
                {
                    int bytes = read.Item1.EndRead(callback);
                    if (!ReferenceEquals(_stream, read.Item1) || _isClosed) return;
                    if (bytes == 0)
                    {
                        Close();
                        return;
                    }

                    // Stream reads can split a frame or contain several frames.
                    _pendingData.AddRange(read.Item2.Take(bytes));
                    while (_pendingData.Count >= 8)
                    {
                        var header = _pendingData.Take(8).ToArray();
                        uint length = BitConverter.ToUInt32(header, 4);
                        if (length > PipeFrame.MAX_SIZE)
                            throw new InvalidDataException("Discord IPC frame exceeds the maximum size.");
                        int frameSize = 8 + (int)length;
                        if (_pendingData.Count < frameSize) break;
                        var frame = new PipeFrame
                        {
                            Opcode = (Opcode)BitConverter.ToUInt32(header, 0),
                            Data = _pendingData.Skip(8).Take((int)length).ToArray()
                        };
                        _pendingData.RemoveRange(0, frameSize);
                        lock (_framequeuelock)
                            _framequeue.Enqueue(frame);
                    }
                    BeginReadStream();
                }
                catch (Exception e)
                {
                    // A callback from an old connection must not close its replacement.
                    if (!ReferenceEquals(_stream, read.Item1)) return;
                    Logger.Trace("IPC read ended: {0}", e.Message);
                    Close();
                }
            }
        }

        /// <summary>
        /// Reads a frame, returning false if none are available
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public bool ReadFrame(out PipeFrame frame)
        {
            if (_isDisposed)
                throw new ObjectDisposedException("_stream");

            //Check the queue, returning the pipe if we have anything available. Otherwise null.
            lock (_framequeuelock)
            {
                if (_framequeue.Count == 0)
                {
                    //We found nothing, so just default and return null
                    frame = default(PipeFrame);
                    return false;
                }

                //Return the dequed frame
                frame = _framequeue.Dequeue();
                return true;
            }
        }

        /// <summary>
        /// Writes a frame to the pipe
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public bool WriteFrame(PipeFrame frame)
        {
            if (_isDisposed)
                throw new ObjectDisposedException("_stream");

            //Write the frame. We are assuming proper duplex connection here
            if (_isClosed || !IsConnected)
            {
                Logger.Error("Failed to write frame because the stream is closed");
                return false;
            }

            try
            {
                //Write the pipe
                //This can only happen on the main thread so it should be fine.
                frame.WriteStream(_stream);
                return true;
            }
            catch (IOException io)
            {
                Logger.Error("Failed to write frame because of a IO Exception: {0}", io.Message);
            }
            catch (ObjectDisposedException)
            {
                Logger.Warning("Failed to write frame as the stream was already disposed");
            }
            catch (InvalidOperationException)
            {
                Logger.Warning("Failed to write frame because of a invalid operation");
            }

            //We must have failed the try catch
            return false;
        }

        /// <summary>
        /// Closes the pipe
        /// </summary>
        public void Close()
        {
            Stream stream;
            lock (l_stream)
            {
                stream = _stream;
                _stream = null;
                _isClosed = true;
            }

            // Dispose outside the lock: pending read callbacks also acquire it.
            try { stream?.Dispose(); }
            catch (IOException e) { Logger.Trace("IPC close: {0}", e.Message); }
        }

        /// <summary>
        /// Disposes of the stream.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Close();
        }

        // Unity 2019's API profile does not expose UnixDomainSocketEndPoint.
        private sealed class UnixSocketEndPoint : EndPoint
        {
            private readonly byte[] path;

            public UnixSocketEndPoint(string socketPath)
            {
                path = Encoding.UTF8.GetBytes(socketPath);
                if (path.Length == 0 || path.Length > 107)
                    throw new ArgumentException("Unix socket path must contain 1 to 107 UTF-8 bytes.", nameof(socketPath));
            }

            public override AddressFamily AddressFamily => AddressFamily.Unix;

            public override SocketAddress Serialize()
            {
                var address = new SocketAddress(AddressFamily.Unix, path.Length + 3);
                for (int i = 0; i < path.Length; i++) address[i + 2] = path[i];
                return address;
            }
        }

        [System.Obsolete("Use PipePermutation.GetPipes instead", true)]
        public static string GetPipeName(int pipe)
            => string.Empty;
        [System.Obsolete("Use PipePermutation.GetPipes instead", true)]
        public static string GetPipeName(int pipe, string sandbox)
            => string.Empty;
        [System.Obsolete("Use PipePermutation.GetPipes instead", true)]
        public static string GetPipeSandbox()
            => string.Empty;
        [System.Obsolete("Use PipePermutation.GetPipes instead", true)]
        public static bool IsUnix()
            => true;
    }
}
