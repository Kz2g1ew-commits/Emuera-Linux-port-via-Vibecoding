using System.Drawing;
using System.Drawing.Text;

namespace MinorShift.Emuera.UI.Game;

internal static class UiPlatformBridge
{
	public static bool TryCreateFont(string fontName, long fontSize, FontStyle style, out Font font)
	{
		font = null;
		if (string.IsNullOrWhiteSpace(fontName) || fontSize <= 0 || fontSize > int.MaxValue)
			return false;

		try
		{
			font = new Font(fontName, (int)fontSize, style, GraphicsUnit.Pixel);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool IsFontInstalled(string fontName)
	{
		if (string.IsNullOrWhiteSpace(fontName))
			return false;

		try
		{
			using InstalledFontCollection collection = new();
			foreach (FontFamily family in collection.Families)
			{
				if (string.Equals(family.Name, fontName, StringComparison.OrdinalIgnoreCase))
					return true;
			}
		}
		catch
		{
		}

		try
		{
			using Font font = new(fontName, 10f, FontStyle.Regular, GraphicsUnit.Pixel);
			return string.Equals(font.Name, fontName, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}
}
