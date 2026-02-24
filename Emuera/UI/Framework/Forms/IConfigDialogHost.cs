namespace MinorShift.Emuera.Forms;

internal interface IConfigDialogHost
{
	int ClientAreaWidth { get; }
	int ClientAreaHeight { get; }
	int WindowPositionX { get; }
	int WindowPositionY { get; }
	void UpdateClipboardRuntimeSettings(int maxCb, int scrollCount, int timerInterval);
}
