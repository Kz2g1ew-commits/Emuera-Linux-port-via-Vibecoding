using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI;
using MinorShift.Emuera.UI.Game;
using System;
using System.Drawing;
using System.Threading.Tasks;

namespace MinorShift.Emuera.GameView;

internal sealed partial class EmueraConsole
{
	private void RemoveToolTip()
	{
		window.RemoveAllToolTips();
	}

	private int GetToolTipInitialDelay()
	{
		return window.ToolTipInitialDelay;
	}

	private void ShowToolTip(string title, Point point)
	{
		window.ShowToolTip(title, point, tooltip_duration);
	}

	private void QueueToolTipShow(string title)
	{
		System.Threading.SynchronizationContext context = System.Threading.SynchronizationContext.Current;
		Task.Run(async () =>
		{
			ConsoleButtonString savedPointingString = pointingString;
			if (GetToolTipInitialDelay() != 0)
				await Task.Delay(GetToolTipInitialDelay());
			context.Post((state) =>
			{
				MoveMouse(GetMousePosition());
				if (lastPointingString == savedPointingString)
				{
					Point mousePos = window.GetMousePositionInClient();
					Point point = new(mousePos.X + 2, mousePos.Y + UiPlatformBridge.GetCursorHeight());
					Point absolutePoint = UiPlatformBridge.GetCursorPosition();
					if (absolutePoint.Y + tooltip_size.Height > UiPlatformBridge.GetWorkingAreaHeightForPoint(mousePos))
						point.Y -= UiPlatformBridge.GetCursorHeight() * 2;
					ShowToolTip(title, point);
				}
			}, null);
		});
	}

	private void ToolTip_Draw(object sender, ConsoleToolTipDrawEventArgs e)
	{
		if (tooltip_img && int.TryParse(e.ToolTipText, out int i))
		{
			var g = GameData.Function.FunctionMethodCreator.ReadGraphics(i);
			if (g.IsCreated)
			{
				Image img = g.Bitmap;
				e.Graphics.DrawImage(img, 0, 0);
				return;
			}

		}
		e.DrawBackground();
		e.DrawBorder();
		foreach (FontFamily ff in FontRegistry.Collection.Families)
		{
			if (ff.Name == tooltip_fontname)
			{
				using Font f = new(ff, tooltip_fontsize);
				UiPlatformBridge.DrawText(
					e.Graphics,
					e.ToolTipText,
					f,
					e.Bounds,
					Color.FromArgb(window.ToolTipForeColorArgb),
					Color.FromArgb(window.ToolTipBackColorArgb),
					tooltip_format);
				return;
			}
		}
		using (Font f = new(tooltip_fontname, tooltip_fontsize))
		{
			UiPlatformBridge.DrawText(
				e.Graphics,
				e.ToolTipText,
				f,
				e.Bounds,
				Color.FromArgb(window.ToolTipForeColorArgb),
				Color.FromArgb(window.ToolTipBackColorArgb),
				tooltip_format);
		}
	}

	Size tooltip_size;
	private void ToolTip_Popup(object sender, ConsoleToolTipPopupEventArgs e)
	{
		if (tooltip_img && int.TryParse(e.ToolTipText, out int i))
		{
			var g = GameData.Function.FunctionMethodCreator.ReadGraphics(i);
			if (g.IsCreated)
			{
				e.ToolTipSize = new Size(g.Width, g.Height);
				return;
			}
		}
		Font f;
		foreach (FontFamily ff in FontRegistry.Collection.Families)
		{
			if (ff.Name == tooltip_fontname)
			{
				f = new Font(ff, tooltip_fontsize);
				goto foundfont;
			}
		}
		f = new Font(tooltip_fontname, tooltip_fontsize);
	foundfont:
		var size = UiPlatformBridge.MeasureText(e.ToolTipText, f, new Size(int.MaxValue, int.MaxValue), tooltip_format);
		e.ToolTipSize = new Size(size.Width, size.Height);
		tooltip_size = e.ToolTipSize;
	}

	public void CustomToolTip(bool b)
	{
		if (!b)
		{
			window.RemoveToolTipDrawHandler(ToolTip_Draw);
			window.RemoveToolTipPopupHandler(ToolTip_Popup);
		}
		else if (!window.ToolTipOwnerDraw)
		{
			window.AddToolTipDrawHandler(ToolTip_Draw);
			window.AddToolTipPopupHandler(ToolTip_Popup);
		}
		window.ToolTipOwnerDraw = b;
	}

	public void SetToolTipColor(Color foreColor, Color backColor)
	{
		window.ToolTipForeColorArgb = foreColor.ToArgb();
		window.ToolTipBackColorArgb = backColor.ToArgb();
	}

	public void SetToolTipColorRgb(int foreColorRgb, int backColorRgb)
	{
		var fore = Color.FromArgb((foreColorRgb >> 16) & 0xFF, (foreColorRgb >> 8) & 0xFF, foreColorRgb & 0xFF);
		var back = Color.FromArgb((backColorRgb >> 16) & 0xFF, (backColorRgb >> 8) & 0xFF, backColorRgb & 0xFF);
		SetToolTipColor(fore, back);
	}

	public void SetToolTipDelay(int delay)
	{
		window.ToolTipInitialDelay = delay;
	}

	int tooltip_duration;
	string tooltip_fontname = Config.FontName;
	long tooltip_fontsize = Config.FontSize;
	long tooltip_format;
	bool tooltip_img;
	public void SetToolTipDuration(int duration)
	{
		tooltip_duration = duration;
		window.ToolTipAutoPopDelay = duration;
	}

	public void SetToolTipFontName(string fn)
	{
		tooltip_fontname = fn;
	}

	public void SetToolTipFontSize(long fs)
	{
		tooltip_fontsize = fs;
	}

	public void SetToolTipFormat(long f)
	{
		tooltip_format = f;
	}

	public void SetToolTipImg(bool b)
	{
		tooltip_img = b;
	}
}
