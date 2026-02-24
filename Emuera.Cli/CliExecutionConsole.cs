using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Utils;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

internal sealed class CliExecutionConsole : IExecutionConsole
{
	enum CliState
	{
		Initializing,
		Running,
		WaitInput,
		Quit,
		Error,
	}

	CliState state = CliState.Initializing;
	InputRequest? currentRequest;
	IRuntimeProcess? process;
	long lineCount;
	long warningCount;
	bool isTimeOut;
	bool warningSuppressed;
	readonly int warningLimit = ResolveWarningLimit();
	const int OutputHistoryLimit = 8192;
	static readonly Regex NumericAngleTokenRegex = new(
		"<\\s*(?<value>[+-]?\\d{1,19})\\s*>",
		RegexOptions.Compiled);
	static readonly Regex NumericBracketTokenRegex = new(
		"(?:\\[|\\uFF3B)\\s*(?<value>[+-]?\\d{1,19})\\s*(?:\\]|\\uFF3D)",
		RegexOptions.Compiled);
	static readonly Regex AnsiEscapeRegex = new(
		"\u001b\\[[0-?]*[ -/]*[@-~]",
		RegexOptions.Compiled);
	static readonly Regex AnsiSgrRegex = new(
		"\u001b\\[(?<params>[0-9;]*)m",
		RegexOptions.Compiled);
	readonly object outputSync = new();
	readonly List<string> outputHistory = [];
	readonly List<List<CliInteractiveToken>> outputInteractiveTokenHistory = [];
	readonly List<int> outputTerminalRowHistory = [];
	readonly StringBuilder pendingOutput = new();
	readonly List<CliInteractiveToken> pendingOutputInteractiveTokens = [];
	readonly StringBuilder pendingScriptOutput = new();
	readonly StringBuilder pendingScriptRenderedOutput = new();
	readonly List<CliInteractiveToken> pendingScriptInteractiveTokens = [];
	bool pendingScriptLineEnd = true;
	const int DefaultConsoleWidth = 120;
	const int DefaultConsoleHeight = 40;

	public bool RunERBFromMemory => false;
	public bool Enabled => state != CliState.Quit;
	public bool IsRunning => state == CliState.Initializing || state == CliState.Running;
	public bool IsTimeOut => isTimeOut;
	public bool MesSkip { get; set; }
	public long LineCount => lineCount;

	public bool IsWaitingInput => state == CliState.WaitInput && currentRequest != null;
	public bool IsTerminated => state == CliState.Quit || state == CliState.Error;
	public bool HasRuntimeError => state == CliState.Error;
	public InputRequest? PendingRequest => currentRequest;

	public void AttachProcess(IRuntimeProcess runtimeProcess)
	{
		process = runtimeProcess ?? throw new ArgumentNullException(nameof(runtimeProcess));
	}

	public void BeginRunning()
	{
		if (state == CliState.Quit || state == CliState.Error)
			return;
		state = CliState.Running;
	}

