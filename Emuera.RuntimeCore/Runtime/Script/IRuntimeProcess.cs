using MinorShift.Emuera.Runtime.Utils;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MinorShift.Emuera.Runtime.Script;

public readonly record struct RuntimeScanningLineInfo(
	bool HasPosition,
	string FileName,
	long LineNo,
	string LabelName);

public readonly record struct RuntimeDebugCommandResult(
	string DisplayCommand,
	bool ShouldEchoCommand);

public interface IRuntimeProcess : IDebugRuntimeProcess
{
	long GetCurrentLineMarker();
	bool NeedWaitToEventComEnd { get; set; }
	Task<bool> Initialize(StreamWriter logWriter);
	string GetScriptTitle();
	string GetScriptVersionText();
	void InputInteger(long i);
	void InputInteger(long idx, long i);
	void InputString(string s);
	void InputString(long idx, string i);
	void InputSystemInteger(long i);
	void InputResult5(int r0, int r1, int r2, int r3, int r4, long r5);
	void DoScript();
	RuntimeScanningLineInfo GetScanningLineInfo();
	RuntimeDebugCommandResult ExecuteDebugCommand(string command, bool munchkin, bool outputDebugConsole);
	void BeginTitle();
	void LoadSilent();
	void UpdateCheckInfiniteLoopState();
	void printRawLine(ScriptPosition? position);
	Task ReloadErb();
	Task ReloadPartialErb(List<string> paths);
}
