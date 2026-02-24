using System;
using System.Drawing;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Script;

namespace MinorShift.Emuera.UI.Game;

internal interface IUiPlatformBackend
{
	int MeasureTextNoPaddingNoPrefix(Graphics graphics, ReadOnlySpan<char> text, Font font, Size layoutSize);
	void DrawTextNoPrefix(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color);
	void DrawTextNoPrefixWithBackColor(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color, Color backColor);
	void DrawTextNoPrefixPreserveClip(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color);
	void DoEvents();
	Point GetMousePositionInClient(object control);
	bool IsPointInClient(object control, Point point);
	Point GetCursorPosition();
	int GetCursorHeight();
	int GetWorkingAreaHeightForPoint(Point point);
	bool IsAnyFormActive();
	short GetKeyState(int keyCode);
	bool TryParseKeyCode(string keyName, out int keyCode);
	void SetClipboardText(string text);
	void ShowInfo(string message);
	void ShowInfo(string message, string caption);
	bool ConfirmYesNo(string message, string caption);
	void DrawText(Graphics graphics, string text, Font font, Rectangle bounds, Color foreColor, Color backColor, long format);
	Size MeasureText(string text, Font font, Size proposedSize, long format);
	bool TryShowRikaiIndexDialog(byte[] edict, Action<byte[]> onIndexReady);
	IUiDebugDialogHandle TryCreateDebugDialog(IDebugDialogConsoleHost console, IDebugRuntimeProcess process);
}
