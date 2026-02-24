using System;
using System.Diagnostics;
using System.IO;

namespace MinorShift.Emuera.UI.Game;

internal sealed class LinuxLauncherUiAppHost : IUiAppHost
{
	public string Name => "linux-launcher";

	public bool TryRun(string[] args, object appIcon)
	{
		if (!OperatingSystem.IsLinux())
			return false;

		var target = ResolveLauncherPath();
		if (string.IsNullOrWhiteSpace(target))
			return false;

		try
		{
			var psi = BuildStartInfo(target, args);
			Process.Start(psi);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static ProcessStartInfo BuildStartInfo(string targetPath, string[] args)
	{
		var fullPath = Path.GetFullPath(targetPath);
		var workingDir = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory;

		if (fullPath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
		{
			var scriptPsi = new ProcessStartInfo
			{
				FileName = "/usr/bin/env",
				WorkingDirectory = workingDir,
				UseShellExecute = false,
			};
			scriptPsi.ArgumentList.Add("bash");
			scriptPsi.ArgumentList.Add(fullPath);
			AppendArgs(scriptPsi.ArgumentList, args);
			return scriptPsi;
		}

		var psi = new ProcessStartInfo
		{
			FileName = fullPath,
			WorkingDirectory = workingDir,
			UseShellExecute = false,
		};
		AppendArgs(psi.ArgumentList, args);
		return psi;
	}

	private static void AppendArgs(System.Collections.ObjectModel.Collection<string> argumentList, string[] args)
	{
		if (args != null && args.Length > 0)
		{
			foreach (var arg in args)
				argumentList.Add(arg);
			return;
		}

		argumentList.Add("--play-like");
	}

	private static string ResolveLauncherPath()
	{
		var envPath = Environment.GetEnvironmentVariable("EMUERA_LINUX_UI_LAUNCHER");
		if (!string.IsNullOrWhiteSpace(envPath))
		{
			var candidate = Path.IsPathRooted(envPath)
				? Path.GetFullPath(envPath)
				: Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, envPath));
			if (File.Exists(candidate))
				return candidate;
		}

		var baseDir = AppContext.BaseDirectory;
		string[] candidates =
		{
			Path.Combine(baseDir, "Run-Emuera-Linux.sh"),
			Path.Combine(baseDir, "Emuera.Cli"),
		};

		foreach (var candidate in candidates)
		{
			if (File.Exists(candidate))
				return candidate;
		}

		return string.Empty;
	}
}
