using System;
using System.Drawing;

namespace MinorShift.Emuera.UI.Game;

internal delegate void ConsoleToolTipDrawHandler(object sender, ConsoleToolTipDrawEventArgs e);
internal delegate void ConsoleToolTipPopupHandler(object sender, ConsoleToolTipPopupEventArgs e);

internal sealed class ConsoleToolTipDrawEventArgs : EventArgs
{
	public ConsoleToolTipDrawEventArgs(Graphics graphics, string toolTipText, Rectangle bounds, Action drawBackground, Action drawBorder)
	{
		Graphics = graphics;
		ToolTipText = toolTipText;
		Bounds = bounds;
		this.drawBackground = drawBackground;
		this.drawBorder = drawBorder;
	}

	public Graphics Graphics { get; }
	public string ToolTipText { get; }
	public Rectangle Bounds { get; }

	public void DrawBackground() { drawBackground(); }
	public void DrawBorder() { drawBorder(); }

	private readonly Action drawBackground;
	private readonly Action drawBorder;
}

internal sealed class ConsoleToolTipPopupEventArgs : EventArgs
{
	public ConsoleToolTipPopupEventArgs(object associatedControl, string toolTipText, Size toolTipSize)
	{
		AssociatedControl = associatedControl;
		ToolTipText = toolTipText;
		ToolTipSize = toolTipSize;
	}

	public object AssociatedControl { get; }
	public string ToolTipText { get; }
	public Size ToolTipSize { get; set; }
}

internal interface IConsoleWindowHost
{
	int ClientAreaWidth { get; }
	int ClientAreaHeight { get; }
	int ScrollValue { get; set; }
	int ScrollMaximum { get; set; }
	bool ScrollEnabled { get; set; }
	string InputText { get; set; }
	int InputBackColorArgb { get; set; }
	bool Created { get; }
	bool TextBoxIgnoreScrollBarChanges { get; set; }
	bool TextBoxPosChanged { get; }
	string Text { get; set; }

	void ApplyTextBoxChanges();
	void ResetTextBoxPos();
	void SetTextBoxPos(int xOffset, int yOffset, int width);
	void ChangeTextBox(string text);
	Point GetMousePositionInClient();
	bool IsPointInClient(Point point);
	void RemoveAllToolTips();
	int ToolTipInitialDelay { get; set; }
	void ShowToolTip(string title, Point point, int duration);
	bool ToolTipOwnerDraw { get; set; }
	void AddToolTipDrawHandler(ConsoleToolTipDrawHandler handler);
	void RemoveToolTipDrawHandler(ConsoleToolTipDrawHandler handler);
	void AddToolTipPopupHandler(ConsoleToolTipPopupHandler handler);
	void RemoveToolTipPopupHandler(ConsoleToolTipPopupHandler handler);
	int ToolTipForeColorArgb { get; set; }
	int ToolTipBackColorArgb { get; set; }
	int ToolTipAutoPopDelay { get; set; }
	void HotkeyStateSet(nint key, nint value);
	void HotkeyStateInit(nint key);
	void update_lastinput();
	void clear_richText();
	void Reboot();
	void ShowConfigDialog();
	void Close();
	bool Focus();
	void Refresh();
	object Invoke(Delegate method);
}
