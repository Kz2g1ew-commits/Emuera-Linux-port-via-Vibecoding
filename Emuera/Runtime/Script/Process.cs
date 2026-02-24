using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Loader;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.PluginSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;
using trmb = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.MessageBox;
using trsl = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.SystemLine;

namespace MinorShift.Emuera.GameProc;

internal sealed partial class Process(IExecutionConsole view) : IRuntimeProcess
{
	public LogicalLine getCurrentLine { get { return state.CurrentLine; } }

	/// <summary>
	/// @~~と$~~を集めたもの。CALL命令などで使う
	/// 実行順序はLogicalLine自身が保持する。
	/// </summary>
	LabelDictionary labelDic;
	public LabelDictionary LabelDictionary { get { return labelDic; } }

	/// <summary>
	/// 変数全部。スクリプト中で必要になる変数は（ユーザーが直接触れないものも含め）この中にいれる
	/// </summary>
	private VariableEvaluator vEvaluator;
	public VariableEvaluator VEvaluator { get { return vEvaluator; } }
	private ExpressionMediator exm;
	private GameBase gamebase;
	public GameBase gameBase { get { return gamebase; } }
	readonly IExecutionConsole console = view;
	private IdentifierDictionary idDic;
	internal IdentifierDictionary IdentifierDictionary => idDic;
	ProcessState state;
	ProcessState originalState;//リセットする時のために
	bool noError;
	//色々あって復活させてみる
	bool initialiing;
	public bool inInitializeing { get { return initialiing; } }

