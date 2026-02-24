using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MinorShift.Emuera.Runtime;

internal static class CliTerminalMouseInput
{
	private static readonly Regex MouseEventRegex = new(
		@"^\x1b\[<(?<button>\d+);(?<x>\d+);(?<y>\d+)(?<state>[mM])$",
		RegexOptions.Compiled);
	private static readonly Regex UrxvtMouseEventRegex = new(
		@"^\x1b\[(?<button>\d+);(?<x>\d+);(?<y>\d+)M$",
		RegexOptions.Compiled);

	private static readonly Regex BracketTokenRegex = new(
		@"(?<token>(?:\[|\uFF3B)(?<inner>[^\]\uFF3D\r\n]{1,48})(?:\]|\uFF3D))",
		RegexOptions.Compiled);

	private static readonly Regex AnsiEscapeRegex = new(
		"\u001b\\[[0-?]*[ -/]*[@-~]",
		RegexOptions.Compiled);

	internal readonly record struct ReadResult(
		bool Handled,
		bool Retry,
		bool TimedOut,
		bool Cancelled,
		bool UsedDirectInput,
		string Input);

	private readonly record struct MouseEvent(int Button, int X, int Y, bool IsPress);
	private readonly record struct ScreenHit(int Row, int ColStart, int ColEnd, string Value);
	private readonly record struct GlobalHit(int Row, int ColStart, int ColEnd, string Value);
	private enum HitLayoutMode
	{
		Auto,
		TopAnchored,
		BottomAnchored,
	}

	public static bool TryRead(
		InputRequest request,
		string prompt,
		CliExecutionConsole executionConsole,
		long timeoutMilliseconds,
		out ReadResult result)
	{
		result = default;
		if (!IsFeatureEnabled(request))
			return false;
		if (!TerminalSession.TryEnter(out var session))
			return false;

		using (session)
		{
			try
			{
				result = ReadLoop(request, prompt ?? string.Empty, executionConsole, timeoutMilliseconds);
				return result.Handled;
			}
			catch
			{
				// Fallback to existing input path if terminal parsing fails.
				return false;
			}
		}
	}

	private static ReadResult ReadLoop(
		InputRequest request,
		string prompt,
		CliExecutionConsole executionConsole,
		long timeoutMilliseconds)
	{
		var buffer = new StringBuilder();
		var deadline = timeoutMilliseconds > 0
			? DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds)
			: (DateTimeOffset?)null;

