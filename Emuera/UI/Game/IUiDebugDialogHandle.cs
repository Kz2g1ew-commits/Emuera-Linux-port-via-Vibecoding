namespace MinorShift.Emuera.UI.Game;

internal interface IUiDebugDialogHandle
{
	bool IsCreated { get; }
	void Focus();
	void Show();
	void UpdateData();
	void Close();
	void Dispose();
}
