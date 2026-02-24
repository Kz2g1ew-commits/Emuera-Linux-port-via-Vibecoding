using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Config.JSON;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.UI;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.UI.Game;
using MinorShift.Emuera.UI.Game.Image;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime;
using MixedNum = MinorShift.Emuera.Runtime.Utils.EvilMask.Utils.MixedNum;

namespace MinorShift.Emuera;
#nullable enable
static partial class Program
{
static EmueraConsole CurrentConsoleHost => RuntimeHost.GetConsoleHost() as EmueraConsole;

	/*
	コードの開始地点。
	ここでMainWindowを作り、
	MainWindowがProcessを作り、
	ProcessがGameBase・ConstantData・Variableを作る。


	*.ERBの読み込み、実行、その他の処理をProcessが、
	入出力をMainWindowが、
	定数の保存をConstantDataが、
	変数の管理をVariableが行う。

	と言う予定だったが改変するうちに境界が曖昧になってしまった。

	後にEmueraConsoleを追加し、それに入出力を担当させることに。

	1750 DebugConsole追加
	 Debugを全て切り離すことはできないので一部EmueraConsoleにも担当させる

	TODO: 1819 MainWindow & Consoleの入力・表示組とProcess&Dataのデータ処理組だけでも分離したい

	*/
	/// <summary>
	/// アプリケーションのメイン エントリ ポイントです。
	/// </summary>
	[STAThread]
	static void Main(string[] args)
	{
		// memo: Shift-JISを扱うためのおまじない
		System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

		var rootCommand = new RootCommand("Emuera");

		#region eee_カレントディレクトリー
		var exeDirOption = new Option<string>(
			name: "--ExeDir",
			description: "与えられたフォルダのEraを起動します"
		);
		exeDirOption.AddAlias("-exedir");
		exeDirOption.AddAlias("-EXEDIR");
		rootCommand.AddOption(exeDirOption);

		var debugModeOption = new Option<bool>(
			name: "-Debug",
			description: "デバッグモード"
		);
		debugModeOption.AddAlias("-debug");
		debugModeOption.AddAlias("-DEBUG");
		rootCommand.AddOption(debugModeOption);

		var genLangOption = new Option<bool>(
			name: "-GenLang",
			description: "言語ファイルテンプレ生成"
		);
		genLangOption.AddAlias("-genlang");
		genLangOption.AddAlias("-GENLANG");
		rootCommand.AddOption(genLangOption);

		var filesArg = new Argument<string[]>(
					"解析するファイル"
				)
		{ Arity = ArgumentArity.ZeroOrMore };
		rootCommand.AddArgument(filesArg);

		var result = rootCommand.Parse(args);

		//実行ディレクトリが引数で与えられた場合t
		var exeDir = result.GetValueForOption(exeDirOption);
		if (exeDir != null)
		{
			SetDirPaths(exeDir);
		}
		else
		{
			SetDirPaths(AssemblyData.WorkingDir);
		}

		#endregion
		//エラー出力用
		//1815 .exeが東方板のNGワードに引っかかるそうなので除去
		ExeName = Path.GetFileNameWithoutExtension(AssemblyData.ExeName);


		var debugMode = result.GetValueForOption(debugModeOption);
		DebugMode = debugMode;
		RuntimeEnvironment.SetModes(DebugMode, AnalysisMode);

		var genLang = result.GetValueForOption(genLangOption);
		if (genLang)
			Lang.GenerateDefaultLangFile();

		#region EM_私家版_Emuera多言語化改造
		List<string> otherArgs = [];

		//引数の後ろにある他のフラグにマッチしなかった文字列を解析指定されたファイルとみなす
		var fileArgs = result.GetValueForArgument(filesArg);
		var analysisRequestPaths = fileArgs;
		if (analysisRequestPaths.Length > 0)
		{
			/*
			foreach (var arg in args)
			{

				//if ((args.Length > 0) && (args[0].Equals("-DEBUG", StringComparison.CurrentCultureIgnoreCase)))
				if (arg.Equals("-DEBUG", StringComparison.CurrentCultureIgnoreCase))
				{
					// argsStart = 1;//デバッグモードかつ解析モード時に最初の1っこ(-DEBUG)を飛ばす
					DebugMode = true;
				}
				else if (arg.Equals("-GENLANG", StringComparison.CurrentCultureIgnoreCase))
				{
					Lang.GenerateDefaultLangFile();
				}
				else otherArgs.Add(arg);
			}
			//if (args.Length > argsStart)
			if (otherArgs.Count > 0)
			{
			*/
			//必要なファイルのチェックにはConfig読み込みが必須なので、ここではフラグだけ立てておく
			AnalysisMode = true;
			RuntimeEnvironment.SetModes(DebugMode, AnalysisMode);
			//}
		}
		#endregion

		RuntimeHost.DoEventsHook = UiPlatformBridge.DoEvents;
		RuntimeHost.ShowInfoHook = UiPlatformBridge.ShowInfo;
		RuntimeHost.ShowInfoWithCaptionHook = UiPlatformBridge.ShowInfo;
		RuntimeHost.GetConsoleHostHook = static () => null;
		RuntimeHost.CreateRuntimeProcessHook = static (console) => new GameProc.Process(console);
			RuntimeHost.SetClipboardTextHook = UiPlatformBridge.SetClipboardText;
			RuntimeHost.SetWindowTitleHook = static (title) =>
			{
				CurrentConsoleHost?.SetWindowTitle(title);
			};
			RuntimeHost.GetWindowTitleHook = static () => CurrentConsoleHost?.GetWindowTitle() ?? string.Empty;
			RuntimeHost.GetConsoleLineNoHook = static () => CurrentConsoleHost?.GetLineNo ?? 0;
			RuntimeHost.GetConsoleClientHeightHook = static () => CurrentConsoleHost?.ClientHeight ?? 0;
			RuntimeHost.ConfirmYesNoHook = UiPlatformBridge.ConfirmYesNo;
		RuntimeHost.ResolveDrawLineStringHook = static (fallback) => CurrentConsoleHost?.getDefStBar() ?? fallback;
		RuntimeHost.FormatDrawLineStringHook = static (raw) => CurrentConsoleHost?.getStBar(raw) ?? raw;
		RuntimeHost.InitializeDrawLineStringHook = static (raw) => CurrentConsoleHost?.setStBar(raw);
		RuntimeHost.HtmlTagSplitHook = static (raw) => MinorShift.Emuera.UI.Game.HtmlManager.HtmlTagSplit(raw);
		RuntimeHost.HtmlLengthHook = static (raw) => MinorShift.Emuera.UI.Game.HtmlManager.HtmlLength(raw);
		RuntimeHost.HtmlSubStringHook = static (raw, width) => MinorShift.Emuera.UI.Game.HtmlManager.HtmlSubString(raw, width);
		RuntimeHost.HtmlToPlainTextHook = static (raw) => MinorShift.Emuera.UI.Game.HtmlManager.Html2PlainText(raw);
		RuntimeHost.HtmlEscapeHook = static (raw) => MinorShift.Emuera.UI.Game.HtmlManager.Escape(raw);
		RuntimeHost.PlayErrorToneHook = static () => System.Media.SystemSounds.Hand.Play();
		RuntimeHost.PlayNotifyToneHook = static () => System.Media.SystemSounds.Asterisk.Play();
		RuntimeHost.ResolveNamedColorRgbHook = UiPlatformBridge.ResolveNamedColorRgb;
		RuntimeHost.IsFontInstalledHook = UiPlatformBridge.IsFontInstalled;
		RuntimeHost.GetDisplayLineTextHook = static (lineNo) =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole == null || lineNo < 0 || lineNo >= processConsole.DisplayLineList.Count)
				return string.Empty;
			return processConsole.DisplayLineList[(int)lineNo].ToString();
		};
		RuntimeHost.GetDisplayLineHtmlHook = static (lineNo) =>
		{
			var dispLines = CurrentConsoleHost?.GetDisplayLines(lineNo);
			if (dispLines == null)
				return string.Empty;
			return MinorShift.Emuera.UI.Game.HtmlManager.DisplayLine2Html(dispLines, true);
		};
		RuntimeHost.PopDisplayLineHtmlHook = static () =>
		{
			var dispLines = CurrentConsoleHost?.PopDisplayingLines();
			if (dispLines == null)
				return string.Empty;
			return MinorShift.Emuera.UI.Game.HtmlManager.DisplayLine2Html(dispLines, false);
		};
		RuntimeHost.GetPointingButtonInputHook = static () =>
		{
			var processConsole = CurrentConsoleHost;
			processConsole?.RefreshMousePointingState();
			if (processConsole?.PointingSring == null || !processConsole.PointingSring.IsButton)
				return string.Empty;
			if (processConsole.PointingSring.IsInteger)
				return processConsole.PointingSring.Input.ToString();
			return processConsole.PointingSring.Inputs;
		};
		RuntimeHost.CbgClearHook = static () => CurrentConsoleHost?.CBG_Clear();
		RuntimeHost.CbgClearRangeHook = static (zmin, zmax) => CurrentConsoleHost?.CBG_ClearRange(zmin, zmax);
		RuntimeHost.CbgClearButtonHook = static () => CurrentConsoleHost?.CBG_ClearButton();
		RuntimeHost.CbgClearButtonMapHook = static () => CurrentConsoleHost?.CBG_ClearBMap();
		RuntimeHost.CbgSetGraphicsHook = static (graphics, x, y, zdepth) =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole == null || graphics is not GraphicsImage g)
				return false;
			return processConsole.CBG_SetGraphics(g, x, y, zdepth);
		};
		RuntimeHost.CbgSetButtonMapHook = static (graphics) =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole == null || graphics is not GraphicsImage g)
				return false;
			return processConsole.CBG_SetButtonMap(g);
		};
		RuntimeHost.CbgSetImageHook = static (image, x, y, zdepth) =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole == null || image is not ASprite sprite)
				return false;
			return processConsole.CBG_SetImage(sprite, x, y, zdepth);
		};
		RuntimeHost.CbgSetButtonImageHook = static (buttonValue, imageN, imageB, x, y, zdepth, tooltip) =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole == null || imageN is not ASprite spriteN || imageB is not ASprite spriteB)
				return false;
			return processConsole.CBG_SetButtonImage(buttonValue, spriteN, spriteB, x, y, zdepth, tooltip);
		};
		RuntimeHost.ApplyTextBoxChangesHook = static () => CurrentConsoleHost?.ApplyTextBoxChanges();
		RuntimeHost.AddBackgroundImageHook = static (name, depth, opacity) => CurrentConsoleHost?.AddBackgroundImage(name, depth, opacity);
		RuntimeHost.RemoveBackgroundImageHook = static (name) => CurrentConsoleHost?.RemoveBackground(name);
		RuntimeHost.ClearBackgroundImageHook = static () => CurrentConsoleHost?.ClearBackgroundImage();
		RuntimeHost.SetToolTipColorRgbHook = static (foreColorRgb, backColorRgb) => CurrentConsoleHost?.SetToolTipColorRgb(foreColorRgb, backColorRgb);
		RuntimeHost.SetToolTipDelayHook = static (delay) => CurrentConsoleHost?.SetToolTipDelay(delay);
		RuntimeHost.SetToolTipDurationHook = static (duration) => CurrentConsoleHost?.SetToolTipDuration(duration);
		RuntimeHost.SetToolTipFontNameHook = static (fontName) => CurrentConsoleHost?.SetToolTipFontName(fontName);
			RuntimeHost.SetToolTipFontSizeHook = static (fontSize) => CurrentConsoleHost?.SetToolTipFontSize(fontSize);
			RuntimeHost.SetCustomToolTipHook = static (enabled) => CurrentConsoleHost?.CustomToolTip(enabled);
			RuntimeHost.SetToolTipFormatHook = static (format) => CurrentConsoleHost?.SetToolTipFormat(format);
			RuntimeHost.SetToolTipImageEnabledHook = static (enabled) => CurrentConsoleHost?.SetToolTipImg(enabled);
			RuntimeHost.IsCtrlZEnabledHook = static () => Config.Ctrl_Z_Enabled;
			RuntimeHost.CaptureRandomSeedHook = static (seedBuffer) => RuntimeGlobals.VEvaluator?.Rand.GetRand(seedBuffer);
			RuntimeHost.CtrlZAddInputHook = static (input) => CurrentConsoleHost?.CtrlZState.Add(input);
			RuntimeHost.CtrlZOnSavePrepareHook = static (saveSlot) => CurrentConsoleHost?.CtrlZState.OnSavePrepare(saveSlot);
			RuntimeHost.CtrlZOnSaveHook = static () => CurrentConsoleHost?.CtrlZState.OnSave();
			RuntimeHost.CtrlZOnLoadHook = static (saveSlot) => CurrentConsoleHost?.CtrlZState.OnLoad(saveSlot);
		RuntimeHost.PrintHtmlHook = static (html, toBuffer) => CurrentConsoleHost?.PrintHtml(html, toBuffer);
		RuntimeHost.PrintHtmlIslandHook = static (html) => CurrentConsoleHost?.PrintHTMLIsland(html);
		RuntimeHost.ClearHtmlIslandHook = static () => CurrentConsoleHost?.ClearHTMLIsland();
		RuntimeHost.SetUseUserStyleHook = static (enabled) =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole != null)
				processConsole.UseUserStyle = enabled;
		};
		RuntimeHost.SetUseSetColorStyleHook = static (enabled) =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole != null)
				processConsole.UseSetColorStyle = enabled;
		};
		RuntimeHost.SetBackgroundColorRgbHook = static (rgb) => CurrentConsoleHost?.SetBackgroundColorRgb(rgb);
		RuntimeHost.GetBackgroundColorRgbHook = static () => CurrentConsoleHost?.GetBackgroundColorRgb() ?? Config.BackColorRuntime.ToRgb24();
		RuntimeHost.SetStringColorRgbHook = static (rgb) => CurrentConsoleHost?.SetStringColorRgb(rgb);
		RuntimeHost.GetStringColorRgbHook = static () => CurrentConsoleHost?.GetStringColorRgb() ?? Config.ForeColorRuntime.ToRgb24();
		RuntimeHost.SetStringStyleFlagsHook = static (styleFlags) => CurrentConsoleHost?.SetStringStyleFlags(styleFlags);
		RuntimeHost.GetStringStyleFlagsHook = static () => CurrentConsoleHost?.GetStringStyleFlags() ?? RuntimeFontStyleFlags.Regular;
		RuntimeHost.SetFontHook = static (fontName) => CurrentConsoleHost?.SetFont(fontName);
		RuntimeHost.GetFontNameHook = static () => CurrentConsoleHost?.GetFontName() ?? Config.FontName;
		RuntimeHost.SetAlignmentHook = static (alignment) =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole != null)
				processConsole.Alignment = (DisplayLineAlignment)alignment;
		};
		RuntimeHost.GetAlignmentHook = static () =>
		{
			var processConsole = CurrentConsoleHost;
			return processConsole == null ? RuntimeDisplayLineAlignment.LEFT : (RuntimeDisplayLineAlignment)processConsole.Alignment;
		};
		RuntimeHost.SetRedrawHook = static (redraw) => CurrentConsoleHost?.SetRedraw(redraw);
		RuntimeHost.GetRedrawModeHook = static () => CurrentConsoleHost == null || CurrentConsoleHost.Redraw == ConsoleRedraw.None ? RuntimeRedrawMode.None : RuntimeRedrawMode.Normal;
		RuntimeHost.PrintButtonStringHook = static (text, input) => CurrentConsoleHost?.PrintButton(text, input);
		RuntimeHost.PrintButtonLongHook = static (text, input) => CurrentConsoleHost?.PrintButton(text, input);
		RuntimeHost.PrintButtonCStringHook = static (text, input, isRight) => CurrentConsoleHost?.PrintButtonC(text, input, isRight);
		RuntimeHost.PrintButtonCLongHook = static (text, input, isRight) => CurrentConsoleHost?.PrintButtonC(text, input, isRight);
		RuntimeHost.PrintPlainSingleLineHook = static (text) => CurrentConsoleHost?.PrintPlainSingleLine(text);
		RuntimeHost.PrintErrorButtonHook = static (text, position, level) => CurrentConsoleHost?.PrintErrorButton(text, position, level);
		RuntimeHost.PrintPlainHook = static (text) => CurrentConsoleHost?.PrintPlain(text);
		RuntimeHost.ClearTextHook = static () => CurrentConsoleHost?.ClearText();
		RuntimeHost.ReloadErbFinishedHook = static () => CurrentConsoleHost?.ReloadErbFinished();
		RuntimeHost.IsLastLineEmptyHook = static () => CurrentConsoleHost?.LastLineIsEmpty ?? false;
		RuntimeHost.IsLastLineTemporaryHook = static () => CurrentConsoleHost?.LastLineIsTemporary ?? false;
		RuntimeHost.IsPrintBufferEmptyHook = static () => CurrentConsoleHost?.IsPrintBufferEmpty ?? true;
		RuntimeHost.CountInteractiveButtonsHook = static (integerOnly) => CurrentConsoleHost?.CountInteractiveButtons(integerOnly) ?? 0;
		RuntimeHost.GetConsoleClientWidthHook = static () => CurrentConsoleHost?.ClientWidth ?? 0;
		RuntimeHost.IsConsoleActiveHook = static () => CurrentConsoleHost?.IsActive ?? false;
		RuntimeHost.GetMousePositionXYHook = static () => CurrentConsoleHost?.GetMousePositionXY() ?? new RuntimePoint(0, 0);
		RuntimeHost.MoveMouseXYHook = static (x, y) => CurrentConsoleHost?.MoveMouseXY(x, y) ?? false;
		RuntimeHost.SetBitmapCacheEnabledForNextLineHook = static (enabled) =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole != null)
				processConsole.bitmapCacheEnabledForNextLine = enabled;
		};
		RuntimeHost.SetRedrawTimerHook = static (tickCount) => CurrentConsoleHost?.setRedrawTimer(tickCount);
		RuntimeHost.GetTextBoxTextHook = static () => CurrentConsoleHost?.GetTextBoxText() ?? string.Empty;
		RuntimeHost.ChangeTextBoxHook = static (text) => CurrentConsoleHost?.ChangeTextBox(text);
		RuntimeHost.ResetTextBoxPosHook = static () => CurrentConsoleHost?.ResetTextBoxPos();
		RuntimeHost.SetTextBoxPosHook = static (xOffset, yOffset, width) => CurrentConsoleHost?.SetTextBoxPos(xOffset, yOffset, width);
		RuntimeHost.HotkeyStateSetHook = static (key, value) => CurrentConsoleHost?.HotkeyStateSet(key, value);
		RuntimeHost.HotkeyStateInitHook = static (key) => CurrentConsoleHost?.HotkeyStateInit(key);
		RuntimeHost.PrintBarHook = static () => CurrentConsoleHost?.PrintBar();
		RuntimeHost.PrintCustomBarHook = static (barText, isConst) => CurrentConsoleHost?.printCustomBar(barText, isConst);
		RuntimeHost.DeleteLineHook = static (lineCount) => CurrentConsoleHost?.deleteLine(lineCount);
		RuntimeHost.ClearDisplayHook = static () => CurrentConsoleHost?.ClearDisplay();
		RuntimeHost.ResetStyleHook = static () => CurrentConsoleHost?.ResetStyle();
		RuntimeHost.ThrowErrorHook = static (playSound) => CurrentConsoleHost?.ThrowError(playSound);
		RuntimeHost.ForceStopTimerHook = static () => CurrentConsoleHost?.forceStopTimer();
		RuntimeHost.DebugPrintHook = static (text) => CurrentConsoleHost?.DebugPrint(text);
		RuntimeHost.DebugNewLineHook = static () => CurrentConsoleHost?.DebugNewLine();
		RuntimeHost.DebugClearHook = static () => CurrentConsoleHost?.DebugClear();
		RuntimeHost.PrintTemporaryLineHook = static (text) => CurrentConsoleHost?.PrintTemporaryLine(text);
		RuntimeHost.PrintFlushHook = static (force) => CurrentConsoleHost?.PrintFlush(force);
		RuntimeHost.RefreshStringsHook = static (forcePaint) => CurrentConsoleHost?.RefreshStrings(forcePaint);
		RuntimeHost.MarkUpdatedGenerationHook = static () =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole != null)
				processConsole.updatedGeneration = true;
		};
		RuntimeHost.DisableOutputLogHook = static () =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole != null)
				processConsole.noOutputLog = true;
		};
		RuntimeHost.OutputLogHook = static (filename, hideInfo) => CurrentConsoleHost?.OutputLog(filename, hideInfo) ?? false;
		RuntimeHost.OutputSystemLogHook = static (filename) => CurrentConsoleHost?.OutputSystemLog(filename) ?? false;
		RuntimeHost.ThrowTitleErrorHook = static (error) => CurrentConsoleHost?.ThrowTitleError(error);
		RuntimeHost.PrintImageHook = static (name, nameb, namem, height, width, yOffset) =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole == null)
				return;
			processConsole.PrintImg(name, nameb, namem, height as MixedNum, width as MixedNum, yOffset as MixedNum);
		};
		RuntimeHost.PrintShapeHook = static (shapeType, parameters) =>
		{
			var processConsole = CurrentConsoleHost;
			if (processConsole == null)
				return;
			var mixedParameters = new MixedNum[parameters?.Length ?? 0];
			for (var i = 0; i < mixedParameters.Length; i++)
				mixedParameters[i] = parameters[i] as MixedNum;
			processConsole.PrintShape(shapeType, mixedParameters);
		};
		RuntimeHost.SetGameBaseDataHook = static (_) => { };
		RuntimeHost.SetConstantDataHook = static (_) => { };
		RuntimeHost.SetVariableEvaluatorHook = static (_) => { };
		RuntimeHost.SetVariableDataHook = static (_) => { };
		RuntimeHost.SetIdentifierDictionaryHook = static (_) => { };
		RuntimeHost.SetLabelDictionaryHook = static (_) => { };
		RuntimeHost.GetKeyStateHook = UiPlatformBridge.GetKeyState;
		RuntimeHost.LoadImageHook = static (filePath) => MinorShift.Emuera.UI.Game.Image.ImgUtils.LoadImage(filePath);
		RuntimeHost.OpenUrlHook = static (url) =>
		{
			try
			{
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
				{
					UseShellExecute = true,
					FileName = url,
				});
				return true;
			}
			catch
			{
				return false;
			}
		};
		RuntimeHost.ProductVersionHook = static () => AssemblyData.EmueraVersionText;
		RuntimeHost.LoadContentsHook = static (reload) => AppContents.LoadContents(reload);
		RuntimeHost.UnloadTempLoadedConstImagesHook = static () => AppContents.UnloadTempLoadedConstImageNames();
		RuntimeHost.UnloadTempLoadedGraphicsImagesHook = static () => AppContents.UnloadTempLoadedGraphicsImageNames();
		RuntimeHost.GetGraphicsHook = static (id) => AppContents.GetGraphics(id);
		RuntimeHost.GetSpriteHook = static (name) => AppContents.GetSprite(name);
		RuntimeHost.CreateSpriteGHook = static (name, parent, rect) =>
		{
			if (parent is GraphicsImage g)
				AppContents.CreateSpriteG(name, g, rect);
		};
		RuntimeHost.SpriteDisposeHook = static (name) => AppContents.SpriteDispose(name);
		RuntimeHost.SpriteDisposeAllHook = static (deleteCsv) => AppContents.SpriteDisposeAll(deleteCsv);
		RuntimeHost.CreateSpriteAnimeHook = static (name, width, height) => AppContents.CreateSpriteAnime(name, width, height);
		RuntimeHost.ResolveMessageHook = static (key) => key switch
		{
			"save.invalid" => Lang.Error.InvalidSaveDataFormat.Text,
			"save.invalid_num" => Lang.Error.CanNotInterpretNumValue.Text,
			"save.not_impl" => Lang.Error.NotImplement.Text,
			"preload.abnormal_encode" => Lang.Error.AbnormalEncode.Text,
			"preload.file_locked" => Lang.Error.FileUsingOtherProcess.Text,
			_ => null
		};

		ProfileOptimization.SetProfileRoot(exeDir ?? ExeDir);
		ProfileOptimization.StartProfile("profile");

		ConfigData.Instance.LoadConfig();
		JSONConfig.Load();

		//WMPも終了しておく
		/*
		FunctionIdentifier.bgm.close();
		for (int i = 0; i < FunctionIdentifier.sound.Length; i++)
		{
			if (FunctionIdentifier.sound[i] != null) FunctionIdentifier.sound[i].close();
		}
		*/

		#region EM_私家版_Emuera多言語化改造
		Lang.LoadLanguageFiles();
		Lang.SetLanguage();
		#endregion
		#region EM_私家版_Icon指定機能
		object icon = UiPlatformBridge.LoadConfiguredIcon(Config.EmueraIcon);
		#endregion

		//二重起動の禁止かつ二重起動
		if ((!Config.AllowMultipleInstances) && AssemblyData.PrevInstance())
		{
			//Dialog.Show("既に起動しています", "多重起動を許可する場合、emuera.configを書き換えて下さい");
			Dialog.Show(Lang.UI.MainWindow.MsgBox.InstaceExists.Text, Lang.UI.MainWindow.MsgBox.MultiInstanceInfo.Text);
			return;
		}
		if (!Directory.Exists(CsvDir))
		{
			//Dialog.Show("フォルダなし", "csvフォルダが見つかりません");
			Dialog.Show(Lang.UI.MainWindow.MsgBox.FolderNotFound.Text, Lang.UI.MainWindow.MsgBox.NoCsvFolder.Text);
			return;
		}
		if (!Directory.Exists(ErbDir))
		{
			//Dialog.Show("フォルダなし", "erbフォルダが見つかりません");
			Dialog.Show(Lang.UI.MainWindow.MsgBox.FolderNotFound.Text, Lang.UI.MainWindow.MsgBox.NoErbFolder.Text);
			return;
		}
		#region EE_フォントファイル対応
		//フォントファイルを読み込む
		if (Directory.Exists(FontDir))
		{
			foreach (string fontFile in Directory.GetFiles(FontDir, "*.ttf", SearchOption.AllDirectories))
				FontRegistry.Collection.AddFontFile(fontFile);

			foreach (string fontFile in Directory.GetFiles(FontDir, "*.otf", SearchOption.AllDirectories))
				FontRegistry.Collection.AddFontFile(fontFile);
		}
		#endregion

		if (DebugMode)
		{
			ConfigData.Instance.LoadDebugConfig();
			if (!Directory.Exists(DebugDir))
			{
				try
				{
					Directory.CreateDirectory(DebugDir);
				}
				catch
				{
					Dialog.Show(Lang.UI.MainWindow.MsgBox.FolderNotFound.Text, Lang.UI.MainWindow.MsgBox.FailedCreateDebugFolder.Text);
					return;
				}
			}
		}

		if (AnalysisMode)
		{
			AnalysisFiles = [];
			#region EM_私家版_Emuera多言語化改造
			// for (int i = argsStart; i < args.Length; i++)
			foreach (var path in analysisRequestPaths)
			{
				//if (!File.Exists(args[i]) && !Directory.Exists(args[i]))
				if (!File.Exists(path) && !Directory.Exists(path))
				{
						UiPlatformBridge.ShowInfo(Lang.UI.MainWindow.MsgBox.ArgPathNotExists.Text);
					return;
				}
				//if ((File.GetAttributes(args[i]) & FileAttributes.Directory) == FileAttributes.Directory)
				if ((File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory)
				{
					//List<KeyValuePair<string, string>> fnames = Config.Config.GetFiles(args[i] + "\\", "*.ERB");
					string analysisDir = Path.GetFullPath(path);
					if (!Path.EndsInDirectorySeparator(analysisDir))
						analysisDir += Path.DirectorySeparatorChar;
					List<KeyValuePair<string, string>> fnames = Config.GetFiles(analysisDir, "*.ERB");
					for (int j = 0; j < fnames.Count; j++)
					{
						AnalysisFiles.Add(fnames[j].Value);
					}
				}
				else
				{
					//if (Path.GetExtension(args[i]).ToUpper() != ".ERB")
					if (!Path.GetExtension(path).Equals(".ERB", StringComparison.OrdinalIgnoreCase))
					{
							UiPlatformBridge.ShowInfo(Lang.UI.MainWindow.MsgBox.InvalidArg.Text);
						return;
					}
					//AnalysisFiles.Add(args[i]);
					AnalysisFiles.Add(path);
				}
			}
			#endregion
		}

		if (AnalysisMode)
			RuntimeEnvironment.SetAnalysisFiles(AnalysisFiles);
		else
			RuntimeEnvironment.SetAnalysisFiles([]);

		RunWindowsGuiHost(args, icon);
	}

	[MemberNotNull(nameof(ExeDir))]
	[MemberNotNull(nameof(CsvDir))]
	[MemberNotNull(nameof(ErbDir))]
	[MemberNotNull(nameof(DebugDir))]
	[MemberNotNull(nameof(DatDir))]
	[MemberNotNull(nameof(ContentDir))]

	private static void SetDirPaths(string exeDir)
	{

		ExeDir = Path.GetFullPath(new DirectoryInfo(exeDir).FullName + Path.DirectorySeparatorChar);
		RuntimeEnvironment.SetPaths(ExeDir);

		CsvDir = RuntimeEnvironment.CsvDir;
		ErbDir = RuntimeEnvironment.ErbDir;
		DebugDir = RuntimeEnvironment.DebugDir;
		DatDir = RuntimeEnvironment.DatDir;
		ContentDir = RuntimeEnvironment.ContentDir;
		#region EE_PLAYSOUND系
		SoundDir = RuntimeEnvironment.SoundDir;
		#endregion
		#region EE_フォントファイル対応
		FontDir = RuntimeEnvironment.FontDir;
		#endregion

		/*
		CsvDir = WorkingDir + "csv\\";
		ErbDir = WorkingDir + "erb\\";
		DebugDir = WorkingDir + "debug\\";
		DatDir = WorkingDir + "dat\\";
		ContentDir = WorkingDir + "resources\\";
		#region EE_フォントファイル対応
		FontDir = WorkingDir + "font\\";
		#endregion
		*/
	}

	#region eee_カレントディレクトリー
	/// <summary>
	/// 実行ファイルのディレクトリ。最後にPath.DirectorySeparatorCharを付けたstring
	/// </summary>
	public static string ExeDir { get; private set; }
	#endregion
	public static string CsvDir { get; private set; }
	public static string ErbDir { get; private set; }
	public static string DebugDir { get; private set; }
	public static string DatDir { get; private set; }
	public static string ContentDir { get; private set; }
	public static string ExeName { get; private set; }
	#region EE_PLAYSOUND系
	public static string SoundDir { get; private set; }
	#endregion
	#region EE_フォントファイル対応
	public static string FontDir { get; private set; }
	#endregion


	public static bool rebootFlag;
	//public static int RebootClientX = 0;
	//public static int RebootClientY = 0;
	//public static Point RebootLocation;

	public static bool AnalysisMode;
	public static List<string> AnalysisFiles;

	//public static bool debugMode = false;
	//public static bool DebugMode { get { return debugMode; } }
	public static bool DebugMode { get; private set; }

	static Program()
	{
		var baseDirectory = AppContext.BaseDirectory;
		if (Directory.Exists(Path.Combine(baseDirectory, "Data", "erb")))
		{
			baseDirectory = Path.Combine(baseDirectory, "Data");
		}
		SetDirPaths(baseDirectory);
	}

	private static void RunWindowsGuiHost(string[] args, object icon)
	{
		IUiAppHost host = CreateAppUiHost();
		if (host.TryRun(args, icon))
			return;

		if (host is UnsupportedUiAppHost unsupported)
			UiPlatformBridge.ShowInfo(unsupported.Reason, "UI Host");
		else
			UiPlatformBridge.ShowInfo($"UI host '{host.Name}' failed to start.", "UI Host");
	}

	private static IUiAppHost CreateAppUiHost()
	{
		string requested = Environment.GetEnvironmentVariable("EMUERA_UI_HOST");
		if (!string.IsNullOrEmpty(requested))
		{
			if (requested.Equals("winforms", StringComparison.OrdinalIgnoreCase))
				return new Forms.WinFormsAppUiHost();
			if (requested.Equals("linux-launcher", StringComparison.OrdinalIgnoreCase) || requested.Equals("launcher", StringComparison.OrdinalIgnoreCase))
				return new LinuxLauncherUiAppHost();
			if (requested.Equals("headless", StringComparison.OrdinalIgnoreCase) || requested.Equals("none", StringComparison.OrdinalIgnoreCase))
				return new UnsupportedUiAppHost(requested, "GUI host is disabled by EMUERA_UI_HOST.");
		}

		if (OperatingSystem.IsWindows())
			return new Forms.WinFormsAppUiHost();
		if (OperatingSystem.IsLinux())
			return new LinuxLauncherUiAppHost();

		return new UnsupportedUiAppHost("unsupported", $"No GUI host backend for platform '{Environment.OSVersion.Platform}'.");
	}
}
