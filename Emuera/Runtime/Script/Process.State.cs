using MinorShift.Emuera.GameData.Variable;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;
using trsl = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.SystemLine;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;

namespace MinorShift.Emuera.Runtime.Script;

//1756 インナークラス解除して一般に開放

internal sealed class ProcessState
{
	public ProcessState(IDebugConsole console, Func<int> getMethodStackDepth = null)
	{
		if (RuntimeEnvironment.DebugMode)//DebugModeでなければ知らなくて良い
			this.console = console;
		this.getMethodStackDepth = getMethodStackDepth;
	}
	readonly IDebugConsole console;
	readonly Func<int> getMethodStackDepth;
	readonly List<CalledFunction> functionList = [];
	private LogicalLine currentLine;
	//private LogicalLine nextLine;
	public int lineCount;
	public int currentMin;
	//private bool sequential;

	public bool ScriptEnd
	{
		get
		{
			return functionList.Count == currentMin;
		}
	}

	public int functionCount
	{
		get
		{
			return functionList.Count;
		}
	}

	SystemStateCode sysStateCode = SystemStateCode.Title_Begin;
	BeginType begintype = BeginType.NULL;
	public bool isBegun { get { return begintype != BeginType.NULL; } }

	public LogicalLine CurrentLine { get { return currentLine; } set { currentLine = value; } }
	public LogicalLine ErrorLine
	{
		get
		{
			//if (RunningLine != null)
			//	return RunningLine;
			return currentLine;
		}
	}

	//IF文中でELSEIF文の中身をチェックするなどCurrentLineと作業中のLineが違う時にセットする
	//public LogicalLine RunningLine { get; set; }
	//1755a 呼び出し元消滅
	//public bool Sequential { get { return sequential; } }
	public CalledFunction CurrentCalled
	{
		get
		{
			//実行関数なしの状態は一部のシステムINPUT以外では存在しないのでGOTO系の処理でしかここに来ない関係上、前提を満たしようがない
			//if (functionList.Count == 0)
			//    throw new ExeEE("実行中関数がない");
			return functionList[^1];
		}
	}
	public SystemStateCode SystemState
	{
		get { return sysStateCode; }
		set { sysStateCode = value; }
	}

	public void ShiftNextLine()
	{
		currentLine = currentLine.NextLine;
		//nextLine = nextLine.NextLine;
		//RunningLine = null;
		//sequential = true;
		// Runtime process line counter
		lineCount++;
	}

	/// <summary>
	/// 関数内の移動。JUMPではなくGOTOやIF文など
	/// </summary>
	/// <param name="line"></param>
	public void JumpTo(LogicalLine line)
	{
		currentLine = line;
		lineCount++;
		//sequential = false;
		//ShfitNextLine();
	}
	#region EE_FORCE_QUIT系
	// public void SetBegin(string keyword)
	public void SetBegin(string keyword, bool force)
	{//TrimとToUpper済みのはず
		switch (keyword)
		{
			case "SHOP":
				RuntimeHost.UnloadTempLoadedConstImages();
				RuntimeHost.UnloadTempLoadedGraphicsImages();
				SetBegin(BeginType.SHOP, force); return;
			case "TRAIN":
				SetBegin(BeginType.TRAIN, force); return;
			case "AFTERTRAIN":
				SetBegin(BeginType.AFTERTRAIN, force); return;
			case "ABLUP":
				SetBegin(BeginType.ABLUP, force); return;
			case "TURNEND":
				SetBegin(BeginType.TURNEND, force); return;
			case "FIRST":
				RuntimeHost.UnloadTempLoadedConstImages();
				RuntimeHost.UnloadTempLoadedGraphicsImages();
				SetBegin(BeginType.FIRST, force); return;
			case "TITLE":
				SetBegin(BeginType.TITLE, force); return;
		}
		throw new CodeEE(string.Format(trerror.InvalidBeginArg.Text, keyword));
	}

