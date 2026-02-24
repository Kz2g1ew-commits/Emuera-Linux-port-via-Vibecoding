namespace MinorShift.Emuera.Runtime.Script;

public interface IDebugRuntimeProcess
{
	void saveCurrentState(bool single);
	void clearMethodStack();
	void loadPrevState();
	void SetResultString(string value);
	void RestoreRandomSeed(long[] randomSeed);
	string EvaluateExpressionForDebugWatch(string expression);
}
