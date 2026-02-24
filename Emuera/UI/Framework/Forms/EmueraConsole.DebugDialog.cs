using MinorShift.Emuera.UI.Game;

namespace MinorShift.Emuera.GameView;

internal sealed partial class EmueraConsole
{
	IUiDebugDialogHandle dd;
	bool IDebugDialogConsoleHost.IsInProcess => IsInProcess;
	public bool IsDebugDialogCreated => (dd != null) && dd.IsCreated;

	public void OpenDebugDialog()
	{
		if (!Program.DebugMode)
			return;
		if (dd != null)
		{
			if (dd.IsCreated)
			{
				dd.Focus();
				return;
			}
			else
			{
				dd.Dispose();
				dd = null;
			}
		}
		dd = UiPlatformBridge.TryCreateDebugDialog(this, process);
		if (dd == null)
			return;
		dd.Show();
	}

	public void CloseDebugDialog()
	{
		if (dd == null)
			return;
		if (dd.IsCreated)
			dd.Close();
	}

	public void UpdateDebugDialog()
	{
		if ((dd == null) || !dd.IsCreated)
			return;
		dd.UpdateData();
	}
}
