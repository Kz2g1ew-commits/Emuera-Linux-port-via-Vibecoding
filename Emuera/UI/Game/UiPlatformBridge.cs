using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.UI;
using MinorShift.Emuera.Runtime.Utils.EvilMask;

namespace MinorShift.Emuera.UI.Game;

internal static class UiPlatformBridge
{
	private static IUiPlatformBackend backend = CreateDefaultBackend();

	private static IUiPlatformBackend CreateDefaultBackend()
	{
		var requested = Environment.GetEnvironmentVariable("EMUERA_UI_BACKEND");
		if (!string.IsNullOrEmpty(requested))
		{
			if (requested.Equals("headless", StringComparison.OrdinalIgnoreCase))
				return new HeadlessUiPlatformBackend();
			if (requested.Equals("winforms", StringComparison.OrdinalIgnoreCase))
				return new WinFormsUiPlatformBackend();
			if (requested.Equals("linux-shell", StringComparison.OrdinalIgnoreCase) ||
				requested.Equals("linux", StringComparison.OrdinalIgnoreCase) ||
				requested.Equals("zenity", StringComparison.OrdinalIgnoreCase))
				return new LinuxShellUiPlatformBackend();
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return new WinFormsUiPlatformBackend();
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			return new LinuxShellUiPlatformBackend();
		return new HeadlessUiPlatformBackend();
	}

	public static string BackendName => backend.GetType().Name;

	public static IUiPlatformBackend Backend
	{
		get { return backend; }
		set { backend = value ?? throw new ArgumentNullException(nameof(value)); }
	}

	public static int MeasureTextNoPaddingNoPrefix(Graphics graphics, ReadOnlySpan<char> text, Font font, Size layoutSize)
	{
		return backend.MeasureTextNoPaddingNoPrefix(graphics, text, font, layoutSize);
	}

	public static void DrawTextNoPrefix(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color)
	{
		backend.DrawTextNoPrefix(graphics, text, font, point, color);
	}

	public static void DrawTextNoPrefixWithBackColor(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color, Color backColor)
	{
		backend.DrawTextNoPrefixWithBackColor(graphics, text, font, point, color, backColor);
	}

	public static void DrawTextNoPrefixPreserveClip(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color)
	{
		backend.DrawTextNoPrefixPreserveClip(graphics, text, font, point, color);
	}

	public static void DoEvents()
	{
		backend.DoEvents();
	}

	public static Point GetMousePositionInClient(object control)
	{
		return backend.GetMousePositionInClient(control);
	}

	public static bool IsPointInClient(object control, Point point)
	{
		return backend.IsPointInClient(control, point);
	}

	public static Point GetCursorPosition()
	{
		return backend.GetCursorPosition();
	}

	public static int GetCursorHeight()
	{
		return backend.GetCursorHeight();
	}

	public static int GetWorkingAreaHeightForPoint(Point point)
	{
		return backend.GetWorkingAreaHeightForPoint(point);
	}

	public static bool IsAnyFormActive()
	{
		return backend.IsAnyFormActive();
	}

	public static short GetKeyState(int keyCode)
	{
		return backend.GetKeyState(keyCode);
	}

	public static bool TryParseKeyCode(string keyName, out int keyCode)
	{
		return backend.TryParseKeyCode(keyName, out keyCode);
	}

	public static void SetClipboardText(string text)
	{
		backend.SetClipboardText(text);
	}

	public static void ShowInfo(string message)
	{
		backend.ShowInfo(message);
	}

	public static void ShowInfo(string message, string caption)
	{
		backend.ShowInfo(message, caption);
	}

	public static bool ConfirmYesNo(string message, string caption)
	{
		return backend.ConfirmYesNo(message, caption);
	}

	public static void DrawText(Graphics graphics, string text, Font font, Rectangle bounds, Color foreColor, Color backColor, long format)
	{
		backend.DrawText(graphics, text, font, bounds, foreColor, backColor, format);
	}

	public static Size MeasureText(string text, Font font, Size proposedSize, long format)
	{
		return backend.MeasureText(text, font, proposedSize, format);
	}

	public static bool TryShowRikaiIndexDialog(byte[] edict, Action<byte[]> onIndexReady)
	{
		return backend.TryShowRikaiIndexDialog(edict, onIndexReady);
	}

	public static IUiDebugDialogHandle TryCreateDebugDialog(IDebugDialogConsoleHost console, MinorShift.Emuera.Runtime.Script.IDebugRuntimeProcess process)
	{
		return backend.TryCreateDebugDialog(console, process);
	}

	public static int? ResolveNamedColorRgb(string colorName)
	{
		if (string.IsNullOrWhiteSpace(colorName))
			return null;

		var color = Color.FromName(colorName);
		return color.A == 0 ? null : color.ToArgb() & 0xFFFFFF;
	}

	public static IReadOnlyList<string> GetInstalledFontNames()
	{
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		try
		{
			foreach (FontFamily family in FontFamily.Families)
			{
				names.Add(family.Name);
			}
		}
		catch
		{
			// Some non-Windows hosts may not expose system font enumeration.
		}

		foreach (FontFamily family in FontRegistry.Collection.Families)
		{
			names.Add(family.Name);
		}

		var ordered = names.ToList();
		ordered.Sort(StringComparer.OrdinalIgnoreCase);
		return ordered;
	}

	public static bool IsFontInstalled(string fontName)
	{
		if (string.IsNullOrWhiteSpace(fontName))
			return false;

		foreach (var name in GetInstalledFontNames())
		{
			if (string.Equals(name, fontName, StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	public static bool TryCreateFont(string fontName, long fontSize, FontStyle style, out Font font)
	{
		font = null;
		if (string.IsNullOrWhiteSpace(fontName) || fontSize <= 0 || fontSize > int.MaxValue)
			return false;

		try
		{
			foreach (FontFamily family in FontRegistry.Collection.Families)
			{
				if (family.Name == fontName)
				{
					font = new Font(family, (int)fontSize, style, GraphicsUnit.Pixel);
					return true;
				}
			}

			font = new Font(fontName, (int)fontSize, style, GraphicsUnit.Pixel);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static object LoadConfiguredIcon(string configuredPath)
	{
		if (string.IsNullOrWhiteSpace(configuredPath))
			return null;

		var validPath = Utils.GetValidPath(configuredPath);
		if (string.IsNullOrWhiteSpace(validPath))
			return null;

		var bitmap = Utils.LoadImage(validPath);
		if (bitmap == null)
			return null;

		return Utils.MakeIconFromBmpFile(bitmap);
	}
}