	public bool TrySubmitInput(string rawInput, bool timedOut = false)
	{
		if (!IsWaitingInput || currentRequest == null || process == null)
			return false;

		var req = currentRequest;
		var input = rawInput ?? string.Empty;
		if (timedOut)
			isTimeOut = true;
		var allowTimedDefault = req.Timelimit <= 0 || timedOut || isTimeOut;

		switch (req.InputType)
		{
			case InputType.EnterKey:
			case InputType.AnyKey:
			case InputType.Void:
				break;

			case InputType.IntValue:
			case InputType.IntButton:
				{
					if (string.IsNullOrEmpty(input) && req.HasDefValue && allowTimedDefault)
						input = req.DefIntValue.ToString();
					if (!long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
						return false;
					if (req.InputType == InputType.IntButton && !IsAcceptedIntButtonValue(req, intValue))
						return false;
					if (req.IsSystemInput)
						process.InputSystemInteger(intValue);
					else
						process.InputInteger(intValue);
					break;
				}

			case InputType.StrValue:
			case InputType.StrButton:
				{
					if (string.IsNullOrEmpty(input) && req.HasDefValue && allowTimedDefault)
						input = req.DefStrValue ?? string.Empty;
					if (req.IsSystemInput)
						process.InputSystemInteger(req.DefIntValue);
					process.InputString(input ?? string.Empty);
					break;
				}

			case InputType.AnyValue:
				{
					if (long.TryParse(input, out var anyInt))
					{
						if (req.IsSystemInput)
							process.InputSystemInteger(anyInt);
						else
							process.InputInteger(anyInt);
					}
					else
					{
						process.InputString(input ?? string.Empty);
					}
					break;
				}

			case InputType.PrimitiveMouseKey:
				{
					if (!TryParsePrimitivePacket(input, out var packet))
						return false;

					process.InputResult5(packet.Result0, packet.Result1, packet.Result2, packet.Result3, packet.Result4, packet.Result5);
					break;
				}
		}

			FlushPendingScriptOutput(force: false);
			WriteCommittedLine(input ?? string.Empty);
			currentRequest = null;
			state = CliState.Running;
			return true;
		}

	public void PrintWarning(string str, ScriptPosition? position, int level)
	{
		warningCount++;
		if (warningLimit >= 0 && warningCount > warningLimit)
		{
			if (!warningSuppressed)
			{
				Console.Error.WriteLine($"[WARN] further warnings suppressed after {warningLimit} entries (set EMUERA_CLI_WARNING_LIMIT to adjust).");
				warningSuppressed = true;
			}
			return;
		}

		if (position.HasValue)
		{
			var pos = position.Value;
			Console.Error.WriteLine($"[WARN:{level}] {pos.Filename}:{pos.LineNo} {str}");
			return;
		}
		Console.Error.WriteLine($"[WARN:{level}] {str}");
	}

	public void PrintSystemLine(string str)
	{
		FlushPendingScriptOutput(force: false);
		WriteCommittedLine(str ?? string.Empty);
	}

	public void PrintSingleLine(string str)
	{
		FlushPendingScriptOutput(force: false);
		WriteCommittedLine(str ?? string.Empty);
	}

	public void PrintSingleLine(string str, bool temporary)
	{
		FlushPendingScriptOutput(force: false);
		WriteCommittedLine(str ?? string.Empty);
	}

	public void Print(string str, bool lineEnd = true)
	{
		var text = str ?? string.Empty;
		if (text.Length == 0)
			return;

		var lineEndIndex = text.IndexOf('\n', StringComparison.Ordinal);
		if (lineEndIndex != -1)
		{
			var upper = text[..lineEndIndex];
			if (upper.Length > 0)
				AppendPendingScriptOutput(upper, lineEnd: false);
			FlushPendingScriptOutput(force: true);
			if (lineEndIndex < text.Length - 1)
			{
				var lower = text[(lineEndIndex + 1)..];
				Print(lower, lineEnd);
			}
			return;
		}

		if (lineEnd && IsStandaloneSeparatorLine(text))
			FlushPendingScriptOutput(force: true);

		AppendPendingScriptOutput(text, lineEnd);
		// Match WinForms PrintStringBuffer semantics:
		// lineEnd is metadata for the pending logical line, not an immediate commit trigger.
	}

	public void PrintC(string str, bool alignmentRight)
	{
		var text = CliRuntimeHostBridge.FormatTypeCString(str ?? string.Empty, alignmentRight);
		if (text.Length == 0)
			return;
		AppendPendingScriptOutput(text, lineEnd: true);
	}

	public void NewLine()
	{
		FlushPendingScriptOutput(force: true);
	}

	static bool IsStandaloneSeparatorLine(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return false;

		var plain = StripAnsi(text);
		if (string.IsNullOrWhiteSpace(plain))
			return false;
		var trimmed = plain.Trim();
		if (trimmed.Length < 24)
			return false;

		for (var i = 0; i < trimmed.Length; i++)
		{
			var ch = trimmed[i];
			if (ch is '-' or '=')
				continue;
			return false;
		}

		return true;
	}

	public void PrintError(string str)
	{
		Console.Error.WriteLine(str ?? string.Empty);
	}

	public void DebugAddTraceLog(string str)
	{
	}

	public void DebugRemoveTraceLog()
	{
	}

	public void DebugClearTraceLog()
	{
	}

	public void Await(int time)
	{
		if (time <= 0)
			return;
		Thread.Sleep((int)Math.Min(time, int.MaxValue));
	}

	public void ForceQuit()
	{
		state = CliState.Error;
	}

	public void Quit()
	{
		state = CliState.Quit;
	}

	public void ReadAnyKey(bool anykey = false, bool stopMesskip = false)
	{
		FlushPendingScriptOutput(force: false);
		isTimeOut = false;
		currentRequest = new InputRequest
		{
			InputType = anykey ? InputType.AnyKey : InputType.EnterKey,
			StopMesskip = stopMesskip,
		};
		state = CliState.WaitInput;
		if (process != null)
			process.NeedWaitToEventComEnd = false;
	}

	public void WaitInput(InputRequest req)
	{
		FlushPendingScriptOutput(force: false);
		isTimeOut = false;
		currentRequest = req ?? throw new ArgumentNullException(nameof(req));
		state = CliState.WaitInput;
	}

	internal void MarkTimedOut()
	{
		isTimeOut = true;
	}

	internal IReadOnlyList<string> GetOutputLinesSnapshot(bool includePendingLine)
	{
		lock (outputSync)
		{
			var snapshot = new List<string>(outputHistory.Count + 1);
			snapshot.AddRange(outputHistory);
			if (includePendingLine && pendingOutput.Length > 0)
				snapshot.Add(pendingOutput.ToString());
			if (includePendingLine && pendingScriptOutput.Length > 0)
			{
				if (snapshot.Count > 0 && pendingOutput.Length > 0)
					snapshot[^1] += pendingScriptRenderedOutput.ToString();
				else
					snapshot.Add(pendingScriptRenderedOutput.ToString());
			}
			return snapshot;
		}
	}

	internal IReadOnlyList<CliInteractiveLineSnapshot> GetOutputInteractiveLineSnapshots(bool includePendingLine)
	{
		lock (outputSync)
		{
			var snapshot = new List<CliInteractiveLineSnapshot>(outputHistory.Count + 1);
			for (var i = 0; i < outputHistory.Count; i++)
			{
				var text = outputHistory[i] ?? string.Empty;
				var tokens = i < outputInteractiveTokenHistory.Count
					? new List<CliInteractiveToken>(outputInteractiveTokenHistory[i])
					: [];
				snapshot.Add(new CliInteractiveLineSnapshot(text, true, tokens));
			}

			if (!includePendingLine)
				return snapshot;
			if (pendingOutput.Length == 0 && pendingScriptOutput.Length == 0)
				return snapshot;

			var pendingOutputText = pendingOutput.ToString();
			var pendingScriptText = pendingScriptRenderedOutput.ToString();
			var mergedText = pendingOutputText + pendingScriptText;
			var mergedTokens = new List<CliInteractiveToken>(pendingOutputInteractiveTokens.Count + pendingScriptInteractiveTokens.Count);
			if (pendingOutputInteractiveTokens.Count > 0)
				mergedTokens.AddRange(pendingOutputInteractiveTokens);

			if (pendingScriptInteractiveTokens.Count > 0)
			{
				var scriptOffset = CliTextDisplayWidth.GetDisplayWidth(StripAnsi(pendingOutputText));
				AppendShiftedTokensNoLock(mergedTokens, pendingScriptInteractiveTokens, scriptOffset);
			}

			var lineEnded = pendingOutput.Length == 0 ? pendingScriptLineEnd : false;
			snapshot.Add(new CliInteractiveLineSnapshot(mergedText, lineEnded, mergedTokens));
			return snapshot;
		}
	}

	internal IReadOnlyList<string> GetOutputInteractiveValuesSnapshot(bool includePendingLine, int maxLines)
	{
		lock (outputSync)
		{
			var values = new List<string>();
			var limitedMaxLines = Math.Max(1, maxLines);
			var start = Math.Max(0, outputInteractiveTokenHistory.Count - limitedMaxLines);
			for (var i = start; i < outputInteractiveTokenHistory.Count; i++)
			{
				foreach (var token in outputInteractiveTokenHistory[i])
				{
					if (!string.IsNullOrWhiteSpace(token.Value))
						values.Add(token.Value);
				}
			}

			if (!includePendingLine)
				return values;

			foreach (var token in pendingOutputInteractiveTokens)
			{
				if (!string.IsNullOrWhiteSpace(token.Value))
					values.Add(token.Value);
			}

			foreach (var token in pendingScriptInteractiveTokens)
			{
				if (!string.IsNullOrWhiteSpace(token.Value))
					values.Add(token.Value);
			}

			return values;
		}
	}

	internal int DeleteOutputTailLines(int lineCount)
	{
		if (lineCount <= 0)
			return 0;

		lock (outputSync)
		{
			if (outputHistory.Count == 0)
				return 0;
			var drop = Math.Min(lineCount, outputHistory.Count);
			var removedRows = 0;
			for (var i = outputTerminalRowHistory.Count - drop; i < outputTerminalRowHistory.Count; i++)
			{
				if (i >= 0)
					removedRows += outputTerminalRowHistory[i];
			}
			outputHistory.RemoveRange(outputHistory.Count - drop, drop);
			outputInteractiveTokenHistory.RemoveRange(outputInteractiveTokenHistory.Count - drop, drop);
			outputTerminalRowHistory.RemoveRange(outputTerminalRowHistory.Count - drop, drop);
			if (drop > 0)
				this.lineCount = Math.Max(0, this.lineCount - drop);
			return Math.Max(drop, removedRows);
		}
	}

	internal void ClearOutputHistory()
	{
		lock (outputSync)
		{
			outputHistory.Clear();
			outputInteractiveTokenHistory.Clear();
			outputTerminalRowHistory.Clear();
			pendingOutput.Clear();
			pendingOutputInteractiveTokens.Clear();
			pendingScriptOutput.Clear();
			pendingScriptRenderedOutput.Clear();
			pendingScriptInteractiveTokens.Clear();
			pendingScriptLineEnd = true;
			lineCount = 0;
		}
	}

	internal void TryClearTerminalDisplay()
	{
		if (!CanUseTerminalControl())
			return;

		if (!TryClearTerminalUsingAnsi())
		{
			try
			{
				Console.Clear();
			}
			catch
			{
				// Ignore terminal clear failures and continue.
			}
		}
	}

	internal void RedrawOutputFromHistory(bool includePendingLine)
	{
		if (!CanUseTerminalControl())
			return;

		List<string> committedLines;
		List<int> committedRows;
		string pendingRendered;
		lock (outputSync)
		{
			committedLines = [.. outputHistory];
			committedRows = [.. outputTerminalRowHistory];
			pendingRendered = includePendingLine ? BuildPendingRenderedLineNoLock() : string.Empty;
		}

		if (!TryClearTerminalUsingAnsi())
		{
			try
			{
				Console.Clear();
			}
			catch
			{
				// Ignore terminal clear failures and continue.
			}
		}

		var startIndex = 0;
		var terminalHeight = TryGetConsoleHeight();
		if (terminalHeight > 0 && committedLines.Count > 0)
		{
			var pendingRows = string.IsNullOrEmpty(pendingRendered) ? 0 : EstimateTerminalRowsNoLock(pendingRendered);
			var availableRows = Math.Max(1, terminalHeight - pendingRows - 1);
			var usedRows = 0;
			for (var i = committedLines.Count - 1; i >= 0; i--)
			{
				var rows = i < committedRows.Count && committedRows[i] > 0
					? committedRows[i]
					: EstimateTerminalRowsNoLock(committedLines[i]);
				rows = Math.Max(1, rows);
				if (usedRows + rows > availableRows)
				{
					startIndex = Math.Min(committedLines.Count - 1, i + 1);
					break;
				}
				usedRows += rows;
				startIndex = i;
			}
		}

		for (var i = startIndex; i < committedLines.Count; i++)
			Console.WriteLine(NormalizeRenderedForBlackSurface(committedLines[i] ?? string.Empty));

		if (!string.IsNullOrEmpty(pendingRendered))
			Console.Write(NormalizeRenderedForBlackSurface(pendingRendered));
	}

	internal void FlushPendingScriptOutput(bool force)
	{
		MergePendingInlineScriptOutput();

		string text;
		string renderedText;
		bool lineEnd;
		List<CliInteractiveToken> interactiveTokens;
		lock (outputSync)
		{
			if (pendingScriptOutput.Length == 0)
			{
				if (!force)
					return;
				text = string.Empty;
				renderedText = string.Empty;
				lineEnd = true;
				interactiveTokens = [];
			}
			else
			{
				text = pendingScriptOutput.ToString();
				renderedText = pendingScriptRenderedOutput.ToString();
				lineEnd = force || pendingScriptLineEnd;
				interactiveTokens = pendingScriptInteractiveTokens.Count > 0
					? [.. pendingScriptInteractiveTokens]
					: [];
				pendingScriptOutput.Clear();
				pendingScriptRenderedOutput.Clear();
				pendingScriptInteractiveTokens.Clear();
				pendingScriptLineEnd = true;
			}
		}

		if (lineEnd)
			WriteCommittedLine(text, renderedText, interactiveTokens);
		else if (text.Length > 0)
			WriteOpenText(text, renderedText, interactiveTokens);
	}

	void MergePendingInlineScriptOutput()
	{
		var inlineChunk = CliRuntimeHostBridge.ConsumePendingInlineForScriptMerge();
		if (inlineChunk.Text.Length == 0)
			return;

		lock (outputSync)
		{
			var colOffset = CliTextDisplayWidth.GetDisplayWidth(pendingScriptOutput.ToString());
			pendingScriptOutput.Append(inlineChunk.Text);
			pendingScriptRenderedOutput.Append(inlineChunk.RenderedText);
			AppendShiftedTokensNoLock(pendingScriptInteractiveTokens, inlineChunk.Tokens, colOffset);
			pendingScriptLineEnd = inlineChunk.LineEnded;
		}
	}

	void AppendPendingScriptOutput(string text, bool lineEnd)
	{
		MergePendingInlineScriptOutput();

		lock (outputSync)
		{
			var plain = text ?? string.Empty;
			pendingScriptOutput.Append(plain);
			pendingScriptRenderedOutput.Append(CliRuntimeHostBridge.DecorateExecutionConsoleLine(plain));
			pendingScriptLineEnd = lineEnd;
		}
	}

	void WriteCommittedLine(string text, string? renderedText = null, IReadOnlyList<CliInteractiveToken>? interactiveTokens = null)
	{
		var rendered = renderedText ?? CliRuntimeHostBridge.DecorateExecutionConsoleLine(text ?? string.Empty);
		rendered = NormalizeRenderedForBlackSurface(rendered);
		Console.WriteLine(rendered);
		RecordOutput(rendered, lineEnd: true, interactiveTokens);
		lineCount++;
	}

	void WriteOpenText(string text, string? renderedText = null, IReadOnlyList<CliInteractiveToken>? interactiveTokens = null)
	{
		var rendered = renderedText ?? CliRuntimeHostBridge.DecorateExecutionConsoleLine(text ?? string.Empty);
		rendered = NormalizeRenderedForBlackSurface(rendered);
		Console.Write(rendered);
		RecordOutput(rendered, lineEnd: false, interactiveTokens);
	}

	static string NormalizeRenderedForBlackSurface(string rendered)
	{
		if (string.IsNullOrEmpty(rendered))
			return rendered ?? string.Empty;
		if (!CanUseTerminalControl())
			return rendered;
		if (!CliRuntimeHostBridge.IsBlackTerminalSurfaceActive())
			return rendered;

		var blackBackgroundSgr = CliRuntimeHostBridge.GetBlackBackgroundSgrParameters();
		var blackBackgroundSequence = $"\u001b[{blackBackgroundSgr}m";
		var styleResetWithBlackBackground = CliRuntimeHostBridge.BuildStyleResetWithBlackBackgroundSgrParameters();
		var blackBackgroundParts = blackBackgroundSgr.Split(';', StringSplitOptions.RemoveEmptyEntries);
		var forceBlackBackground = CliRuntimeHostBridge.IsForceBlackBackgroundPolicyActive();
		var normalized = AnsiSgrRegex.Replace(rendered, match =>
		{
			var raw = match.Groups["params"].Value;
			var parts = string.IsNullOrEmpty(raw)
				? []
				: raw.Split(';', StringSplitOptions.RemoveEmptyEntries);

			if (!forceBlackBackground)
			{
				var hasReset = parts.Length == 0 || parts.Any(p => p == "0");
				var hasDefaultBg = parts.Any(p => p == "49");
				if (!hasReset && !hasDefaultBg)
					return match.Value;

				var kept = new List<string>();
				foreach (var p in parts)
				{
					if (p == "0" || p == "49")
						continue;
					kept.Add(p);
				}

				if (hasReset)
				{
					// Keep black background invariant while still resetting style/foreground.
					kept.InsertRange(0, styleResetWithBlackBackground.Split(';', StringSplitOptions.RemoveEmptyEntries));
				}
				else if (hasDefaultBg)
				{
					kept.AddRange(blackBackgroundParts);
				}

				return kept.Count == 0
					? blackBackgroundSequence
					: $"\u001b[{string.Join(';', kept)}m";
			}

			var strippedReset = parts.Length == 0;
			var strippedBackground = false;
			var keptForceBlack = new List<string>(parts.Length);
			for (var i = 0; i < parts.Length; i++)
			{
				var p = parts[i];
				if (p == "38")
				{
					AppendForegroundColorSgrParameters(parts, ref i, keptForceBlack);
					continue;
				}
				if (p == "0")
				{
					strippedReset = true;
					continue;
				}
				if (p == "49")
				{
					strippedBackground = true;
					continue;
				}
				if (TryConsumeBackgroundSgrParameters(parts, ref i))
				{
					strippedBackground = true;
					continue;
				}
				keptForceBlack.Add(p);
			}

			if (!strippedReset && !strippedBackground)
				return match.Value;

			if (strippedReset)
				keptForceBlack.InsertRange(0, styleResetWithBlackBackground.Split(';', StringSplitOptions.RemoveEmptyEntries));
			else if (strippedBackground)
				keptForceBlack.AddRange(blackBackgroundParts);

			return keptForceBlack.Count == 0
				? blackBackgroundSequence
				: $"\u001b[{string.Join(';', keptForceBlack)}m";
		});

		// Ensure subsequent wraps/blank cells inherit black background.
		return $"{blackBackgroundSequence}{normalized}{blackBackgroundSequence}";
	}

	static void AppendForegroundColorSgrParameters(string[] parts, ref int index, List<string> output)
	{
		output.Add("38");
		if (index + 1 >= parts.Length)
			return;

		var mode = parts[index + 1];
		output.Add(mode);
		if (mode == "5")
		{
			if (index + 2 < parts.Length)
			{
				output.Add(parts[index + 2]);
				index += 2;
			}
			else
			{
				index += 1;
			}
			return;
		}

		if (mode == "2")
		{
			var remaining = parts.Length - (index + 2);
			var take = Math.Min(3, Math.Max(remaining, 0));
			for (var j = 0; j < take; j++)
				output.Add(parts[index + 2 + j]);
			index += 1 + take;
			return;
		}

		index += 1;
	}

	static bool TryConsumeBackgroundSgrParameters(string[] parts, ref int index)
	{
		if (index < 0 || index >= parts.Length)
			return false;
		if (!int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
			return false;

		if ((code >= 40 && code <= 47) || (code >= 100 && code <= 107))
			return true;
		if (code != 48)
			return false;

		if (index + 1 >= parts.Length)
			return true;

		var mode = parts[index + 1];
		if (mode == "5")
		{
			index = Math.Min(index + 2, parts.Length - 1);
			return true;
		}
		if (mode == "2")
		{
			index = Math.Min(index + 4, parts.Length - 1);
			return true;
		}

		index = Math.Min(index + 1, parts.Length - 1);
		return true;
	}

	void RecordOutput(string text, bool lineEnd, IReadOnlyList<CliInteractiveToken>? interactiveTokens = null)
	{
		lock (outputSync)
		{
			var colOffset = GetRenderedDisplayWidthNoLock(pendingOutput);
			AppendShiftedTokensNoLock(pendingOutputInteractiveTokens, interactiveTokens, colOffset);

			if (lineEnd)
			{
				pendingOutput.Append(text ?? string.Empty);
				AppendLineNoLock(pendingOutput.ToString(), pendingOutputInteractiveTokens);
				pendingOutput.Clear();
				pendingOutputInteractiveTokens.Clear();
				return;
			}

			pendingOutput.Append(text ?? string.Empty);
		}
	}

	void AppendLineNoLock(string line, IReadOnlyList<CliInteractiveToken>? interactiveTokens)
	{
		var normalized = line ?? string.Empty;
		outputHistory.Add(normalized);
		var tokens = interactiveTokens?.Count > 0
			? new List<CliInteractiveToken>(interactiveTokens)
			: [];
		outputInteractiveTokenHistory.Add(tokens);
		outputTerminalRowHistory.Add(EstimateTerminalRowsNoLock(normalized));
		if (outputHistory.Count > OutputHistoryLimit)
		{
			var drop = outputHistory.Count - OutputHistoryLimit;
			outputHistory.RemoveRange(0, drop);
			outputInteractiveTokenHistory.RemoveRange(0, drop);
			outputTerminalRowHistory.RemoveRange(0, drop);
		}
	}

	string BuildPendingRenderedLineNoLock()
	{
		if (pendingOutput.Length == 0 && pendingScriptRenderedOutput.Length == 0)
			return string.Empty;

		var builder = new StringBuilder(pendingOutput.Length + pendingScriptRenderedOutput.Length);
		if (pendingOutput.Length > 0)
			builder.Append(pendingOutput);
		if (pendingScriptRenderedOutput.Length > 0)
			builder.Append(pendingScriptRenderedOutput);
		return builder.ToString();
	}

	static bool CanUseTerminalControl()
	{
		if (Console.IsOutputRedirected)
			return false;

		var term = Environment.GetEnvironmentVariable("TERM");
		if (string.IsNullOrWhiteSpace(term) || term.Equals("dumb", StringComparison.OrdinalIgnoreCase))
			return false;

		return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
	}

	static bool TryClearTerminalUsingAnsi()
	{
		try
		{
			const string clearSequence = "\u001b[2J\u001b[H";
			var clearPrefix = CliRuntimeHostBridge.BuildAnsiBlackClearPrefix();
			if (!string.IsNullOrEmpty(clearPrefix))
				Console.Write(clearPrefix);
			Console.Write(clearSequence);
			// First terminal clear is used as the transition point:
			// keep startup default background, then switch to deferred black policy.
			if (CliRuntimeHostBridge.TryActivateDeferredBackgroundAfterTerminalClear())
			{
				clearPrefix = CliRuntimeHostBridge.BuildAnsiBlackClearPrefix();
				if (!string.IsNullOrEmpty(clearPrefix))
					Console.Write(clearPrefix);
				Console.Write(clearSequence);
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	static int EstimateTerminalRowsNoLock(string renderedLine)
	{
		var columns = TryGetConsoleWidth();
		if (columns <= 0)
			return 1;

		var text = renderedLine ?? string.Empty;
		if (text.Length == 0)
			return 1;

		var normalized = text.Replace("\r", string.Empty, StringComparison.Ordinal);
		var lines = normalized.Split('\n');
		var rows = 0;
		foreach (var line in lines)
		{
			var width = CliTextDisplayWidth.GetDisplayWidth(StripAnsi(line));
			rows += Math.Max(1, (width + columns - 1) / columns);
		}
		return Math.Max(1, rows);
	}

	static int TryGetConsoleWidth()
	{
		try
		{
			if (!Console.IsOutputRedirected)
			{
				if (Console.WindowWidth > 0)
					return Console.WindowWidth;
				if (Console.BufferWidth > 0)
					return Console.BufferWidth;
			}
		}
		catch
		{
		}

		var fromEnv = ParsePositiveIntEnv("COLUMNS");
		return fromEnv > 0 ? fromEnv : DefaultConsoleWidth;
	}

	static int TryGetConsoleHeight()
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
		return fromEnv > 0 ? fromEnv : DefaultConsoleHeight;
	}

	static int ParsePositiveIntEnv(string name)
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

	static int GetRenderedDisplayWidthNoLock(StringBuilder builder)
	{
		if (builder == null || builder.Length == 0)
			return 0;
		return CliTextDisplayWidth.GetDisplayWidth(StripAnsi(builder.ToString()));
	}

	static void AppendShiftedTokensNoLock(
		List<CliInteractiveToken> destination,
		IReadOnlyList<CliInteractiveToken>? source,
		int colOffset)
	{
		if (destination == null || source == null || source.Count == 0)
			return;

		foreach (var token in source)
		{
			if (string.IsNullOrWhiteSpace(token.Value))
				continue;
			if (token.StartCol <= 0 || token.EndCol < token.StartCol)
				continue;

			destination.Add(new CliInteractiveToken(
				token.StartCol + colOffset,
				token.EndCol + colOffset,
				token.Value));
		}
	}

	static bool TryParsePrimitivePacket(string rawInput, out PrimitivePacket packet)
	{
		packet = default;
		if (string.IsNullOrWhiteSpace(rawInput))
		{
			packet = new PrimitivePacket(0, 0, 0, 0, 0, 0);
			return true;
		}

		var text = rawInput.Trim();
		if (text.Equals("timeout", StringComparison.OrdinalIgnoreCase))
		{
			packet = new PrimitivePacket(4, 0, 0, 0, 0, 0);
			return true;
		}

		if (TryParsePrimitiveCsv(text, out packet))
			return true;
		if (TryParsePrimitivePrefixed(text, out packet))
			return true;
		if (TryBuildPrimitiveKeyPacket(text, out packet))
			return true;

		return false;
	}

	static bool TryParsePrimitiveCsv(string text, out PrimitivePacket packet)
	{
		packet = default;
		var parts = text.Split(',', StringSplitOptions.TrimEntries);
		if (parts.Length != 6)
			return false;
		if (!int.TryParse(parts[0], out var result0))
			return false;
		if (!int.TryParse(parts[1], out var result1))
			return false;
		if (!int.TryParse(parts[2], out var result2))
			return false;
		if (!int.TryParse(parts[3], out var result3))
			return false;
		if (!int.TryParse(parts[4], out var result4))
			return false;
		if (!long.TryParse(parts[5], out var result5))
			return false;

		packet = new PrimitivePacket(result0, result1, result2, result3, result4, result5);
		return true;
	}

	static bool TryParsePrimitivePrefixed(string text, out PrimitivePacket packet)
	{
		packet = default;
		var parts = text.Split(':', StringSplitOptions.TrimEntries);
		if (parts.Length == 0)
			return false;

		if (parts[0].Equals("key", StringComparison.OrdinalIgnoreCase))
		{
			if (parts.Length < 2 || !TryParsePrimitiveKeySpec(parts[1], out var keyCode, out var keyModifiers))
				return false;

			var keyData = keyModifiers == 0
				? keyCode
				: VirtualKeyMap.ComposeKeyData(keyCode, keyModifiers);
			if (parts.Length >= 3)
			{
				if (int.TryParse(parts[2], out var explicitKeyData))
				{
					if (parts.Length != 3)
						return false;
					keyData = explicitKeyData;
				}
				else
				{
					if (!TryParsePrimitiveModifiers(parts, 2, out var extraModifiers))
						return false;
					keyData = VirtualKeyMap.ComposeKeyData(keyCode, keyModifiers | extraModifiers);
				}
			}

			packet = new PrimitivePacket(3, keyCode, keyData, 0, 0, 0);
			return true;
		}

		if (parts[0].Equals("wheel", StringComparison.OrdinalIgnoreCase))
		{
			if (parts.Length < 2 || !int.TryParse(parts[1], out var delta))
				return false;
			var x = 0;
			var y = 0;
			if (parts.Length >= 3 && !int.TryParse(parts[2], out x))
				return false;
			if (parts.Length >= 4 && !int.TryParse(parts[3], out y))
				return false;

			packet = new PrimitivePacket(2, delta, x, y, 0, 0);
			return true;
		}

		if (parts[0].Equals("mouse", StringComparison.OrdinalIgnoreCase))
		{
			if (parts.Length < 2 || !int.TryParse(parts[1], out var button))
				return false;
			var x = 0;
			var y = 0;
			var mapValue = 0;
			var result5 = 0L;
			if (parts.Length >= 3 && !int.TryParse(parts[2], out x))
				return false;
			if (parts.Length >= 4 && !int.TryParse(parts[3], out y))
				return false;
			if (parts.Length >= 5 && !int.TryParse(parts[4], out mapValue))
				return false;
			if (parts.Length >= 6 && !long.TryParse(parts[5], out result5))
				return false;

			packet = new PrimitivePacket(1, button, x, y, mapValue, result5);
			return true;
		}

		return false;
	}

	static bool TryBuildPrimitiveKeyPacket(string text, out PrimitivePacket packet)
	{
		packet = default;
		if (!TryParsePrimitiveKeySpec(text, out var keyCode, out var modifiers))
			return false;
		var keyData = modifiers == 0
			? keyCode
			: VirtualKeyMap.ComposeKeyData(keyCode, modifiers);
		packet = new PrimitivePacket(3, keyCode, keyData, 0, 0, 0);
		return true;
	}

	static bool TryParsePrimitiveKeySpec(string keySpec, out int keyCode, out ConsoleModifiers modifiers)
	{
		keyCode = 0;
		modifiers = 0;
		if (string.IsNullOrWhiteSpace(keySpec))
			return false;

		var hasKey = false;
		var segments = keySpec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (var segment in segments)
		{
			if (TryParsePrimitiveModifier(segment, out var modifier))
			{
				modifiers |= modifier;
				continue;
			}

			if (hasKey)
				return false;
			if (!VirtualKeyMap.TryParseKeyName(segment, out keyCode))
				return false;
			hasKey = true;
		}

		return hasKey;
	}

	static bool TryParsePrimitiveModifiers(string[] parts, int startIndex, out ConsoleModifiers modifiers)
	{
		modifiers = 0;
		if (parts == null || startIndex >= parts.Length)
			return false;

		var parsedAny = false;
		for (var index = startIndex; index < parts.Length; index++)
		{
			var text = parts[index];
			if (string.IsNullOrWhiteSpace(text))
				return false;
			var segments = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			if (segments.Length == 0)
				return false;
			foreach (var segment in segments)
			{
				if (!TryParsePrimitiveModifier(segment, out var modifier))
					return false;
				modifiers |= modifier;
				parsedAny = true;
			}
		}

		return parsedAny;
	}

	static bool TryParsePrimitiveModifier(string token, out ConsoleModifiers modifier)
	{
		modifier = 0;
		if (string.IsNullOrWhiteSpace(token))
			return false;

		if (token.Equals("shift", StringComparison.OrdinalIgnoreCase))
		{
			modifier = ConsoleModifiers.Shift;
			return true;
		}

		if (token.Equals("ctrl", StringComparison.OrdinalIgnoreCase) ||
			token.Equals("control", StringComparison.OrdinalIgnoreCase) ||
			token.Equals("controlkey", StringComparison.OrdinalIgnoreCase))
		{
			modifier = ConsoleModifiers.Control;
			return true;
		}

		if (token.Equals("alt", StringComparison.OrdinalIgnoreCase) ||
			token.Equals("menu", StringComparison.OrdinalIgnoreCase))
		{
			modifier = ConsoleModifiers.Alt;
			return true;
		}

		return false;
	}

	readonly record struct PrimitivePacket(int Result0, int Result1, int Result2, int Result3, int Result4, long Result5);

	static int ResolveWarningLimit()
	{
		const int defaultLimit = 200;
		var raw = Environment.GetEnvironmentVariable("EMUERA_CLI_WARNING_LIMIT");
		if (string.IsNullOrWhiteSpace(raw))
			return defaultLimit;
		if (!int.TryParse(raw, out var parsed))
			return defaultLimit;
		return parsed < 0 ? defaultLimit : parsed;
	}

	private bool IsAcceptedIntButtonValue(InputRequest request, long inputValue)
	{
		if (request == null)
			return false;
		if (request.HasDefValue && request.DefIntValue == inputValue)
			return true;

		var choices = CollectVisibleIntButtonChoices();
		if (choices.Count == 0)
			return true;
		return choices.Contains(inputValue);
	}

	private HashSet<long> CollectVisibleIntButtonChoices()
	{
		var choices = new HashSet<long>();

		var executionInteractiveValues = GetOutputInteractiveValuesSnapshot(includePendingLine: true, maxLines: 256);
		foreach (var raw in executionInteractiveValues)
		{
			if (string.IsNullOrWhiteSpace(raw))
				continue;
			if (!long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
				continue;
			choices.Add(parsed);
		}

		var lines = GetOutputLinesSnapshot(includePendingLine: true);
		if (lines.Count == 0)
			lines = CliRuntimeHostBridge.GetDisplayTextHistorySnapshot(includePendingLine: true);
		if (lines.Count == 0)
			return choices;

		var start = Math.Max(0, lines.Count - 256);
		for (var index = start; index < lines.Count; index++)
		{
			var line = StripAnsi(lines[index]);
			if (string.IsNullOrWhiteSpace(line))
				continue;

			ExtractChoicesFromRegex(line, NumericAngleTokenRegex, choices);
			ExtractChoicesFromRegex(line, NumericBracketTokenRegex, choices);
		}

		var interactiveValues = CliRuntimeHostBridge.GetDisplayInteractiveValuesSnapshot(includePendingLine: true, maxLines: 256);
		foreach (var raw in interactiveValues)
		{
			if (string.IsNullOrWhiteSpace(raw))
				continue;
			if (!long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
				continue;
			choices.Add(parsed);
		}

		return choices;
	}

	private static void ExtractChoicesFromRegex(string line, Regex regex, HashSet<long> output)
	{
		if (string.IsNullOrWhiteSpace(line) || regex == null || output == null)
			return;

		foreach (Match match in regex.Matches(line))
		{
			if (!match.Success)
				continue;

			var value = match.Groups["value"].Value.Trim();
			if (value.Length == 0)
				continue;
			if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
				continue;
			output.Add(parsed);
		}
	}

	private static string StripAnsi(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;
		return AnsiEscapeRegex.Replace(text, string.Empty);
	}
}
