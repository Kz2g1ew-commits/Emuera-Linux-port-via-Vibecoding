using System;
using System.Collections.Generic;
using System.Reflection;
using MinorShift.Emuera.Runtime.Script;

namespace MinorShift.Emuera.Runtime.Utils;

public static class RuntimeHost
{
	public static Action DoEventsHook { get; set; }
	public static Action<string> ShowInfoHook { get; set; }
	public static Action<string, string> ShowInfoWithCaptionHook { get; set; }
	public static Action<string> SetClipboardTextHook { get; set; }
	public static Action<string> SetWindowTitleHook { get; set; }
	public static Func<string> GetWindowTitleHook { get; set; }
	public static Func<int> GetConsoleLineNoHook { get; set; }
	public static Func<int> GetConsoleClientHeightHook { get; set; }
	public static Func<string, string, bool> ConfirmYesNoHook { get; set; }
	public static Func<string> ProductVersionHook { get; set; }
	public static Func<bool, Exception> LoadContentsHook { get; set; }
	public static Action UnloadTempLoadedConstImagesHook { get; set; }
	public static Action UnloadTempLoadedGraphicsImagesHook { get; set; }
	public static Func<int, object> GetGraphicsHook { get; set; }
	public static Func<string, object> GetSpriteHook { get; set; }
	public static Action<string, object, RuntimeRect> CreateSpriteGHook { get; set; }
	public static Action<string> SpriteDisposeHook { get; set; }
	public static Func<bool, long> SpriteDisposeAllHook { get; set; }
	public static Action<string, int, int> CreateSpriteAnimeHook { get; set; }
	public static Func<string, string> ResolveMessageHook { get; set; }
	public static Func<string, string> ResolveDrawLineStringHook { get; set; }
	public static Func<string, string> FormatDrawLineStringHook { get; set; }
	public static Action<string> InitializeDrawLineStringHook { get; set; }
	public static Func<string, string[]> HtmlTagSplitHook { get; set; }
	public static Func<string, int> HtmlLengthHook { get; set; }
	public static Func<string, int, string[]> HtmlSubStringHook { get; set; }
	public static Func<string, string> HtmlToPlainTextHook { get; set; }
	public static Func<string, string> HtmlEscapeHook { get; set; }
	public static Action PlayErrorToneHook { get; set; }
	public static Action PlayNotifyToneHook { get; set; }
	public static Func<string, bool> OpenUrlHook { get; set; }
	public static Func<int, short> GetKeyStateHook { get; set; }
	public static Func<string, object> LoadImageHook { get; set; }
	public static Func<string, int?> ResolveNamedColorRgbHook { get; set; }
	public static Func<string, bool> IsFontInstalledHook { get; set; }
	public static Func<long, string> GetDisplayLineTextHook { get; set; }
	public static Func<long, string> GetDisplayLineHtmlHook { get; set; }
	public static Func<string> PopDisplayLineHtmlHook { get; set; }
	public static Func<string> GetPointingButtonInputHook { get; set; }
	public static Action CbgClearHook { get; set; }
	public static Action<int, int> CbgClearRangeHook { get; set; }
	public static Action CbgClearButtonHook { get; set; }
	public static Action CbgClearButtonMapHook { get; set; }
	public static Func<object, int, int, int, bool> CbgSetGraphicsHook { get; set; }
	public static Func<object, bool> CbgSetButtonMapHook { get; set; }
	public static Func<object, int, int, int, bool> CbgSetImageHook { get; set; }
	public static Func<int, object, object, int, int, int, string, bool> CbgSetButtonImageHook { get; set; }
	public static Action ApplyTextBoxChangesHook { get; set; }
	public static Action<string, long, float> AddBackgroundImageHook { get; set; }
	public static Action<string> RemoveBackgroundImageHook { get; set; }
	public static Action ClearBackgroundImageHook { get; set; }
	public static Action<int, int> SetToolTipColorRgbHook { get; set; }
	public static Action<int> SetToolTipDelayHook { get; set; }
	public static Action<int> SetToolTipDurationHook { get; set; }
	public static Action<string> SetToolTipFontNameHook { get; set; }
	public static Action<long> SetToolTipFontSizeHook { get; set; }
	public static Action<bool> SetCustomToolTipHook { get; set; }
	public static Action<long> SetToolTipFormatHook { get; set; }
	public static Action<bool> SetToolTipImageEnabledHook { get; set; }
	public static Action<string> CtrlZAddInputHook { get; set; }
	public static Action<int> CtrlZOnSavePrepareHook { get; set; }
	public static Action CtrlZOnSaveHook { get; set; }
	public static Action<int> CtrlZOnLoadHook { get; set; }
	public static Func<bool> IsCtrlZEnabledHook { get; set; }
	public static Action<long[]> CaptureRandomSeedHook { get; set; }
	public static Action<string, bool> PrintHtmlHook { get; set; }
	public static Action<string> PrintHtmlIslandHook { get; set; }
	public static Action ClearHtmlIslandHook { get; set; }
	public static Action<string, string, string, object, object, object> PrintImageHook { get; set; }
	public static Action<string, object[]> PrintShapeHook { get; set; }
	public static Action<bool> SetUseUserStyleHook { get; set; }
	public static Action<bool> SetUseSetColorStyleHook { get; set; }
	public static Action<int> SetBackgroundColorRgbHook { get; set; }
	public static Func<int> GetBackgroundColorRgbHook { get; set; }
	public static Action<int> SetStringColorRgbHook { get; set; }
	public static Func<int> GetStringColorRgbHook { get; set; }
	public static Action<RuntimeFontStyleFlags> SetStringStyleFlagsHook { get; set; }
	public static Func<RuntimeFontStyleFlags> GetStringStyleFlagsHook { get; set; }
	public static Action<string> SetFontHook { get; set; }
	public static Func<string> GetFontNameHook { get; set; }
	public static Action<RuntimeDisplayLineAlignment> SetAlignmentHook { get; set; }
	public static Func<RuntimeDisplayLineAlignment> GetAlignmentHook { get; set; }
	public static Action<long> SetRedrawHook { get; set; }
	public static Func<RuntimeRedrawMode> GetRedrawModeHook { get; set; }
	public static Action<string, string> PrintButtonStringHook { get; set; }
	public static Action<string, long> PrintButtonLongHook { get; set; }
	public static Action<string, string, bool> PrintButtonCStringHook { get; set; }
	public static Action<string, long, bool> PrintButtonCLongHook { get; set; }
	public static Action<string> PrintPlainSingleLineHook { get; set; }
	public static Action<string, ScriptPosition?, int> PrintErrorButtonHook { get; set; }
	public static Action<string> PrintPlainHook { get; set; }
	public static Action ClearTextHook { get; set; }
	public static Action ReloadErbFinishedHook { get; set; }
	public static Func<bool> IsLastLineEmptyHook { get; set; }
	public static Func<bool> IsLastLineTemporaryHook { get; set; }
	public static Func<bool> IsPrintBufferEmptyHook { get; set; }
	public static Func<bool, int> CountInteractiveButtonsHook { get; set; }
	public static Func<int> GetConsoleClientWidthHook { get; set; }
	public static Func<bool> IsConsoleActiveHook { get; set; }
	public static Func<RuntimePoint> GetMousePositionXYHook { get; set; }
	public static Func<int, int, bool> MoveMouseXYHook { get; set; }
	public static Action<bool> SetBitmapCacheEnabledForNextLineHook { get; set; }
	public static Action<int> SetRedrawTimerHook { get; set; }
	public static Func<string> GetTextBoxTextHook { get; set; }
	public static Action<string> ChangeTextBoxHook { get; set; }
	public static Action ResetTextBoxPosHook { get; set; }
	public static Action<int, int, int> SetTextBoxPosHook { get; set; }
	public static Action<nint, nint> HotkeyStateSetHook { get; set; }
	public static Action<nint> HotkeyStateInitHook { get; set; }
	public static Action PrintBarHook { get; set; }
	public static Action<string, bool> PrintCustomBarHook { get; set; }
	public static Action<int> DeleteLineHook { get; set; }
	public static Action ClearDisplayHook { get; set; }
	public static Action ResetStyleHook { get; set; }
	public static Action<bool> ThrowErrorHook { get; set; }
	public static Action ForceStopTimerHook { get; set; }
	public static Action<string> DebugPrintHook { get; set; }
	public static Action DebugNewLineHook { get; set; }
	public static Action DebugClearHook { get; set; }
	public static Action<string> PrintTemporaryLineHook { get; set; }
	public static Action<bool> PrintFlushHook { get; set; }
	public static Action<bool> RefreshStringsHook { get; set; }
	public static Action MarkUpdatedGenerationHook { get; set; }
	public static Action DisableOutputLogHook { get; set; }
	public static Func<string, bool, bool> OutputLogHook { get; set; }
	public static Func<string, bool> OutputSystemLogHook { get; set; }
	public static Action<bool> ThrowTitleErrorHook { get; set; }
	public static Action<object> SetGameBaseDataHook { get; set; }
	public static Action<object> SetConstantDataHook { get; set; }
	public static Action<object> SetVariableEvaluatorHook { get; set; }
	public static Action<object> SetVariableDataHook { get; set; }
	public static Action<object> SetIdentifierDictionaryHook { get; set; }
	public static Action<object> SetLabelDictionaryHook { get; set; }
	public static Func<object> GetGameBaseDataHook { get; set; }
	public static Func<object> GetConstantDataHook { get; set; }
	public static Func<object> GetVariableEvaluatorHook { get; set; }
	public static Func<object> GetVariableDataHook { get; set; }
	public static Func<object> GetIdentifierDictionaryHook { get; set; }
	public static Func<object> GetLabelDictionaryHook { get; set; }
	public static Func<object> GetConsoleHostHook { get; set; }
	public static Func<IExecutionConsole, IRuntimeProcess> CreateRuntimeProcessHook { get; set; }
	static object gameBaseData;
	static object constantData;
	static object variableEvaluator;
	static object variableData;
	static object identifierDictionary;
	static object labelDictionary;
	static readonly Dictionary<string, long> analysisLabelCounters = new(StringComparer.OrdinalIgnoreCase);
	static readonly List<object> debugStack = [];

