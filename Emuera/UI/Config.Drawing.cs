using System;
using System.Drawing;
using System.Reflection;

namespace MinorShift.Emuera.Runtime.Config;

internal static partial class Config
{
	public static Color ForeColor => Color.FromArgb(ForeColorRuntime.R, ForeColorRuntime.G, ForeColorRuntime.B);
	public static Color BackColor => Color.FromArgb(BackColorRuntime.R, BackColorRuntime.G, BackColorRuntime.B);
	public static Color FocusColor => Color.FromArgb(FocusColorRuntime.R, FocusColorRuntime.G, FocusColorRuntime.B);
	public static Color LogColor => Color.FromArgb(LogColorRuntime.R, LogColorRuntime.G, LogColorRuntime.B);
	public static Color RikaiColorBack => Color.FromArgb(RikaiColorBackRuntime.R, RikaiColorBackRuntime.G, RikaiColorBackRuntime.B);
	public static Color RikaiColorText => Color.FromArgb(RikaiColorTextRuntime.R, RikaiColorTextRuntime.G, RikaiColorTextRuntime.B);
	public static Font DefaultFont => ResolveDefaultFont();

	static Font ResolveDefaultFont()
	{
		try
		{
			var fontFactoryType = Type.GetType("MinorShift.Emuera." + "UI.FontFactory, Emuera", throwOnError: false);
			var getFont = fontFactoryType?.GetMethod(
				"GetFont",
				BindingFlags.Public | BindingFlags.Static,
				binder: null,
				types: [typeof(string), typeof(FontStyle)],
				modifiers: null);
			if (getFont?.Invoke(null, ["", FontStyle.Regular]) is Font resolvedFont)
				return resolvedFont;
		}
		catch
		{
		}

		return new Font(FontFamily.GenericSansSerif, Math.Max(8, FontSize), FontStyle.Regular);
	}
}
