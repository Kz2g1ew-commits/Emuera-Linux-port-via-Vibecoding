using System.Text;
using System.Reflection;
using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Utils;

internal readonly record struct CliInteractiveToken(int StartCol, int EndCol, string Value);
internal readonly record struct CliInteractiveLineSnapshot(string Text, bool LineEnded, IReadOnlyList<CliInteractiveToken> Tokens);
internal readonly record struct CliInlineMergeChunk(string Text, string RenderedText, IReadOnlyList<CliInteractiveToken> Tokens, bool LineEnded);

internal static class CliRuntimeHostBridge
{
	private const int HistoryLimit = 4096;
	private static readonly TimeSpan KeyStateLatch = TimeSpan.FromMilliseconds(350);
	private static readonly Regex HtmlTagRegex = new(
		@"<(?<close>/)?(?<name>[A-Za-z0-9]+)(?<attrs>[^>]*)>",
		RegexOptions.Compiled);
	private static readonly Regex HtmlColorAttributeRegex = new(
		@"\bcolor\s*=\s*(?:(['""])(?<value>.*?)\1|(?<value>[^\s>]+))",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);
	private static readonly Regex HtmlButtonValueAttributeRegex = new(
		@"\bvalue\s*=\s*(?:(['""])(?<value>.*?)\1|(?<value>[^\s>]+))",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);
	private static readonly Regex HtmlShapeTypeAttributeRegex = new(
		@"\btype\s*=\s*(?:(['""])(?<value>.*?)\1|(?<value>[^\s>]+))",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);
	private static readonly Regex HtmlShapeParamAttributeRegex = new(
		@"\bparam\s*=\s*(?:(['""])(?<value>.*?)\1|(?<value>[^\s>]+))",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);
	private static readonly Regex NumericBracketTokenRegex = new(
		@"(?:\[|\uFF3B)\s*[+-]?(?:0[xX][0-9A-Fa-f]+|0[bB][01]+|[0-9]+)\s*(?:\]|\uFF3D)",
		RegexOptions.Compiled);
	private static readonly Regex NumericBracketChoiceRegex = new(
		@"(?:\[|\uFF3B)\s*(?<value>[+-]?\d{1,19})\s*(?:\]|\uFF3D)",
		RegexOptions.Compiled);
	private static readonly Regex NumericAngleChoiceRegex = new(
		@"<\s*(?<value>[+-]?\d{1,19})\s*>",
		RegexOptions.Compiled);
	private static readonly Regex AnsiEscapeRegex = new(
		"\u001b\\[[0-?]*[ -/]*[@-~]",
		RegexOptions.Compiled);
	private static readonly Regex AngleValueTokenRegex = new(
		@"<[^<>\r\n]{0,24}>",
		RegexOptions.Compiled);
	private static readonly Regex CollapsedSeparatorRegex = new(
		@"[-=]{24,}",
		RegexOptions.Compiled);
	private enum AnsiColorMode
	{
		TrueColor,
		Palette256,
		Palette16,
	}
	private enum CbgEntryType
	{
		Graphics,
		Image,
		ButtonImage,
	}
	private readonly record struct CbgEntry(CbgEntryType Type, int ZDepth);
	private static readonly int[] Ansi16RgbPalette =
	[
		0x000000, 0x800000, 0x008000, 0x808000, 0x000080, 0x800080, 0x008080, 0xC0C0C0,
		0x808080, 0xFF0000, 0x00FF00, 0xFFFF00, 0x0000FF, 0xFF00FF, 0x00FFFF, 0xFFFFFF,
	];
	private static readonly int[] Ansi16ForegroundCodes =
	[
		30, 31, 32, 33, 34, 35, 36, 37,
		90, 91, 92, 93, 94, 95, 96, 97,
	];
	private static readonly int[] Ansi16BackgroundCodes =
	[
		40, 41, 42, 43, 44, 45, 46, 47,
		100, 101, 102, 103, 104, 105, 106, 107,
	];
	private const string ForcedBlackBackgroundSgrParameters = "48;2;0;0;0";

	private static readonly object SyncRoot = new();
	private static readonly StringBuilder PendingLine = new();
	private static readonly StringBuilder PendingRenderedLine = new();
	private static readonly StringBuilder PendingHtml = new();
	private static bool PendingLineEnd = true;
	private static readonly List<string> DisplayTextHistory = [];
	private static readonly List<string> DisplayHtmlHistory = [];
	private static readonly List<bool> DisplayTemporaryHistory = [];
	private static readonly List<bool> DisplayLineEndedHistory = [];
	private static readonly List<List<CliInteractiveToken>> DisplayInteractiveTokenHistory = [];
	private static readonly List<CliInteractiveToken> PendingInteractiveTokens = [];
	private static readonly List<string> HtmlIslandBuffer = [];
	private static readonly Dictionary<nint, nint> HotkeyStateMap = [];
	private static readonly Dictionary<int, DateTimeOffset> ActiveKeyStates = [];
	private static readonly HashSet<string> BackgroundImageNames = new(StringComparer.OrdinalIgnoreCase);
	private static readonly List<CbgEntry> CbgEntries = [];

	private static CliExecutionConsole? executionConsole;
	private static RuntimeDisplayLineAlignment alignment = RuntimeDisplayLineAlignment.LEFT;
	private static RuntimeFontStyleFlags styleFlags = RuntimeFontStyleFlags.Regular;
	private static RuntimeRedrawMode redrawMode = RuntimeRedrawMode.None;
	private static string fontName = string.Empty;
	private static int stringColorRgb;
	private static int backgroundColorRgb;
	private static bool hasStringColor;
	private static bool hasBackgroundColor;
	private static bool useSetColorStyle = true;
	private static bool useUserStyle = true;
	private static int defaultForeColorRgb = 0xC0C0C0;
	private static int defaultBackColorRgb = 0x000000;
	private static int defaultFocusColorRgb = 0xFFFF00;
	private static int interactiveButtonCount;
	private static int lastPrintedInteractiveButtonCount;
	private static long historyStartLineNo;
	private static string lastPointingButtonInput = string.Empty;
	private static string textBoxText = string.Empty;
	private static RuntimePoint mousePosition = new(0, 0);
	private static int textBoxPosX;
	private static int textBoxPosY;
	private static int textBoxWidth;
	private static bool bitmapCacheEnabledForNextLine;
	private static int redrawTimerTick;
	private static bool outputLogDisabled;
	private static bool generationMarkedUpdated;
	private static bool titleErrorState;
	private static bool titleErrorByError;
	private static bool deferredBlackBackgroundActivated;
	private static bool osc11BackgroundApplied;
	private static int cbgGraphicsCount;
	private static int cbgImageCount;
	private static int cbgButtonImageCount;
	private static object? cbgButtonMapToken;
	private static bool hotkeyStateInitialized;
	private static nint hotkeyStateSize;
	private static int toolTipForeColorRgb;
	private static int toolTipBackColorRgb;
	private static int toolTipDelay;
	private static int toolTipDuration;
	private static string toolTipFontName = string.Empty;
	private static long toolTipFontSize;
	private static bool customToolTipEnabled;
	private static long toolTipFormat;
	private static bool toolTipImageEnabled = true;
	private static int ctrlZLastSave = -1;
	private static int ctrlZLastSaveExpected = -1;
	private static readonly List<string> ctrlZInputs = [];
	private static long[] ctrlZRandomSeed = [];
	private static int lastKnownConsoleWidth;

	public static void AttachExecutionConsole(CliExecutionConsole console)
	{
		ArgumentNullException.ThrowIfNull(console);

		lock (SyncRoot)
		{
			executionConsole = console;
			deferredBlackBackgroundActivated = false;
			osc11BackgroundApplied = false;
			ResetStyleStateNoLock();
		}

		RuntimeHost.SetUseUserStyleHook = SetUseUserStyle;
		RuntimeHost.SetUseSetColorStyleHook = SetUseSetColorStyle;
		RuntimeHost.SetBackgroundColorRgbHook = SetBackgroundColorRgb;
		RuntimeHost.GetBackgroundColorRgbHook = GetBackgroundColorRgb;
		RuntimeHost.SetStringColorRgbHook = SetStringColorRgb;
		RuntimeHost.GetStringColorRgbHook = GetStringColorRgb;
		RuntimeHost.SetStringStyleFlagsHook = SetStringStyleFlags;
		RuntimeHost.GetStringStyleFlagsHook = GetStringStyleFlags;
		RuntimeHost.SetFontHook = SetFont;
		RuntimeHost.GetFontNameHook = GetFontName;
		RuntimeHost.SetAlignmentHook = SetAlignment;
		RuntimeHost.GetAlignmentHook = GetAlignment;
		RuntimeHost.SetRedrawHook = SetRedraw;
		RuntimeHost.GetRedrawModeHook = GetRedrawMode;
		RuntimeHost.PrintButtonStringHook = PrintButtonString;
		RuntimeHost.PrintButtonLongHook = PrintButtonLong;
		RuntimeHost.PrintButtonCStringHook = PrintButtonCString;
		RuntimeHost.PrintButtonCLongHook = PrintButtonCLong;
		RuntimeHost.PrintPlainSingleLineHook = PrintPlainSingleLine;
		RuntimeHost.PrintErrorButtonHook = PrintErrorButton;
		RuntimeHost.PrintPlainHook = PrintPlain;
		RuntimeHost.ClearTextHook = ClearText;
		RuntimeHost.IsLastLineEmptyHook = IsLastLineEmpty;
		RuntimeHost.IsLastLineTemporaryHook = IsLastLineTemporary;
		RuntimeHost.IsPrintBufferEmptyHook = IsPrintBufferEmpty;
		RuntimeHost.CountInteractiveButtonsHook = CountInteractiveButtons;
		RuntimeHost.GetConsoleClientWidthHook = GetConsoleClientWidth;
		RuntimeHost.IsConsoleActiveHook = static () => !Console.IsOutputRedirected;
		RuntimeHost.PrintBarHook = PrintBar;
		RuntimeHost.PrintCustomBarHook = PrintCustomBar;
		RuntimeHost.DeleteLineHook = DeleteLine;
		RuntimeHost.ClearDisplayHook = ClearDisplay;
		RuntimeHost.ResetStyleHook = ResetStyle;
		RuntimeHost.ThrowErrorHook = ThrowError;
		RuntimeHost.ForceStopTimerHook = ForceStopTimer;
		RuntimeHost.DebugPrintHook = DebugPrint;
		RuntimeHost.DebugNewLineHook = DebugNewLine;
		RuntimeHost.DebugClearHook = DebugClear;
		RuntimeHost.PrintTemporaryLineHook = PrintTemporaryLine;
		RuntimeHost.PrintFlushHook = PrintFlush;
		RuntimeHost.RefreshStringsHook = RefreshStrings;
		RuntimeHost.GetConsoleLineNoHook = GetConsoleLineNo;
		RuntimeHost.GetKeyStateHook = GetKeyState;
		RuntimeHost.MarkUpdatedGenerationHook = MarkUpdatedGeneration;
		RuntimeHost.DisableOutputLogHook = DisableOutputLog;
		RuntimeHost.OutputLogHook = OutputLog;
		RuntimeHost.OutputSystemLogHook = OutputSystemLog;
		RuntimeHost.ThrowTitleErrorHook = ThrowTitleError;
		RuntimeHost.GetConsoleHostHook = GetConsoleHost;
		RuntimeHost.PrintHtmlHook = PrintHtml;
		RuntimeHost.PrintHtmlIslandHook = PrintHtmlIsland;
		RuntimeHost.ClearHtmlIslandHook = ClearHtmlIsland;
		RuntimeHost.PrintImageHook = PrintImage;
		RuntimeHost.PrintShapeHook = PrintShape;
		RuntimeHost.GetDisplayLineTextHook = GetDisplayLineText;
		RuntimeHost.GetDisplayLineHtmlHook = GetDisplayLineHtml;
		RuntimeHost.PopDisplayLineHtmlHook = PopDisplayLineHtml;
		RuntimeHost.GetPointingButtonInputHook = GetPointingButtonInput;
		RuntimeHost.ReloadErbFinishedHook = ReloadErbFinished;
		RuntimeHost.GetTextBoxTextHook = GetTextBoxText;
		RuntimeHost.ChangeTextBoxHook = ChangeTextBox;
		RuntimeHost.ResetTextBoxPosHook = ResetTextBoxPos;
		RuntimeHost.SetTextBoxPosHook = SetTextBoxPos;
		RuntimeHost.ApplyTextBoxChangesHook = ApplyTextBoxChanges;
		RuntimeHost.HotkeyStateSetHook = HotkeyStateSet;
		RuntimeHost.HotkeyStateInitHook = HotkeyStateInit;
		RuntimeHost.GetMousePositionXYHook = GetMousePositionXY;
		RuntimeHost.MoveMouseXYHook = MoveMouseXY;
		RuntimeHost.SetBitmapCacheEnabledForNextLineHook = SetBitmapCacheEnabledForNextLine;
		RuntimeHost.SetRedrawTimerHook = SetRedrawTimer;
		RuntimeHost.GetConsoleClientHeightHook = GetConsoleClientHeight;
		RuntimeHost.CbgClearHook = CbgClear;
		RuntimeHost.CbgClearRangeHook = CbgClearRange;
		RuntimeHost.CbgClearButtonHook = CbgClearButton;
		RuntimeHost.CbgClearButtonMapHook = CbgClearButtonMap;
		RuntimeHost.CbgSetGraphicsHook = CbgSetGraphics;
		RuntimeHost.CbgSetButtonMapHook = CbgSetButtonMap;
		RuntimeHost.CbgSetImageHook = CbgSetImage;
		RuntimeHost.CbgSetButtonImageHook = CbgSetButtonImage;
		RuntimeHost.AddBackgroundImageHook = AddBackgroundImage;
		RuntimeHost.RemoveBackgroundImageHook = RemoveBackgroundImage;
		RuntimeHost.ClearBackgroundImageHook = ClearBackgroundImage;
		RuntimeHost.SetToolTipColorRgbHook = SetToolTipColorRgb;
		RuntimeHost.SetToolTipDelayHook = SetToolTipDelay;
		RuntimeHost.SetToolTipDurationHook = SetToolTipDuration;
		RuntimeHost.SetToolTipFontNameHook = SetToolTipFontName;
		RuntimeHost.SetToolTipFontSizeHook = SetToolTipFontSize;
		RuntimeHost.SetCustomToolTipHook = SetCustomToolTip;
		RuntimeHost.SetToolTipFormatHook = SetToolTipFormat;
		RuntimeHost.SetToolTipImageEnabledHook = SetToolTipImageEnabled;
		RuntimeHost.IsCtrlZEnabledHook = IsCtrlZEnabled;
		RuntimeHost.CaptureRandomSeedHook = CaptureRandomSeed;
		RuntimeHost.CtrlZAddInputHook = CtrlZAddInput;
		RuntimeHost.CtrlZOnSavePrepareHook = CtrlZOnSavePrepare;
		RuntimeHost.CtrlZOnSaveHook = CtrlZOnSave;
		RuntimeHost.CtrlZOnLoadHook = CtrlZOnLoad;
	}