	public static void DoEvents()
	{
		DoEventsHook?.Invoke();
	}

	public static void ShowInfo(string message)
	{
		ShowInfoHook?.Invoke(message);
	}

	public static void ShowInfo(string message, string caption)
	{
		if (ShowInfoWithCaptionHook != null)
		{
			ShowInfoWithCaptionHook(message, caption);
			return;
		}
		ShowInfo(message);
	}

	public static void SetClipboardText(string text)
	{
		SetClipboardTextHook?.Invoke(text);
	}

	public static void SetWindowTitle(string title)
	{
		SetWindowTitleHook?.Invoke(title);
	}

	public static string GetWindowTitle()
	{
		return GetWindowTitleHook?.Invoke() ?? string.Empty;
	}

	public static int GetConsoleLineNo()
	{
		return GetConsoleLineNoHook?.Invoke() ?? 0;
	}

	public static int GetConsoleClientHeight()
	{
		return GetConsoleClientHeightHook?.Invoke() ?? 0;
	}

	public static bool ConfirmYesNo(string message, string caption)
	{
		return ConfirmYesNoHook?.Invoke(message, caption) ?? false;
	}

	public static string GetProductVersion()
	{
		if (ProductVersionHook != null)
			return ProductVersionHook();

		return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
	}

	public static Exception LoadContents(bool reload)
	{
		return LoadContentsHook?.Invoke(reload);
	}

