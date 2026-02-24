//新設したコンフィグ設定のロード、セーブ、公開を担当する。
using MinorShift.Emuera.Runtime.Utils;
using System.IO;
using System.Text.Json;


namespace MinorShift.Emuera.Runtime.Config.JSON;
static class JSONConfig
{
	public static JSONConfigData Data;

	const string _configFileName = "setting.json";
	static string ResolveConfigPath() => RuntimeFileSearch.ResolveFilePath(Path.Combine(RuntimeEnvironment.ExeDir, _configFileName));

	public static void Load()
	{
		var configFilePath = ResolveConfigPath();
		if (!File.Exists(configFilePath))
		{
			var defaultData = new JSONConfigData();
			var defaultJson = JsonSerializer.Serialize(defaultData);
			File.WriteAllText(configFilePath, defaultJson);
		}

		var json = File.ReadAllText(configFilePath);

		Data = JsonSerializer.Deserialize<JSONConfigData>(json);
	}

	public static void Save()
	{
		var configFilePath = ResolveConfigPath();
		var json = JsonSerializer.Serialize(Data);
		File.WriteAllText(configFilePath, json);
	}
}
