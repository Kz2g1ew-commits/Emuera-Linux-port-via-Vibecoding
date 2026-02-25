using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

internal static class CliTextDisplayWidth
{
	private static readonly bool UseNativeWcWidth =
		!OperatingSystem.IsWindows() &&
		!string.Equals(Environment.GetEnvironmentVariable("EMUERA_CLI_USE_NATIVE_WCWIDTH"), "0", StringComparison.OrdinalIgnoreCase);

	private static readonly bool HasNativeWcWidth = DetectNativeWcWidth();

	public static int GetDisplayWidth(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return 0;

		var width = 0;
		foreach (var rune in text.EnumerateRunes())
			width += GetRuneWidth(rune);
		return width;
	}

	public static string[] SplitByDisplayWidth(string? text, int width)
	{
		var raw = text ?? string.Empty;
		if (raw.Length == 0)
			return [string.Empty, string.Empty];
		if (width <= 0)
			return [string.Empty, raw];

		var consumedWidth = 0;
		var splitAt = 0;
		foreach (var rune in raw.EnumerateRunes())
		{
			var runeWidth = GetRuneWidth(rune);
			if (consumedWidth + runeWidth > width)
				break;

			consumedWidth += runeWidth;
			splitAt += rune.Utf16SequenceLength;
		}

		if (splitAt <= 0)
			return [string.Empty, raw];
		if (splitAt >= raw.Length)
			return [raw, string.Empty];
		return [raw[..splitAt], raw[splitAt..]];
	}

	private static int GetRuneWidth(Rune rune)
	{
		var value = rune.Value;
		if (value == 0)
			return 0;
		if (IsControl(value))
			return 0;

		var category = Rune.GetUnicodeCategory(rune);
		if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
			return 0;

		if (TryGetNativeRuneWidth(rune, out var nativeWidth))
			return nativeWidth;

		if (IsWide(value))
			return 2;
		return 1;
	}

	private static bool DetectNativeWcWidth()
	{
		if (!UseNativeWcWidth)
			return false;
		try
		{
			_ = NativeWcWidth('A');
			return true;
		}
		catch (DllNotFoundException)
		{
			return false;
		}
		catch (EntryPointNotFoundException)
		{
			return false;
		}
	}

	private static bool TryGetNativeRuneWidth(Rune rune, out int width)
	{
		width = 0;
		if (!HasNativeWcWidth)
			return false;

		var result = NativeWcWidth(rune.Value);
		width = result < 0 ? 0 : result;
		return true;
	}

	[DllImport("libc", EntryPoint = "wcwidth")]
	private static extern int NativeWcWidth(int codePoint);

	private static bool IsControl(int value)
	{
		return (value >= 0 && value < 0x20) || (value >= 0x7F && value < 0xA0);
	}

	private static bool IsWide(int value)
	{
		// Adapted for terminal rendering behavior on Linux/macOS CJK locales.
		return
			(value >= 0x1100 && value <= 0x115F) ||
			value == 0x2329 ||
			value == 0x232A ||
			(value >= 0x2E80 && value <= 0x303E) ||
			(value >= 0x3040 && value <= 0xA4CF) ||
			(value >= 0xAC00 && value <= 0xD7A3) ||
			(value >= 0xF900 && value <= 0xFAFF) ||
			(value >= 0xFE10 && value <= 0xFE19) ||
			(value >= 0xFE30 && value <= 0xFE6F) ||
			(value >= 0xFF00 && value <= 0xFF60) ||
			(value >= 0xFFE0 && value <= 0xFFE6) ||
			(value >= 0x1F300 && value <= 0x1FAFF) ||
			(value >= 0x20000 && value <= 0x2FFFD) ||
			(value >= 0x30000 && value <= 0x3FFFD);
	}
}
