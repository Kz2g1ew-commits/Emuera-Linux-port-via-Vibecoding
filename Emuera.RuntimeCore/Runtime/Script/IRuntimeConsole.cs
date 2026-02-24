using MinorShift.Emuera.Runtime.Utils;

namespace MinorShift.Emuera.Runtime.Script;

public interface IRuntimeConsole : IScriptConsole, IDebugConsole
{
	bool Enabled { get; }
	bool IsRunning { get; }
	bool IsTimeOut { get; }
	bool MesSkip { get; set; }
	long LineCount { get; }

	void Await(int time);
	void ForceQuit();
	void Quit();
	void ReadAnyKey(bool anykey = false, bool stopMesskip = false);
	void WaitInput(InputRequest req);
}