	public async Task<bool> Initialize(StreamWriter logWriter)
	{
		var stopWatch = new Stopwatch();
		stopWatch.Start();
		LexicalAnalyzer.UseMacro = false;
		state = new ProcessState(console, MethodStack);
		originalState = state;
		RuntimeGlobals.BindCurrentScanningLineAccessors(GetScaningLine, SetScaningLine);
		initialiing = true;
		try
		{
			logWriter?.WriteLine($"Proc:Init:Start {stopWatch.ElapsedMilliseconds}ms");
			logWriter?.WriteLine($"Proc:Init:Parser:Start {stopWatch.ElapsedMilliseconds}ms");
			ParserMediator.Initialize(console);
			//コンフィグファイルに関するエラーの処理（コンフィグファイルはこの関数に入る前に読込済み）
			if (ParserMediator.HasWarning)
			{
				ParserMediator.FlushWarningList();
				if (Dialog.ShowPrompt(trmb.ConfigFileError.Text, trmb.ConfigError.Text))
				{
					console.PrintSystemLine(trsl.SelectExitConfigMB.Text);
					return false;
				}
			}
			logWriter?.WriteLine($"Proc:Init:Parser:End {stopWatch.ElapsedMilliseconds}ms");

			logWriter?.WriteLine($"Proc:Init:Image:Start {stopWatch.ElapsedMilliseconds}ms");
			//リソースフォルダ読み込み
			var err = await Task.Run(() => RuntimeHost.LoadContents(false));
			if (err != null)
			{
				ParserMediator.FlushWarningList();
				console.PrintSystemLine(trsl.ResourceReadError.Text);
				console.Print(err.ToString());
				return false;
			}
			ParserMediator.FlushWarningList();
			logWriter?.WriteLine($"Proc:Init:Image:End {stopWatch.ElapsedMilliseconds}ms");

			logWriter?.WriteLine($"Proc:Init:KeyMacro:Start {stopWatch.ElapsedMilliseconds}ms");
			//キーマクロ読み込み
			#region eee_カレントディレクトリー
			if (Config.UseKeyMacro && !RuntimeEnvironment.AnalysisMode)
			{
				string macroPath = RuntimeFileSearch.ResolveFilePath(Path.Combine(RuntimeEnvironment.ExeDir, "macro.txt"));
				//if (File.Exists(RuntimeEnvironment.ExeDir + "macro.txt"))
				if (File.Exists(macroPath))
				{
					if (Config.DisplayReport)
						console.PrintSystemLine(trsl.LoadingMacro.Text);
					//KeyMacro.LoadMacroFile(RuntimeEnvironment.ExeDir + "macro.txt");
					KeyMacro.LoadMacroFile(macroPath);
				}
			}
			#endregion
			logWriter?.WriteLine($"Proc:Init:KeyMacro:End {stopWatch.ElapsedMilliseconds}ms");

			logWriter?.WriteLine($"Proc:Init:Replace:Start {stopWatch.ElapsedMilliseconds}ms");
			//_replace.csv読み込み
			if (Config.UseReplaceFile && !RuntimeEnvironment.AnalysisMode)
			{
					string replacePath = RuntimeFileSearch.ResolveFilePath(Path.Combine(RuntimeEnvironment.CsvDir, "_Replace.csv"));
				if (File.Exists(replacePath))
				{
					if (Config.DisplayReport)
						console.PrintSystemLine(trsl.LoadingReplace.Text);
					ConfigData.Instance.LoadReplaceFile(replacePath);
					if (ParserMediator.HasWarning)
					{
						ParserMediator.FlushWarningList();
						if (Dialog.ShowPrompt(trmb.ReplaceFileError.Text, trmb.ReplaceError.Text))
						{
							console.PrintSystemLine(trsl.SelectExitReplaceMB.Text);
							return false;
						}
					}
				}
			}
			logWriter?.WriteLine($"Proc:Init:Replace:End {stopWatch.ElapsedMilliseconds}ms");

			Config.SetReplace(ConfigData.Instance);
			// Host-specific DRAWLINE baseline string initialization.
			RuntimeHost.InitializeDrawLineString(Config.DrawLineString);

			logWriter?.WriteLine($"Proc:Init:Rename:Load:Start {stopWatch.ElapsedMilliseconds}ms");
			//_rename.csv読み込み
			if (Config.UseRenameFile)
			{
					string renamePath = RuntimeFileSearch.ResolveFilePath(Path.Combine(RuntimeEnvironment.CsvDir, "_Rename.csv"));
				if (File.Exists(renamePath))
				{
					if (Config.DisplayReport || RuntimeEnvironment.AnalysisMode)
						console.PrintSystemLine(trsl.LoadingRename.Text);
					ParserMediator.LoadEraExRenameFile(renamePath);
				}
				else
					console.PrintError(trsl.MissingRename.Text);
			}
			logWriter?.WriteLine($"Proc:Init:Rename:Load:End {stopWatch.ElapsedMilliseconds}ms");

			if (!Config.DisplayReport)
			{
				console.PrintSingleLine(Config.LoadLabel);
				RuntimeHost.RefreshStrings(true);
			}
			//gamebase.csv読み込み
			gamebase = new GameBase();
				string gamebasePath = RuntimeFileSearch.ResolveFilePath(Path.Combine(RuntimeEnvironment.CsvDir, "GAMEBASE.CSV"));
			if (!await Task.Run(() => gamebase.LoadGameBaseCsv(gamebasePath)))
			{
				ParserMediator.FlushWarningList();
				console.PrintSystemLine(trsl.GamebaseError.Text);
				return false;
			}
			RuntimeHost.SetWindowTitle(gamebase.ScriptWindowTitle);
			RuntimeHost.SetGameBaseData(gamebase);
			logWriter?.WriteLine($"Proc:Init:MainCSV:End {stopWatch.ElapsedMilliseconds}ms");

			//前記以外のcsvを全て読み込み
			ConstantData constant = new();
			constant.LoadData(RuntimeEnvironment.CsvDir, console, Config.DisplayReport);
			logWriter?.WriteLine($"Proc:Init:EtcCSV:End {stopWatch.ElapsedMilliseconds}ms");

			RuntimeHost.SetConstantData(constant);
			TrainName = constant.GetCsvNameList(VariableCode.TRAINNAME);
			logWriter?.WriteLine($"Proc:Init:EtcCSV:End {stopWatch.ElapsedMilliseconds}ms");

			vEvaluator = new VariableEvaluator(gamebase, constant);
			RuntimeHost.SetVariableEvaluator(vEvaluator);

			idDic = new IdentifierDictionary(vEvaluator.VariableData);
			RuntimeHost.SetIdentifierDictionary(idDic);

			StrForm.Initialize();
			VariableParser.Initialize();

				exm = new ExpressionMediator(this, vEvaluator, console);

			logWriter?.WriteLine($"Proc:Init:ERH:Start {stopWatch.ElapsedMilliseconds}ms");

			labelDic = new LabelDictionary(idDic);
			RuntimeHost.SetLabelDictionary(labelDic);
			ErhLoader hLoader = new(console, idDic, exm, vEvaluator.VariableData);

			LexicalAnalyzer.UseMacro = false;

			PluginManager.GetInstance().SetParent(this, state, exm);
			PluginManager.GetInstance().LoadPlugins();

			//ERH読込
			if (!await Task.Run(() => hLoader.LoadHeaderFiles(RuntimeEnvironment.ErbDir, Config.DisplayReport)))
			{
				ParserMediator.FlushWarningList();
				console.PrintSystemLine("");
				return false;
			}
			LexicalAnalyzer.UseMacro = idDic.UseMacro();
			logWriter?.WriteLine($"Proc:Init:ERH:End {stopWatch.ElapsedMilliseconds}ms");

			//TODO:ユーザー定義変数用のcsvの適用

			//ERB読込
			logWriter?.WriteLine($"Proc:Init:ERB:Start {stopWatch.ElapsedMilliseconds}ms");
			var loader = new ErbLoader(console, exm);
			if (RuntimeEnvironment.AnalysisMode)
				noError = await loader.LoadErbList(RuntimeEnvironment.AnalysisFiles.ToList(), labelDic);
			else
				noError = await loader.LoadErbDir(RuntimeEnvironment.ErbDir, Config.DisplayReport, labelDic);
			logWriter?.WriteLine($"Proc:Init:ERB:End {stopWatch.ElapsedMilliseconds}ms");

			initSystemProcess();
			initialiing = false;

			logWriter?.WriteLine($"Proc:Init:End {stopWatch.ElapsedMilliseconds}ms");
		}
		catch (Exception e)
		{
			handleException(e, null, true);
			console.PrintSystemLine(trsl.ErhLoadingError.Text);
			return false;
		}
		if (labelDic == null)
		{
			return false;
		}
		state.Begin(BeginType.TITLE);
		return true;
	}

