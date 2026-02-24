using MinorShift.Emuera.Runtime.Utils;
using System.Diagnostics;

namespace MinorShift.Emuera.RuntimeCore.Bootstrap;

public static class RuntimePreloadBootstrap
{
	static readonly HashSet<string> ErbPreprocessorTokens = new(StringComparer.Ordinal)
	{
		"SKIPSTART", "SKIPEND", "IF", "IF_DEBUG", "IF_NDEBUG", "ELSEIF", "ELSE", "ENDIF"
	};

	public static async Task<RuntimePreloadResult> PreloadAsync(string csvDir, string erbDir)
	{
		if (string.IsNullOrWhiteSpace(csvDir))
			throw new ArgumentException("CSV directory is required.", nameof(csvDir));
		if (string.IsNullOrWhiteSpace(erbDir))
			throw new ArgumentException("ERB directory is required.", nameof(erbDir));

		var candidateFileCount = CountTargetFiles(csvDir) + CountTargetFiles(erbDir);
		var stopWatch = Stopwatch.StartNew();
		var warningCount = 0;
		var warningSamples = new List<string>();
		var previousWarnHook = Preload.WarnHook;
		Preload.WarnHook = (message, path) =>
		{
			warningCount++;
			if (warningSamples.Count < 20)
			{
				var sourcePath = string.IsNullOrWhiteSpace(path) ? "(unknown)" : path;
				warningSamples.Add($"{sourcePath}: {message}");
			}
		};
		Preload.Clear();
		try
		{
			await Preload.Load(erbDir);
			await Preload.Load(csvDir);
			stopWatch.Stop();
		}
		finally
		{
			Preload.WarnHook = previousWarnHook;
		}

		var cachedFileCount = Preload.CachedFileCount;
		var skippedFileCount = Math.Max(0, candidateFileCount - cachedFileCount);
		var extensionStats = BuildExtensionStats(csvDir, erbDir, Preload.GetCachedFilePathsSnapshot());
		return new RuntimePreloadResult(candidateFileCount, cachedFileCount, skippedFileCount, warningCount, stopWatch.ElapsedMilliseconds, extensionStats, warningSamples);
	}

	static int CountTargetFiles(string directoryPath)
	{
		if (!Directory.Exists(directoryPath))
			return 0;

		return Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
			.Count(path => IsTargetExtension(Path.GetExtension(path)));
	}

	static bool IsTargetExtension(string extension)
	{
		return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
			extension.Equals(".erb", StringComparison.OrdinalIgnoreCase) ||
			extension.Equals(".erh", StringComparison.OrdinalIgnoreCase) ||
			extension.Equals(".erd", StringComparison.OrdinalIgnoreCase) ||
			extension.Equals(".als", StringComparison.OrdinalIgnoreCase);
	}

	static IReadOnlyList<RuntimePreloadExtensionStat> BuildExtensionStats(string csvDir, string erbDir, IEnumerable<string> cachedFiles)
	{
		var candidate = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (var path in Directory.EnumerateFiles(csvDir, "*", SearchOption.AllDirectories)
			.Concat(Directory.EnumerateFiles(erbDir, "*", SearchOption.AllDirectories)))
		{
			var ext = NormalizeExtension(Path.GetExtension(path));
			if (!IsTargetExtension(ext))
				continue;
			candidate[ext] = candidate.TryGetValue(ext, out var count) ? count + 1 : 1;
		}

		var cached = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (var path in cachedFiles)
		{
			var ext = NormalizeExtension(Path.GetExtension(path));
			if (!IsTargetExtension(ext))
				continue;
			cached[ext] = cached.TryGetValue(ext, out var count) ? count + 1 : 1;
		}

		var extensions = candidate.Keys
			.Concat(cached.Keys)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(ext => ext, StringComparer.OrdinalIgnoreCase);

		var result = new List<RuntimePreloadExtensionStat>();
		foreach (var ext in extensions)
		{
			var targetCount = candidate.TryGetValue(ext, out var target) ? target : 0;
			var cachedCount = cached.TryGetValue(ext, out var loaded) ? loaded : 0;
			result.Add(new RuntimePreloadExtensionStat(ext, targetCount, cachedCount, Math.Max(0, targetCount - cachedCount)));
		}

		return result;
	}

	static string NormalizeExtension(string ext)
	{
		if (string.IsNullOrWhiteSpace(ext))
			return string.Empty;
		return ext.ToLowerInvariant();
	}

