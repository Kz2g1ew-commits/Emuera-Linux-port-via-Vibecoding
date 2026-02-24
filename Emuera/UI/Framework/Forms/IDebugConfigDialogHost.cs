namespace MinorShift.Emuera.Forms;

internal interface IDebugConfigDialogHost
{
	bool IsWindowCreated { get; }
	int WindowWidth { get; }
	int WindowHeight { get; }
	int WindowPositionX { get; }
	int WindowPositionY { get; }
}