	public async Task ReloadErb()
	{
		await Preload.Load(RuntimeEnvironment.ErbDir);
		await Preload.Load(RuntimeEnvironment.CsvDir);
		saveCurrentState(false);
		state.SystemState = SystemStateCode.System_Reloaderb;
		ErbLoader loader = new(console, exm);
		await loader.LoadErbDir(RuntimeEnvironment.ErbDir, false, labelDic);
		console.ReadAnyKey();
	}

	public async Task ReloadPartialErb(List<string> paths)
	{
		saveCurrentState(false);
		state.SystemState = SystemStateCode.System_Reloaderb;
		await Preload.Load(paths);
		var loader = new ErbLoader(console, exm);
		await loader.LoadErbList(paths, labelDic);
		console.ReadAnyKey();
	}

	public void SetCommnds(long count)
	{
		coms = new List<long>((int)count);
		isCTrain = true;
		long[] selectcom = vEvaluator.SELECTCOM_ARRAY;
		if (count >= selectcom.Length)
		{
			throw new CodeEE(trerror.CalltrainArgMoreThanSelectcom.Text);
		}
		for (int i = 0; i < (int)count; i++)
		{
			coms.Add(selectcom[i + 1]);
		}
	}

	public bool ClearCommands()
	{
		coms.Clear();
		count = 0;
		isCTrain = false;
		skipPrint = true;
		return callFunction("CALLTRAINEND", false, false);
	}
	#region EE_INPUTMOUSEKEYのボタン対応
	// public void InputResult5(int r0, int r1, int r2, int r3, int r4)
	public void InputResult5(int r0, int r1, int r2, int r3, int r4, long r5)
	{
		long[] result = vEvaluator.RESULT_ARRAY;
		result[0] = r0;
		result[1] = r1;
		result[2] = r2;
		result[3] = r3;
		result[4] = r4;
		result[5] = r5;
	}
	#endregion
	public void InputInteger(long i)
	{
		RuntimeHost.CtrlZAddInput(i.ToString());
		vEvaluator.RESULT = i;
	}
	#region EM_私家版_INPUT系機能拡張
	public void InputInteger(long idx, long i)
	{
		if (idx < vEvaluator.RESULT_ARRAY.Length)
			vEvaluator.RESULT_ARRAY[idx] = i;
	}
	public void InputString(long idx, string i)
	{
		if (idx < vEvaluator.RESULT_ARRAY.Length)
			vEvaluator.RESULTS_ARRAY[idx] = i;
	}
	#endregion
	public void InputSystemInteger(long i)
	{
		RuntimeHost.CtrlZAddInput(i.ToString());
		systemResult = i;
	}
	public void InputString(string s)
	{
		RuntimeHost.CtrlZAddInput(s);
		vEvaluator.RESULTS = s;
	}