	public static void UnloadTempLoadedConstImages()
	{
		UnloadTempLoadedConstImagesHook?.Invoke();
	}

	public static void UnloadTempLoadedGraphicsImages()
	{
		UnloadTempLoadedGraphicsImagesHook?.Invoke();
	}

	public static object GetGraphics(int id)
	{
		return GetGraphicsHook?.Invoke(id);
	}

	public static object GetSprite(string name)
	{
		return GetSpriteHook?.Invoke(name);
	}

	public static void CreateSpriteG(string imageName, object parent, RuntimeRect rect)
	{
		CreateSpriteGHook?.Invoke(imageName, parent, rect);
	}

	public static void SpriteDispose(string imageName)
	{
		SpriteDisposeHook?.Invoke(imageName);
	}

	public static long SpriteDisposeAll(bool deleteCsvImage)
	{
		return SpriteDisposeAllHook?.Invoke(deleteCsvImage) ?? 0;
	}

	public static void CreateSpriteAnime(string imageName, int width, int height)
	{
		CreateSpriteAnimeHook?.Invoke(imageName, width, height);
	}

	public static string ResolveMessage(string key, string fallback)
	{
		return ResolveMessageHook?.Invoke(key) ?? fallback;
	}

	public static string ResolveDrawLineString(string fallback)
	{
		return ResolveDrawLineStringHook?.Invoke(fallback) ?? fallback;
	}