	public static void RecordConsoleKey(ConsoleKeyInfo keyInfo)
	{
		var now = DateTimeOffset.UtcNow;
		lock (SyncRoot)
		{
			PruneExpiredKeyStatesNoLock(now);

			if (VirtualKeyMap.TryMapConsoleKey(keyInfo.Key, out var keyCode))
				RememberKeyStateNoLock(keyCode, now);
			if ((keyInfo.Modifiers & ConsoleModifiers.Shift) != 0)
			{
				RememberKeyStateNoLock(0x10, now);
				RememberKeyStateNoLock(0xA0, now);
				RememberKeyStateNoLock(0xA1, now);
			}
			if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
			{
				RememberKeyStateNoLock(0x11, now);
				RememberKeyStateNoLock(0xA2, now);
				RememberKeyStateNoLock(0xA3, now);
			}
			if ((keyInfo.Modifiers & ConsoleModifiers.Alt) != 0)
			{
				RememberKeyStateNoLock(0x12, now);
				RememberKeyStateNoLock(0xA4, now);
				RememberKeyStateNoLock(0xA5, now);
			}
		}
	}

	public static void RecordInputText(string rawInput)
	{
		if (string.IsNullOrWhiteSpace(rawInput))
			return;

		var text = rawInput.Trim();
		if (text.Length == 0)
			return;

		var token = text[0];
		if (!char.IsLetterOrDigit(token))
			return;

		if (!VirtualKeyMap.TryParseKeyName(token.ToString(), out var keyCode))
			return;

		lock (SyncRoot)
		{
			var now = DateTimeOffset.UtcNow;
			PruneExpiredKeyStatesNoLock(now);
			RememberKeyStateNoLock(keyCode, now);
		}
	}

	private static void SetUseUserStyle(bool enabled)
	{
		lock (SyncRoot)
			useUserStyle = enabled;
	}

	private static void SetUseSetColorStyle(bool enabled)
	{
		lock (SyncRoot)
			useSetColorStyle = enabled;
	}

	private static void SetBackgroundColorRgb(int rgb)
	{
		lock (SyncRoot)
		{
			backgroundColorRgb = rgb & 0xFFFFFF;
			hasBackgroundColor = true;
		}
	}

	private static int GetBackgroundColorRgb()
	{
		lock (SyncRoot)
			return hasBackgroundColor ? backgroundColorRgb : 0;
	}

	private static void SetStringColorRgb(int rgb)
	{
		lock (SyncRoot)
		{
			stringColorRgb = rgb & 0xFFFFFF;
			hasStringColor = true;
		}
	}

	private static int GetStringColorRgb()
	{
		lock (SyncRoot)
			return hasStringColor ? stringColorRgb : 0;
	}

	private static void SetStringStyleFlags(RuntimeFontStyleFlags flags)
	{
		lock (SyncRoot)
			styleFlags = flags;
	}

	private static RuntimeFontStyleFlags GetStringStyleFlags()
	{
		lock (SyncRoot)
			return styleFlags;
	}

	private static void SetFont(string name)
	{
		lock (SyncRoot)
			fontName = name ?? string.Empty;
	}

	private static string GetFontName()
	{
		lock (SyncRoot)
			return fontName;
	}

	private static void SetAlignment(RuntimeDisplayLineAlignment value)
	{
		lock (SyncRoot)
			alignment = value;
	}

	private static RuntimeDisplayLineAlignment GetAlignment()
	{
		lock (SyncRoot)
			return alignment;
	}

	private static void SetRedraw(long redraw)
	{
		lock (SyncRoot)
			redrawMode = redraw == 0 ? RuntimeRedrawMode.None : RuntimeRedrawMode.Normal;
	}

	private static RuntimeRedrawMode GetRedrawMode()
	{
		lock (SyncRoot)
			return redrawMode;
	}

	private static void PrintButtonString(string text, string input)
	{
		var token = BuildButtonToken(text);
		AppendInline(token, isButton: true, htmlFragment: RuntimeHost.HtmlEscape(token), buttonValue: input);
		SetLastPointingButtonInput(input);
	}

	private static void PrintButtonLong(string text, long input)
	{
		var token = BuildButtonToken(text);
		AppendInline(token, isButton: true, htmlFragment: RuntimeHost.HtmlEscape(token), buttonValue: input.ToString());
		SetLastPointingButtonInput(input.ToString());
	}

	private static void PrintButtonCString(string text, string input, bool isRight)
	{
		var token = BuildButtonCToken(text, isRight);
		AppendInline(token, isButton: true, htmlFragment: RuntimeHost.HtmlEscape(token), buttonValue: input);
		SetLastPointingButtonInput(input);
	}

	private static void PrintButtonCLong(string text, long input, bool isRight)
	{
		var token = BuildButtonCToken(text, isRight);
		AppendInline(token, isButton: true, htmlFragment: RuntimeHost.HtmlEscape(token), buttonValue: input.ToString());
		SetLastPointingButtonInput(input.ToString());
	}

	private static void PrintPlainSingleLine(string text)
	{
		FlushPending(force: false);
		var plain = text ?? string.Empty;
		PrintLine(plain, RuntimeHost.HtmlEscape(plain), rendered: null);
	}

	private static void PrintErrorButton(string text, ScriptPosition? position, int level)
	{
		FlushPending(force: false);
		if (position.HasValue)
		{
			var pos = position.Value;
			Console.Error.WriteLine($"[ERROR:{level}] {pos.Filename}:{pos.LineNo} {text}");
			return;
		}
		Console.Error.WriteLine($"[ERROR:{level}] {text}");
	}

	private static void PrintPlain(string text)
	{
		if (string.IsNullOrEmpty(text))
			return;
		AppendInline(text, isButton: false, htmlFragment: RuntimeHost.HtmlEscape(text));
	}

	private static void ClearText()
	{
		lock (SyncRoot)
		{
			PendingLine.Clear();
			PendingRenderedLine.Clear();
			PendingHtml.Clear();
			PendingInteractiveTokens.Clear();
			PendingLineEnd = true;
			interactiveButtonCount = 0;
			lastPrintedInteractiveButtonCount = 0;
			DisplayTextHistory.Clear();
			DisplayHtmlHistory.Clear();
			DisplayTemporaryHistory.Clear();
			DisplayLineEndedHistory.Clear();
			DisplayInteractiveTokenHistory.Clear();
			HtmlIslandBuffer.Clear();
			historyStartLineNo = executionConsole?.LineCount ?? 0;
		}
		ClearDisplay();
	}

	private static bool IsLastLineEmpty()
	{
		lock (SyncRoot)
		{
			if (PendingLine.Length > 0)
				return false;
			if (DisplayTextHistory.Count == 0)
				return true;
			var last = DisplayTextHistory[DisplayTextHistory.Count - 1];
			return string.IsNullOrEmpty(last);
		}
	}

	private static bool IsLastLineTemporary()
	{
		lock (SyncRoot)
		{
			if (PendingLine.Length > 0)
				return false;
			if (DisplayTemporaryHistory.Count == 0)
				return false;
			return DisplayTemporaryHistory[DisplayTemporaryHistory.Count - 1];
		}
	}

	private static bool IsPrintBufferEmpty()
	{
		lock (SyncRoot)
			return PendingLine.Length == 0;
	}

	private static int CountInteractiveButtons(bool integerOnly)
	{
		List<string> bridgeTextLines;
		CliExecutionConsole? localConsole;
		lock (SyncRoot)
		{
			if (generationMarkedUpdated)
				generationMarkedUpdated = false;

			var explicitCount = Math.Max(interactiveButtonCount, lastPrintedInteractiveButtonCount);
			if (explicitCount > 0)
				return explicitCount;

			bridgeTextLines = BuildBridgeTextLinesSnapshotNoLock();
			localConsole = executionConsole;
		}

		var choices = new HashSet<string>(StringComparer.Ordinal);
		foreach (var line in bridgeTextLines)
			CollectInferredChoicesFromLine(line, integerOnly, choices);

		if (localConsole != null)
		{
			var outputLines = localConsole.GetOutputLinesSnapshot(includePendingLine: true);
			var start = Math.Max(0, outputLines.Count - 256);
			for (var i = start; i < outputLines.Count; i++)
				CollectInferredChoicesFromLine(outputLines[i], integerOnly, choices);
		}

		return choices.Count;
	}

	private static List<string> BuildBridgeTextLinesSnapshotNoLock()
	{
		var lines = new List<string>(257);
		var start = Math.Max(0, DisplayTextHistory.Count - 256);
		for (var i = start; i < DisplayTextHistory.Count; i++)
			lines.Add(DisplayTextHistory[i]);

		if (PendingLine.Length > 0)
			lines.Add(PendingLine.ToString());

		return lines;
	}

	private static void CollectInferredChoicesFromLine(string? line, bool integerOnly, HashSet<string> output)
	{
		if (string.IsNullOrWhiteSpace(line))
			return;
		var normalized = AnsiEscapeRegex.Replace(line, string.Empty);

		foreach (Match match in NumericBracketChoiceRegex.Matches(normalized))
		{
			if (!match.Success)
				continue;
			var value = match.Groups["value"].Value.Trim();
			if (value.Length > 0)
				output.Add(value);
		}

		foreach (Match match in NumericAngleChoiceRegex.Matches(normalized))
		{
			if (!match.Success)
				continue;
			var value = match.Groups["value"].Value.Trim();
			if (value.Length > 0)
				output.Add(value);
		}

		if (!integerOnly)
		{
			foreach (Match match in AngleValueTokenRegex.Matches(normalized))
			{
				if (!match.Success)
					continue;
				var value = match.Value.Trim();
				if (value.Length > 2)
					output.Add(value);
			}
		}
	}

	private static int GetConsoleClientWidth()
	{
		var forcedLayoutWidth = ParsePositiveIntEnv("EMUERA_CLI_LAYOUT_WIDTH");
		if (forcedLayoutWidth > 0)
		{
			lastKnownConsoleWidth = Math.Clamp(forcedLayoutWidth, 40, 512);
			return lastKnownConsoleWidth;
		}

		try
		{
			if (!Console.IsOutputRedirected)
			{
				if (Console.WindowWidth > 0)
				{
					lastKnownConsoleWidth = Console.WindowWidth;
					return lastKnownConsoleWidth;
				}
				if (Console.BufferWidth > 0)
				{
					lastKnownConsoleWidth = Console.BufferWidth;
					return lastKnownConsoleWidth;
				}
			}
		}
		catch
		{
		}

		var fromEnv = ParsePositiveIntEnv("COLUMNS");
		if (fromEnv > 0)
		{
			lastKnownConsoleWidth = fromEnv;
			return lastKnownConsoleWidth;
		}

		return lastKnownConsoleWidth > 0 ? lastKnownConsoleWidth : 0;
	}