	//public void SetBegin(BeginType type)
	public void SetBegin(BeginType type, bool force)
	{
		string errmes;
		switch (type)
		{
			case BeginType.SHOP:
			case BeginType.TRAIN:
			case BeginType.AFTERTRAIN:
			case BeginType.ABLUP:
			case BeginType.TURNEND:
			case BeginType.FIRST:
				if (force == true) break;
				if ((sysStateCode & SystemStateCode.__CAN_BEGIN__) != SystemStateCode.__CAN_BEGIN__)
				{
					errmes = "BEGIN";
					goto err;
				}
				break;
			//1.729 BEGIN TITLEはどこでも使えるように
			case BeginType.TITLE:
				break;
				//BEGINの処理中でチェック済み
				//default:
				//    throw new ExeEE("不適当なBEGIN呼び出し");
		}
		begintype = type;
		return;
	err:
		CalledFunction func = functionList[0];
		string funcName = func.FunctionName;
		throw new CodeEE(string.Format(trerror.CanNotUseInstruction.Text, funcName, errmes));
	}
	#endregion

	public void SaveLoadData(bool saveData)
	{

		if (saveData)
			sysStateCode = SystemStateCode.SaveGame_Begin;
		else
			sysStateCode = SystemStateCode.LoadGame_Begin;
		//ClearFunctionList();
		return;
	}

	public void ClearFunctionList()
	{
		if (RuntimeEnvironment.DebugMode && !isClone && (getMethodStackDepth?.Invoke() ?? 0) == 0)
			console.DebugClearTraceLog();
		foreach (CalledFunction called in functionList)
			if (called.CurrentLabel.hasPrivDynamicVar)
				called.CurrentLabel.ScopeOut();
		functionList.Clear();
		begintype = BeginType.NULL;
	}

	public bool calledWhenNormal = true;
	/// <summary>
	/// BEGIN命令によるプログラム状態の変化
	/// </summary>
	/// <param name="key"></param>
	/// <returns></returns>
	public void Begin()
	{
		//@EVENTSHOPからの呼び出しは一旦破棄
		if (sysStateCode == SystemStateCode.Shop_CallEventShop)
			return;

		switch (begintype)
		{
			case BeginType.SHOP:
				if (sysStateCode == SystemStateCode.Normal)
					calledWhenNormal = true;
				else
					calledWhenNormal = false;
				sysStateCode = SystemStateCode.Shop_Begin;
				break;
			case BeginType.TRAIN:
				sysStateCode = SystemStateCode.Train_Begin;
				break;
			case BeginType.AFTERTRAIN:
				sysStateCode = SystemStateCode.AfterTrain_Begin;
				break;
			case BeginType.ABLUP:
				sysStateCode = SystemStateCode.Ablup_Begin;
				break;
			case BeginType.TURNEND:
				sysStateCode = SystemStateCode.Turnend_Begin;
				break;
			case BeginType.FIRST:
				sysStateCode = SystemStateCode.First_Begin;
				break;
			case BeginType.TITLE:
				sysStateCode = SystemStateCode.Title_Begin;
				break;
				//セット時に判定してるので、ここには来ないはず
				//default:
				//    throw new ExeEE("不適当なBEGIN呼び出し");
		}
		if (RuntimeEnvironment.DebugMode)
		{
			console.DebugClearTraceLog();
			console.DebugAddTraceLog("BEGIN:" + begintype.ToString());
		}
		foreach (CalledFunction called in functionList)
			if (called.CurrentLabel.hasPrivDynamicVar)
				called.CurrentLabel.ScopeOut();
		functionList.Clear();
		begintype = BeginType.NULL;
		return;
	}

	/// <summary>
	/// システムによる強制的なBEGIN
	/// </summary>
	/// <param name="type"></param>
	public void Begin(BeginType type)
	{
		begintype = type;
		sysStateCode = SystemStateCode.Title_Begin;
		Begin();
	}

	public LogicalLine GetCurrentReturnAddress
	{
		get
		{
			if (functionList.Count == currentMin)
				return null;
			return functionList[^1].ReturnAddress;
		}
	}

	public LogicalLine GetReturnAddressSequensial(int curerntDepth)
	{
		if (functionList.Count == currentMin)
			return null;
		return functionList[functionList.Count - curerntDepth - 1].ReturnAddress;
	}

	public string Scope
	{
		get
		{
			//スクリプトの実行中処理からしか呼び出されないので、ここはない…はず
			//if (functionList.Count == 0)
			//{
			//    throw new ExeEE("実行中の関数が存在しません");
			//}
			if (functionList.Count == 0)
				return null;//1756 デバッグコマンドから呼び出されるようになったので
			return functionList[^1].FunctionName;
		}
	}