	public static string FormatDrawLineString(string raw)
	{
		return FormatDrawLineStringHook?.Invoke(raw) ?? raw;
	}

	public static void InitializeDrawLineString(string raw)
	{
		InitializeDrawLineStringHook?.Invoke(raw);
	}

	public static string[] HtmlTagSplit(string raw)
	{
		return HtmlTagSplitHook?.Invoke(raw);
	}

	public static int HtmlLength(string raw)
	{
		if (HtmlLengthHook != null)
			return HtmlLengthHook(raw ?? string.Empty);
		return raw?.Length ?? 0;
	}

	public static string[] HtmlSubString(string raw, int width)
	{
		if (HtmlSubStringHook != null)
			return HtmlSubStringHook(raw ?? string.Empty, width);
		if (string.IsNullOrEmpty(raw))
			return [string.Empty, string.Empty];
		if (width <= 0 || width >= raw.Length)
			return [raw, string.Empty];
		return [raw[..width], raw[width..]];
	}

	public static string HtmlToPlainText(string raw)
	{
		if (HtmlToPlainTextHook != null)
			return HtmlToPlainTextHook(raw ?? string.Empty);
		return raw ?? string.Empty;
	}

	public static string HtmlEscape(string raw)
	{
		if (HtmlEscapeHook != null)
			return HtmlEscapeHook(raw ?? string.Empty);
		if (string.IsNullOrEmpty(raw))
			return string.Empty;
		return raw
			.Replace("&", "&amp;", StringComparison.Ordinal)
			.Replace("<", "&lt;", StringComparison.Ordinal)
			.Replace(">", "&gt;", StringComparison.Ordinal)
			.Replace("\"", "&quot;", StringComparison.Ordinal);
	}

	public static void PlayErrorTone()
	{
		PlayErrorToneHook?.Invoke();
	}

	public static void PlayNotifyTone()
	{
		PlayNotifyToneHook?.Invoke();
	}

	public static bool OpenUrl(string url)
	{
		return OpenUrlHook?.Invoke(url) ?? false;
	}

	public static object GetConsoleHost()
	{
		return GetConsoleHostHook?.Invoke();
	}

	public static IRuntimeProcess CreateRuntimeProcess(IExecutionConsole console)
	{
		ArgumentNullException.ThrowIfNull(console);
		return CreateRuntimeProcessHook?.Invoke(console);
	}

