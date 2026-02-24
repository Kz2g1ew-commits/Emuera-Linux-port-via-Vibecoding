using MinorShift.Emuera.Runtime.Utils;
using System.Text;

namespace MinorShift.Emuera.RuntimeCore.Bootstrap;

public static class GameBaseInspector
{
	public static GameBaseInspectionResult Inspect(string gamebaseCsvPath)
	{
		if (string.IsNullOrWhiteSpace(gamebaseCsvPath))
			throw new ArgumentException("GAMEBASE.CSV path is required.", nameof(gamebaseCsvPath));
		gamebaseCsvPath = RuntimeFileSearch.ResolveFilePath(gamebaseCsvPath);
		if (!File.Exists(gamebaseCsvPath))
			return new GameBaseInspectionResult(gamebaseCsvPath, false, null, null, null, 0, ["GAMEBASE.CSV is missing."]);

		var warnings = new List<string>();
		var keyCount = 0;
		string title = null;
		string author = null;
		string versionRaw = null;
		try
		{
			var lines = File.ReadAllLines(gamebaseCsvPath, EncodingHandler.DetectEncoding(gamebaseCsvPath));
			foreach (var raw in lines)
			{
				if (string.IsNullOrWhiteSpace(raw))
					continue;

				var line = raw.TrimStart();
				if (line.StartsWith(';'))
					continue;

				var comma = line.IndexOf(',');
				if (comma <= 0 || comma >= line.Length - 1)
					continue;

				var key = line[..comma].Trim();
				var value = line[(comma + 1)..].Trim();
				if (key.Length == 0)
					continue;
				keyCount++;

				switch (key)
				{
					case "タイトル":
						title = value;
						break;
					case "作者":
						author = value;
						break;
					case "バージョン":
						versionRaw = value;
						break;
				}
			}
		}
		catch (Exception ex)
		{
			warnings.Add($"Failed to inspect GAMEBASE.CSV: {ex.Message}");
			return new GameBaseInspectionResult(gamebaseCsvPath, false, title, author, versionRaw, keyCount, warnings);
		}

		if (keyCount == 0)
			warnings.Add("No GAMEBASE key/value pairs were parsed.");
		if (string.IsNullOrWhiteSpace(title))
			warnings.Add("GAMEBASE title (タイトル) is missing.");

		return new GameBaseInspectionResult(gamebaseCsvPath, true, title, author, versionRaw, keyCount, warnings);
	}
}

public sealed record GameBaseInspectionResult(
	string Path,
	bool ReadSucceeded,
	string Title,
	string Author,
	string VersionRaw,
	int ParsedKeyCount,
	IReadOnlyList<string> Warnings);