	public long GetCurrentLineMarker()
	{
		return state.CurrentLine == null ? 0 : RuntimeHelpers.GetHashCode(state.CurrentLine);
	}

	public string GetScriptTitle()
	{
		return gamebase?.ScriptTitle ?? string.Empty;
	}

	public string GetScriptVersionText()
	{
		return gamebase?.ScriptVersionText ?? string.Empty;
	}

	public void SetResultString(string value)
	{
		vEvaluator.RESULTS = value ?? string.Empty;
	}

	public void RestoreRandomSeed(long[] randomSeed)
	{
		if (randomSeed == null || randomSeed.Length == 0)
			return;
		vEvaluator?.Rand.SetRand(randomSeed);
	}

	public void PrepareInstructionArguments(InstructionLine instruction)
	{
		if (instruction == null)
			return;
		ArgumentParser.SetArgumentTo(instruction, exm);
	}

	public RuntimeDebugCommandResult ExecuteDebugCommand(string command, bool munchkin, bool outputDebugConsole)
	{
		string com = command ?? string.Empty;

		// Debug command path bypasses ReadEnabledLine, so apply rename substitutions here.
		if (Config.UseRenameFile && (com.IndexOf("[[", StringComparison.Ordinal) >= 0) && (com.IndexOf("]]", StringComparison.Ordinal) >= 0))
		{
			foreach (KeyValuePair<string, string> pair in ParserMediator.RenameDic)
				com = com.Replace(pair.Key, pair.Value);
		}

		LogicalLine line = null;
		if (!com.StartsWith('@') && !com.StartsWith('"') && !com.StartsWith('\\'))
			line = LogicalLineParser.ParseLine(com, null);
		if (line == null || line is InvalidLine)
		{
			WordCollection wc = LexicalAnalyzer.Analyse(new CharStream(com), LexEndWith.EoL, LexAnalyzeFlag.None);
			AExpression term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);
			if (term == null)
				throw new CodeEE(trerror.CanNotInterpretedLine.Text);
			if (term.GetOperandType() == typeof(long))
			{
				com = outputDebugConsole ? "DEBUGPRINTFORML {" + com + "}" : "PRINTVL " + com;
			}
			else
			{
				com = outputDebugConsole ? "DEBUGPRINTFORML %" + com + "%" : "PRINTFORMSL " + com;
			}
			line = LogicalLineParser.ParseLine(com, null);
		}
		if (line == null)
			throw new CodeEE(trerror.CanNotInterpretedLine.Text);
		if (line is InvalidLine invalidLine)
			throw new CodeEE(invalidLine.ErrMes);
		if (line is not InstructionLine func)
			throw new CodeEE(trerror.InvalidDebugCommand.Text);
		if (func.Function.IsFlowContorol())
			throw new CodeEE(trerror.CanNotUseFlowInstruction.Text);
		if (func.Function.IsWaitInput())
			throw new CodeEE(string.Format(trerror.CanNotUseInstruction.Text, func.Function.Name));
		if (!func.Function.IsMethodSafe())
			throw new CodeEE(string.Format(trerror.CanNotUseInstruction.Text, func.Function.Name));
		if (func.Function.IsPartial())
			throw new CodeEE(string.Format(trerror.CanNotUseInstruction.Text, func.Function.Name));
		switch (func.FunctionCode)
		{
			case FunctionCode.PUTFORM:
			case FunctionCode.UPCHECK:
			case FunctionCode.CUPCHECK:
			case FunctionCode.SAVEDATA:
				throw new CodeEE(string.Format(trerror.CanNotUseInstruction.Text, func.Function.Name));
		}

