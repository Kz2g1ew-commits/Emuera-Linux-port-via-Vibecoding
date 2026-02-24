using Microsoft.VisualBasic;
using System.Text;

namespace MinorShift.Emuera.Runtime.Utils;

public static class StringConversionCompat
{
	private static readonly Lazy<bool> StrConvAvailable = new(static () =>
	{
		if (!OperatingSystem.IsWindows())
			return false;

		try
		{
			_ = Strings.StrConv("A", VbStrConv.Wide, 0x0411);
			return true;
		}
		catch (Exception ex) when (ex is PlatformNotSupportedException || ex is NotSupportedException)
		{
			return false;
		}
	});

	public static string ToNarrow(string str, int locale)
	{
		return TryStrConv(str, VbStrConv.Narrow, locale, static fallback => fallback.Normalize(NormalizationForm.FormKC));
	}

	public static string ToWide(string str, int locale)
	{
		return TryStrConv(str, VbStrConv.Wide, locale, ToWideFallback);
	}

	public static string ToKatakana(string str)
	{
		return TryStrConv(str, VbStrConv.Katakana, 0x0411, ToKatakanaFallback);
	}

	public static string ToHiragana(string str, bool halfToFull)
	{
		var flags = halfToFull ? VbStrConv.Hiragana | VbStrConv.Wide : VbStrConv.Hiragana;
		return TryStrConv(str, flags, 0x0411, fallback => ToHiraganaFallback(fallback, halfToFull));
	}

	private static string TryStrConv(string str, VbStrConv flags, int locale, Func<string, string> fallback)
	{
		if (string.IsNullOrEmpty(str))
			return string.Empty;

		if (!StrConvAvailable.Value)
			return fallback(str);

		try
		{
			if (!OperatingSystem.IsWindows())
				return fallback(str);
			return Strings.StrConv(str, flags, locale);
		}
		catch (Exception ex) when (ex is PlatformNotSupportedException || ex is NotSupportedException)
		{
			return fallback(str);
		}
	}

	private static string ToWideFallback(string input)
	{
		if (string.IsNullOrEmpty(input))
			return string.Empty;

		var buffer = new StringBuilder(input.Length);
		foreach (var c in input)
		{
			if (c == ' ')
			{
				buffer.Append('\u3000');
				continue;
			}

			if (c >= '!' && c <= '~')
			{
				buffer.Append((char)(c + 0xFEE0));
				continue;
			}

			buffer.Append(c);
		}

		return buffer.ToString();
	}

	private static string ToKatakanaFallback(string input)
	{
		if (string.IsNullOrEmpty(input))
			return string.Empty;

		var source = input.Normalize(NormalizationForm.FormKC);
		var buffer = new StringBuilder(source.Length);
		foreach (var c in source)
		{
			if (c >= '\u3041' && c <= '\u3096')
				buffer.Append((char)(c + 0x0060));
			else
				buffer.Append(c);
		}

		return buffer.ToString();
	}

	private static string ToHiraganaFallback(string input, bool halfToFull)
	{
		if (string.IsNullOrEmpty(input))
			return string.Empty;

		var source = halfToFull
			? input.Normalize(NormalizationForm.FormKC)
			: input.Normalize(NormalizationForm.FormKC);
		var buffer = new StringBuilder(source.Length);
		foreach (var c in source)
		{
			if (c >= '\u30A1' && c <= '\u30F6')
				buffer.Append((char)(c - 0x0060));
			else
				buffer.Append(c);
		}

		return buffer.ToString();
	}
}