	public static void SetGameBaseData(object value)
	{
		gameBaseData = value;
		SetGameBaseDataHook?.Invoke(value);
	}

	public static void SetConstantData(object value)
	{
		constantData = value;
		SetConstantDataHook?.Invoke(value);
	}

	public static void SetVariableEvaluator(object value)
	{
		variableEvaluator = value;
		SetVariableEvaluatorHook?.Invoke(value);
	}

	public static void SetVariableData(object value)
	{
		variableData = value;
		SetVariableDataHook?.Invoke(value);
	}

	public static void SetIdentifierDictionary(object value)
	{
		identifierDictionary = value;
		SetIdentifierDictionaryHook?.Invoke(value);
	}

	public static void SetLabelDictionary(object value)
	{
		labelDictionary = value;
		SetLabelDictionaryHook?.Invoke(value);
	}

	public static object GetGameBaseData()
	{
		return GetGameBaseDataHook?.Invoke() ?? gameBaseData;
	}

	public static object GetConstantData()
	{
		return GetConstantDataHook?.Invoke() ?? constantData;
	}

	public static object GetVariableEvaluator()
	{
		return GetVariableEvaluatorHook?.Invoke() ?? variableEvaluator;
	}

	public static object GetVariableData()
	{
		return GetVariableDataHook?.Invoke() ?? variableData;
	}

	public static object GetIdentifierDictionary()
	{
		return GetIdentifierDictionaryHook?.Invoke() ?? identifierDictionary;
	}

	public static object GetLabelDictionary()
	{
		return GetLabelDictionaryHook?.Invoke() ?? labelDictionary;
	}

	public static void IncrementAnalysisLabelCounter(string labelName)
	{
		if (string.IsNullOrEmpty(labelName))
			return;
		if (analysisLabelCounters.TryGetValue(labelName, out var count))
			analysisLabelCounters[labelName] = count + 1;
		else
			analysisLabelCounters[labelName] = 1;
	}

	public static IReadOnlyDictionary<string, long> GetAnalysisLabelCounters()
	{
		return analysisLabelCounters;
	}

	public static void ClearAnalysisLabelCounters()
	{
		analysisLabelCounters.Clear();
	}

	public static void DebugStackPush(object frame)
	{
		if (frame == null)
			return;
		debugStack.Add(frame);
	}

	public static void DebugStackPop(object frame)
	{
		if (frame == null)
			return;
		debugStack.Remove(frame);
	}

	public static short GetKeyState(int keyCode)
	{
		return GetKeyStateHook?.Invoke(keyCode) ?? 0;
	}

	public static object LoadImage(string filePath)
	{
		return LoadImageHook?.Invoke(filePath);
	}

	public static bool TryResolveNamedColorRgb(string colorName, out int rgb)
	{
		rgb = 0;
		if (string.IsNullOrWhiteSpace(colorName))
			return false;
		var resolved = ResolveNamedColorRgbHook?.Invoke(colorName);
		if (!resolved.HasValue)
			return false;
		rgb = resolved.Value & 0xFFFFFF;
		return true;
	}

	public static bool IsFontInstalled(string fontName)
	{
		if (string.IsNullOrWhiteSpace(fontName))
			return false;
		return IsFontInstalledHook?.Invoke(fontName) ?? false;
	}

	public static string GetDisplayLineText(long lineNo)
	{
		return GetDisplayLineTextHook?.Invoke(lineNo) ?? string.Empty;
	}

	public static string GetDisplayLineHtml(long lineNo)
	{
		return GetDisplayLineHtmlHook?.Invoke(lineNo) ?? string.Empty;
	}

	public static string PopDisplayLineHtml()
	{
		return PopDisplayLineHtmlHook?.Invoke() ?? string.Empty;
	}

	public static string GetPointingButtonInput()
	{
		return GetPointingButtonInputHook?.Invoke() ?? string.Empty;
	}