	public void Return(long ret)
	{
		if (IsFunctionMethod)
		{
			ReturnF(null);
			return;
		}
		//sequential = false;//いずれにしろ順列ではない。
		//呼び出し元は全部スクリプト処理
		//if (functionList.Count == 0)
		//{
		//    throw new ExeEE("実行中の関数が存在しません");
		//}
		CalledFunction called = functionList[^1];
		if (called.IsJump)
		{//JUMPした場合。即座にRETURN RESULTする。
			if (called.TopLabel.hasPrivDynamicVar)
				called.TopLabel.ScopeOut();
			functionList.Remove(called);
			if (RuntimeEnvironment.DebugMode)
				console.DebugRemoveTraceLog();
			Return(ret);
			return;
		}
		if (!called.IsEvent)
		{
			if (called.TopLabel.hasPrivDynamicVar)
				called.TopLabel.ScopeOut();
			currentLine = null;
		}
		else
		{
			if (called.CurrentLabel.hasPrivDynamicVar)
				called.CurrentLabel.ScopeOut();
			//#Singleフラグ付き関数で1が返された。
			//1752 非0ではなく1と等価であることを見るように修正
			//1756 全てを終了ではなく#PRIや#LATERのグループごとに修正
			if (called.IsOnly)
				called.FinishEvent();
			else if (called.HasSingleFlag && ret == 1)
				called.ShiftNextGroup();
			else
				called.ShiftNext();//次の同名関数に進む。
			currentLine = called.CurrentLabel;//関数の始点(@～～)へ移動。呼ぶべき関数が無ければnull
			if (called.CurrentLabel != null)
			{
				lineCount++;
				if (called.CurrentLabel.hasPrivDynamicVar)
					called.CurrentLabel.ScopeIn();
			}
		}
		if (RuntimeEnvironment.DebugMode)
			console.DebugRemoveTraceLog();
		//関数終了
		if (currentLine == null)
		{
			currentLine = called.ReturnAddress;
			functionList.RemoveAt(functionList.Count - 1);
			if (currentLine == null)
			{
				//この時点でfunctionListは空のはず
				//functionList.Clear();//全て終了。stateEndProcessに処理を返す
				if (begintype != BeginType.NULL)//BEGIN XXが行なわれていれば
				{
					Begin();
				}
				return;
			}
			lineCount++;
			//ShfitNextLine();
			return;
		}
		else if (RuntimeEnvironment.DebugMode)
		{
			FunctionLabelLine label = called.CurrentLabel;
			long line = currentLine.Position.Value.LineNo;
			console.DebugAddTraceLog(string.Format(trsl.DebugTraceCall.Text, label.LabelName, label.Position.Value.Filename, label.Position.Value.LineNo, line));
		}
		lineCount++;
		//ShfitNextLine();
		return;
	}

