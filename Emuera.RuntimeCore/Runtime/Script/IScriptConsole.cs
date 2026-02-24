using MinorShift.Emuera.Runtime.Utils;

namespace MinorShift.Emuera.Runtime.Script;

public interface IScriptConsole
{
	bool RunERBFromMemory { get; }
	void PrintWarning(string str, ScriptPosition? position, int level);
	void PrintSystemLine(string str);
	void PrintSingleLine(string str);
	void PrintSingleLine(string str, bool temporary);
	void Print(string str, bool lineEnd = true);
	void PrintC(string str, bool alignmentRight);
	void NewLine();
	void PrintError(string str);
}