	public static void CbgClear()
	{
		CbgClearHook?.Invoke();
	}

	public static void CbgClearRange(int zmin, int zmax)
	{
		CbgClearRangeHook?.Invoke(zmin, zmax);
	}

	public static void CbgClearButton()
	{
		CbgClearButtonHook?.Invoke();
	}

	public static void CbgClearButtonMap()
	{
		CbgClearButtonMapHook?.Invoke();
	}

	public static bool CbgSetGraphics(object graphics, int x, int y, int zdepth)
	{
		return CbgSetGraphicsHook?.Invoke(graphics, x, y, zdepth) ?? false;
	}

	public static bool CbgSetButtonMap(object graphics)
	{
		return CbgSetButtonMapHook?.Invoke(graphics) ?? false;
	}

	public static bool CbgSetImage(object image, int x, int y, int zdepth)
	{
		return CbgSetImageHook?.Invoke(image, x, y, zdepth) ?? false;
	}

	public static bool CbgSetButtonImage(int buttonValue, object imageN, object imageB, int x, int y, int zdepth, string tooltip)
	{
		return CbgSetButtonImageHook?.Invoke(buttonValue, imageN, imageB, x, y, zdepth, tooltip) ?? false;
	}

	public static void ApplyTextBoxChanges()
	{
		ApplyTextBoxChangesHook?.Invoke();
	}

	public static void AddBackgroundImage(string name, long depth, float opacity)
	{
		AddBackgroundImageHook?.Invoke(name, depth, opacity);
	}

	public static void RemoveBackgroundImage(string name)
	{
		RemoveBackgroundImageHook?.Invoke(name);
	}

	public static void ClearBackgroundImage()
	{
		ClearBackgroundImageHook?.Invoke();
	}

	public static void SetToolTipColorRgb(int foregroundRgb, int backgroundRgb)
	{
		SetToolTipColorRgbHook?.Invoke(foregroundRgb, backgroundRgb);
	}

	public static void SetToolTipDelay(int delay)
	{
		SetToolTipDelayHook?.Invoke(delay);
	}

	public static void SetToolTipDuration(int duration)
	{
		SetToolTipDurationHook?.Invoke(duration);
	}

	public static void SetToolTipFontName(string fontName)
	{
		SetToolTipFontNameHook?.Invoke(fontName);
	}

	public static void SetToolTipFontSize(long fontSize)
	{
		SetToolTipFontSizeHook?.Invoke(fontSize);
	}

	public static void SetCustomToolTip(bool enabled)
	{
		SetCustomToolTipHook?.Invoke(enabled);
	}

	public static void SetToolTipFormat(long format)
	{
		SetToolTipFormatHook?.Invoke(format);
	}

	public static void SetToolTipImageEnabled(bool enabled)
	{
		SetToolTipImageEnabledHook?.Invoke(enabled);
	}

	public static void CtrlZAddInput(string input)
	{
		CtrlZAddInputHook?.Invoke(input);
	}

	public static void CtrlZOnSavePrepare(int saveSlot)
	{
		CtrlZOnSavePrepareHook?.Invoke(saveSlot);
	}

	public static void CtrlZOnSave()
	{
		CtrlZOnSaveHook?.Invoke();
	}

	public static void CtrlZOnLoad(int saveSlot)
	{
		CtrlZOnLoadHook?.Invoke(saveSlot);
	}

	public static bool IsCtrlZEnabled()
	{
		return IsCtrlZEnabledHook?.Invoke() ?? false;
	}

	public static void CaptureRandomSeed(long[] seedBuffer)
	{
		if (seedBuffer == null || seedBuffer.Length == 0)
			return;

		CaptureRandomSeedHook?.Invoke(seedBuffer);
	}

	public static void PrintHtml(string html, bool toBuffer)
	{
		PrintHtmlHook?.Invoke(html, toBuffer);
	}

	public static void PrintHtmlIsland(string html)
	{
		PrintHtmlIslandHook?.Invoke(html);
	}

