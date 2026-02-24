using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MinorShift.Emuera.Runtime;

public static class RuntimeEnvironment
{
	static List<string> analysisFiles = [];

	public static string ExeDir { get; private set; } = EnsureTrailingSeparator(AppContext.BaseDirectory);
	public static string CsvDir { get; private set; } = ResolveSubdir(EnsureTrailingSeparator(AppContext.BaseDirectory), "csv", "CSV", "Csv");
	public static string ErbDir { get; private set; } = ResolveSubdir(EnsureTrailingSeparator(AppContext.BaseDirectory), "erb", "ERB", "Erb");
	public static string DebugDir { get; private set; } = ResolveSubdir(EnsureTrailingSeparator(AppContext.BaseDirectory), "debug", "DEBUG", "Debug");
	public static string DatDir { get; private set; } = ResolveSubdir(EnsureTrailingSeparator(AppContext.BaseDirectory), "dat", "DAT", "Dat");
	public static string ContentDir { get; private set; } = ResolveSubdir(EnsureTrailingSeparator(AppContext.BaseDirectory), "resources", "Resources", "RESOURCES");
	public static string SoundDir { get; private set; } = ResolveSubdir(EnsureTrailingSeparator(AppContext.BaseDirectory), "sound", "Sound", "SOUND");
	public static string FontDir { get; private set; } = ResolveSubdir(EnsureTrailingSeparator(AppContext.BaseDirectory), "font", "Font", "FONT");

	public static bool DebugMode { get; private set; }
	public static bool AnalysisMode { get; private set; }
	public static IReadOnlyList<string> AnalysisFiles => analysisFiles;
	public static bool RebootRequested { get; set; }

	public static void SetPaths(string exeDir)
	{
		ExeDir = EnsureTrailingSeparator(exeDir);
		CsvDir = ResolveSubdir(ExeDir, "csv", "CSV", "Csv");
		ErbDir = ResolveSubdir(ExeDir, "erb", "ERB", "Erb");
		DebugDir = ResolveSubdir(ExeDir, "debug", "DEBUG", "Debug");
		DatDir = ResolveSubdir(ExeDir, "dat", "DAT", "Dat");
		ContentDir = ResolveSubdir(ExeDir, "resources", "Resources", "RESOURCES");
		SoundDir = ResolveSubdir(ExeDir, "sound", "Sound", "SOUND");
		FontDir = ResolveSubdir(ExeDir, "font", "Font", "FONT");
	}

	public static void SetModes(bool debugMode, bool analysisMode)
	{
		DebugMode = debugMode;
		AnalysisMode = analysisMode;
	}

	public static void SetAnalysisFiles(IEnumerable<string> files)
	{
		analysisFiles = files?.Where(static path => !string.IsNullOrWhiteSpace(path)).ToList() ?? [];
	}

	public static string ResolveSoundPath(string soundPath)
	{
		if (string.IsNullOrWhiteSpace(soundPath))
			return Path.GetFullPath(SoundDir);

		var normalized = soundPath
			.Replace('\\', Path.DirectorySeparatorChar)
			.Replace('/', Path.DirectorySeparatorChar);
		return Path.GetFullPath(Path.Combine(SoundDir, normalized));
	}

	static string EnsureTrailingSeparator(string path)
	{
		var fullPath = Path.GetFullPath(new DirectoryInfo(path).FullName);
		if (!fullPath.EndsWith(Path.DirectorySeparatorChar))
			return fullPath + Path.DirectorySeparatorChar;
		return fullPath;
	}

	static string ResolveSubdir(string baseDir, params string[] candidates)
	{
		foreach (var candidate in candidates)
		{
			var fullPath = Path.Combine(baseDir, candidate);
			if (Directory.Exists(fullPath))
				return EnsureTrailingSeparator(fullPath);
		}
		return EnsureTrailingSeparator(Path.Combine(baseDir, candidates[0]));
	}
}