	private static void PrintBar()
	{
		// DRAWLINE should start from a committed line boundary.
		FlushPending(force: true);
		var text = BuildBarText("-", isConst: false);
		PrintLine(text, RuntimeHost.HtmlEscape(text), rendered: null);
	}

	private static void PrintCustomBar(string barText, bool isConst)
	{
		// DRAWLINE-style custom bars should not be glued to an open previous line.
		FlushPending(force: true);
		var text = BuildBarText(barText, isConst);
		PrintLine(text, RuntimeHost.HtmlEscape(text), rendered: null);
	}

	private static void DeleteLine(int lineCount)
	{
		if (lineCount <= 0)
			return;

		CliExecutionConsole? localConsole = null;
		var removed = 0;
		lock (SyncRoot)
		{
			var drop = Math.Min(lineCount, DisplayTextHistory.Count);
			if (drop > 0)
			{
				removed = drop;
				var start = DisplayTextHistory.Count - drop;
				DisplayTextHistory.RemoveRange(start, drop);
				var dropHtml = Math.Min(drop, DisplayHtmlHistory.Count);
				if (dropHtml > 0)
					DisplayHtmlHistory.RemoveRange(DisplayHtmlHistory.Count - dropHtml, dropHtml);
				var dropTemp = Math.Min(drop, DisplayTemporaryHistory.Count);
				if (dropTemp > 0)
					DisplayTemporaryHistory.RemoveRange(DisplayTemporaryHistory.Count - dropTemp, dropTemp);
				var dropEnded = Math.Min(drop, DisplayLineEndedHistory.Count);
				if (dropEnded > 0)
					DisplayLineEndedHistory.RemoveRange(DisplayLineEndedHistory.Count - dropEnded, dropEnded);
				var dropTokens = Math.Min(drop, DisplayInteractiveTokenHistory.Count);
				if (dropTokens > 0)
					DisplayInteractiveTokenHistory.RemoveRange(DisplayInteractiveTokenHistory.Count - dropTokens, dropTokens);
				if (DisplayTextHistory.Count == 0)
					historyStartLineNo = executionConsole?.LineCount ?? 0;
			}
			localConsole = executionConsole;
		}

		if (removed > 0)
			localConsole?.DeleteOutputTailLines(removed);

		// Cursor-up row estimation is error-prone with CJK/ambiguous-width glyphs.
		// Repainting from committed history keeps layout stable across terminals.
		localConsole?.RedrawOutputFromHistory(includePendingLine: true);
	}

	private static void ClearDisplay()
	{
		CliExecutionConsole? localConsole;
			lock (SyncRoot)
				{
					DisplayTextHistory.Clear();
					DisplayHtmlHistory.Clear();
					DisplayTemporaryHistory.Clear();
					DisplayLineEndedHistory.Clear();
					DisplayInteractiveTokenHistory.Clear();
					PendingInteractiveTokens.Clear();
					PendingLineEnd = true;
					HtmlIslandBuffer.Clear();
					historyStartLineNo = executionConsole?.LineCount ?? 0;
					lastPrintedInteractiveButtonCount = 0;
				lastPointingButtonInput = string.Empty;
			localConsole = executionConsole;
			}

		localConsole?.ClearOutputHistory();
		localConsole?.TryClearTerminalDisplay();
	}

	private static void ResetStyle()
	{
		lock (SyncRoot)
			ResetStyleStateNoLock();
	}

	private static void ThrowError(bool playSound)
	{
		if (playSound && !Console.IsOutputRedirected)
			Console.Error.Write('\a');
	}

	private static void DebugPrint(string text)
	{
		Console.Error.Write(text ?? string.Empty);
	}

	private static void DebugNewLine()
	{
		Console.Error.WriteLine();
	}

	private static void DebugClear()
	{
		if (!CanUseAnsiControl())
			return;

		try
		{
			Console.Error.Write("\u001b[2J\u001b[H");
		}
		catch
		{
			// Ignore terminal control failures and continue.
		}
	}

	private static void PrintTemporaryLine(string text)
	{
		FlushPending(force: false);
		var rendered = string.IsNullOrEmpty(text) ? string.Empty : $"[TEMP] {text}";
		PrintLine(rendered, RuntimeHost.HtmlEscape(rendered), rendered: null, temporary: true);
	}

	private static void PrintFlush(bool force)
	{
		var consumedForcedConsoleFlush = FlushPending(force);
		if (force && consumedForcedConsoleFlush)
			return;
		FlushExecutionConsolePending(force);
	}

	// Runtime script output (PRINT/PRINTFORM) is buffered in CliExecutionConsole.
	// PRINTBUTTON-family text is buffered here. To keep visual order and hit-mapping stable,
	// let execution console merge pending inline fragments into its own line buffer before flush.
	internal static CliInlineMergeChunk ConsumePendingInlineForScriptMerge()
	{
		lock (SyncRoot)
		{
			if (PendingLine.Length == 0)
				return new CliInlineMergeChunk(string.Empty, string.Empty, [], true);

			var plain = PendingLine.ToString();
			var rendered = PendingRenderedLine.ToString();
			var tokens = PendingInteractiveTokens.Count > 0
				? new List<CliInteractiveToken>(PendingInteractiveTokens)
				: [];
			var lineEnd = PendingLineEnd;
			var printedButtons = interactiveButtonCount;
			PendingLine.Clear();
			PendingRenderedLine.Clear();
			PendingHtml.Clear();
			PendingInteractiveTokens.Clear();
			PendingLineEnd = true;
			interactiveButtonCount = 0;
			lastPrintedInteractiveButtonCount = printedButtons;
			return new CliInlineMergeChunk(plain, rendered, tokens, lineEnd);
		}
	}

	private static int GetConsoleLineNo()
	{
		lock (SyncRoot)
			return (int)Math.Min(int.MaxValue, executionConsole?.LineCount ?? 0);
	}

	private static short GetKeyState(int keyCode)
	{
		if (keyCode < 0 || keyCode > ushort.MaxValue)
			return 0;

		lock (SyncRoot)
		{
			PruneExpiredKeyStatesNoLock(DateTimeOffset.UtcNow);
			return ActiveKeyStates.ContainsKey(keyCode) ? unchecked((short)0x8000) : (short)0;
		}
	}

	private static void PrintHtml(string html, bool toBuffer)
	{
		if (string.IsNullOrEmpty(html))
			return;

		var plain = RenderHtmlForCli(html);
		MaybeLogHtmlDebug(html, plain, toBuffer);
		if (toBuffer)
		{
			AppendInline(plain, isButton: false, htmlFragment: html);
			return;
		}

		FlushPending(force: false);
		PrintMultiLine(plain, html);
	}

	private static string RenderHtmlForCli(string html)
	{
		if (string.IsNullOrEmpty(html))
			return string.Empty;

		var output = new StringBuilder(html.Length + 32);
		var colorStack = new Stack<int?>();
		var index = 0;

		while (index < html.Length)
		{
			var match = HtmlTagRegex.Match(html, index);
			if (!match.Success)
			{
				AppendDecodedHtmlSegment(output, html[index..], colorStack.Count > 0 ? colorStack.Peek() : null);
				break;
			}

			if (match.Index > index)
			{
				var text = html.Substring(index, match.Index - index);
				AppendDecodedHtmlSegment(output, text, colorStack.Count > 0 ? colorStack.Peek() : null);
			}

			var isCloseTag = match.Groups["close"].Success;
			var tagName = match.Groups["name"].Value.ToLowerInvariant();
			var attrs = match.Groups["attrs"].Value;
			var nextIndex = match.Index + match.Length;

			if (!isCloseTag && tagName == "button")
			{
				if (TryFindButtonCloseTag(html, nextIndex, out var closeTagStart, out var closeTagEnd))
				{
					var innerHtml = html.Substring(nextIndex, closeTagStart - nextIndex);
					var innerText = RuntimeHost.HtmlToPlainText(innerHtml) ?? string.Empty;
					var value = string.Empty;
					var valueMatch = HtmlButtonValueAttributeRegex.Match(attrs ?? string.Empty);
					if (valueMatch.Success)
						value = WebUtility.HtmlDecode(valueMatch.Groups["value"].Value ?? string.Empty);
					var token = BuildButtonToken(innerText);
					output.Append(FormatInteractiveButtonToken(token));
					index = closeTagEnd;
					continue;
				}
			}

			switch (tagName)
			{
				case "br":
					if (!isCloseTag)
						output.Append('\n');
					break;

				case "p":
					if (isCloseTag && output.Length > 0 && output[^1] != '\n')
						output.Append('\n');
					break;

				case "shape":
					if (!isCloseTag && TryResolveShapeSpace(attrs, out var spaceCount))
						output.Append(' ', spaceCount);
					break;

				case "font":
					if (isCloseTag)
					{
						if (colorStack.Count > 0)
							colorStack.Pop();
					}
					else
					{
						int? color = null;
						var colorMatch = HtmlColorAttributeRegex.Match(attrs ?? string.Empty);
						if (colorMatch.Success && TryParseHtmlColorRgb(colorMatch.Groups["value"].Value, out var parsed))
							color = parsed;
						colorStack.Push(color);
					}
					break;
			}

			index = nextIndex;
		}

		return output.ToString();
	}

