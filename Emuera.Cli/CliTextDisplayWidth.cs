using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

internal static class CliTextDisplayWidth
{
	private static readonly bool UseNativeWcWidth =
		!OperatingSystem.IsWindows() &&
		string.Equals(Environment.GetEnvironmentVariable("EMUERA_CLI_USE_NATIVE_WCWIDTH"), "1", StringComparison.OrdinalIgnoreCase);

	private static readonly bool HasNativeWcWidth = DetectNativeWcWidth();
	private static readonly bool TreatAmbiguousAsWide = ResolveAmbiguousAsWide();

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
		var intrinsicWidth = GetIntrinsicTerminalWidth(rune);
		if (intrinsicWidth == 0)
			return 0;

		if (TreatAmbiguousAsWide && IsAmbiguousWidthCandidate(value))
			return 2;

		if (TryGetNativeRuneWidth(rune, out var nativeWidth))
			return nativeWidth;

		return intrinsicWidth;
	}

	private static bool ResolveAmbiguousAsWide()
	{
		if (TryParsePositiveInt(Environment.GetEnvironmentVariable("EMUERA_CLI_AMBIGUOUS_WIDTH"), out var explicitWidth))
			return explicitWidth >= 2;

		if (TryParsePositiveInt(Environment.GetEnvironmentVariable("VTE_CJK_WIDTH"), out var vteWidth))
			return vteWidth >= 2;

		return false;
	}

	private static bool TryParsePositiveInt(string? raw, out int value)
	{
		value = 0;
		if (string.IsNullOrWhiteSpace(raw))
			return false;
		return int.TryParse(raw.Trim(), out value) && value > 0;
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

	internal static int GetIntrinsicTerminalWidth(Rune rune)
	{
		var value = rune.Value;
		if (IsControl(value))
			return 0;

		var category = Rune.GetUnicodeCategory(rune);
		if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
			return 0;

		return IsWide(value) ? 2 : 1;
	}

	internal static bool IsAmbiguousWidthCandidate(int value)
	{
		if (IsGaugeBarGlyph(value))
			return false;

		// Broad ambiguous-symbol ranges used by many ERA map/text art sets.
		// When EMUERA_CLI_AMBIGUOUS_WIDTH=2 (or VTE_CJK_WIDTH=2), these are treated as width-2.
		return
			(value >= 0x0370 && value <= 0x052F) || // Greek + Cyrillic
			(value >= 0x2000 && value <= 0x206F) || // General punctuation
			(value >= 0x2070 && value <= 0x209F) || // Super/subscripts
			(value >= 0x20A0 && value <= 0x20CF) || // Currency symbols
			(value >= 0x2100 && value <= 0x214F) || // Letterlike symbols
			(value >= 0x2150 && value <= 0x218F) || // Number forms
			(value >= 0x2190 && value <= 0x21FF) ||
			(value >= 0x2200 && value <= 0x22FF) || // Mathematical operators
			(value >= 0x2300 && value <= 0x23FF) || // Misc technical
			(value >= 0x2400 && value <= 0x245F) || // Control pictures + OCR
			(value >= 0x2460 && value <= 0x24FF) || // Enclosed alphanumerics
			(value >= 0x2500 && value <= 0x257F) ||
			(value >= 0x2580 && value <= 0x259F) ||
			(value >= 0x25A0 && value <= 0x25FF) ||
			(value >= 0x2600 && value <= 0x27BF);   // Misc symbols + dingbats
	}

	private static bool IsGaugeBarGlyph(int value)
	{
		// Keep status bars compact (e.g. ▅▅▅...) by treating block bars as single-width.
		return value >= 0x2581 && value <= 0x2588;
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
