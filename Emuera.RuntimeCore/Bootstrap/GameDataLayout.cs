using MinorShift.Emuera.Runtime.Utils;

namespace MinorShift.Emuera.RuntimeCore.Bootstrap;

public static class GameDataLayout
{
	public static GameDataValidationResult Validate(string rootDir)
	{
		var csvDir = RuntimeFileSearch.ResolveDirectoryPath(Path.Combine(rootDir, "CSV"));
		var erbDir = RuntimeFileSearch.ResolveDirectoryPath(Path.Combine(rootDir, "ERB"));
		var missing = new List<string>();

		if (!Directory.Exists(csvDir))
			missing.Add(csvDir);
		if (!Directory.Exists(erbDir))
			missing.Add(erbDir);

		return new GameDataValidationResult(rootDir, csvDir, erbDir, missing);
	}

	public static RuntimePrerequisiteResult CheckRuntimePrerequisites(string rootDir, string csvDir, string erbDir)
	{
		var gamebaseCsvPath = RuntimeFileSearch.ResolveFilePath(Path.Combine(csvDir, "GAMEBASE.CSV"));
		var emueraConfigPath = RuntimeFileSearch.ResolveFilePath(Path.Combine(rootDir, "emuera.config"));
		var titleErbPath = RuntimeFileSearch.ResolveFilePath(Path.Combine(erbDir, "TITLE.ERB"));
		var systemErbPath = RuntimeFileSearch.ResolveFilePath(Path.Combine(erbDir, "SYSTEM.ERB"));
		var requiredMissing = new List<string>();
		var recommendedMissing = new List<string>();

		if (!File.Exists(gamebaseCsvPath))
			requiredMissing.Add(gamebaseCsvPath);
		if (!File.Exists(emueraConfigPath))
			recommendedMissing.Add(emueraConfigPath);
		if (!File.Exists(titleErbPath))
			recommendedMissing.Add(titleErbPath);
		if (!File.Exists(systemErbPath))
			recommendedMissing.Add(systemErbPath);

		return new RuntimePrerequisiteResult(
			rootDir,
			gamebaseCsvPath,
			emueraConfigPath,
			titleErbPath,
			systemErbPath,
			requiredMissing,
			recommendedMissing);
	}
}

public sealed record GameDataValidationResult(
	string RootDir,
	string CsvDir,
	string ErbDir,
	IReadOnlyList<string> MissingPaths)
{
	public bool IsValid => MissingPaths.Count == 0;
}

public sealed record RuntimePrerequisiteResult(
	string RootDir,
	string GamebaseCsvPath,
	string EmueraConfigPath,
	string TitleErbPath,
	string SystemErbPath,
	IReadOnlyList<string> MissingRequiredPaths,
	IReadOnlyList<string> MissingRecommendedPaths)
{
	public bool HasRequiredMissing => MissingRequiredPaths.Count > 0;
}