	private static void MaybeLogHtmlDebug(string html, string plain, bool toBuffer)
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("EMUERA_CLI_DEBUG_HTML"), "1", StringComparison.Ordinal))
			return;

		try
		{
			var hasInterestingToken =
				html.Contains("START", StringComparison.OrdinalIgnoreCase) ||
				html.Contains("역극", StringComparison.Ordinal) ||
				html.Contains("이름:", StringComparison.Ordinal) ||
				html.Contains("통칭:", StringComparison.Ordinal);
			if (!hasInterestingToken)
				return;

			var path = Path.Combine(Path.GetTempPath(), "emuera_cli_html_debug.log");
			var sb = new StringBuilder(512);
			sb.AppendLine("----- HTML_PRINT -----");
			sb.Append("toBuffer=").AppendLine(toBuffer ? "1" : "0");
			sb.AppendLine("html:");
			sb.AppendLine(EscapeForDebugLog(html));
			sb.AppendLine("plain:");
			sb.AppendLine(EscapeForDebugLog(plain));
			File.AppendAllText(path, sb.ToString());
		}
		catch
		{
		}
	}

	private static string EscapeForDebugLog(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;

		return text
			.Replace("\r", "\\r", StringComparison.Ordinal)
			.Replace("\n", "\\n\n", StringComparison.Ordinal);
	}

	private static bool TryFindButtonCloseTag(string html, int searchStart, out int closeTagStart, out int closeTagEnd)
	{
		closeTagStart = -1;
		closeTagEnd = -1;
		if (string.IsNullOrEmpty(html))
			return false;
		if (searchStart < 0 || searchStart >= html.Length)
			return false;

		closeTagStart = html.IndexOf("</button>", searchStart, StringComparison.OrdinalIgnoreCase);
		if (closeTagStart < 0)
			return false;

		closeTagEnd = closeTagStart + "</button>".Length;
		return true;
	}

	private static bool TryResolveShapeSpace(string attrs, out int spaceCount)
	{
		spaceCount = 0;
		if (string.IsNullOrWhiteSpace(attrs))
			return false;

		var typeMatch = HtmlShapeTypeAttributeRegex.Match(attrs);
		if (!typeMatch.Success)
			return false;
		var type = WebUtility.HtmlDecode(typeMatch.Groups["value"].Value ?? string.Empty).Trim();
		if (!type.Equals("space", StringComparison.OrdinalIgnoreCase))
			return false;

		var paramMatch = HtmlShapeParamAttributeRegex.Match(attrs);
		if (!paramMatch.Success)
			return false;
		var paramText = WebUtility.HtmlDecode(paramMatch.Groups["value"].Value ?? string.Empty).Trim();
		if (paramText.Length == 0)
			return false;

		if (!double.TryParse(paramText, NumberStyles.Float, CultureInfo.InvariantCulture, out var param))
			return false;
		if (param <= 0)
			return false;

		spaceCount = Math.Max(1, (int)Math.Round(param / 50.0, MidpointRounding.AwayFromZero));
		return true;
	}

	private static void AppendDecodedHtmlSegment(StringBuilder output, string rawText, int? colorRgb)
	{
		if (output == null || string.IsNullOrEmpty(rawText))
			return;

		var decoded = WebUtility.HtmlDecode(rawText);
		if (string.IsNullOrEmpty(decoded))
			return;

		if (colorRgb.HasValue)
		{
			output.Append(WrapWithForegroundColor(decoded, colorRgb.Value));
			return;
		}

		output.Append(decoded);
	}

	private static bool TryParseHtmlColorRgb(string raw, out int rgb)
	{
		rgb = 0;
		if (string.IsNullOrWhiteSpace(raw))
			return false;

		var text = WebUtility.HtmlDecode(raw).Trim();
		if (text.Length == 0)
			return false;

		if (text.StartsWith('#'))
			return TryParseHexColor(text[1..], out rgb);

		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			return TryParseHexColor(text[2..], out rgb);

		if (text.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && text.EndsWith(')'))
		{
			var body = text[4..^1];
			var parts = body.Split(',', StringSplitOptions.TrimEntries);
			if (parts.Length != 3)
				return false;
			if (!int.TryParse(parts[0], out var r) || !int.TryParse(parts[1], out var g) || !int.TryParse(parts[2], out var b))
				return false;
			if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255)
				return false;
			rgb = (r << 16) | (g << 8) | b;
			return true;
		}

		if (RuntimeHost.TryResolveNamedColorRgb(text, out var namedRgb))
		{
			rgb = namedRgb & 0xFFFFFF;
			return true;
		}

		return false;
	}

	private static bool TryParseHexColor(string hex, out int rgb)
	{
		rgb = 0;
		if (string.IsNullOrWhiteSpace(hex))
			return false;

		var text = hex.Trim();
		if (text.Length == 3)
		{
			if (!TryParseHexDigit(text[0], out var r) || !TryParseHexDigit(text[1], out var g) || !TryParseHexDigit(text[2], out var b))
				return false;
			rgb = ((r * 17) << 16) | ((g * 17) << 8) | (b * 17);
			return true;
		}

		if (text.Length != 6)
			return false;

		if (!int.TryParse(text, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
			return false;
		rgb = parsed & 0xFFFFFF;
		return true;
	}

	private static bool TryParseHexDigit(char ch, out int value)
	{
		value = 0;
		if (ch >= '0' && ch <= '9')
		{
			value = ch - '0';
			return true;
		}
		if (ch >= 'a' && ch <= 'f')
		{
			value = 10 + (ch - 'a');
			return true;
		}
		if (ch >= 'A' && ch <= 'F')
		{
			value = 10 + (ch - 'A');
			return true;
		}
		return false;
	}

	private static void PrintHtmlIsland(string html)
	{
		if (string.IsNullOrEmpty(html))
			return;

		lock (SyncRoot)
		{
			HtmlIslandBuffer.Add(html);
			if (HtmlIslandBuffer.Count > 256)
				HtmlIslandBuffer.RemoveRange(0, HtmlIslandBuffer.Count - 256);
		}
	}

	private static void ClearHtmlIsland()
	{
		lock (SyncRoot)
			HtmlIslandBuffer.Clear();
	}

	private static void PrintImage(string name, string nameb, string namem, object height, object width, object yOffset)
	{
		var label = BuildImageToken(name, nameb, namem, height, width, yOffset);
		AppendInline(label, isButton: false, htmlFragment: RuntimeHost.HtmlEscape(label));
	}

	private static void PrintShape(string shapeType, object[] parameters)
	{
		var label = BuildShapeToken(shapeType, parameters);
		AppendInline(label, isButton: false, htmlFragment: RuntimeHost.HtmlEscape(label));
	}

	private static string GetDisplayLineText(long lineNo)
	{
		lock (SyncRoot)
			return TryGetHistoryLine(DisplayTextHistory, lineNo);
	}

	internal static IReadOnlyList<string> GetDisplayTextHistorySnapshot(bool includePendingLine)
	{
		lock (SyncRoot)
		{
			var snapshot = new List<string>(DisplayTextHistory.Count + 1);
			snapshot.AddRange(DisplayTextHistory);
			if (!includePendingLine || PendingLine.Length == 0)
				return snapshot;

			var pending = PendingLine.ToString();
			if (snapshot.Count > 0 &&
				DisplayLineEndedHistory.Count > 0 &&
				!DisplayLineEndedHistory[^1])
			{
				snapshot[^1] = (snapshot[^1] ?? string.Empty) + pending;
			}
			else
			{
				snapshot.Add(pending);
			}

			return snapshot;
		}
	}

	internal static IReadOnlyList<CliInteractiveLineSnapshot> GetDisplayInteractiveLineSnapshots(bool includePendingLine)
	{
		lock (SyncRoot)
		{
			var snapshot = new List<CliInteractiveLineSnapshot>(DisplayTextHistory.Count + 1);
				for (var i = 0; i < DisplayTextHistory.Count; i++)
				{
					var text = DisplayTextHistory[i] ?? string.Empty;
					var lineEnded = i < DisplayLineEndedHistory.Count ? DisplayLineEndedHistory[i] : true;
					var tokens = i < DisplayInteractiveTokenHistory.Count
						? new List<CliInteractiveToken>(DisplayInteractiveTokenHistory[i])
						: new List<CliInteractiveToken>();
					snapshot.Add(new CliInteractiveLineSnapshot(text, lineEnded, tokens));
				}

			if (!includePendingLine || PendingLine.Length == 0)
				return snapshot;

				var pendingText = PendingLine.ToString();
				var pendingTokens = PendingInteractiveTokens.Count > 0
					? new List<CliInteractiveToken>(PendingInteractiveTokens)
					: new List<CliInteractiveToken>();
				var pendingLineEnded = PendingLineEnd;
			if (snapshot.Count > 0 &&
				DisplayLineEndedHistory.Count > 0 &&
				!DisplayLineEndedHistory[^1])
			{
				var last = snapshot[^1];
				var colOffset = CliTextDisplayWidth.GetDisplayWidth(last.Text);
				for (var i = 0; i < pendingTokens.Count; i++)
				{
					var token = pendingTokens[i];
					pendingTokens[i] = new CliInteractiveToken(
						token.StartCol + colOffset,
						token.EndCol + colOffset,
						token.Value);
				}

					var mergedTokens = last.Tokens.Count > 0
						? new List<CliInteractiveToken>(last.Tokens)
						: new List<CliInteractiveToken>();
					mergedTokens.AddRange(pendingTokens);
					snapshot[^1] = new CliInteractiveLineSnapshot(
						(last.Text ?? string.Empty) + pendingText,
						pendingLineEnded,
						mergedTokens);
				}
				else
				{
					snapshot.Add(new CliInteractiveLineSnapshot(
						pendingText,
						pendingLineEnded,
						pendingTokens));
				}

			return snapshot;
		}
	}

	internal static IReadOnlyList<string> GetDisplayInteractiveValuesSnapshot(bool includePendingLine, int maxLines)
	{
		lock (SyncRoot)
		{
			var values = new List<string>();
			var limitedMaxLines = Math.Max(1, maxLines);
			var start = Math.Max(0, DisplayInteractiveTokenHistory.Count - limitedMaxLines);
			for (var i = start; i < DisplayInteractiveTokenHistory.Count; i++)
			{
				foreach (var token in DisplayInteractiveTokenHistory[i])
				{
					if (!string.IsNullOrWhiteSpace(token.Value))
						values.Add(token.Value);
				}
			}

			if (includePendingLine)
			{
				foreach (var token in PendingInteractiveTokens)
				{
					if (!string.IsNullOrWhiteSpace(token.Value))
						values.Add(token.Value);
				}
			}

			return values;
		}
	}

	private static string GetDisplayLineHtml(long lineNo)
	{
		lock (SyncRoot)
			return TryGetHistoryLine(DisplayHtmlHistory, lineNo);
	}

	private static string PopDisplayLineHtml()
	{
		lock (SyncRoot)
		{
			if (DisplayHtmlHistory.Count == 0)
				return string.Empty;

			var lastIndex = DisplayHtmlHistory.Count - 1;
			var html = DisplayHtmlHistory[lastIndex];
			DisplayHtmlHistory.RemoveAt(lastIndex);
			if (DisplayTextHistory.Count > lastIndex)
				DisplayTextHistory.RemoveAt(lastIndex);
				if (DisplayTemporaryHistory.Count > lastIndex)
					DisplayTemporaryHistory.RemoveAt(lastIndex);
				if (DisplayLineEndedHistory.Count > lastIndex)
					DisplayLineEndedHistory.RemoveAt(lastIndex);
				if (DisplayInteractiveTokenHistory.Count > lastIndex)
					DisplayInteractiveTokenHistory.RemoveAt(lastIndex);
				if (DisplayHtmlHistory.Count == 0)
					historyStartLineNo = executionConsole?.LineCount ?? 0;
				return html;
			}
		}

	private static string GetPointingButtonInput()
	{
		lock (SyncRoot)
			return lastPointingButtonInput;
	}

	private static void ReloadErbFinished()
	{
		FlushPending(force: true);
		FlushExecutionConsolePending(force: true);
	}

	private static string GetTextBoxText()
	{
		lock (SyncRoot)
			return textBoxText;
	}

	private static void ChangeTextBox(string text)
	{
		lock (SyncRoot)
			textBoxText = text ?? string.Empty;
	}

	private static void ResetTextBoxPos()
	{
		lock (SyncRoot)
		{
			textBoxPosX = 0;
			textBoxPosY = 0;
			textBoxWidth = 0;
		}
	}

	private static void SetTextBoxPos(int xOffset, int yOffset, int width)
	{
		lock (SyncRoot)
		{
			textBoxPosX = xOffset;
			textBoxPosY = yOffset;
			textBoxWidth = Math.Max(0, width);
		}
	}

	private static void ApplyTextBoxChanges()
	{
		lock (SyncRoot)
		{
			if (textBoxPosX < 0)
				textBoxPosX = 0;
			if (textBoxPosY < 0)
				textBoxPosY = 0;
			if (textBoxWidth < 0)
				textBoxWidth = 0;
		}
	}

	private static void SetToolTipColorRgb(int foreColorRgb, int backColorRgb)
	{
		lock (SyncRoot)
		{
			toolTipForeColorRgb = foreColorRgb & 0xFFFFFF;
			toolTipBackColorRgb = backColorRgb & 0xFFFFFF;
		}
	}

	private static void SetToolTipDelay(int delay)
	{
		lock (SyncRoot)
			toolTipDelay = Math.Max(0, delay);
	}

	private static void SetToolTipDuration(int duration)
	{
		lock (SyncRoot)
			toolTipDuration = Math.Max(0, duration);
	}

	private static void SetToolTipFontName(string name)
	{
		lock (SyncRoot)
			toolTipFontName = name ?? string.Empty;
	}

	private static void SetToolTipFontSize(long size)
	{
		lock (SyncRoot)
			toolTipFontSize = size <= 0 ? 0 : size;
	}

	private static void SetCustomToolTip(bool enabled)
	{
		lock (SyncRoot)
			customToolTipEnabled = enabled;
	}

	private static void SetToolTipFormat(long format)
	{
		lock (SyncRoot)
			toolTipFormat = format;
	}

	private static void SetToolTipImageEnabled(bool enabled)
	{
		lock (SyncRoot)
			toolTipImageEnabled = enabled;
	}

	private static bool IsCtrlZEnabled()
	{
		var env = Environment.GetEnvironmentVariable("EMUERA_CTRLZ_ENABLED");
		if (string.IsNullOrWhiteSpace(env))
			env = Environment.GetEnvironmentVariable("EMUERA_CTRZ_ENABLED");
		if (!string.IsNullOrWhiteSpace(env))
		{
			if (env.Equals("1", StringComparison.OrdinalIgnoreCase))
				return true;
			if (env.Equals("0", StringComparison.OrdinalIgnoreCase))
				return false;
			if (bool.TryParse(env, out var parsed))
				return parsed;
		}

		var configType = Type.GetType("MinorShift.Emuera.Runtime.Config.Config, Emuera.RuntimeEngine", throwOnError: false);
		var property = configType?.GetProperty("Ctrl_Z_Enabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
		if (property?.PropertyType == typeof(bool))
		{
			try
			{
				var value = property.GetValue(null);
				if (value is bool enabled)
					return enabled;
			}
			catch
			{
			}
		}

		return true;
	}

	private static void CaptureRandomSeed(long[] seedBuffer)
	{
		if (seedBuffer == null || seedBuffer.Length == 0)
			return;

		try
		{
			var evaluator = RuntimeHost.GetVariableEvaluator();
			if (evaluator == null)
				return;

			var randProperty = evaluator.GetType().GetProperty("Rand", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			var randomState = randProperty?.GetValue(evaluator);
			if (randomState == null)
				return;

			var getRandMethod = randomState.GetType().GetMethod(
				"GetRand",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
				binder: null,
				types: new[] { typeof(long[]) },
				modifiers: null);
			getRandMethod?.Invoke(randomState, new object[] { seedBuffer });
		}
		catch
		{
		}
	}

	private static void CtrlZAddInput(string input)
	{
		if (!IsCtrlZEnabled())
			return;

		lock (SyncRoot)
		{
			ctrlZInputs.Add(input ?? string.Empty);
			if (ctrlZInputs.Count > HistoryLimit)
				ctrlZInputs.RemoveRange(0, ctrlZInputs.Count - HistoryLimit);
		}
	}

	private static void CtrlZOnSavePrepare(int saveSlot)
	{
		if (!IsCtrlZEnabled())
			return;

		lock (SyncRoot)
			ctrlZLastSaveExpected = saveSlot;
	}

	private static void CtrlZOnSave()
	{
		if (!IsCtrlZEnabled())
			return;

		lock (SyncRoot)
		{
			ctrlZLastSave = ctrlZLastSaveExpected;
			ctrlZInputs.Clear();
		}

		var randomSeed = new long[64];
		CaptureRandomSeed(randomSeed);
		lock (SyncRoot)
		{
			ctrlZRandomSeed = new long[randomSeed.Length];
			Array.Copy(randomSeed, ctrlZRandomSeed, randomSeed.Length);
		}
	}

	private static void CtrlZOnLoad(int saveSlot)
	{
		if (!IsCtrlZEnabled())
			return;

		lock (SyncRoot)
		{
			ctrlZLastSave = saveSlot;
			ctrlZInputs.Clear();
		}

		var randomSeed = new long[64];
		CaptureRandomSeed(randomSeed);
		lock (SyncRoot)
		{
			ctrlZRandomSeed = new long[randomSeed.Length];
			Array.Copy(randomSeed, ctrlZRandomSeed, randomSeed.Length);
		}
	}

	private static void HotkeyStateSet(nint key, nint value)
	{
		lock (SyncRoot)
		{
			if (!hotkeyStateInitialized)
				throw new InvalidOperationException("use HOTKEY_STATE_INIT before using HOTKEY_STATE");
			if (key < 0 || key >= hotkeyStateSize)
				throw new ArgumentOutOfRangeException(nameof(key));

			HotkeyStateMap[key] = value;
		}
	}

	private static void HotkeyStateInit(nint key)
	{
		lock (SyncRoot)
		{
			if (key < 0)
				throw new ArgumentOutOfRangeException(nameof(key));
			hotkeyStateSize = key;
			hotkeyStateInitialized = true;
			HotkeyStateMap.Clear();
		}
	}

	private static RuntimePoint GetMousePositionXY()
	{
		lock (SyncRoot)
			return mousePosition;
	}

	private static bool MoveMouseXY(int x, int y)
	{
		lock (SyncRoot)
		{
			mousePosition = new RuntimePoint(x, y);
			return true;
		}
	}

	private static void SetBitmapCacheEnabledForNextLine(bool enabled)
	{
		lock (SyncRoot)
			bitmapCacheEnabledForNextLine = enabled;
	}

	private static void SetRedrawTimer(int tickCount)
	{
		lock (SyncRoot)
			redrawTimerTick = tickCount <= 0 ? 0 : Math.Max(10, tickCount);
	}

	private static void ForceStopTimer()
	{
		lock (SyncRoot)
			redrawTimerTick = 0;
	}

	private static void RefreshStrings(bool _)
	{
		FlushPending(force: false);
		FlushExecutionConsolePending(force: false);
	}

	private static void FlushExecutionConsolePending(bool force)
	{
		CliExecutionConsole? localConsole;
		lock (SyncRoot)
			localConsole = executionConsole;
		localConsole?.FlushPendingScriptOutput(force);
	}

	private static void MarkUpdatedGeneration()
	{
		lock (SyncRoot)
			generationMarkedUpdated = true;
	}

	private static void DisableOutputLog()
	{
		lock (SyncRoot)
			outputLogDisabled = true;
	}

	private static bool OutputLog(string filename, bool hideInfo)
	{
		if (!TryResolveLogPath(filename, out var fullPath, out var relativePath))
			return false;

		try
		{
			var payload = BuildOutputLogPayload(hideInfo);
			File.WriteAllText(fullPath, payload);
			EmitInfoLine($"[LOG] {relativePath}");
			return true;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Output log failed: {ex.Message}");
			return false;
		}
	}

	private static bool OutputSystemLog(string filename)
	{
		lock (SyncRoot)
		{
			if (outputLogDisabled)
				return false;
		}

		return OutputLog(filename, hideInfo: false);
	}

	private static void ThrowTitleError(bool error)
	{
		lock (SyncRoot)
		{
			titleErrorState = true;
			titleErrorByError = error;
		}

		CliExecutionConsole? localConsole;
		lock (SyncRoot)
			localConsole = executionConsole;

		localConsole?.ForceQuit();
	}

	private static object? GetConsoleHost()
	{
		lock (SyncRoot)
			return executionConsole;
	}

	private static int GetConsoleClientHeight()
	{
		try
		{
			if (!Console.IsOutputRedirected)
			{
				if (Console.WindowHeight > 0)
					return Console.WindowHeight;
				if (Console.BufferHeight > 0)
					return Console.BufferHeight;
			}
		}
		catch
		{
		}

		var fromEnv = ParsePositiveIntEnv("LINES");
		return fromEnv > 0 ? fromEnv : 0;
	}

	private static int ParsePositiveIntEnv(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return 0;
		var raw = Environment.GetEnvironmentVariable(name);
		if (string.IsNullOrWhiteSpace(raw))
			return 0;
		return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
			? value
			: 0;
	}

	private static void CbgClear()
	{
		lock (SyncRoot)
		{
			CbgEntries.Clear();
			cbgButtonMapToken = null;
			RecountCbgNoLock();
		}
	}

	private static void CbgClearRange(int zmin, int zmax)
	{
		if (zmin > zmax)
			return;

		lock (SyncRoot)
		{
			CbgEntries.RemoveAll(entry => entry.ZDepth >= zmin && entry.ZDepth <= zmax && entry.ZDepth != 0);
			RecountCbgNoLock();
		}
	}

	private static void CbgClearButton()
	{
		lock (SyncRoot)
		{
			CbgEntries.RemoveAll(static entry => entry.Type == CbgEntryType.ButtonImage);
			cbgButtonMapToken = null;
			RecountCbgNoLock();
		}
	}

	private static void CbgClearButtonMap()
	{
		lock (SyncRoot)
		{
			cbgButtonMapToken = null;
		}
	}

	private static bool CbgSetGraphics(object graphics, int x, int y, int zdepth)
	{
		if (graphics == null || zdepth == 0)
			return false;

		lock (SyncRoot)
		{
			CbgEntries.Add(new CbgEntry(CbgEntryType.Graphics, zdepth));
			RecountCbgNoLock();
			return true;
		}
	}

	private static bool CbgSetButtonMap(object graphics)
	{
		if (graphics == null)
			return false;

		lock (SyncRoot)
		{
			if (ReferenceEquals(cbgButtonMapToken, graphics))
				return false;
			cbgButtonMapToken = graphics;
			return true;
		}
	}

	private static bool CbgSetImage(object sprite, int x, int y, int zdepth)
	{
		if (sprite == null || zdepth == 0)
			return false;

		lock (SyncRoot)
		{
			CbgEntries.Add(new CbgEntry(CbgEntryType.Image, zdepth));
			RecountCbgNoLock();
			return true;
		}
	}

	private static bool CbgSetButtonImage(int buttonValue, object spriteN, object spriteB, int x, int y, int zdepth, string tooltip)
	{
		if (zdepth == 0)
			return false;

		lock (SyncRoot)
		{
			CbgEntries.Add(new CbgEntry(CbgEntryType.ButtonImage, zdepth));
			RecountCbgNoLock();
			return true;
		}
	}

	private static void AddBackgroundImage(string name, long depth, float opacity)
	{
		if (string.IsNullOrWhiteSpace(name))
			return;

		lock (SyncRoot)
			BackgroundImageNames.Add(name.Trim());
	}

	private static void RemoveBackgroundImage(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return;

		lock (SyncRoot)
			BackgroundImageNames.Remove(name.Trim());
	}

	private static void ClearBackgroundImage()
	{
		lock (SyncRoot)
			BackgroundImageNames.Clear();
	}

	private static void AppendInline(
		string text,
		bool isButton,
		string htmlFragment,
		string? buttonValue = null,
		bool lineEnd = true)
	{
		if (string.IsNullOrEmpty(text))
			return;

		lock (SyncRoot)
		{
			if (isButton && !string.IsNullOrEmpty(buttonValue))
			{
				var startCol = CliTextDisplayWidth.GetDisplayWidth(PendingLine.ToString()) + 1;
				var width = CliTextDisplayWidth.GetDisplayWidth(text);
				if (width > 0)
					PendingInteractiveTokens.Add(new CliInteractiveToken(startCol, startCol + width - 1, buttonValue));
			}

			PendingLine.Append(text);
			PendingRenderedLine.Append(FormatInlineFragmentNoLock(text));
			PendingHtml.Append(htmlFragment);
			PendingLineEnd = lineEnd;
			if (isButton)
				interactiveButtonCount++;
		}
	}

	private static bool FlushPending(bool force)
	{
		string line;
		string renderedLine;
		string html;
		bool lineEnd;
		List<CliInteractiveToken> interactiveTokens;
		int printedButtons;
		CliExecutionConsole? localConsoleForForcedFlush = null;
		lock (SyncRoot)
		{
			if (PendingLine.Length == 0)
			{
				if (!force)
					return false;
				line = string.Empty;
				renderedLine = string.Empty;
				html = string.Empty;
				lineEnd = true;
				interactiveTokens = [];
				printedButtons = 0;
				localConsoleForForcedFlush = executionConsole;
			}
			else
			{
				line = PendingLine.ToString();
				renderedLine = PendingRenderedLine.ToString();
				html = PendingHtml.ToString();
				lineEnd = force || PendingLineEnd;
				interactiveTokens = [.. PendingInteractiveTokens];
				printedButtons = interactiveButtonCount;
				PendingLine.Clear();
				PendingRenderedLine.Clear();
				PendingHtml.Clear();
				PendingInteractiveTokens.Clear();
				PendingLineEnd = true;
				interactiveButtonCount = 0;
			}
			lastPrintedInteractiveButtonCount = printedButtons;
		}

		if (line.Length == 0 && force)
		{
			// Force-flush may be called at DRAWLINE boundaries with no pending host text.
			// Still close any open execution-console line so next output starts on a new row.
			localConsoleForForcedFlush?.FlushPendingScriptOutput(force: true);
			return true;
		}

		PrintLine(line, html, renderedLine, temporary: false, lineEnd: lineEnd, interactiveTokens: interactiveTokens);
		return false;
	}

	private static void PrintLine(
		string text,
		string html,
		string? rendered,
		bool temporary = false,
		bool lineEnd = true,
		IReadOnlyList<CliInteractiveToken>? interactiveTokens = null,
		bool allowLayoutSplit = true)
	{
		var plainRaw = text ?? string.Empty;
		var plainLine = lineEnd ? ApplyAlignment(plainRaw) : plainRaw;

		if (allowLayoutSplit &&
			lineEnd &&
			TrySplitOverflowByTerminalWidth(plainLine, interactiveTokens, out var overflowHead, out var overflowTail, out var overflowHeadTokens, out var overflowTailTokens))
		{
			PrintLine(
				overflowHead,
				RuntimeHost.HtmlEscape(overflowHead),
				rendered: null,
				temporary: temporary,
				lineEnd: true,
				interactiveTokens: overflowHeadTokens,
				allowLayoutSplit: true);
			PrintLine(
				overflowTail,
				RuntimeHost.HtmlEscape(overflowTail),
				rendered: null,
				temporary: temporary,
				lineEnd: lineEnd,
				interactiveTokens: overflowTailTokens,
				allowLayoutSplit: true);
			return;
		}

		if (allowLayoutSplit &&
			TryFindCollapsedSeparatorSplitIndex(plainLine, out var splitIndex))
		{
			var head = plainLine[..splitIndex].TrimEnd();
			var tail = plainLine[splitIndex..];
			if (head.Length > 0 && tail.Length > 0)
			{
				List<CliInteractiveToken>? headTokens = null;
				List<CliInteractiveToken>? tailTokens = null;
				if (interactiveTokens != null && interactiveTokens.Count > 0)
				{
					var splitCol = CliTextDisplayWidth.GetDisplayWidth(head);
					headTokens = [];
					tailTokens = [];
					foreach (var token in interactiveTokens)
					{
						if (token.StartCol <= 0 || token.EndCol < token.StartCol)
							continue;
						if (token.EndCol <= splitCol)
						{
							headTokens.Add(token);
							continue;
						}
						if (token.StartCol > splitCol)
						{
							tailTokens.Add(new CliInteractiveToken(
								token.StartCol - splitCol,
								token.EndCol - splitCol,
								token.Value));
							continue;
						}

						// Token crosses split boundary. Keep both visible slices.
						headTokens.Add(new CliInteractiveToken(
							token.StartCol,
							splitCol,
							token.Value));
						tailTokens.Add(new CliInteractiveToken(
							1,
							token.EndCol - splitCol,
							token.Value));
					}
				}

				PrintLine(
					head,
					RuntimeHost.HtmlEscape(head),
					rendered: null,
					temporary: temporary,
					lineEnd: true,
					interactiveTokens: headTokens,
					allowLayoutSplit: false);
				PrintLine(
					tail,
					RuntimeHost.HtmlEscape(tail),
					rendered: null,
					temporary: temporary,
					lineEnd: lineEnd,
					interactiveTokens: tailTokens,
					allowLayoutSplit: false);
				return;
			}
		}

		var htmlLine = string.IsNullOrEmpty(html) ? RuntimeHost.HtmlEscape(plainLine) : html;
		StoreHistoryLine(plainLine, htmlLine, temporary, lineEnd, interactiveTokens);
		var line = ResolveRenderedLine(plainRaw, plainLine, rendered);

		CliExecutionConsole? localConsole;
		lock (SyncRoot)
			localConsole = executionConsole;

		if (localConsole == null)
		{
			if (lineEnd)
				Console.WriteLine(line);
			else
				Console.Write(line);
			return;
		}
		localConsole.Print(line, lineEnd);
		// Host-side PrintLine already carries finalized lineEnd semantics.
		// Flush immediately so HTML_PRINT/ASK_CHOICES multi-line output does not collapse.
		if (lineEnd)
			localConsole.FlushPendingScriptOutput(force: false);
	}

	private static bool TryFindCollapsedSeparatorSplitIndex(string line, out int splitIndex)
	{
		splitIndex = -1;
		if (string.IsNullOrEmpty(line))
			return false;

		foreach (Match match in CollapsedSeparatorRegex.Matches(line))
		{
			if (!match.Success || match.Index <= 0)
				continue;

			var prefix = line[..match.Index];
			if (!ContainsNonSeparatorText(prefix))
				continue;

			splitIndex = match.Index;
			return true;
		}

		return false;
	}

	private static bool TrySplitOverflowByTerminalWidth(
		string line,
		IReadOnlyList<CliInteractiveToken>? interactiveTokens,
		out string head,
		out string tail,
		out List<CliInteractiveToken>? headTokens,
		out List<CliInteractiveToken>? tailTokens)
	{
		head = string.Empty;
		tail = string.Empty;
		headTokens = null;
		tailTokens = null;

		if (string.IsNullOrEmpty(line))
			return false;
		// Preserve ASCII-art/map layout for non-interactive lines.
		// Let the terminal do physical wrapping instead of host-side reflow.
		if (interactiveTokens == null || interactiveTokens.Count == 0)
			return false;
		// Dense numeric menu rows (e.g. 4-column filter lists) should keep
		// script-provided spacing; host-side split causes unstable columns by width.
		if (IsLikelyDenseChoiceGridLine(line, interactiveTokens))
			return false;

		var terminalWidth = GetConsoleClientWidth();
		if (terminalWidth <= 1)
			return false;

		var totalWidth = CliTextDisplayWidth.GetDisplayWidth(line);
		if (totalWidth <= terminalWidth)
			return false;

		var splitCol = 0;
		if (interactiveTokens != null && interactiveTokens.Count > 0)
		{
			foreach (var token in interactiveTokens)
			{
				if (token.StartCol <= 0 || token.EndCol < token.StartCol)
					continue;
				if (token.EndCol <= terminalWidth)
					splitCol = Math.Max(splitCol, token.EndCol);
			}

			if (splitCol <= 0)
			{
				foreach (var token in interactiveTokens)
				{
					if (token.StartCol <= 0 || token.EndCol < token.StartCol)
						continue;
					if (token.StartCol <= terminalWidth && token.EndCol > terminalWidth)
					{
						splitCol = token.StartCol - 1;
						break;
					}
				}
			}
		}

		if (splitCol <= 0)
			splitCol = FindWhitespaceSplitColumn(line, terminalWidth);
		if (splitCol <= 0 || splitCol >= totalWidth)
			return false;

		var split = CliTextDisplayWidth.SplitByDisplayWidth(line, splitCol);
		head = split[0];
		tail = split[1];
		if (head.Length == 0 || tail.Length == 0)
			return false;

		if (interactiveTokens == null || interactiveTokens.Count == 0)
			return true;

		var shift = CliTextDisplayWidth.GetDisplayWidth(head);
		headTokens = [];
		tailTokens = [];
		foreach (var token in interactiveTokens)
		{
			if (token.StartCol <= 0 || token.EndCol < token.StartCol)
				continue;

			if (token.EndCol <= shift)
			{
				headTokens.Add(token);
				continue;
			}

			if (token.StartCol > shift)
			{
				tailTokens.Add(new CliInteractiveToken(
					token.StartCol - shift,
					token.EndCol - shift,
					token.Value));
				continue;
			}

			headTokens.Add(new CliInteractiveToken(token.StartCol, shift, token.Value));
			tailTokens.Add(new CliInteractiveToken(1, token.EndCol - shift, token.Value));
		}

		if (headTokens.Count == 0)
			headTokens = null;
		if (tailTokens.Count == 0)
			tailTokens = null;
		return true;
	}

	private static bool IsLikelyDenseChoiceGridLine(string line, IReadOnlyList<CliInteractiveToken> interactiveTokens)
	{
		if (string.IsNullOrEmpty(line) || interactiveTokens == null || interactiveTokens.Count < 3)
			return false;

		var numericChoiceCount = 0;
		var wideGapCount = 0;
		var previousEndCol = -1;
		foreach (var token in interactiveTokens)
		{
			if (token.StartCol <= 0 || token.EndCol < token.StartCol)
				continue;
			if (!long.TryParse(token.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
				continue;

			numericChoiceCount++;
			if (previousEndCol > 0 && token.StartCol - previousEndCol >= 4)
				wideGapCount++;
			previousEndCol = token.EndCol;
		}

		return numericChoiceCount >= 3 && wideGapCount >= 2;
	}

	private static int FindWhitespaceSplitColumn(string line, int width)
	{
		if (string.IsNullOrEmpty(line) || width <= 0)
			return 0;

		var split = CliTextDisplayWidth.SplitByDisplayWidth(line, width);
		var prefix = split[0];
		if (prefix.Length == 0)
			return 0;

		var index = prefix.LastIndexOfAny([' ', '\t', '\u3000']);
		if (index <= 0)
			return CliTextDisplayWidth.GetDisplayWidth(prefix);

		return CliTextDisplayWidth.GetDisplayWidth(prefix[..(index + 1)]);
	}

	private static bool ContainsNonSeparatorText(string text)
	{
		if (string.IsNullOrEmpty(text))
			return false;

		foreach (var ch in text)
		{
			if (char.IsWhiteSpace(ch))
				continue;
			if (ch is '-' or '=')
				continue;
			return true;
		}

		return false;
	}

	private static string ResolveRenderedLine(string plainRaw, string plainLine, string? rendered)
	{
		var renderedRaw = rendered ?? string.Empty;
		if (!string.IsNullOrEmpty(renderedRaw))
		{
			if (ReferenceEquals(plainRaw, plainLine) || plainRaw == plainLine)
				return renderedRaw;

			if (plainLine.Length > plainRaw.Length)
			{
				var paddingLength = plainLine.Length - plainRaw.Length;
				if (paddingLength > 0)
					return plainLine[..paddingLength] + renderedRaw;
			}

			return renderedRaw;
		}

		var styledLine = FormatWithStyle(plainLine);
		return plainLine.Contains("\u001b[", StringComparison.Ordinal)
			? styledLine
			: ApplyInteractiveTokenHighlight(styledLine);
	}

	private static string FormatInlineFragmentNoLock(string text)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;
		if (text.Contains("\u001b[", StringComparison.Ordinal))
			return text;

		var styled = FormatWithStyle(text);
		return ApplyInteractiveTokenHighlight(styled);
	}

	private static void PrintMultiLine(string plain, string html)
	{
		var normalizedPlain = (plain ?? string.Empty)
			.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Replace('\r', '\n');
		var normalizedHtml = (html ?? string.Empty)
			.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Replace('\r', '\n');

		var plainLines = normalizedPlain.Split('\n');
		var htmlLines = normalizedHtml.Split('\n');
		var max = Math.Max(plainLines.Length, htmlLines.Length);
		if (max <= 0)
			return;

		var trailingBreak = normalizedPlain.EndsWith('\n');
		for (var i = 0; i < max; i++)
		{
			var line = i < plainLines.Length ? plainLines[i] : string.Empty;
			var htmlLine = i < htmlLines.Length ? htmlLines[i] : RuntimeHost.HtmlEscape(line);
			var lineEnd = i < max - 1 || trailingBreak;
			if (!lineEnd && line.Length == 0 && htmlLine.Length == 0)
				continue;
			PrintLine(line, htmlLine, rendered: null, temporary: false, lineEnd: lineEnd);
		}
	}

	private static void StoreHistoryLine(
		string plainLine,
		string htmlLine,
		bool temporary,
		bool lineEnd,
		IReadOnlyList<CliInteractiveToken>? interactiveTokens)
	{
		lock (SyncRoot)
		{
			if (DisplayTextHistory.Count == 0)
				historyStartLineNo = executionConsole?.LineCount ?? 0;

				var tokenList = interactiveTokens?.Count > 0
					? new List<CliInteractiveToken>(interactiveTokens)
					: new List<CliInteractiveToken>();
			var openExists = DisplayLineEndedHistory.Count > 0 && !DisplayLineEndedHistory[^1];
			if (openExists)
			{
				var colOffset = CliTextDisplayWidth.GetDisplayWidth(DisplayTextHistory[^1] ?? string.Empty);
				if (tokenList.Count > 0)
				{
					for (var i = 0; i < tokenList.Count; i++)
					{
						var token = tokenList[i];
						tokenList[i] = new CliInteractiveToken(
							token.StartCol + colOffset,
							token.EndCol + colOffset,
							token.Value);
					}
				}

				DisplayTextHistory[^1] = (DisplayTextHistory[^1] ?? string.Empty) + (plainLine ?? string.Empty);
				DisplayHtmlHistory[^1] = (DisplayHtmlHistory[^1] ?? string.Empty) + (htmlLine ?? string.Empty);
				DisplayTemporaryHistory[^1] = DisplayTemporaryHistory[^1] && temporary;
				DisplayLineEndedHistory[^1] = lineEnd;
				DisplayInteractiveTokenHistory[^1].AddRange(tokenList);
			}
			else
			{
				DisplayTextHistory.Add(plainLine ?? string.Empty);
				DisplayHtmlHistory.Add(htmlLine ?? string.Empty);
				DisplayTemporaryHistory.Add(temporary);
				DisplayLineEndedHistory.Add(lineEnd);
				DisplayInteractiveTokenHistory.Add(tokenList);
			}

			if (DisplayTextHistory.Count > HistoryLimit)
			{
				var drop = DisplayTextHistory.Count - HistoryLimit;
				DisplayTextHistory.RemoveRange(0, drop);
				DisplayHtmlHistory.RemoveRange(0, drop);
				DisplayTemporaryHistory.RemoveRange(0, drop);
				DisplayLineEndedHistory.RemoveRange(0, drop);
				DisplayInteractiveTokenHistory.RemoveRange(0, drop);
				historyStartLineNo += drop;
			}
		}
	}

	private static string TryGetHistoryLine(List<string> lines, long lineNo)
	{
		if (lineNo < historyStartLineNo)
			return string.Empty;

		var index = (int)(lineNo - historyStartLineNo);
		if (index < 0 || index >= lines.Count)
			return string.Empty;

		return lines[index] ?? string.Empty;
	}

	private static void SetLastPointingButtonInput(string input)
	{
		lock (SyncRoot)
			lastPointingButtonInput = input ?? string.Empty;
	}

	private static string BuildImageToken(string name, string nameb, string namem, object height, object width, object yOffset)
	{
		var baseName = string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name.Trim();
		var details = new List<string>();
		if (!string.IsNullOrWhiteSpace(nameb))
			details.Add($"hover={nameb.Trim()}");
		if (!string.IsNullOrWhiteSpace(namem))
			details.Add($"map={namem.Trim()}");
		var widthText = FormatMetric(width);
		if (!string.IsNullOrEmpty(widthText))
			details.Add($"w={widthText}");
		var heightText = FormatMetric(height);
		if (!string.IsNullOrEmpty(heightText))
			details.Add($"h={heightText}");
		var offsetText = FormatMetric(yOffset);
		if (!string.IsNullOrEmpty(offsetText))
			details.Add($"y={offsetText}");
		if (details.Count == 0)
			return $"[IMG:{baseName}]";
		return $"[IMG:{baseName} {string.Join(' ', details)}]";
	}

	private static string BuildShapeToken(string shapeType, object[] parameters)
	{
		var shape = string.IsNullOrWhiteSpace(shapeType) ? "shape" : shapeType.Trim();
		if (parameters == null || parameters.Length == 0)
			return $"[SHAPE:{shape}]";

		var values = new List<string>(parameters.Length);
		foreach (var parameter in parameters)
		{
			var value = FormatMetric(parameter);
			if (!string.IsNullOrEmpty(value))
				values.Add(value);
		}
		if (values.Count == 0)
			return $"[SHAPE:{shape}]";
		return $"[SHAPE:{shape} {string.Join(',', values)}]";
	}

	private static string FormatMetric(object value)
	{
		if (value == null)
			return string.Empty;
		var text = value.ToString();
		return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
	}

	private static string ApplyAlignment(string text)
	{
		RuntimeDisplayLineAlignment currentAlignment;
		lock (SyncRoot)
			currentAlignment = alignment;

		var width = GetConsoleClientWidth();
		var textWidth = CliTextDisplayWidth.GetDisplayWidth(text);
		if (width <= 0 || textWidth >= width || currentAlignment == RuntimeDisplayLineAlignment.LEFT)
			return text;

		var padding = currentAlignment switch
		{
			RuntimeDisplayLineAlignment.RIGHT => width - textWidth,
			RuntimeDisplayLineAlignment.CENTER => (width - textWidth) / 2,
			_ => 0,
		};
		if (padding <= 0)
			return text;
		return new string(' ', padding) + text;
	}

	private static string FormatWithStyle(string text)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;
		if (!CanUseAnsiControl())
			return text;

		List<string> codes = new();
		RuntimeFontStyleFlags currentStyle;
		int fg;
		int bg;
		bool hasFg;
		bool hasBg;
		bool allowSetColor;
		bool allowUserStyle;
		var forceBlackBackground = IsForceBlackBackgroundActive();

		lock (SyncRoot)
		{
			currentStyle = styleFlags;
			fg = stringColorRgb;
			bg = backgroundColorRgb;
			hasFg = hasStringColor;
			hasBg = hasBackgroundColor;
			allowSetColor = useSetColorStyle;
			allowUserStyle = useUserStyle;
		}

		if ((currentStyle & RuntimeFontStyleFlags.Bold) != 0)
			codes.Add("1");
		if ((currentStyle & RuntimeFontStyleFlags.Italic) != 0)
			codes.Add("3");
		if ((currentStyle & RuntimeFontStyleFlags.Underline) != 0)
			codes.Add("4");
		if ((currentStyle & RuntimeFontStyleFlags.Strikeout) != 0)
			codes.Add("9");

		if (allowSetColor && hasFg)
		{
			var fgCode = BuildAnsiForegroundCode(fg);
			if (!string.IsNullOrEmpty(fgCode))
				codes.Add(fgCode);
		}
		if (forceBlackBackground)
		{
			codes.Add(GetBlackBackgroundSgrParameters());
		}
		else if (allowUserStyle && hasBg)
		{
			var bgCode = BuildAnsiBackgroundCode(bg);
			if (!string.IsNullOrEmpty(bgCode))
				codes.Add(bgCode);
		}

		if (codes.Count == 0)
			return text;

		var restoreCode = forceBlackBackground ? BuildStyleResetWithBlackBackgroundSgrParameters() : "0";
		return $"\u001b[{string.Join(';', codes)}m{text}\u001b[{restoreCode}m";
	}

	private static string ApplyInteractiveTokenHighlight(string renderedLine)
	{
		if (string.IsNullOrEmpty(renderedLine))
			return string.Empty;
		if (!CanUseAnsiControl())
			return renderedLine;
		if (!IsInteractiveEmphasisEnabled())
			return renderedLine;

		var highlighted = NumericBracketTokenRegex.Replace(
			renderedLine,
			"\u001b[4m$0\u001b[24m");
		highlighted = AngleValueTokenRegex.Replace(
			highlighted,
			"\u001b[4m$0\u001b[24m");
		return highlighted;
	}

	internal static string DecorateExecutionConsoleLine(string text)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;
		if (text.Contains("\u001b[", StringComparison.Ordinal))
			return text;
		return ApplyInteractiveTokenHighlight(FormatWithStyle(text));
	}

	internal static string DecorateExternalConsoleLine(string text)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;
		if (text.Contains("\u001b[", StringComparison.Ordinal))
			return text;
		return ApplyInteractiveTokenHighlight(text);
	}

	private static string FormatInteractiveButtonToken(string token)
	{
		if (string.IsNullOrEmpty(token))
			return string.Empty;
		if (!CanUseAnsiControl())
			return token;
		if (!IsInteractiveEmphasisEnabled())
			return token;

		// Keep emphasis lightweight so original game colors remain authoritative.
		return $"\u001b[4m{token}\u001b[24m";
	}

	private static bool IsInteractiveEmphasisEnabled()
	{
		var env = Environment.GetEnvironmentVariable("EMUERA_CLI_EMPHASIZE_BUTTONS");
		if (string.IsNullOrWhiteSpace(env))
			return false;

		return env.Equals("1", StringComparison.OrdinalIgnoreCase) ||
			env.Equals("true", StringComparison.OrdinalIgnoreCase) ||
			env.Equals("yes", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsForceBlackBackgroundEnabled()
	{
		var env = Environment.GetEnvironmentVariable("EMUERA_CLI_FORCE_BLACK_BG");
		if (string.IsNullOrWhiteSpace(env))
			return false;

		return env.Equals("1", StringComparison.OrdinalIgnoreCase) ||
			env.Equals("true", StringComparison.OrdinalIgnoreCase) ||
			env.Equals("yes", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsForceBlackBackgroundActive()
	{
		if (!IsForceBlackBackgroundEnabled())
			return false;

		lock (SyncRoot)
			return deferredBlackBackgroundActivated;
	}

	private static bool IsOsc11BackgroundEnabled()
	{
		var env = Environment.GetEnvironmentVariable("EMUERA_CLI_OSC11_BG");
		if (string.IsNullOrWhiteSpace(env))
			return false;

		return env.Equals("1", StringComparison.OrdinalIgnoreCase) ||
			env.Equals("true", StringComparison.OrdinalIgnoreCase) ||
			env.Equals("yes", StringComparison.OrdinalIgnoreCase);
	}

	internal static bool IsBlackTerminalSurfaceActive()
	{
		lock (SyncRoot)
			return deferredBlackBackgroundActivated || osc11BackgroundApplied;
	}

	internal static bool IsForceBlackBackgroundPolicyActive()
	{
		if (!IsForceBlackBackgroundEnabled())
			return false;

		lock (SyncRoot)
			return deferredBlackBackgroundActivated;
	}

	internal static string GetBlackBackgroundSgrParameters()
	{
		// Use true-color black to match OSC 11 default background exactly.
		return ForcedBlackBackgroundSgrParameters;
	}

	internal static string BuildStyleResetWithBlackBackgroundSgrParameters()
	{
		return $"22;23;24;29;39;{GetBlackBackgroundSgrParameters()}";
	}

	internal static string BuildAnsiBlackClearPrefix()
	{
		if (!CanUseAnsiControl())
			return string.Empty;
		if (!IsBlackTerminalSurfaceActive())
			return string.Empty;

		return $"\u001b[{GetBlackBackgroundSgrParameters()}m";
	}

	internal static string DecorateInputPrompt(string prompt)
	{
		if (string.IsNullOrEmpty(prompt))
			return string.Empty;
		if (!CanUseAnsiControl())
			return prompt;
		if (!IsBlackTerminalSurfaceActive())
			return prompt;

		var bgCode = GetBlackBackgroundSgrParameters();
		return $"\u001b[{bgCode}m{prompt}\u001b[{bgCode}m";
	}

	internal static bool TryActivateDeferredBackgroundAfterTerminalClear()
	{
		if (!CanUseAnsiControl())
			return false;

		var activateDeferredBlack = false;
		var activateOsc11 = false;
		lock (SyncRoot)
		{
			if (!deferredBlackBackgroundActivated && IsForceBlackBackgroundEnabled())
			{
				deferredBlackBackgroundActivated = true;
				activateDeferredBlack = true;
			}

			if (!osc11BackgroundApplied && IsOsc11BackgroundEnabled())
			{
				osc11BackgroundApplied = true;
				activateOsc11 = true;
			}
		}

		if (!activateDeferredBlack && !activateOsc11)
			return false;
		if (activateOsc11)
			TryApplyOsc11Background(force: true);
		return true;
	}

	private static void TryApplyOsc11Background(bool force = false)
	{
		if (!CanUseAnsiControl())
			return;
		if (!IsOsc11BackgroundEnabled())
			return;

		lock (SyncRoot)
		{
			if (!force && osc11BackgroundApplied)
				return;
			osc11BackgroundApplied = true;
		}

		try
		{
			// OSC 11: set terminal default background color.
			// Some terminals/tmux setups may ignore this sequence.
			Console.Write("\u001b]11;#000000\u0007");
			Console.Out.Flush();
		}
		catch
		{
		}
	}

	private static string WrapWithForegroundColor(string text, int colorRgb)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;
		if (!CanUseAnsiControl())
			return text;

		int restoreRgb;
		lock (SyncRoot)
			restoreRgb = hasStringColor ? stringColorRgb : defaultForeColorRgb;

		var colorCode = BuildAnsiForegroundCode(colorRgb);
		var restoreCode = BuildAnsiForegroundCode(restoreRgb);
		if (string.IsNullOrEmpty(colorCode) || string.IsNullOrEmpty(restoreCode))
			return text;
		return $"\u001b[{colorCode}m{text}\u001b[{restoreCode}m";
	}

	private static string BuildAnsiForegroundCode(int rgb)
	{
		rgb &= 0xFFFFFF;
		return ResolveAnsiColorMode() switch
		{
			AnsiColorMode.TrueColor => BuildTrueColorCode(rgb, foreground: true),
			AnsiColorMode.Palette256 => $"38;5;{MapRgbToAnsi256Index(rgb)}",
			_ => MapRgbToAnsi16Code(rgb, foreground: true),
		};
	}

	private static string BuildAnsiBackgroundCode(int rgb)
	{
		rgb &= 0xFFFFFF;
		return ResolveAnsiColorMode() switch
		{
			AnsiColorMode.TrueColor => BuildTrueColorCode(rgb, foreground: false),
			AnsiColorMode.Palette256 => $"48;5;{MapRgbToAnsi256Index(rgb)}",
			_ => MapRgbToAnsi16Code(rgb, foreground: false),
		};
	}

	private static string BuildTrueColorCode(int rgb, bool foreground)
	{
		var r = (rgb >> 16) & 0xFF;
		var g = (rgb >> 8) & 0xFF;
		var b = rgb & 0xFF;
		var selector = foreground ? 38 : 48;
		return $"{selector};2;{r};{g};{b}";
	}

	private static AnsiColorMode ResolveAnsiColorMode()
	{
		var modeOverride = Environment.GetEnvironmentVariable("EMUERA_ANSI_COLOR_MODE");
		if (!string.IsNullOrWhiteSpace(modeOverride))
		{
			var normalized = modeOverride.Trim().ToLowerInvariant();
			if (normalized is "truecolor" or "24bit" or "24-bit" or "rgb")
				return AnsiColorMode.TrueColor;
			if (normalized is "256" or "256color" or "256-color")
				return AnsiColorMode.Palette256;
			if (normalized is "16" or "ansi16" or "basic")
				return AnsiColorMode.Palette16;
		}

		var colorTerm = Environment.GetEnvironmentVariable("COLORTERM") ?? string.Empty;
		var term = Environment.GetEnvironmentVariable("TERM") ?? string.Empty;
		if (ContainsIgnoreCase(colorTerm, "truecolor") ||
			ContainsIgnoreCase(colorTerm, "24bit") ||
			ContainsIgnoreCase(term, "direct") ||
			ContainsIgnoreCase(term, "truecolor"))
			return AnsiColorMode.TrueColor;
		if (ContainsIgnoreCase(term, "alacritty") ||
			ContainsIgnoreCase(term, "kitty") ||
			ContainsIgnoreCase(term, "wezterm") ||
			ContainsIgnoreCase(term, "ghostty") ||
			ContainsIgnoreCase(term, "foot"))
			return AnsiColorMode.TrueColor;
		if (ContainsIgnoreCase(term, "256color"))
			return AnsiColorMode.Palette256;
		if (ContainsIgnoreCase(term, "xterm") ||
			ContainsIgnoreCase(term, "screen") ||
			ContainsIgnoreCase(term, "tmux") ||
			ContainsIgnoreCase(term, "vte"))
			return AnsiColorMode.Palette256;
		return AnsiColorMode.Palette16;
	}

	private static bool ContainsIgnoreCase(string text, string token)
	{
		if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
			return false;
		return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static int MapRgbToAnsi256Index(int rgb)
	{
		var r = (rgb >> 16) & 0xFF;
		var g = (rgb >> 8) & 0xFF;
		var b = rgb & 0xFF;

		if (r == g && g == b)
		{
			if (r < 8)
				return 16;
			if (r > 248)
				return 231;
			return 232 + (int)Math.Round((r - 8) / 247.0 * 24.0);
		}

		var r6 = (int)Math.Round(r / 255.0 * 5.0);
		var g6 = (int)Math.Round(g / 255.0 * 5.0);
		var b6 = (int)Math.Round(b / 255.0 * 5.0);
		r6 = Math.Clamp(r6, 0, 5);
		g6 = Math.Clamp(g6, 0, 5);
		b6 = Math.Clamp(b6, 0, 5);
		return 16 + (36 * r6) + (6 * g6) + b6;
	}

	private static string MapRgbToAnsi16Code(int rgb, bool foreground)
	{
		var bestIndex = 0;
		var bestDistance = long.MaxValue;
		for (var i = 0; i < Ansi16RgbPalette.Length; i++)
		{
			var distance = ColorDistanceSquared(rgb, Ansi16RgbPalette[i]);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestIndex = i;
			}
		}

		var code = foreground ? Ansi16ForegroundCodes[bestIndex] : Ansi16BackgroundCodes[bestIndex];
		return code.ToString();
	}

	private static long ColorDistanceSquared(int rgbA, int rgbB)
	{
		var dr = ((rgbA >> 16) & 0xFF) - ((rgbB >> 16) & 0xFF);
		var dg = ((rgbA >> 8) & 0xFF) - ((rgbB >> 8) & 0xFF);
		var db = (rgbA & 0xFF) - (rgbB & 0xFF);
		return (long)dr * dr + (long)dg * dg + (long)db * db;
	}

	private static string BuildButtonToken(string text)
	{
		return text ?? string.Empty;
	}

	private static string BuildButtonCToken(string text, bool isRight)
	{
		return FormatTypeCString(text ?? string.Empty, isRight);
	}

	internal static string FormatTypeCString(string text, bool alignmentRight)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;

		var printCLength = ResolveRuntimePrintCLength();
		if (printCLength <= 0)
			printCLength = 25;

		var targetWidth = alignmentRight ? printCLength : printCLength + 1;
		var displayWidth = CliTextDisplayWidth.GetDisplayWidth(text);
		if (displayWidth >= targetWidth)
			return text;

		var pad = targetWidth - displayWidth;
		return alignmentRight
			? new string(' ', pad) + text
			: text + new string(' ', pad);
	}

	private static string BuildBarText(string barText, bool isConst)
	{
		var seed = string.IsNullOrEmpty(barText) ? "-" : barText;
		if (isConst)
			return seed;

		var width = GetConsoleClientWidth();
		if (width <= 0)
			width = 80;
		width = Math.Clamp(width - 1, 8, 240);

		var builder = new StringBuilder(width + seed.Length);
		while (builder.Length < width)
			builder.Append(seed);
		if (builder.Length > width)
			builder.Length = width;
		return builder.ToString();
	}

	private static bool CanUseAnsiControl()
	{
		if (Console.IsOutputRedirected)
			return false;
		var noAnsi = Environment.GetEnvironmentVariable("EMUERA_NO_ANSI");
		if (!string.IsNullOrWhiteSpace(noAnsi))
		{
			if (noAnsi.Equals("1", StringComparison.OrdinalIgnoreCase) ||
				noAnsi.Equals("true", StringComparison.OrdinalIgnoreCase) ||
				noAnsi.Equals("yes", StringComparison.OrdinalIgnoreCase))
				return false;
		}
		var term = Environment.GetEnvironmentVariable("TERM");
		if (string.IsNullOrWhiteSpace(term) || term.Equals("dumb", StringComparison.OrdinalIgnoreCase))
			return false;
		return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
	}

	private static bool TryResolveLogPath(string filename, out string fullPath, out string relativePath)
	{
		var exeDir = RuntimeEnvironment.ExeDir;
		var candidate = string.IsNullOrWhiteSpace(filename)
			? Path.Combine(exeDir, "emuera.log")
			: (Path.IsPathRooted(filename) ? filename : Path.Combine(exeDir, filename));

		fullPath = Path.GetFullPath(candidate);
		var relative = Path.GetRelativePath(exeDir, fullPath);
		if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
		{
			relativePath = string.Empty;
			Console.Error.WriteLine("Output log failed: path must be under game root.");
			return false;
		}

		var directory = Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		relativePath = relative;
		return true;
	}

	private static string BuildOutputLogPayload(bool hideInfo)
	{
		List<string> lines = new();
		string toolTipSummary;
		string ctrlZSummary;

		lock (SyncRoot)
		{
			toolTipSummary =
				$"ToolTip: custom={customToolTipEnabled}, delay={toolTipDelay}, duration={toolTipDuration}, " +
				$"font=\"{toolTipFontName}\"/{toolTipFontSize}, image={toolTipImageEnabled}, format={toolTipFormat}, " +
				$"fore=0x{toolTipForeColorRgb:X6}, back=0x{toolTipBackColorRgb:X6}";
			ctrlZSummary = $"CtrlZ: enabled={IsCtrlZEnabled()}, lastSave={ctrlZLastSave}, bufferedInputs={ctrlZInputs.Count}";
		}

		if (!hideInfo)
		{
			lines.Add("Environment");
			lines.Add(RuntimeHost.GetProductVersion());
			lines.Add($"GameRoot: {RuntimeEnvironment.ExeDir}");
			lines.Add(string.Empty);
			if (!string.IsNullOrWhiteSpace(RuntimeHost.GetWindowTitle()))
				lines.Add($"Title: {RuntimeHost.GetWindowTitle()}");
			if (titleErrorState)
				lines.Add($"TitleError: {(titleErrorByError ? "error" : "info")}");
			lines.Add(toolTipSummary);
			lines.Add(ctrlZSummary);
			lines.Add(string.Empty);
			lines.Add("Log");
			lines.Add(string.Empty);
		}

		CliExecutionConsole? localConsole;
		lock (SyncRoot)
			localConsole = executionConsole;

		if (localConsole != null)
		{
			lines.AddRange(localConsole.GetOutputLinesSnapshot(includePendingLine: true));
		}
		else
		{
			lock (SyncRoot)
			{
				lines.AddRange(DisplayTextHistory);
				if (PendingLine.Length > 0)
					lines.Add(PendingLine.ToString());
			}
		}

		return lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, lines);
	}

	private static void EmitInfoLine(string text)
	{
		if (string.IsNullOrEmpty(text))
			return;

		CliExecutionConsole? localConsole;
		lock (SyncRoot)
			localConsole = executionConsole;

		if (localConsole == null)
		{
			Console.WriteLine(text);
			return;
		}

		localConsole.PrintSystemLine(text);
	}

	private static void RecountCbgNoLock()
	{
		cbgGraphicsCount = 0;
		cbgImageCount = 0;
		cbgButtonImageCount = 0;

		foreach (var entry in CbgEntries)
		{
			switch (entry.Type)
			{
				case CbgEntryType.Graphics:
					cbgGraphicsCount++;
					break;
				case CbgEntryType.Image:
					cbgImageCount++;
					break;
				case CbgEntryType.ButtonImage:
					cbgButtonImageCount++;
					break;
			}
		}
	}

	private static void RememberKeyStateNoLock(int keyCode, DateTimeOffset timestamp)
	{
		if (keyCode <= 0 || keyCode > ushort.MaxValue)
			return;
		ActiveKeyStates[keyCode] = timestamp;
	}

	private static void PruneExpiredKeyStatesNoLock(DateTimeOffset now)
	{
		if (ActiveKeyStates.Count == 0)
			return;

		List<int>? expired = null;
		foreach (var pair in ActiveKeyStates)
		{
			if ((now - pair.Value) > KeyStateLatch)
			{
				expired ??= [];
				expired.Add(pair.Key);
			}
		}

		if (expired == null)
			return;

		foreach (var key in expired)
			ActiveKeyStates.Remove(key);
	}

	private static void RefreshRuntimePaletteNoLock()
	{
		defaultForeColorRgb = TryReadRuntimeConfigColorNoLock("ForeColorRgb", defaultForeColorRgb);
		defaultBackColorRgb = TryReadRuntimeConfigColorNoLock("BackColorRgb", defaultBackColorRgb);
		defaultFocusColorRgb = TryReadRuntimeConfigColorNoLock("FocusColorRgb", defaultFocusColorRgb);
	}

	private static int TryReadRuntimeConfigColorNoLock(string propertyName, int fallbackRgb)
	{
		try
		{
			var configType = Type.GetType("MinorShift.Emuera.Runtime.Config.Config, Emuera.RuntimeEngine", throwOnError: false);
			if (configType == null)
				return fallbackRgb;

			var property = configType.GetProperty(
				propertyName,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			if (property == null)
				return fallbackRgb;

			var value = property.GetValue(null);
			if (value is int rgb)
				return rgb & 0xFFFFFF;
		}
		catch
		{
		}

		return fallbackRgb;
	}

	private static int ResolveRuntimePrintCLength()
	{
		try
		{
			var configType = Type.GetType("MinorShift.Emuera.Runtime.Config.Config, Emuera.RuntimeEngine", throwOnError: false);
			if (configType == null)
				return 25;

			var property = configType.GetProperty(
				"PrintCLength",
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
			if (property == null)
				return 25;

			var value = property.GetValue(null);
			if (value is int length && length > 0)
				return length;
		}
		catch
		{
		}

		return 25;
	}

	private static void ResetStyleStateNoLock()
	{
		RefreshRuntimePaletteNoLock();

				PendingLine.Clear();
				PendingRenderedLine.Clear();
				PendingHtml.Clear();
				PendingInteractiveTokens.Clear();
				PendingLineEnd = true;
				DisplayTextHistory.Clear();
			DisplayHtmlHistory.Clear();
			DisplayTemporaryHistory.Clear();
			DisplayLineEndedHistory.Clear();
			DisplayInteractiveTokenHistory.Clear();
			HtmlIslandBuffer.Clear();
		interactiveButtonCount = 0;
		lastPrintedInteractiveButtonCount = 0;
		historyStartLineNo = executionConsole?.LineCount ?? 0;
		lastPointingButtonInput = string.Empty;
		textBoxText = string.Empty;
		textBoxPosX = 0;
		textBoxPosY = 0;
		textBoxWidth = 0;
		mousePosition = new RuntimePoint(0, 0);
		bitmapCacheEnabledForNextLine = false;
		redrawTimerTick = 0;
		outputLogDisabled = false;
		generationMarkedUpdated = false;
		titleErrorState = false;
		titleErrorByError = false;
		hotkeyStateInitialized = false;
		hotkeyStateSize = 0;
		toolTipForeColorRgb = 0;
		toolTipBackColorRgb = 0;
		toolTipDelay = 0;
		toolTipDuration = 0;
		toolTipFontName = string.Empty;
		toolTipFontSize = 0;
		customToolTipEnabled = false;
		toolTipFormat = 0;
		toolTipImageEnabled = true;
		ctrlZLastSave = -1;
		ctrlZLastSaveExpected = -1;
		ctrlZInputs.Clear();
		ctrlZRandomSeed = [];
		HotkeyStateMap.Clear();
		ActiveKeyStates.Clear();
		BackgroundImageNames.Clear();
		CbgEntries.Clear();
		cbgButtonMapToken = null;
		RecountCbgNoLock();
		alignment = RuntimeDisplayLineAlignment.LEFT;
		styleFlags = RuntimeFontStyleFlags.Regular;
		redrawMode = RuntimeRedrawMode.None;
		fontName = string.Empty;
		stringColorRgb = defaultForeColorRgb;
		backgroundColorRgb = defaultBackColorRgb;
		hasStringColor = true;
		hasBackgroundColor = false;
		useSetColorStyle = true;
		useUserStyle = true;
	}
}