		Console.Write(CliRuntimeHostBridge.DecorateInputPrompt(prompt));
		while (true)
		{
			if (deadline.HasValue && DateTimeOffset.UtcNow >= deadline.Value)
			{
				Console.WriteLine();
				return new ReadResult(
					Handled: true,
					Retry: false,
					TimedOut: true,
					Cancelled: false,
					UsedDirectInput: true,
					Input: string.Empty);
			}

			if (!Console.KeyAvailable)
			{
				Thread.Sleep(8);
				continue;
			}

			var keyInfo = Console.ReadKey(intercept: true);
			CliRuntimeHostBridge.RecordConsoleKey(keyInfo);

			if (IsEscapeKey(keyInfo))
			{
				var escapeSequence = ReadEscapeSequence();
				if (TryParseMouseEvent(escapeSequence, out var mouseEvent))
				{
					if (TryHandleMouseEvent(request, executionConsole, prompt, buffer.ToString(), mouseEvent, out var mouseResult))
						return mouseResult;
					continue;
				}

				if (request.InputType is InputType.AnyKey or InputType.Void)
				{
					Console.WriteLine();
					return new ReadResult(true, false, false, false, true, "Escape");
				}

				continue;
			}

			if (request.InputType == InputType.EnterKey)
			{
				if (keyInfo.Key != ConsoleKey.Enter && keyInfo.KeyChar != '\r' && keyInfo.KeyChar != '\n')
				{
					Console.WriteLine();
					Console.WriteLine("Press ENTER to continue.");
					return new ReadResult(true, true, false, false, true, string.Empty);
				}

				Console.WriteLine();
				return new ReadResult(true, false, false, false, true, string.Empty);
			}

			if (request.InputType is InputType.AnyKey or InputType.Void)
			{
				Console.WriteLine();
				return new ReadResult(true, false, false, false, true, FormatAnyKey(keyInfo));
			}

			if (keyInfo.Key == ConsoleKey.Enter || keyInfo.KeyChar == '\r' || keyInfo.KeyChar == '\n')
			{
				var rawInput = buffer.ToString();
				Console.WriteLine();
				CliRuntimeHostBridge.RecordInputText(rawInput);
				return new ReadResult(true, false, false, false, true, rawInput);
			}

			if (IsBackspaceKey(keyInfo))
			{
				if (buffer.Length > 0)
				{
					buffer.Length--;
					Console.Write("\b \b");
				}
				continue;
			}

			if (IsCtrlC(keyInfo))
			{
				Console.WriteLine();
				return new ReadResult(true, false, false, true, true, string.Empty);
			}

			if (!char.IsControl(keyInfo.KeyChar) && keyInfo.KeyChar != '\0')
			{
				buffer.Append(keyInfo.KeyChar);
				Console.Write(keyInfo.KeyChar);
			}
		}
	}

	private static bool TryHandleMouseEvent(
		InputRequest request,
		CliExecutionConsole executionConsole,
		string prompt,
		string currentInputBuffer,
		MouseEvent mouseEvent,
		out ReadResult result)
	{
		result = default;
		if (!mouseEvent.IsPress)
			return false;

		if (request.InputType == InputType.EnterKey)
		{
			Console.WriteLine();
			result = new ReadResult(true, false, false, false, true, string.Empty);
			return true;
		}

		if (request.InputType is InputType.AnyKey or InputType.Void)
		{
			Console.WriteLine();
			result = new ReadResult(true, false, false, false, true, "Mouse");
			return true;
		}

		if (request.InputType == InputType.PrimitiveMouseKey)
			return false;

		var hitCandidates = BuildScreenHitCandidates(executionConsole, request, prompt, currentInputBuffer);
		foreach (var hits in hitCandidates)
		{
			if (!TryResolveExactHit(hits, mouseEvent.X, mouseEvent.Y, out var selectedValue))
				continue;

			Console.Write(selectedValue);
			Console.WriteLine();
			CliRuntimeHostBridge.RecordInputText(selectedValue);
			result = new ReadResult(true, false, false, false, true, selectedValue);
			return true;
		}

		var sameRowOnlyNearest = request.InputType is InputType.IntButton or InputType.StrButton;
		if (TryResolveBestNearestHit(hitCandidates, mouseEvent.X, mouseEvent.Y, sameRowOnlyNearest, out var selectedNearestValue))
		{
			Console.Write(selectedNearestValue);
			Console.WriteLine();
			CliRuntimeHostBridge.RecordInputText(selectedNearestValue);
			result = new ReadResult(true, false, false, false, true, selectedNearestValue);
			return true;
		}

		return false;
	}

	private static List<IReadOnlyList<ScreenHit>> BuildScreenHitCandidates(
		CliExecutionConsole executionConsole,
		InputRequest request,
		string prompt,
		string currentInputBuffer)
	{
		if (!TryGetTerminalSize(out var terminalWidth, out var terminalHeight))
			return [];
		if (terminalWidth <= 0 || terminalHeight <= 1)
			return [];

		var executionLines = executionConsole.GetOutputLinesSnapshot(includePendingLine: true);
		var executionInteractiveLines = executionConsole.GetOutputInteractiveLineSnapshots(includePendingLine: true);
		var displayLines = CliRuntimeHostBridge.GetDisplayTextHistorySnapshot(includePendingLine: true);
		var interactiveLines = CliRuntimeHostBridge.GetDisplayInteractiveLineSnapshots(includePendingLine: true);
		var candidates = new List<IReadOnlyList<ScreenHit>>(capacity: 6);

		var hasInteractiveCandidates = false;

		if (executionInteractiveLines.Count > 0)
		{
			var before = candidates.Count;
			AddInteractiveLayoutCandidates(
				candidates,
				executionInteractiveLines,
				terminalWidth,
				terminalHeight,
				prompt,
				currentInputBuffer);
			hasInteractiveCandidates |= candidates.Count > before;
		}

		if (interactiveLines.Count > 0)
		{
			var before = candidates.Count;
			AddInteractiveLayoutCandidates(
				candidates,
				interactiveLines,
				terminalWidth,
				terminalHeight,
				prompt,
				currentInputBuffer);
			hasInteractiveCandidates |= candidates.Count > before;
		}

		if (hasInteractiveCandidates &&
			request.InputType is InputType.IntButton or InputType.StrButton)
		{
			return candidates;
		}

		// For plain INPUT/INPUTS flows, execution console text is authoritative.
		// Mixing display/runtime history candidates can cause vertical off-by-one routing
		// when terminals report cursor rows inconsistently.
		if (request.InputType is InputType.IntValue or InputType.StrValue or InputType.AnyValue)
		{
			if (executionLines.Count > 0)
			{
				AddLayoutCandidates(
					candidates,
					executionLines,
					request,
					prompt,
					currentInputBuffer,
					terminalWidth,
					terminalHeight);
				return candidates;
			}

			if (displayLines.Count > 0)
			{
				AddLayoutCandidates(
					candidates,
					displayLines,
					request,
					prompt,
					currentInputBuffer,
					terminalWidth,
					terminalHeight);
				return candidates;
			}
		}

		if (displayLines.Count > 0)
		{
			AddLayoutCandidates(
				candidates,
				displayLines,
				request,
				prompt,
				currentInputBuffer,
				terminalWidth,
				terminalHeight);
		}

		if (executionLines.Count > 0 && (candidates.Count == 0 || displayLines.Count == 0))
		{
			AddLayoutCandidates(
				candidates,
				executionLines,
				request,
				prompt,
				currentInputBuffer,
				terminalWidth,
				terminalHeight);
			return candidates;
		}

		if (executionLines.Count > 0)
		{
			AddLayoutCandidates(
				candidates,
				executionLines,
				request,
				prompt,
				currentInputBuffer,
				terminalWidth,
				terminalHeight);
		}

		return candidates;
	}

	private static bool TryResolveBestNearestHit(
		IReadOnlyList<IReadOnlyList<ScreenHit>> hitCandidates,
		int x,
		int y,
		bool sameRowOnly,
		out string value)
	{
		value = string.Empty;
		if (hitCandidates == null || hitCandidates.Count == 0 || x <= 0 || y <= 0)
			return false;

		var thresholdPasses = sameRowOnly
			? new (int MaxRowDistance, int MaxColDistance)[]
			{
				(0, 16),
				(0, 28),
			}
			: new (int MaxRowDistance, int MaxColDistance)[]
			{
				(0, 40),
				(1, 12),
				(2, 6),
			};

		foreach (var threshold in thresholdPasses)
		{
			var bestScore = int.MaxValue;
			var bestValue = string.Empty;

			foreach (var hits in hitCandidates)
			{
				if (!TryResolveNearestHit(
					hits,
					x,
					y,
					threshold.MaxRowDistance,
					threshold.MaxColDistance,
					out var candidateValue,
					out var candidateScore))
				{
					continue;
				}

				if (candidateScore >= bestScore)
					continue;

				bestScore = candidateScore;
				bestValue = candidateValue;
			}

			if (bestScore != int.MaxValue)
			{
				value = bestValue;
				return true;
			}
		}

		return false;
	}

	private static void AddLayoutCandidates(
		List<IReadOnlyList<ScreenHit>> destination,
		IReadOnlyList<string> lines,
		InputRequest request,
		string prompt,
		string currentInputBuffer,
		int terminalWidth,
		int terminalHeight)
	{
		if (destination == null || lines == null || lines.Count == 0)
			return;

		AddLayoutCandidate(
			destination,
			BuildScreenHitsFromLines(lines, request, prompt, currentInputBuffer, terminalWidth, terminalHeight, HitLayoutMode.Auto));
		AddLayoutCandidate(
			destination,
			BuildScreenHitsFromLines(lines, request, prompt, currentInputBuffer, terminalWidth, terminalHeight, HitLayoutMode.TopAnchored));
		AddLayoutCandidate(
			destination,
			BuildScreenHitsFromLines(lines, request, prompt, currentInputBuffer, terminalWidth, terminalHeight, HitLayoutMode.BottomAnchored));
	}

	private static void AddInteractiveLayoutCandidates(
		List<IReadOnlyList<ScreenHit>> destination,
		IReadOnlyList<CliInteractiveLineSnapshot> lines,
		int terminalWidth,
		int terminalHeight,
		string prompt,
		string currentInputBuffer)
	{
		if (destination == null || lines == null || lines.Count == 0)
			return;

		AddLayoutCandidate(
			destination,
			BuildScreenHitsFromInteractiveLines(lines, terminalWidth, terminalHeight, prompt, currentInputBuffer, HitLayoutMode.Auto));
		AddLayoutCandidate(
			destination,
			BuildScreenHitsFromInteractiveLines(lines, terminalWidth, terminalHeight, prompt, currentInputBuffer, HitLayoutMode.TopAnchored));
		AddLayoutCandidate(
			destination,
			BuildScreenHitsFromInteractiveLines(lines, terminalWidth, terminalHeight, prompt, currentInputBuffer, HitLayoutMode.BottomAnchored));
	}

	private static void AddLayoutCandidate(List<IReadOnlyList<ScreenHit>> destination, IReadOnlyList<ScreenHit> candidate)
	{
		if (destination == null || candidate == null || candidate.Count == 0)
			return;

		for (var i = 0; i < destination.Count; i++)
		{
			if (AreHitListsEqual(destination[i], candidate))
				return;
		}

		destination.Add(candidate);
	}

	private static bool AreHitListsEqual(IReadOnlyList<ScreenHit> left, IReadOnlyList<ScreenHit> right)
	{
		if (ReferenceEquals(left, right))
			return true;
		if (left == null || right == null)
			return false;
		if (left.Count != right.Count)
			return false;

		for (var i = 0; i < left.Count; i++)
		{
			if (left[i].Row != right[i].Row)
				return false;
			if (left[i].ColStart != right[i].ColStart)
				return false;
			if (left[i].ColEnd != right[i].ColEnd)
				return false;
			if (!string.Equals(left[i].Value, right[i].Value, StringComparison.Ordinal))
				return false;
		}

		return true;
	}

	private static List<ScreenHit> BuildScreenHitsFromLines(
		IReadOnlyList<string> lines,
		InputRequest request,
		string prompt,
		string currentInputBuffer,
		int terminalWidth,
		int terminalHeight,
		HitLayoutMode layoutMode)
	{
		var hits = new List<ScreenHit>();
		if (lines == null || lines.Count == 0)
			return hits;

		var globalHits = new List<GlobalHit>();
		var globalRow = 1;
		foreach (var sourceLine in lines)
		{
			var line = StripAnsi(sourceLine ?? string.Empty);
			var lineWidth = CliTextDisplayWidth.GetDisplayWidth(line);
			AppendAngleTokenHits(globalHits, request, line, globalRow, terminalWidth);
			AppendBracketTokenHits(globalHits, request, line, globalRow, terminalWidth);

			var rows = Math.Max(1, (Math.Max(1, lineWidth) + terminalWidth - 1) / terminalWidth);
			globalRow += rows;
		}

		if (globalHits.Count == 0)
			return hits;

		var totalOutputRows = globalRow - 1;
		var promptRows = ComputePromptRows(prompt, currentInputBuffer, terminalWidth);
		var outputWindowRows = Math.Max(1, terminalHeight - promptRows);
		var visibleOutputRows = outputWindowRows;
		if (layoutMode == HitLayoutMode.Auto && TryGetCursorScreenRow(out var cursorRow))
		{
			// Cursor row anchors where output currently ends in the viewport.
			// This keeps hit rows correct even when the game starts mid-screen
			// (before any full-screen clear/refresh happens).
			var cursorBasedRows = cursorRow - promptRows;
			if (cursorBasedRows > 0)
				visibleOutputRows = Math.Clamp(cursorBasedRows, 1, outputWindowRows);
		}
		if (visibleOutputRows <= 0)
			return hits;

		var shift = Math.Max(0, totalOutputRows - visibleOutputRows);
		var displayedRows = Math.Min(totalOutputRows, visibleOutputRows);
		var topOffset = layoutMode == HitLayoutMode.TopAnchored
			? 0
			: Math.Max(0, visibleOutputRows - displayedRows);
		foreach (var global in globalHits)
		{
			var screenRow = topOffset + (global.Row - shift);
			if (screenRow < 1 || screenRow > visibleOutputRows)
				continue;
			hits.Add(new ScreenHit(screenRow, global.ColStart, global.ColEnd, global.Value));
		}

		return hits;
	}

	private static List<ScreenHit> BuildScreenHitsFromInteractiveLines(
		IReadOnlyList<CliInteractiveLineSnapshot> lines,
		int terminalWidth,
		int terminalHeight,
		string prompt,
		string currentInputBuffer,
		HitLayoutMode layoutMode)
	{
		var hits = new List<ScreenHit>();
		if (lines == null || lines.Count == 0)
			return hits;
		if (terminalWidth <= 0 || terminalHeight <= 1)
			return hits;

		var globalHits = new List<GlobalHit>();
		var globalRow = 1;
		foreach (var lineSnapshot in lines)
		{
			var line = StripAnsi(lineSnapshot.Text ?? string.Empty);
			foreach (var token in lineSnapshot.Tokens)
			{
				if (string.IsNullOrWhiteSpace(token.Value))
					continue;
				if (token.EndCol < token.StartCol || token.StartCol <= 0)
					continue;
				AppendWrappedHit(globalHits, globalRow, terminalWidth, token.StartCol, token.EndCol, token.Value);
			}

			var lineWidth = CliTextDisplayWidth.GetDisplayWidth(line);
			var rows = Math.Max(1, (Math.Max(1, lineWidth) + terminalWidth - 1) / terminalWidth);
			globalRow += rows;
		}

		if (globalHits.Count == 0)
			return hits;

		var totalOutputRows = globalRow - 1;
		var promptRows = ComputePromptRows(prompt, currentInputBuffer, terminalWidth);
		var outputWindowRows = Math.Max(1, terminalHeight - promptRows);
		var visibleOutputRows = outputWindowRows;
		if (layoutMode == HitLayoutMode.Auto && TryGetCursorScreenRow(out var cursorRow))
		{
			var cursorBasedRows = cursorRow - promptRows;
			if (cursorBasedRows > 0)
				visibleOutputRows = Math.Clamp(cursorBasedRows, 1, outputWindowRows);
		}
		if (visibleOutputRows <= 0)
			return hits;

		var shift = Math.Max(0, totalOutputRows - visibleOutputRows);
		var displayedRows = Math.Min(totalOutputRows, visibleOutputRows);
		var topOffset = layoutMode == HitLayoutMode.TopAnchored
			? 0
			: Math.Max(0, visibleOutputRows - displayedRows);
		foreach (var global in globalHits)
		{
			var screenRow = topOffset + (global.Row - shift);
			if (screenRow < 1 || screenRow > visibleOutputRows)
				continue;
			hits.Add(new ScreenHit(screenRow, global.ColStart, global.ColEnd, global.Value));
		}

		return hits;
	}

	private static bool TryResolveExactHit(IReadOnlyList<ScreenHit> hits, int x, int y, out string value)
	{
		value = string.Empty;
		if (hits == null || hits.Count == 0 || x <= 0 || y <= 0)
			return false;

		for (var i = hits.Count - 1; i >= 0; i--)
		{
			var hit = hits[i];
			if (hit.Row != y)
				continue;
			if (x < hit.ColStart || x > hit.ColEnd)
				continue;
			value = hit.Value;
			return true;
		}
		return false;
	}

	private static bool TryResolveNearestHit(
		IReadOnlyList<ScreenHit> hits,
		int x,
		int y,
		int maxRowDistance,
		int maxColDistance,
		out string value,
		out int score)
	{
		value = string.Empty;
		score = int.MaxValue;
		if (hits == null || hits.Count == 0)
			return false;

		var bestScore = int.MaxValue;
		var bestValue = string.Empty;
		for (var i = hits.Count - 1; i >= 0; i--)
		{
			var hit = hits[i];
			var rowDistance = Math.Abs(hit.Row - y);
			if (rowDistance > maxRowDistance)
				continue;

			var colDistance = x < hit.ColStart
				? hit.ColStart - x
				: x > hit.ColEnd
					? x - hit.ColEnd
					: 0;
			if (colDistance > maxColDistance)
				continue;

				var candidateScore = (rowDistance * 100) + colDistance;
				if (candidateScore >= bestScore)
					continue;
				bestScore = candidateScore;
				bestValue = hit.Value;
			}

		if (bestScore == int.MaxValue)
			return false;

		value = bestValue;
		score = bestScore;
		return true;
	}

	private static bool TryGetCursorScreenRow(out int row)
	{
		row = 0;
		try
		{
			row = Console.CursorTop + 1;
			return row > 0;
		}
		catch
		{
			return false;
		}
	}

	private static void AppendWrappedHit(
		List<GlobalHit> hits,
		int lineStartRow,
		int terminalWidth,
		int startCol,
		int endCol,
		string value)
	{
		if (hits == null || terminalWidth <= 0 || endCol < startCol || startCol <= 0)
			return;

		var startRowOffset = (startCol - 1) / terminalWidth;
		var endRowOffset = (endCol - 1) / terminalWidth;
		for (var rowOffset = startRowOffset; rowOffset <= endRowOffset; rowOffset++)
		{
			var row = lineStartRow + rowOffset;
			var colStart = rowOffset == startRowOffset
				? ((startCol - 1) % terminalWidth) + 1
				: 1;
			var colEnd = rowOffset == endRowOffset
				? ((endCol - 1) % terminalWidth) + 1
				: terminalWidth;
			hits.Add(new GlobalHit(row, colStart, colEnd, value));
		}
	}

	private static void AppendAngleTokenHits(
		List<GlobalHit> hits,
		InputRequest request,
		string line,
		int lineStartRow,
		int terminalWidth)
	{
		if (hits == null || request == null || string.IsNullOrEmpty(line))
			return;

		var scanIndex = 0;
		while (scanIndex < line.Length)
		{
			var valueOpen = line.IndexOf('<', scanIndex);
			if (valueOpen < 0)
				break;
			var valueClose = line.IndexOf('>', valueOpen + 1);
			if (valueClose < 0)
				break;

			var rawValue = line[(valueOpen + 1)..valueClose];
			var value = rawValue.Trim();
			if (value.Length == 0)
			{
				if (request.InputType is InputType.StrValue or InputType.StrButton or InputType.AnyValue)
					value = string.Empty;
				else
				{
					scanIndex = valueClose + 1;
					continue;
				}
			}

			var emptyStringChoice = value.Length == 0 &&
				request.InputType is InputType.StrValue or InputType.StrButton or InputType.AnyValue;
			if (emptyStringChoice || IsChoiceValueCompatible(request, value))
			{
				var tokenStart = ResolveAngleTokenStart(line, valueOpen);
				var tokenEnd = valueClose + 1;
				if (tokenStart >= 0 && tokenEnd > tokenStart && tokenEnd <= line.Length)
				{
					var startCol = CliTextDisplayWidth.GetDisplayWidth(line[..tokenStart]) + 1;
					var endCol = CliTextDisplayWidth.GetDisplayWidth(line[..tokenEnd]);
					AppendWrappedHit(hits, lineStartRow, terminalWidth, startCol, endCol, value);
				}
			}

			scanIndex = valueClose + 1;
		}
	}

	private static void AppendBracketTokenHits(
		List<GlobalHit> hits,
		InputRequest request,
		string line,
		int lineStartRow,
		int terminalWidth)
	{
		if (hits == null || request == null || string.IsNullOrEmpty(line))
			return;

		foreach (Match match in BracketTokenRegex.Matches(line))
		{
			if (!match.Success)
				continue;

			var tokenGroup = match.Groups["token"];
			var tokenStart = tokenGroup.Index;
			var tokenEnd = tokenStart + tokenGroup.Length;
			if (tokenStart < 0 || tokenEnd <= tokenStart || tokenEnd > line.Length)
				continue;

			var innerText = match.Groups["inner"].Value.Trim();
			if (!TryResolveBracketChoiceValue(request, innerText, out var value))
				continue;

			var startCol = CliTextDisplayWidth.GetDisplayWidth(line[..tokenStart]) + 1;
			var visualEnd = ResolveBracketVisualEnd(line, tokenEnd);
			var endCol = CliTextDisplayWidth.GetDisplayWidth(line[..visualEnd]);
			AppendWrappedHit(hits, lineStartRow, terminalWidth, startCol, endCol, value);
		}
	}

	private static int ResolveBracketVisualEnd(string line, int tokenEnd)
	{
		if (string.IsNullOrEmpty(line) || tokenEnd < 0 || tokenEnd > line.Length)
			return tokenEnd;

		var index = tokenEnd;
		while (index < line.Length)
		{
			var ch = line[index];
			if (ch == '[' || ch == '\uFF3B' || ch == '<' || ch == '\r' || ch == '\n')
				break;
			index++;
		}

		while (index > tokenEnd && char.IsWhiteSpace(line[index - 1]))
			index--;
		return Math.Max(tokenEnd, index);
	}

	private static int ResolveAngleTokenStart(string line, int valueOpen)
	{
		if (string.IsNullOrEmpty(line) || valueOpen <= 0)
			return Math.Max(0, valueOpen);

		var previous = line[valueOpen - 1];
		if (previous == ']' || previous == '\uFF3D')
		{
			var bracketStart = FindBracketTokenStart(line, valueOpen - 1);
			if (bracketStart >= 0)
				return bracketStart;
		}

		var index = valueOpen - 1;
		while (index >= 0)
		{
			var ch = line[index];
			if (char.IsWhiteSpace(ch) || IsAngleTokenBoundary(ch))
				break;
			index--;
		}

		return index + 1;
	}

	private static int FindBracketTokenStart(string line, int closeIndex)
	{
		if (string.IsNullOrEmpty(line) || closeIndex <= 0 || closeIndex >= line.Length)
			return -1;

		for (var index = closeIndex - 1; index >= 0; index--)
		{
			var ch = line[index];
			if (ch == '[' || ch == '\uFF3B')
				return index;
			if (char.IsWhiteSpace(ch) || IsAngleTokenBoundary(ch))
				break;
		}

		return -1;
	}

	private static bool IsAngleTokenBoundary(char ch)
	{
		return ch switch
		{
			'<' or '>' or '|' or '(' or ')' or '{' or '}' or ',' or ';' => true,
			_ => false,
		};
	}

	private static int ComputePromptRows(string prompt, string buffer, int terminalWidth)
	{
		if (terminalWidth <= 0)
			return 1;

		var width = CliTextDisplayWidth.GetDisplayWidth(prompt ?? string.Empty) +
			CliTextDisplayWidth.GetDisplayWidth(buffer ?? string.Empty);
		width = Math.Max(1, width);
		return Math.Max(1, (width + terminalWidth - 1) / terminalWidth);
	}

	private static bool TryGetTerminalSize(out int width, out int height)
	{
		width = 0;
		height = 0;
		try
		{
			width = Console.WindowWidth;
			height = Console.WindowHeight;
			return width > 0 && height > 0;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryParseMouseEvent(string escapeSequence, out MouseEvent mouseEvent)
	{
		mouseEvent = default;
		if (string.IsNullOrEmpty(escapeSequence))
			return false;

		var match = MouseEventRegex.Match(escapeSequence);
		if (match.Success)
		{
			if (!int.TryParse(match.Groups["button"].Value, out var button))
				return false;
			if (!int.TryParse(match.Groups["x"].Value, out var x))
				return false;
			if (!int.TryParse(match.Groups["y"].Value, out var y))
				return false;

			var state = match.Groups["state"].Value;
			mouseEvent = new MouseEvent(button, x, y, state == "M");
			return true;
		}

		if (TryParseUrxvtMouseEvent(escapeSequence, out mouseEvent))
			return true;
		if (TryParseX10MouseEvent(escapeSequence, out mouseEvent))
			return true;

		return false;
	}

	private static bool TryParseUrxvtMouseEvent(string escapeSequence, out MouseEvent mouseEvent)
	{
		mouseEvent = default;
		var match = UrxvtMouseEventRegex.Match(escapeSequence);
		if (!match.Success)
			return false;
		if (!int.TryParse(match.Groups["button"].Value, out var button))
			return false;
		if (!int.TryParse(match.Groups["x"].Value, out var x))
			return false;
		if (!int.TryParse(match.Groups["y"].Value, out var y))
			return false;

		var isRelease = (button & 3) == 3;
		mouseEvent = new MouseEvent(button, x, y, !isRelease);
		return true;
	}

	private static bool TryParseX10MouseEvent(string escapeSequence, out MouseEvent mouseEvent)
	{
		mouseEvent = default;
		if (escapeSequence.Length < 6)
			return false;
		if (escapeSequence[0] != '\x1b' || escapeSequence[1] != '[' || escapeSequence[2] != 'M')
			return false;

		var cb = escapeSequence[3] - 32;
		var x = escapeSequence[4] - 32;
		var y = escapeSequence[5] - 32;
		if (x <= 0 || y <= 0)
			return false;

		var isRelease = (cb & 3) == 3;
		mouseEvent = new MouseEvent(cb, x, y, !isRelease);
		return true;
	}

	private static string ReadEscapeSequence()
	{
		var builder = new StringBuilder();
		builder.Append('\x1b');

		var deadline = DateTimeOffset.UtcNow.AddMilliseconds(20);
		while (DateTimeOffset.UtcNow <= deadline && builder.Length < 128)
		{
			if (!Console.KeyAvailable)
			{
				Thread.Sleep(1);
				continue;
			}

			var keyInfo = Console.ReadKey(intercept: true);
			CliRuntimeHostBridge.RecordConsoleKey(keyInfo);

			if (keyInfo.KeyChar != '\0')
			{
				builder.Append(keyInfo.KeyChar);
			}
			else if (keyInfo.Key == ConsoleKey.Enter)
			{
				builder.Append('\n');
			}
			else
			{
				continue;
			}

			var tail = builder[^1];
			if (tail == 'M' || tail == 'm')
				break;
			deadline = DateTimeOffset.UtcNow.AddMilliseconds(4);
		}

		return builder.ToString();
	}

	private static bool IsFeatureEnabled(InputRequest request)
	{
		if (request == null)
			return false;
		if (Console.IsInputRedirected || Console.IsOutputRedirected)
			return false;
		if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
			return false;
		if (request.InputType == InputType.PrimitiveMouseKey)
			return false;

		var term = Environment.GetEnvironmentVariable("TERM");
		if (string.IsNullOrWhiteSpace(term) || term.Equals("dumb", StringComparison.OrdinalIgnoreCase))
			return false;

		var env = Environment.GetEnvironmentVariable("EMUERA_CLI_TEXT_CLICK");
		if (string.IsNullOrWhiteSpace(env))
			return true;

		if (env.Equals("0", StringComparison.OrdinalIgnoreCase) ||
			env.Equals("false", StringComparison.OrdinalIgnoreCase) ||
			env.Equals("no", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return true;
	}

	private static bool IsChoiceValueCompatible(InputRequest request, string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;

		return request.InputType switch
		{
			InputType.IntValue or InputType.IntButton => TryParseChoiceInteger(value, out _),
			InputType.StrValue or InputType.StrButton => true,
			InputType.AnyValue => true,
			_ => false,
		};
	}

	private static bool TryResolveBracketChoiceValue(InputRequest request, string text, out string value)
	{
		value = string.Empty;
		if (request == null || string.IsNullOrWhiteSpace(text))
			return false;

		var trimmed = text.Trim();
		switch (request.InputType)
		{
			case InputType.IntValue:
			case InputType.IntButton:
				if (!TryParseChoiceInteger(trimmed, out var intValue))
					return false;
				value = intValue.ToString(CultureInfo.InvariantCulture);
				return true;

			case InputType.StrValue:
			case InputType.StrButton:
				if (!IsLikelyTextChoice(trimmed))
					return false;
				value = trimmed;
				return true;

			case InputType.AnyValue:
				if (TryParseChoiceInteger(trimmed, out var anyValue))
				{
					value = anyValue.ToString(CultureInfo.InvariantCulture);
					return true;
				}

				if (!IsLikelyTextChoice(trimmed))
					return false;
				value = trimmed;
				return true;
		}

		return false;
	}

	private static bool IsLikelyTextChoice(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;
		if (value.Length > 48)
			return false;

		var hasText = false;
		foreach (var ch in value)
		{
			if (char.IsControl(ch))
				return false;
			if (char.IsWhiteSpace(ch))
				continue;
			if (char.IsLetterOrDigit(ch))
			{
				hasText = true;
				continue;
			}
			if (char.IsPunctuation(ch) || char.IsSymbol(ch))
				continue;
			hasText = true;
		}

		return hasText;
	}

	private static bool TryParseChoiceInteger(string value, out long parsed)
	{
		parsed = 0;
		if (string.IsNullOrWhiteSpace(value))
			return false;

		var text = value.Trim();
		if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
			return true;

		var sign = 1L;
		if (text.StartsWith('+'))
		{
			text = text[1..];
		}
		else if (text.StartsWith('-'))
		{
			sign = -1L;
			text = text[1..];
		}

		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			var hex = text[2..];
			if (!long.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hexValue))
				return false;
			parsed = unchecked(sign * hexValue);
			return true;
		}

		if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
		{
			var bits = text[2..];
			if (bits.Length == 0)
				return false;
			long valueBits = 0;
			foreach (var ch in bits)
			{
				if (ch != '0' && ch != '1')
					return false;
				valueBits = (valueBits << 1) + (ch - '0');
			}
			parsed = unchecked(sign * valueBits);
			return true;
		}

		return false;
	}

	private static bool IsEscapeKey(ConsoleKeyInfo keyInfo)
	{
		return keyInfo.Key == ConsoleKey.Escape || keyInfo.KeyChar == '\x1b';
	}

	private static bool IsBackspaceKey(ConsoleKeyInfo keyInfo)
	{
		return keyInfo.Key == ConsoleKey.Backspace || keyInfo.KeyChar == '\b' || keyInfo.KeyChar == '\x7f';
	}

	private static bool IsCtrlC(ConsoleKeyInfo keyInfo)
	{
		return keyInfo.Key == ConsoleKey.C &&
			(keyInfo.Modifiers & ConsoleModifiers.Control) != 0;
	}

	private static string FormatAnyKey(ConsoleKeyInfo keyInfo)
	{
		if (!char.IsControl(keyInfo.KeyChar) && keyInfo.KeyChar != '\0')
			return keyInfo.KeyChar.ToString();
		return keyInfo.Key.ToString();
	}

	private static string StripAnsi(string text)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;
		return AnsiEscapeRegex.Replace(text, string.Empty);
	}

	private sealed class TerminalSession(string sttyPath, string sttyState) : IDisposable
	{
		private bool disposed;

		public static bool TryEnter(out TerminalSession? session)
		{
			session = null;
			var stty = FindExecutableInPath("stty");
			if (string.IsNullOrWhiteSpace(stty))
				return false;
			if (!TryRunStty(stty, "-g", out var state))
				return false;

			var savedState = state.Trim();
			if (string.IsNullOrWhiteSpace(savedState))
				return false;
			if (!TryRunStty(stty, "-icanon -echo min 0 time 0", out _))
				return false;

			EnableMouseReporting();
			session = new TerminalSession(stty, savedState);
			return true;
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			DisableMouseReporting();
			TryRunStty(sttyPath, sttyState, out _);
		}

		private static void EnableMouseReporting()
		{
			try
			{
				Console.Out.Write("\u001b[?1000h\u001b[?1006h");
				Console.Out.Flush();
			}
			catch
			{
				// Best effort.
			}
		}

		private static void DisableMouseReporting()
		{
			try
			{
				Console.Out.Write("\u001b[?1006l\u001b[?1000l");
				Console.Out.Flush();
			}
			catch
			{
				// Best effort.
			}
		}

		private static bool TryRunStty(string fileName, string arguments, out string output)
		{
			output = string.Empty;
			try
			{
				using var process = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = fileName,
						Arguments = arguments,
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
					}
				};

				process.Start();
				output = process.StandardOutput.ReadToEnd();
				process.StandardError.ReadToEnd();
				process.WaitForExit();
				return process.ExitCode == 0;
			}
			catch
			{
				return false;
			}
		}

		private static string? FindExecutableInPath(string name)
		{
			var path = Environment.GetEnvironmentVariable("PATH");
			if (string.IsNullOrWhiteSpace(path))
				return null;

			foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				var candidate = Path.Combine(dir, name);
				if (File.Exists(candidate))
					return candidate;
			}
			return null;
		}
	}
}