	public static void ClearHtmlIsland()
	{
		ClearHtmlIslandHook?.Invoke();
	}

	public static void PrintImage(string imageName, string buttonImageName, string mapImageName, object height, object width, object yOffset)
	{
		PrintImageHook?.Invoke(imageName, buttonImageName, mapImageName, height, width, yOffset);
	}

	public static void PrintShape(string shapeType, object[] parameters)
	{
		PrintShapeHook?.Invoke(shapeType, parameters);
	}

	public static void SetUseUserStyle(bool enabled)
	{
		SetUseUserStyleHook?.Invoke(enabled);
	}

	public static void SetUseSetColorStyle(bool enabled)
	{
		SetUseSetColorStyleHook?.Invoke(enabled);
	}

	public static void SetBackgroundColorRgb(int rgb)
	{
		SetBackgroundColorRgbHook?.Invoke(rgb);
	}

	public static int GetBackgroundColorRgb()
	{
		return GetBackgroundColorRgbHook?.Invoke() ?? 0;
	}

	public static void SetStringColorRgb(int rgb)
	{
		SetStringColorRgbHook?.Invoke(rgb);
	}

	public static int GetStringColorRgb()
	{
		return GetStringColorRgbHook?.Invoke() ?? 0;
	}

	public static void SetStringStyleFlags(RuntimeFontStyleFlags styleFlags)
	{
		SetStringStyleFlagsHook?.Invoke(styleFlags);
	}

	public static RuntimeFontStyleFlags GetStringStyleFlags()
	{
		return GetStringStyleFlagsHook?.Invoke() ?? RuntimeFontStyleFlags.Regular;
	}

	public static void SetFont(string fontName)
	{
		SetFontHook?.Invoke(fontName);
	}

	public static string GetFontName()
	{
		return GetFontNameHook?.Invoke() ?? string.Empty;
	}

	public static void SetAlignment(RuntimeDisplayLineAlignment alignment)
	{
		SetAlignmentHook?.Invoke(alignment);
	}

	public static RuntimeDisplayLineAlignment GetAlignment()
	{
		return GetAlignmentHook?.Invoke() ?? RuntimeDisplayLineAlignment.LEFT;
	}

	public static void SetRedraw(long redraw)
	{
		SetRedrawHook?.Invoke(redraw);
	}

	public static RuntimeRedrawMode GetRedrawMode()
	{
		return GetRedrawModeHook?.Invoke() ?? RuntimeRedrawMode.None;
	}

	public static void PrintButton(string text, string input)
	{
		PrintButtonStringHook?.Invoke(text, input);
	}

	public static void PrintButton(string text, long input)
	{
		PrintButtonLongHook?.Invoke(text, input);
	}

	public static void PrintButtonC(string text, string input, bool isRight)
	{
		PrintButtonCStringHook?.Invoke(text, input, isRight);
	}

	public static void PrintButtonC(string text, long input, bool isRight)
	{
		PrintButtonCLongHook?.Invoke(text, input, isRight);
	}

	public static void PrintPlainSingleLine(string text)
	{
		PrintPlainSingleLineHook?.Invoke(text);
	}

	public static void PrintErrorButton(string text, ScriptPosition? position, int level = 0)
	{
		PrintErrorButtonHook?.Invoke(text, position, level);
	}

	public static void PrintPlain(string text)
	{
		PrintPlainHook?.Invoke(text);
	}

	public static void ClearText()
	{
		ClearTextHook?.Invoke();
	}

	public static void ReloadErbFinished()
	{
		ReloadErbFinishedHook?.Invoke();
	}

	public static bool IsLastLineEmpty()
	{
		return IsLastLineEmptyHook?.Invoke() ?? false;
	}

	public static bool IsLastLineTemporary()
	{
		return IsLastLineTemporaryHook?.Invoke() ?? false;
	}