	public static ErhDirectiveScanResult ScanErhDirectivePrefixes()
	{
		var cachedFiles = Preload.GetCachedFilePathsSnapshot();
		var erhFiles = cachedFiles
			.Where(path => string.Equals(Path.GetExtension(path), ".erh", StringComparison.OrdinalIgnoreCase))
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var issues = new List<ErhDirectiveIssue>();
		foreach (var path in erhFiles)
		{
			if (!Preload.TryGetFileLines(path, out var lines) || lines == null)
			{
				issues.Add(new ErhDirectiveIssue(path, 0, "Missing cached lines for ERH file."));
				continue;
			}

			var inBraceBlock = false;
			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				if (string.IsNullOrWhiteSpace(line))
					continue;
				var trimmed = line.TrimStart();
				if (trimmed.StartsWith(';'))
					continue;
				if (trimmed == "{")
				{
					inBraceBlock = true;
					continue;
				}
				if (trimmed == "}")
				{
					inBraceBlock = false;
					continue;
				}
				if (inBraceBlock)
					continue;
				if (trimmed[0] == '#')
					continue;
				issues.Add(new ErhDirectiveIssue(path, i, "Non-comment ERH line does not start with '#'."));
				break;
			}
		}

		return new ErhDirectiveScanResult(erhFiles.Length, issues);
	}

	public static ErbPreprocessorScanResult ScanErbPreprocessorBlocks()
	{
		var cachedFiles = Preload.GetCachedFilePathsSnapshot();
		var erbFiles = cachedFiles
			.Where(path => string.Equals(Path.GetExtension(path), ".erb", StringComparison.OrdinalIgnoreCase))
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var issues = new List<ErbPreprocessorIssue>();
		var directiveCount = 0;

		foreach (var path in erbFiles)
		{
			if (!Preload.TryGetFileLines(path, out var lines) || lines == null)
			{
				issues.Add(new ErbPreprocessorIssue(path, 0, "Missing cached lines for ERB file."));
				continue;
			}

			var stack = new Stack<string>();
			for (var i = 0; i < lines.Length; i++)
			{
				if (!TryReadPreprocessorToken(lines[i], out var token))
					continue;

				directiveCount++;
				switch (token)
				{
					case "SKIPSTART":
						stack.Push("SKIPEND");
						break;
					case "SKIPEND":
						if (stack.Count == 0 || stack.Pop() != "SKIPEND")
							issues.Add(new ErbPreprocessorIssue(path, i, "Unexpected [SKIPEND]"));
						break;
					case "IF":
					case "IF_DEBUG":
					case "IF_NDEBUG":
						stack.Push("ELSEIF");
						break;
					case "ELSEIF":
						if (stack.Count == 0 || stack.Pop() != "ELSEIF")
						{
							issues.Add(new ErbPreprocessorIssue(path, i, "Unexpected [ELSEIF]"));
							break;
						}
						stack.Push("ELSEIF");
						break;
					case "ELSE":
						if (stack.Count == 0 || stack.Pop() != "ELSEIF")
						{
							issues.Add(new ErbPreprocessorIssue(path, i, "Unexpected [ELSE]"));
							break;
						}
						stack.Push("ENDIF");
						break;
					case "ENDIF":
						if (stack.Count == 0)
						{
							issues.Add(new ErbPreprocessorIssue(path, i, "Unexpected [ENDIF]"));
							break;
						}
						var expected = stack.Pop();
						if (expected != "ENDIF" && expected != "ELSEIF")
							issues.Add(new ErbPreprocessorIssue(path, i, "Unexpected [ENDIF]"));
						break;
				}
			}

			while (stack.Count > 0)
			{
				var expected = stack.Pop();
				if (expected == "ELSEIF")
					expected = "ENDIF";
				issues.Add(new ErbPreprocessorIssue(path, lines.Length, $"Missing [{expected}]"));
			}
		}

		return new ErbPreprocessorScanResult(erbFiles.Length, directiveCount, issues);
	}

	public static ErbContinuationScanResult ScanErbContinuationBlocks()
	{
		var cachedFiles = Preload.GetCachedFilePathsSnapshot();
		var erbFiles = cachedFiles
			.Where(path => string.Equals(Path.GetExtension(path), ".erb", StringComparison.OrdinalIgnoreCase))
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var issues = new List<ErbContinuationIssue>();
		var blockCount = 0;

		foreach (var path in erbFiles)
		{
			if (!Preload.TryGetFileLines(path, out var lines) || lines == null)
			{
				issues.Add(new ErbContinuationIssue(path, 0, "Missing cached lines for ERB file."));
				continue;
			}

			var inBlock = false;
			var blockStartLine = -1;

			for (var i = 0; i < lines.Length; i++)
			{
				var raw = lines[i];
				if (raw == null)
					continue;

				var trimmed = raw.Trim();
				if (!inBlock)
				{
					if (trimmed == "{")
					{
						inBlock = true;
						blockStartLine = i;
						blockCount++;
					}
					else if (trimmed.StartsWith('}') && trimmed != "}")
					{
						issues.Add(new ErbContinuationIssue(path, i, "Unexpected continuation terminator content."));
					}
					else if (trimmed == "}")
					{
						issues.Add(new ErbContinuationIssue(path, i, "Unexpected continuation end without matching start."));
					}
				}
				else
				{
					if (trimmed == "{")
					{
						issues.Add(new ErbContinuationIssue(path, i, "Nested continuation start is not allowed."));
					}
					if (trimmed.StartsWith('}'))
					{
						if (trimmed != "}")
							issues.Add(new ErbContinuationIssue(path, i, "Characters after continuation end are not allowed."));
						inBlock = false;
						blockStartLine = -1;
					}
				}
			}

			if (inBlock)
			{
				issues.Add(new ErbContinuationIssue(path, blockStartLine, "Continuation block is not closed."));
			}
		}

		return new ErbContinuationScanResult(erbFiles.Length, blockCount, issues);
	}

	public static async Task<RuntimeStaticGateResult> RunStaticGateAsync(string csvDir, string erbDir)
	{
		var preload = await PreloadAsync(csvDir, erbDir);
		var erh = ScanErhDirectivePrefixes();
		var erbPp = ScanErbPreprocessorBlocks();
		var erbCont = ScanErbContinuationBlocks();
		var entry = ScanErbEntryLabels();
		var bootProfile = EvaluateBootProfile(entry.FoundEntryLikeLabels);
		return new RuntimeStaticGateResult(preload, erh, erbPp, erbCont, entry, bootProfile);
	}

	public static ErbEntryLabelScanResult ScanErbEntryLabels()
	{
		var cachedFiles = Preload.GetCachedFilePathsSnapshot();
		var erbFiles = cachedFiles
			.Where(path => string.Equals(Path.GetExtension(path), ".erb", StringComparison.OrdinalIgnoreCase))
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var issues = new List<ErbEntryLabelIssue>();
		var labelCount = 0;
		var entryLikeLabelCount = 0;
		var foundEntryLikeLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var path in erbFiles)
		{
			if (!Preload.TryGetFileLines(path, out var lines) || lines == null)
			{
				issues.Add(new ErbEntryLabelIssue(path, 0, "Missing cached lines for ERB file."));
				continue;
			}

			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				if (string.IsNullOrWhiteSpace(line))
					continue;

				var trimmed = line.TrimStart();
				if (trimmed.StartsWith(';'))
					continue;
				if (!trimmed.StartsWith('@'))
					continue;

				labelCount++;
				var label = trimmed[1..].Trim();
				if (TryNormalizeEntryLikeLabel(label, out var normalized))
				{
					entryLikeLabelCount++;
					foundEntryLikeLabels.Add(normalized);
				}
			}
		}

		if (labelCount == 0)
		{
			issues.Add(new ErbEntryLabelIssue("(all ERB files)", 0, "No entry labels (@...) were found."));
		}
		else if (entryLikeLabelCount == 0)
		{
			issues.Add(new ErbEntryLabelIssue("(all ERB files)", 0, "No entry-like labels were found (@TITLE/@SYSTEM_TITLE/@EVENTFIRST/@EVENTLOAD)."));
		}

		return new ErbEntryLabelScanResult(erbFiles.Length, labelCount, entryLikeLabelCount, foundEntryLikeLabels.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(), issues);
	}

	static bool TryReadPreprocessorToken(string rawLine, out string token)
	{
		token = null;
		if (string.IsNullOrWhiteSpace(rawLine))
			return false;

		var line = rawLine.TrimStart();
		if (line.Length < 3 || line[0] != '[' || line[1] == '[')
			return false;

		var close = line.IndexOf(']');
		if (close <= 1)
			return false;

		var head = line.Substring(1, close - 1).Trim();
		if (head.Length == 0)
			return false;

		var separator = head.IndexOfAny([' ', '\t']);
		token = (separator < 0 ? head : head[..separator]).ToUpperInvariant();
		return ErbPreprocessorTokens.Contains(token);
	}

	static bool TryNormalizeEntryLikeLabel(string rawLabel, out string normalizedLabel)
	{
		normalizedLabel = string.Empty;
		if (string.IsNullOrWhiteSpace(rawLabel))
			return false;

		var label = rawLabel.Trim().ToUpperInvariant();
		var paren = label.IndexOf('(');
		if (paren > 0)
			label = label[..paren].Trim();

		if (label is "TITLE" or "SYSTEM_TITLE" or "EVENTFIRST" or "EVENTLOAD" or "START" or "SYSTEM_START")
		{
			normalizedLabel = label;
			return true;
		}
		return false;
	}

	static BootProfileResult EvaluateBootProfile(IReadOnlyList<string> labels)
	{
		var set = new HashSet<string>(labels, StringComparer.OrdinalIgnoreCase);
		var hasSystemTitle = set.Contains("SYSTEM_TITLE") || set.Contains("TITLE");
		var hasEventFirst = set.Contains("EVENTFIRST");
		var hasEventLoad = set.Contains("EVENTLOAD");

		var missing = new List<string>();
		if (!hasSystemTitle)
			missing.Add("SYSTEM_TITLE/TITLE");
		if (!hasEventFirst)
			missing.Add("EVENTFIRST");
		if (!hasEventLoad)
			missing.Add("EVENTLOAD");

		var isValid = missing.Count == 0;
		var detail = isValid ? "OK" : $"Missing {string.Join(", ", missing)}";
		return new BootProfileResult(isValid, detail, missing);
	}
}

