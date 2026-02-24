namespace MinorShift.Emuera.Runtime.Script;

public interface IDebugConsole
{
	void DebugAddTraceLog(string str);
	void DebugRemoveTraceLog();
	void DebugClearTraceLog();
}
