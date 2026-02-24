namespace MinorShift.Emuera.UI.Game;

internal interface IDebugDialogConsoleHost
{
	bool IsInProcess { get; }
	bool RunERBFromMemory { get; set; }
	string DebugConsoleLog { get; }
	string GetDebugTraceLog(bool force);
	void DebugPrint(string str);
	void DebugNewLine();
	void DebugCommand(string com, bool munchkin, bool outputDebugConsole);
}