public sealed record RuntimePreloadResult(
	int CandidateFileCount,
	int CachedFileCount,
	int SkippedFileCount,
	int WarningCount,
	long ElapsedMilliseconds,
	IReadOnlyList<RuntimePreloadExtensionStat> ExtensionStats,
	IReadOnlyList<string> WarningSamples);

public sealed record RuntimePreloadExtensionStat(
	string Extension,
	int CandidateCount,
	int CachedCount,
	int SkippedCount);

public sealed record ErhDirectiveScanResult(
	int ErhFileCount,
	IReadOnlyList<ErhDirectiveIssue> Issues)
{
	public int IssueCount => Issues.Count;
}

public sealed record ErhDirectiveIssue(
	string FilePath,
	int LineNo,
	string Message);

public sealed record ErbPreprocessorScanResult(
	int ErbFileCount,
	int DirectiveCount,
	IReadOnlyList<ErbPreprocessorIssue> Issues)
{
	public int IssueCount => Issues.Count;
}

public sealed record ErbPreprocessorIssue(
	string FilePath,
	int LineNo,
	string Message);

public sealed record ErbContinuationScanResult(
	int ErbFileCount,
	int BlockCount,
	IReadOnlyList<ErbContinuationIssue> Issues)
{
	public int IssueCount => Issues.Count;
}

public sealed record ErbContinuationIssue(
	string FilePath,
	int LineNo,
	string Message);

public sealed record ErbEntryLabelScanResult(
	int ErbFileCount,
	int LabelCount,
	int EntryLikeLabelCount,
	IReadOnlyList<string> FoundEntryLikeLabels,
	IReadOnlyList<ErbEntryLabelIssue> Issues)
{
	public int IssueCount => Issues.Count;
}

public sealed record ErbEntryLabelIssue(
	string FilePath,
	int LineNo,
	string Message);

public sealed record BootProfileResult(
	bool IsValid,
	string Detail,
	IReadOnlyList<string> MissingEntries);

public sealed record RuntimeStaticGateResult(
	RuntimePreloadResult Preload,
	ErhDirectiveScanResult Erh,
	ErbPreprocessorScanResult ErbPreprocessor,
	ErbContinuationScanResult ErbContinuation,
	ErbEntryLabelScanResult ErbEntryLabels,
	BootProfileResult BootProfile)
{
	public int TotalIssueCount => Erh.IssueCount + ErbPreprocessor.IssueCount + ErbContinuation.IssueCount + ErbEntryLabels.IssueCount;
	public bool Passed => TotalIssueCount == 0;
}
