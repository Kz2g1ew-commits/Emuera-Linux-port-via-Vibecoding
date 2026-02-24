using MinorShift.Emuera.UI.Game;
using System.Drawing;

namespace MinorShift.Emuera.GameView;

internal sealed partial class EmueraConsole
{
	private void PumpUiEvents()
	{
		UiPlatformBridge.DoEvents();
	}

	private void ShowInfoMessage(string message)
	{
		UiPlatformBridge.ShowInfo(message);
	}

	private bool ConfirmYesNo(string text, string caption)
	{
		return UiPlatformBridge.ConfirmYesNo(text, caption);
	}

	private void RefreshMouseFromCurrentCursorIfNeeded()
	{
		if (state != ConsoleState.WaitInput || !inputReq.NeedValue)
			return;
		Point point = window.GetMousePositionInClient();
		if (window.IsPointInClient(point))
			MoveMouse(point);
	}

	public void RefreshMousePointingState()
	{
		bool previous = AlwaysRefresh;
		Point point = window.GetMousePositionInClient();
		AlwaysRefresh = true;
		if (window.IsPointInClient(point))
			MoveMouse(point);
		AlwaysRefresh = previous;
	}

	/// <summary>
	/// 現在、Emueraがアクティブかどうか
	/// </summary>
	public bool IsActive
	{
		get { return !(window == null || !window.Created || !UiPlatformBridge.IsAnyFormActive()); }
	}

	public void Await(int time)
	{
		if (!Enabled || state != ConsoleState.Running)
		{
			Quit();
			return;
		}
		RefreshStrings(true);
		state = ConsoleState.Sleep;
		process.UpdateCheckInfiniteLoopState();
		UiPlatformBridge.DoEvents();
		if (time > 0)
			System.Threading.Thread.Sleep(time);
		state = ConsoleState.Running;
	}
}
