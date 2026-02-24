using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Utils;

namespace MinorShift.Emuera.UI.Game;

internal sealed class HeadlessUiPlatformBackend : IUiPlatformBackend
{
	private static bool IsInteractiveConsole =>
		!Console.IsInputRedirected &&
		!Console.IsOutputRedirected &&
		!Console.IsErrorRedirected;

	public int MeasureTextNoPaddingNoPrefix(Graphics graphics, ReadOnlySpan<char> text, Font font, Size layoutSize)
	{
		var proposedWidth = layoutSize.Width > 0 ? layoutSize.Width : int.MaxValue;
		return EstimateTextSize(text, font, proposedWidth).Width;
	}

	public void DrawTextNoPrefix(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color)
	{
		// Headless backend intentionally skips drawing work.
	}

	public void DrawTextNoPrefixWithBackColor(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color, Color backColor)
	{
		// Headless backend intentionally skips drawing work.
	}

	public void DrawTextNoPrefixPreserveClip(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color)
	{
		// Headless backend intentionally skips drawing work.
	}

	public void DoEvents()
	{
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
		Console.Error.WriteLine($"[emuera] {message}");
	}

	public void ShowInfo(string message, string caption)
	{
		Console.Error.WriteLine($"[{caption}] {message}");
	}

	public bool ConfirmYesNo(string message, string caption)
	{
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
		// Headless backend intentionally skips drawing work.
	}

	public Size MeasureText(string text, Font font, Size proposedSize, long format)
	{
		var proposedWidth = proposedSize.Width > 0 ? proposedSize.Width : int.MaxValue;
		return EstimateTextSize(text.AsSpan(), font, proposedWidth);
	}

	public bool TryShowRikaiIndexDialog(byte[] edict, Action<byte[]> onIndexReady)
	{
		if (edict == null || onIndexReady == null)
			return false;

		var outputPath = Path.Combine(Program.ExeDir, Config.RikaiFilename + ".ind");
		if (!RikaiIndexGenerator.TryGenerateAndSave(
			edict,
			outputPath,
			static _ => { },
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
		return new TextDebugDialogHandle(console, "headless");
	}

	private static Size EstimateTextSize(ReadOnlySpan<char> text, Font font, int proposedWidth)
	{
		var scale = GetGlyphWidthScale(font);
		var lineHeight = GetLineHeight(font);

		var maxRawWidth = 0;
		var currentRawWidth = 0;
		var lineCount = 1;

		foreach (var ch in text)
		{
			if (ch == '\r')
				continue;

			if (ch == '\n')
			{
				maxRawWidth = Math.Max(maxRawWidth, currentRawWidth);
				currentRawWidth = 0;
				lineCount++;
				continue;
			}

			currentRawWidth += EstimateGlyphWeight(ch);
		}

		maxRawWidth = Math.Max(maxRawWidth, currentRawWidth);

		var width = (int)Math.Ceiling(maxRawWidth * scale);
		if (proposedWidth > 0 && proposedWidth != int.MaxValue && width > proposedWidth)
		{
			var wrapLines = (int)Math.Ceiling((double)width / proposedWidth);
			lineCount = Math.Max(1, lineCount * wrapLines);
			width = proposedWidth;
		}

		return new Size(Math.Max(0, width), Math.Max(lineHeight, lineHeight * lineCount));
	}

	private static int GetLineHeight(Font font)
	{
		var size = font?.Size;
		if (size is null || size.Value <= 0)
			return 16;
		return Math.Max(12, (int)Math.Ceiling(size.Value * 1.6f));
	}

	private static float GetGlyphWidthScale(Font font)
	{
		var size = font?.Size;
		if (size is null || size.Value <= 0)
			return 7f;
		return Math.Max(6f, size.Value * 0.62f);
	}

	private static int EstimateGlyphWeight(char ch)
	{
		if (char.IsWhiteSpace(ch))
			return 1;
		if (IsWideGlyph(ch))
			return 2;
		return 1;
	}

	private static bool IsWideGlyph(char ch)
	{
		var code = (int)ch;
		return
			(code >= 0x1100 && code <= 0x11FF) || // Hangul Jamo
			(code >= 0x2E80 && code <= 0x9FFF) || // CJK + radicals
			(code >= 0xAC00 && code <= 0xD7AF) || // Hangul syllables
			(code >= 0xF900 && code <= 0xFAFF) || // CJK compatibility ideographs
			(code >= 0xFF00 && code <= 0xFFEF);   // Fullwidth forms
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