		PrepareInstructionArguments(func);
		if (func.IsError)
			throw new CodeEE(func.ErrMes);
		DoDebugNormalFunction(func, munchkin);

		return new RuntimeDebugCommandResult(com, !outputDebugConsole && func.FunctionCode == FunctionCode.SET);
	}

	public string EvaluateExpressionForDebugWatch(string expression)
	{
		if (string.IsNullOrEmpty(expression) || exm == null)
			return string.Empty;
		CharStream st = new(expression);
		WordCollection wc = LexicalAnalyzer.Analyse(st, LexEndWith.EoL, LexAnalyzeFlag.None);
		AExpression term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);
		SingleTerm value = term.GetValue(exm);
		return value.ToString();
	}

	readonly Stopwatch startTime = new();

	public void DoScript()
	{
		startTime.Restart();
		state.lineCount = 0;
		bool systemProcRunning = true;
		try
		{
			while (true)
			{
				methodStack = 0;
				systemProcRunning = true;
				while (state.ScriptEnd && console.IsRunning)
					runSystemProc();
				if (!console.IsRunning)
					break;
				systemProcRunning = false;
				runScriptProc();
			}
		}
		catch (Exception ec)
		{
			LogicalLine currentLine = state.ErrorLine;
			if (currentLine != null && currentLine is NullLine)
				currentLine = null;
			if (systemProcRunning)
				handleExceptionInSystemProc(ec, currentLine, true);
			else
				handleException(ec, currentLine, true);
		}
	}

	public void BeginTitle()
	{
		vEvaluator.ResetData();
		state = originalState;
		state.Begin(BeginType.TITLE);
	}

	public void UpdateCheckInfiniteLoopState()
	{
		startTime.Restart();
		state.lineCount = 0;
	}

	bool IRuntimeProcess.NeedWaitToEventComEnd
	{
		get => NeedWaitToEventComEnd;
		set => NeedWaitToEventComEnd = value;
	}

	private void checkInfiniteLoop()
	{
		//うまく動かない。BEEP音が鳴るのを止められないのでこの処理なかったことに（1.51）
		////フリーズ防止。処理中でも履歴を見たりできる
		////System.Threading.Thread.Sleep(0);

		//if (!console.Enabled)
		//{
		//    //DoEvents()の間にウインドウが閉じられたらおしまい。
		//    console.ReadAnyKey();
		//    return;
		//}
		var elapsedTime = startTime.ElapsedMilliseconds;
		if (elapsedTime < Config.InfiniteLoopAlertTime)
			return;
		LogicalLine currentLine = state.CurrentLine;
		if ((currentLine == null) || (currentLine is NullLine))
			return;//現在の行が特殊な状態ならスルー
		if (!console.Enabled)
			return;//クローズしてるとMessageBox.Showができないので。
		string caption = string.Format(trmb.InfiniteLoop.Text);
		string text = string.Format(
			trmb.TooLongLoop.Text,
			currentLine.Position.Value.Filename, currentLine.Position.Value.LineNo, state.lineCount, elapsedTime);
		if (Dialog.ShowPrompt(text, caption))
		{
			throw new CodeEE(trerror.SelectExitInfiniteLoopMB.Text);
		}
		else
		{
			state.lineCount = 0;
			startTime.Restart();
		}
	}

	int methodStack;
	public SingleTerm GetValue(SuperUserDefinedMethodTerm udmt)
	{
		methodStack++;
		if (methodStack > 100)
		{
			//StackOverflowExceptionはcatchできない上に再現性がないので発生前に一定数で打ち切る。
			//環境によっては100以前にStackOverflowExceptionがでるかも？
			throw new CodeEE(trerror.OverflowFuncStack.Text);
		}
		SingleTerm ret = null;
		int temp_current = state.currentMin;
		state.currentMin = state.functionCount;
		udmt.Call.updateRetAddress(state.CurrentLine);
		try
		{
			state.IntoFunction(udmt.Call, udmt.Argument, exm);
			//do whileの中でthrow されたエラーはここではキャッチされない。
			//#functionを全て抜けてDoScriptでキャッチされる。
			runScriptProc();
			ret = state.MethodReturnValue;
		}
		finally
		{
			if (udmt.Call.TopLabel.hasPrivDynamicVar)
				udmt.Call.TopLabel.ScopeOut();
			//1756beta2+v3:こいつらはここにないとデバッグコンソールで式中関数が事故った時に大事故になる
			state.currentMin = temp_current;
			methodStack--;
		}
		return ret;
	}

	public void clearMethodStack()
	{
		methodStack = 0;
	}

	public int MethodStack()
	{
		return methodStack;
	}

	public ScriptPosition? GetRunningPosition()
	{
		LogicalLine line = state.ErrorLine;
		if (line == null)
			return null;
		return line.Position;
	}
	/*
			private readonly string scaningScope = null;
			private string GetScaningScope()
			{
				if (scaningScope != null)
					return scaningScope;
				return state.Scope;
			}
	*/
	private LogicalLine scaningLine;
	internal void SetScaningLine(LogicalLine line)
	{
		scaningLine = line;
	}

	internal LogicalLine GetScaningLine()
	{
		if (scaningLine != null)
			return scaningLine;
		LogicalLine line = state.ErrorLine;
		if (line == null)
			return default;
		return line;
	}

	public RuntimeScanningLineInfo GetScanningLineInfo()
	{
		LogicalLine line = GetScaningLine();
		if ((line == null) || (line.Position == null))
			return new RuntimeScanningLineInfo(false, string.Empty, 0, string.Empty);
		string labelName = line.ParentLabelLine?.LabelName ?? string.Empty;
		return new RuntimeScanningLineInfo(true, line.Position.Value.Filename, line.Position.Value.LineNo, labelName);
	}

	private void PrintErrorButtonSafe(string str, ScriptPosition? pos, int level = 0)
	{
		RuntimeHost.PrintErrorButton(str, pos, level);
	}


	private void handleExceptionInSystemProc(Exception exc, LogicalLine current, bool playSound)
	{
		RuntimeHost.ThrowError(playSound);
		if (exc is CodeEE)
		{
			console.PrintError(string.Format(trerror.FuncEndError.Text, AssemblyData.EmueraVersionText));
			console.PrintError(exc.Message);
		}
		else if (exc is ExeEE)
		{
			console.PrintError(string.Format(trerror.FuncEndEmueraError.Text, AssemblyData.EmueraVersionText));
			console.PrintError(exc.Message);
		}
		else
		{
			console.PrintError(string.Format(trerror.FuncEndUnexpectedError.Text, AssemblyData.EmueraVersionText));
			console.PrintError(exc.GetType().ToString() + ":" + exc.Message);
			string[] stack = exc.StackTrace.Split('\n');
			for (int i = 0; i < stack.Length; i++)
			{
				console.PrintError(stack[i]);
			}
		}
	}

	private void handleException(Exception exc, LogicalLine current, bool playSound)
	{
		RuntimeHost.ThrowError(playSound);
		ScriptPosition? position = null;
		if ((exc is EmueraException ee) && (ee.Position != null))
			position = ee.Position;
		else if ((current != null) && (current.Position != null))
			position = current.Position;
		string posString = "";
		if (position != null)
		{
			if (position.Value.LineNo >= 0)
				posString = string.Format(trerror.ErrorFileAndLine.Text, position.Value.Filename, position.Value.LineNo.ToString());
			else
				posString = string.Format(trerror.ErrorFile.Text, position.Value.Filename);

		}
		if (exc is CodeEE)
		{
			if (position != default)
			{
				if (current is InstructionLine procline && procline.FunctionCode == FunctionCode.THROW)
				{
					PrintErrorButtonSafe(string.Format(trerror.HasThrow.Text, posString), position);
					printRawLine(position);
					console.PrintError(string.Format(trerror.ThrowMessage.Text, exc.Message));
				}
				else
				{
					PrintErrorButtonSafe(string.Format(trerror.HasError.Text, posString, AssemblyData.EmueraVersionText), position);
					printRawLine(position);
					console.PrintError(string.Format(trerror.ErrorMessage.Text, exc.Message));
				}
				console.PrintError(string.Format(trerror.ErrorInFunc.Text, current.ParentLabelLine.LabelName, current.ParentLabelLine.Position.Value.Filename, current.ParentLabelLine.Position.Value.LineNo.ToString()));
				console.PrintError(trerror.FuncCallStack.Text);
				LogicalLine parent;
				int depth = 0;
				while ((parent = state.GetReturnAddressSequensial(depth++)) != null)
				{
					if (parent.Position != null)
					{
						PrintErrorButtonSafe(string.Format(trerror.ErrorFuncStack.Text, parent.Position.Value.Filename, parent.Position.Value.LineNo.ToString(), parent.ParentLabelLine.LabelName), parent.Position);
					}
				}
			}
			else
			{
				console.PrintError(string.Format(trerror.HasError.Text, posString, AssemblyData.EmueraVersionText));
				console.PrintError(exc.Message);
			}
		}
		else if (exc is ExeEE)
		{
			console.PrintError(string.Format(trerror.HasEmueraError.Text, posString, AssemblyData.EmueraVersionText));
			console.PrintError(exc.Message);
		}
		else
		{
			console.PrintError(string.Format(trerror.HasUnexpectedError.Text, posString, AssemblyData.EmueraVersionText));
			console.PrintError(exc.GetType().ToString() + ":" + exc.Message);
			string[] stack = exc.StackTrace.Split('\n');
			for (int i = 0; i < stack.Length; i++)
			{
				console.PrintError(stack[i]);
			}
		}
	}

	public void printRawLine(ScriptPosition? position)
	{
		string str = getRawTextFormFilewithLine(position);
		if (!string.IsNullOrEmpty(str))
			console.PrintError(str);
	}

	public static string getRawTextFormFilewithLine(ScriptPosition? position)
	{
		string extents = position.Value.Filename[^4..].ToLower();
		if (extents == ".erb")
		{
			string erbPath = RuntimeFileSearch.ResolveFilePath(Path.Combine(RuntimeEnvironment.ErbDir, position.Value.Filename));
			return File.Exists(erbPath)
				? position.Value.LineNo > 0 ? File.ReadLines(erbPath, EncodingHandler.DetectEncoding(erbPath)).Skip(position.Value.LineNo - 1).First() : ""
				: "";
		}
		else if (extents == ".csv")
		{
			string csvPath = RuntimeFileSearch.ResolveFilePath(Path.Combine(RuntimeEnvironment.CsvDir, position.Value.Filename));
			return File.Exists(csvPath)
				? position.Value.LineNo > 0 ? File.ReadLines(csvPath, EncodingHandler.DetectEncoding(csvPath)).Skip(position.Value.LineNo - 1).First() : ""
				: "";
		}
		else
			return "";
	}

}
