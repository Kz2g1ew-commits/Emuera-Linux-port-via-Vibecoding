using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Utils;

namespace MinorShift.Emuera.UI.Game;

internal sealed class LinuxShellUiPlatformBackend : IUiPlatformBackend
{
	private readonly HeadlessUiPlatformBackend fallback = new();

	private static bool IsInteractiveConsole =>
		!Console.IsInputRedirected &&
		!Console.IsOutputRedirected &&
		!Console.IsErrorRedirected;

	public int MeasureTextNoPaddingNoPrefix(Graphics graphics, ReadOnlySpan<char> text, Font font, Size layoutSize)
	{
		return fallback.MeasureTextNoPaddingNoPrefix(graphics, text, font, layoutSize);
	}

	public void DrawTextNoPrefix(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color)
	{
		fallback.DrawTextNoPrefix(graphics, text, font, point, color);
	}

	public void DrawTextNoPrefixWithBackColor(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color, Color backColor)
	{
		fallback.DrawTextNoPrefixWithBackColor(graphics, text, font, point, color, backColor);
	}

	public void DrawTextNoPrefixPreserveClip(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color)
	{
		fallback.DrawTextNoPrefixPreserveClip(graphics, text, font, point, color);
	}

	public void DoEvents()
	{
		// Linux shell backend does not own UI message loop.
	}

	public Point GetMousePositionInClient(object control)
	{
		return Point.Empty;
	}

	public bool IsPointInClient(object control, Point point)
	{
		return false;
	}

	public Point GetCursorPosition()
	{
		return Point.Empty;
	}

	public int GetCursorHeight()
	{
		return 0;
	}

	public int GetWorkingAreaHeightForPoint(Point point)
	{
		return 0;
	}

	public bool IsAnyFormActive()
	{
		return false;
	}

	public short GetKeyState(int keyCode)
	{
		return 0;
	}

	public bool TryParseKeyCode(string keyName, out int keyCode)
	{
		if (VirtualKeyMap.TryParseKeyName(keyName, out keyCode))
			return true;
		keyCode = 0;
		return false;
	}

	public void SetClipboardText(string text)
	{
		if (string.IsNullOrEmpty(text))
			return;
		if (TryPipeClipboard("wl-copy", string.Empty, text))
			return;
		if (TryPipeClipboard("xclip", "-selection clipboard", text))
			return;
		if (TryPipeClipboard("xsel", "--clipboard --input", text))
			return;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && TryPipeClipboard("pbcopy", string.Empty, text))
			return;

		Console.Error.WriteLine("[emuera] Clipboard helper not available (wl-copy/xclip/xsel/pbcopy).");
	}

	public void ShowInfo(string message)
	{
		if (TryZenityInfo(message, caption: "Emuera"))
			return;
		Console.Error.WriteLine($"[emuera] {message}");
	}

	public void ShowInfo(string message, string caption)
	{
		if (TryZenityInfo(message, caption))
			return;
		Console.Error.WriteLine($"[{caption}] {message}");
	}

	public bool ConfirmYesNo(string message, string caption)
	{
		if (TryZenityQuestion(message, caption, out bool answer))
			return answer;

		Console.Error.WriteLine($"[{caption}] {message}");
		if (!IsInteractiveConsole)
		{
			Console.Error.WriteLine("[emuera] Non-interactive mode: defaulting to 'No'.");
			return false;
		}

		Console.Error.Write("[emuera] Continue? [y/N]: ");
		var input = Console.ReadLine();
		if (string.IsNullOrWhiteSpace(input))
			return false;
		return input.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) ||
			input.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
	}

	public void DrawText(Graphics graphics, string text, Font font, Rectangle bounds, Color foreColor, Color backColor, long format)
	{
		fallback.DrawText(graphics, text, font, bounds, foreColor, backColor, format);
	}

	public Size MeasureText(string text, Font font, Size proposedSize, long format)
	{
		return fallback.MeasureText(text, font, proposedSize, format);
	}

	public bool TryShowRikaiIndexDialog(byte[] edict, Action<byte[]> onIndexReady)
	{
		if (edict == null || onIndexReady == null)
			return false;

		var outputPath = Path.Combine(Program.ExeDir, Config.RikaiFilename + ".ind");
		if (!RikaiIndexGenerator.TryGenerateAndSave(
			edict,
			outputPath,
			ReportRikaiProgress,
			out var edictIndex,
			out var errorMessage))
		{
			Console.Error.WriteLine($"[emuera] Failed to generate {Config.RikaiFilename}.ind: {errorMessage}");
			return false;
		}

		onIndexReady(edictIndex);
		return true;
	}

	public IUiDebugDialogHandle TryCreateDebugDialog(IDebugDialogConsoleHost console, MinorShift.Emuera.Runtime.Script.IDebugRuntimeProcess process)
	{
		if (console == null || process == null)
			return null;
		return new TextDebugDialogHandle(console, "linux-shell");
	}

	private static bool TryZenityInfo(string message, string caption)
	{
		if (!OperatingSystem.IsLinux())
			return false;
		var zenity = FindExecutableInPath("zenity");
		if (string.IsNullOrWhiteSpace(zenity))
			return false;

		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = zenity,
				UseShellExecute = false,
				RedirectStandardError = true,
			};
			psi.ArgumentList.Add("--info");
			if (!string.IsNullOrWhiteSpace(caption))
			{
				psi.ArgumentList.Add("--title");
				psi.ArgumentList.Add(caption);
			}
			psi.ArgumentList.Add("--text");
			psi.ArgumentList.Add(message ?? string.Empty);
			using var p = Process.Start(psi);
			if (p == null)
				return false;
			p.WaitForExit();
			return p.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryZenityQuestion(string message, string caption, out bool answer)
	{
		answer = false;
		if (!OperatingSystem.IsLinux())
			return false;
		var zenity = FindExecutableInPath("zenity");
		if (string.IsNullOrWhiteSpace(zenity))
			return false;

		try
		{
			var psi = new ProcessStartInfo
			{
				FileName = zenity,
				UseShellExecute = false,
				RedirectStandardError = true,
			};
			psi.ArgumentList.Add("--question");
			if (!string.IsNullOrWhiteSpace(caption))
			{
				psi.ArgumentList.Add("--title");
				psi.ArgumentList.Add(caption);
			}
			psi.ArgumentList.Add("--text");
			psi.ArgumentList.Add(message ?? string.Empty);
			using var p = Process.Start(psi);
			if (p == null)
				return false;
			p.WaitForExit();
			answer = p.ExitCode == 0;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string FindExecutableInPath(string name)
	{
		var path = Environment.GetEnvironmentVariable("PATH");
		if (string.IsNullOrWhiteSpace(path))
			return string.Empty;

		foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			var candidate = Path.Combine(dir, name);
			if (File.Exists(candidate))
				return candidate;
		}

		return string.Empty;
	}

	private static void ReportRikaiProgress(int progress)
	{
		if (progress < 0 || progress > 100)
			return;
		if (progress != 100 && progress % 20 != 0)
			return;
		Console.Error.WriteLine($"[emuera] Generating {Config.RikaiFilename}.ind... {progress}%");
	}

	private static bool TryPipeClipboard(string fileName, string arguments, string text)
	{
		try
		{
			using var process = Process.Start(new ProcessStartInfo
			{
				FileName = fileName,
				Arguments = arguments,
				UseShellExecute = false,
				RedirectStandardInput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			});
			if (process == null)
				return false;

			process.StandardInput.Write(text);
			process.StandardInput.Close();
			process.WaitForExit(1000);
			return process.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}
}
