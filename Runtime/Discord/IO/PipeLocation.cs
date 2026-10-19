using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DiscordRPC.IO
{
	/// <summary>
	/// Provides the location of the Discord IPC pipes for the current operating system.
	/// </summary>
	public static class PipeLocation
	{
		const string DiscordPipePrefix = @"discord-ipc-";
		const int MaximumPipeVariations = 10;

		static readonly string[] LinuxPackageManagers = new string[]
		{
			"app/com.discordapp.Discord/",	// Flatpak
			"snap.discord/",				// Snap
            ".flatpak/dev.vencord.Vesktop/xdg-run" // Vesktop (Flatpak)
			// TODO: Add more package managers as needed such as AppImage
		};

		/// <summary>
		/// Generates a sequence of pipe names that Discord can use for the IPC for the current operating system.
		/// </summary>
		/// <param name="startPipe">The starting index for the pipe names.</param>
		/// <returns>An enumerable collection of pipe names.</returns>
		/// <remarks>
		/// The names are ordered and will check multiple locations for a suitable pipe. For Unix systems, multiple TEMP directories are checked and multiple package managers are considered.
		/// This means that there is not a 1 to 1 mapping of pipe names to indices, as there are many locations for a single pipe index.
		/// </remarks>	
		public static IEnumerable<string> GetPipes(int startPipe = 0)
		{
			if (startPipe < 0 || startPipe >= MaximumPipeVariations)
				throw new ArgumentOutOfRangeException(nameof(startPipe));

			if (IsOSUnix())
			{
				return Enumerable.Range(startPipe, MaximumPipeVariations - startPipe).SelectMany(GetUnixPipes).Distinct();
			}
			else
			{
				return Enumerable.Range(startPipe, MaximumPipeVariations - startPipe).SelectMany(GetWindowsPipes);
			}
		}

		private static IEnumerable<string> GetWindowsPipes(int index)
		{
			yield return $"{DiscordPipePrefix}{index}";
		}

		private static IEnumerable<string> GetUnixPipes(int index)
		{
			foreach (var tempDir in TemporaryDirectories())
			{
				// No Package Manager. First because its common for MacOS. 
				// Linux users tend to either just use a browser or a package manager.
				yield return Path.Combine(tempDir, $"{DiscordPipePrefix}{index}");

				// Package Managers
				foreach (var pmDir in LinuxPackageManagers)
					yield return Path.Combine(tempDir, pmDir, $"{DiscordPipePrefix}{index}");
			}
		}

		private static IEnumerable<string> TemporaryDirectories()
		{
			string temp;
			temp = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
			if (!string.IsNullOrEmpty(temp))
				yield return temp;

			// Desktop launchers may omit XDG_RUNTIME_DIR from Unity's environment.
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				yield return Path.Combine("/run/user", GetEffectiveUserId().ToString(CultureInfo.InvariantCulture));

			temp = Environment.GetEnvironmentVariable("TMPDIR");
			if (!string.IsNullOrEmpty(temp))
				yield return temp;

			temp = Environment.GetEnvironmentVariable("TMP");
			if (!string.IsNullOrEmpty(temp))
				yield return temp;

			temp = Environment.GetEnvironmentVariable("TEMP");
			if (!string.IsNullOrEmpty(temp))
				yield return temp;

			yield return "/tmp";
		}

		[DllImport("libc", EntryPoint = "geteuid")]
		private static extern uint GetEffectiveUserId();

		private static bool IsOSUnix()
		{
			if (Environment.OSVersion.Platform == PlatformID.Unix)
				return true;

			if (Environment.OSVersion.Platform == PlatformID.MacOSX)
				return true;

			return false;
		}
	}
}
