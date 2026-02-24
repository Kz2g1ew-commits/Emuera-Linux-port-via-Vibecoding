using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using System;

namespace MinorShift.Emuera.Runtime.Utils;

internal static class RuntimeGlobals
{
	private static Func<LogicalLine> currentScanningLineGetter;
	private static Action<LogicalLine> currentScanningLineSetter;

	public static GameBase GameBaseData => RuntimeHost.GetGameBaseData() as GameBase;
	public static ConstantData ConstantData => RuntimeHost.GetConstantData() as ConstantData;
	public static VariableEvaluator VEvaluator => RuntimeHost.GetVariableEvaluator() as VariableEvaluator;
	public static VariableData VariableData => (RuntimeHost.GetVariableData() as VariableData) ?? VEvaluator?.VariableData;
	public static IdentifierDictionary IdentifierDictionary => RuntimeHost.GetIdentifierDictionary() as IdentifierDictionary;
	public static LabelDictionary LabelDictionary => RuntimeHost.GetLabelDictionary() as LabelDictionary;

	public static void BindCurrentScanningLineAccessors(Func<LogicalLine> getter, Action<LogicalLine> setter)
	{
		currentScanningLineGetter = getter;
		currentScanningLineSetter = setter;
	}

	public static LogicalLine CurrentScanningLine
	{
		get
		{
			if (currentScanningLineGetter != null)
				return currentScanningLineGetter();
			return null;
		}
	}

	public static void SetCurrentScanningLine(LogicalLine line)
	{
		currentScanningLineSetter?.Invoke(line);
	}
}
