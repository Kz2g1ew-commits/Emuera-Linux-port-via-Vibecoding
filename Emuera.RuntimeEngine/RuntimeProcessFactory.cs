using MinorShift.Emuera.GameProc;
using MinorShift.Emuera.Runtime.Script;

namespace MinorShift.Emuera.RuntimeEngine;

public static class RuntimeProcessFactory
{
	public static IRuntimeProcess? Create(IExecutionConsole console)
	{
		ArgumentNullException.ThrowIfNull(console);
		return new Process(console);
	}
}
