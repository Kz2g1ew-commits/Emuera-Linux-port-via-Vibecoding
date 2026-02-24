using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.UI.Game;
using System.Drawing;

namespace MinorShift.Emuera.GameView;

internal sealed partial class EmueraConsole
{
	public Point GetMousePosition()
	{
		if (window == null || !window.Created)
			return new Point();
		//クライアント左上基準の座標取得
		Point pos = window.GetMousePositionInClient();
		//クライアント左下基準の座標に置き換え
		pos.Y -= ClientHeight;
		return pos;
	}

	public RuntimePoint GetMousePositionXY()
	{
		Point point = GetMousePosition();
		return new RuntimePoint(point.X, point.Y);
	}

	public bool MoveMouseXY(int x, int y)
	{
		return MoveMouse(new Point(x, y));
	}

	public void LeaveMouse()
	{
		bool needRefresh = selectingButton != null || pointingString != null;
		selectingButton = null;
		pointingString = null;
		pointingStrings.Clear();
		if (needRefresh)
			RefreshStrings(true);
	}

	private void verticalScrollBarUpdate()
	{
		int max = displayLineList.Count;
		int move = max - window.ScrollMaximum;
		if (move == 0)
			return;
		window.TextBoxIgnoreScrollBarChanges = true;
		if (move > 0)
		{
			window.ScrollMaximum = max;
			window.ScrollValue += move;
		}
		else
		{
			if (max > window.ScrollValue)
				window.ScrollValue = max;
			window.ScrollMaximum = max;
		}
		window.ScrollEnabled = max > 0;
		window.TextBoxIgnoreScrollBarChanges = false;
	}
}