	public static bool IsPrintBufferEmpty()
	{
		return IsPrintBufferEmptyHook?.Invoke() ?? true;
	}

	public static int CountInteractiveButtons(bool integerOnly)
	{
		return CountInteractiveButtonsHook?.Invoke(integerOnly) ?? 0;
	}

	public static int GetConsoleClientWidth()
	{
		return GetConsoleClientWidthHook?.Invoke() ?? 0;
	}

	public static bool IsConsoleActive()
	{
		return IsConsoleActiveHook?.Invoke() ?? false;
	}

	public static RuntimePoint GetMousePositionXY()
	{
		return GetMousePositionXYHook?.Invoke() ?? new RuntimePoint(0, 0);
	}

	public static bool MoveMouseXY(int x, int y)
	{
		return MoveMouseXYHook?.Invoke(x, y) ?? false;
	}

	public static void SetBitmapCacheEnabledForNextLine(bool enabled)
	{
		SetBitmapCacheEnabledForNextLineHook?.Invoke(enabled);
	}

	public static void SetRedrawTimer(int tickCount)
	{
		SetRedrawTimerHook?.Invoke(tickCount);
	}

	public static string GetTextBoxText()
	{
		return GetTextBoxTextHook?.Invoke() ?? string.Empty;
	}

	public static void ChangeTextBox(string text)
	{
		ChangeTextBoxHook?.Invoke(text);
	}

	public static void ResetTextBoxPos()
	{
		ResetTextBoxPosHook?.Invoke();
	}

	public static void SetTextBoxPos(int xOffset, int yOffset, int width)
	{
		SetTextBoxPosHook?.Invoke(xOffset, yOffset, width);
	}

	public static void HotkeyStateSet(nint key, nint value)
	{
		HotkeyStateSetHook?.Invoke(key, value);
	}

	public static void HotkeyStateInit(nint key)
	{
		HotkeyStateInitHook?.Invoke(key);
	}

	public static void PrintBar()
	{
		PrintBarHook?.Invoke();
	}

	public static void PrintCustomBar(string barText, bool isConst)
	{
		PrintCustomBarHook?.Invoke(barText, isConst);
	}

	public static void DeleteLine(int lineCount)
	{
		DeleteLineHook?.Invoke(lineCount);
	}

	public static void ClearDisplay()
	{
		ClearDisplayHook?.Invoke();
	}

	public static void ResetStyle()
	{
		ResetStyleHook?.Invoke();
	}

	public static void ThrowError(bool playSound)
	{
		ThrowErrorHook?.Invoke(playSound);
	}

	public static void ForceStopTimer()
	{
		ForceStopTimerHook?.Invoke();
	}

	public static void DebugPrint(string text)
	{
		DebugPrintHook?.Invoke(text);
	}

	public static void DebugNewLine()
	{
		DebugNewLineHook?.Invoke();
	}

	public static void DebugClear()
	{
		DebugClearHook?.Invoke();
	}

	public static void PrintTemporaryLine(string text)
	{
		PrintTemporaryLineHook?.Invoke(text);
	}

	public static void PrintFlush(bool force)
	{
		PrintFlushHook?.Invoke(force);
	}

	public static void RefreshStrings(bool forcePaint)
	{
		RefreshStringsHook?.Invoke(forcePaint);
	}

	public static void MarkUpdatedGeneration()
	{
		MarkUpdatedGenerationHook?.Invoke();
	}

	public static void DisableOutputLog()
	{
		DisableOutputLogHook?.Invoke();
	}

	public static bool OutputLog(string filename, bool hideInfo)
	{
		return OutputLogHook?.Invoke(filename, hideInfo) ?? false;
	}

	public static bool OutputSystemLog(string filename)
	{
		return OutputSystemLogHook?.Invoke(filename) ?? false;
	}

	public static void ThrowTitleError(bool error)
	{
		ThrowTitleErrorHook?.Invoke(error);
	}
}
