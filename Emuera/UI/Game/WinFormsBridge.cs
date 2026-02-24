using System;
using System.Drawing;
using System.Windows.Forms;
using MinorShift.Emuera.Forms;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Utils;

namespace MinorShift.Emuera.UI.Game;

internal sealed class WinFormsUiPlatformBackend : IUiPlatformBackend
{
	public int MeasureTextNoPaddingNoPrefix(Graphics graphics, ReadOnlySpan<char> text, Font font, Size layoutSize)
	{
		var size = TextRenderer.MeasureText(graphics, text, font, layoutSize, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
		return size.Width;
	}

	public void DrawTextNoPrefix(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color)
	{
		TextRenderer.DrawText(graphics, text, font, point, color, TextFormatFlags.NoPrefix);
	}

	public void DrawTextNoPrefixWithBackColor(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color, Color backColor)
	{
		TextRenderer.DrawText(graphics, text, font, point, color, backColor, TextFormatFlags.NoPrefix);
	}

	public void DrawTextNoPrefixPreserveClip(Graphics graphics, ReadOnlySpan<char> text, Font font, Point point, Color color)
	{
		TextRenderer.DrawText(graphics, text, font, point, color, TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
	}

	public void DoEvents()
	{
		Application.DoEvents();
	}

	public Point GetMousePositionInClient(object control)
	{
		var winControl = (Control)control;
		return winControl.PointToClient(Control.MousePosition);
	}

	public bool IsPointInClient(object control, Point point)
	{
		return ((Control)control).ClientRectangle.Contains(point);
	}

	public Point GetCursorPosition()
	{
		return Cursor.Position;
	}

	public int GetCursorHeight()
	{
		return Cursor.Current?.Size.Height ?? 0;
	}

	public int GetWorkingAreaHeightForPoint(Point point)
	{
		return Screen.FromPoint(point).WorkingArea.Height;
	}

	public bool IsAnyFormActive()
	{
		return Form.ActiveForm != null;
	}

	public short GetKeyState(int keyCode)
	{
		return WinInput.GetKeyState(keyCode);
	}

	public bool TryParseKeyCode(string keyName, out int keyCode)
	{
		if (Enum.TryParse(keyName, out Keys keys))
		{
			keyCode = (int)keys;
			return true;
		}
		keyCode = 0;
		return false;
	}

	public void SetClipboardText(string text)
	{
		Clipboard.SetDataObject(text, false, 3, 200);
	}

	public void ShowInfo(string message)
	{
		MessageBox.Show(message);
	}

	public void ShowInfo(string message, string caption)
	{
		MessageBox.Show(message, caption);
	}

	public bool ConfirmYesNo(string message, string caption)
	{
		return MessageBox.Show(message, caption, MessageBoxButtons.YesNo) == DialogResult.Yes;
	}

	public void DrawText(Graphics graphics, string text, Font font, Rectangle bounds, Color foreColor, Color backColor, long format)
	{
		TextRenderer.DrawText(graphics, text, font, bounds, foreColor, backColor, (TextFormatFlags)format);
	}

	public Size MeasureText(string text, Font font, Size proposedSize, long format)
	{
		return TextRenderer.MeasureText(text, font, proposedSize, (TextFormatFlags)format);
	}

	public bool TryShowRikaiIndexDialog(byte[] edict, Action<byte[]> onIndexReady)
	{
		if (edict == null || onIndexReady == null)
			return false;

		var dialog = new RikaiDialog(edict, data => onIndexReady(data));
		dialog.Show();
		return true;
	}

	public IUiDebugDialogHandle TryCreateDebugDialog(IDebugDialogConsoleHost console, IDebugRuntimeProcess process)
	{
		if (console == null || process == null)
			return null;

		return new WinFormsDebugDialogHandle(console, process);
	}

	private sealed class WinFormsDebugDialogHandle(IDebugDialogConsoleHost console, IDebugRuntimeProcess process) : IUiDebugDialogHandle
	{
		private readonly DebugDialog dialog = CreateDialog(console, process);

		private static DebugDialog CreateDialog(IDebugDialogConsoleHost console, IDebugRuntimeProcess process)
		{
			var created = new DebugDialog();
			created.SetParent(console, process);
			created.TranslateUI();
			return created;
		}

		public bool IsCreated => dialog.Created;

		public void Focus()
		{
			dialog.Focus();
		}

		public void Show()
		{
			dialog.Show();
		}

		public void UpdateData()
		{
			dialog.UpdateData();
		}

		public void Close()
		{
			dialog.Close();
		}

		public void Dispose()
		{
			dialog.Dispose();
		}
	}
}