	public void IntoFunction(CalledFunction call, UserDefinedFunctionArgument srcArgs, ExpressionMediator exm)
	{

		if (call.IsEvent)
		{
			foreach (CalledFunction called in functionList)
			{
				if (called.IsEvent)
					throw new CodeEE(trerror.CalleventBeforeFinishEvent.Text);
			}
		}
		if (RuntimeEnvironment.DebugMode)
		{
			FunctionLabelLine label = call.CurrentLabel;
			if (exm != null)
			{
				long line = exm.Process.getCurrentLine.Position.Value.LineNo;
				if (call.IsJump)
					console.DebugAddTraceLog(string.Format(trsl.DebugTraceJump2.Text, label.LabelName, label.Position.Value.Filename, label.Position.Value.LineNo, line));
				else
					console.DebugAddTraceLog(string.Format(trsl.DebugTraceCall2.Text, label.LabelName, label.Position.Value.Filename, label.Position.Value.LineNo, line));
			}
			else
			{
				if (call.IsJump)
					console.DebugAddTraceLog(string.Format(trsl.DebugTraceJump.Text, label.LabelName, label.Position.Value.Filename, label.Position.Value.LineNo));
				else
				{
					string trace = $"CALL @{label.LabelName}:{label.Position.Value.Filename}:{label.Position.Value.LineNo}";
					if (call.ReturnAddress != null)
						trace += $" at @{call.ReturnAddress.ParentLabelLine.LabelName}:{call.ReturnAddress.ParentLabelLine.Position.Value.Filename}:{call.ReturnAddress.Position.Value.LineNo}";
					console.DebugAddTraceLog(trace);
				}
			}
		}
		if (srcArgs != null)
		{
			//引数の値を確定させる
			srcArgs.SetTransporter(exm);
			//プライベート変数更新
			if (call.TopLabel.hasPrivDynamicVar)
				call.TopLabel.ScopeIn();
			//更新した変数へ引数を代入
			for (int i = 0; i < call.TopLabel.Arg.Length; i++)
			{
				if (srcArgs.Arguments[i] != null)
				{
					if (call.TopLabel.Arg[i].Identifier.IsReference)
						((ReferenceToken)call.TopLabel.Arg[i].Identifier).SetRef(srcArgs.TransporterRef[i]);
					else if (srcArgs.Arguments[i].GetOperandType() == typeof(long))
						call.TopLabel.Arg[i].SetValue(srcArgs.TransporterInt[i], exm);
					else
						call.TopLabel.Arg[i].SetValue(srcArgs.TransporterStr[i], exm);
				}
			}
		}
		else//こっちに来るのはシステムからの呼び出し=引数は存在しない関数のみ ifネストの外に出していい気もしないでもないがはてさて
		{
			//プライベート変数更新
			if (call.TopLabel.hasPrivDynamicVar)
				call.TopLabel.ScopeIn();
		}
		functionList.Add(call);
		//sequential = false;
		currentLine = call.CurrentLabel;
		lineCount++;
		//ShfitNextLine();
	}

	#region userdifinedmethod
	public bool IsFunctionMethod
	{
		get
		{
			return functionList[currentMin].TopLabel.IsMethod;
		}
	}

	public SingleTerm MethodReturnValue;

	public void ReturnF(SingleTerm ret)
	{
		//読み込み時のチェック済みのはず
		//if (!IsFunctionMethod)
		//    throw new ExeEE("ReturnFと#FUNCTIONのチェックがおかしい");
		//sequential = false;//いずれにしろ順列ではない。
		//呼び出し元はRETURNFコマンドか関数終了時のみ
		//if (functionList.Count == 0)
		//    throw new ExeEE("実行中の関数が存在しません");
		//非イベント呼び出しなので、これは起こりえない
		//else if (functionList.Count != 1)
		//    throw new ExeEE("関数が複数ある");
		if (RuntimeEnvironment.DebugMode)
		{
			console.DebugRemoveTraceLog();
		}
		//OutはGetValue側で行う
		//functionList[0].TopLabel.Out();
		currentLine = functionList[^1].ReturnAddress;
		functionList.RemoveAt(functionList.Count - 1);
		//nextLine = null;
		MethodReturnValue = ret;
		return;
	}

	#endregion

	bool isClone;
	public bool IsClone { get { return isClone; } set { isClone = value; } }

	// functionListのコピーを必要とする呼び出し元が無かったのでコピーしないことにする。
	public ProcessState Clone()
	{
		ProcessState ret = new(console, getMethodStackDepth)
		{
			isClone = true,
			//どうせ消すからコピー不要
			//foreach (CalledFunction func in functionList)
			//	ret.functionList.Add(func.Clone());
			currentLine = currentLine,
			//ret.nextLine = this.nextLine;
			//ret.sequential = this.sequential;
			sysStateCode = sysStateCode,
			begintype = begintype
		};
		//ret.MethodReturnValue = this.MethodReturnValue;
		return ret;

	}
	//public ProcessState CloneForFunctionMethod()
	//{
	//    ProcessState ret = new ProcessState(console);
	//    ret.isClone = true;

	//    //どうせ消すからコピー不要
	//    //foreach (CalledFunction func in functionList)
	//    //	ret.functionList.Add(func.Clone());
	//    ret.currentLine = this.currentLine;
	//    ret.nextLine = this.nextLine;
	//    //ret.sequential = this.sequential;
	//    ret.sysStateCode = this.sysStateCode;
	//    ret.begintype = this.begintype;
	//    //ret.MethodReturnValue = this.MethodReturnValue;
	//    return ret;
	//}
}
