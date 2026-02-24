using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Config.JSON;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift.Emuera.UI.Game;
using MinorShift.Emuera.UI.Game.Image;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.RuntimeEngine;

public static class RuntimeEngineBootstrap
{
	public static void InitializeForCli(string gameRoot)
	{
		if (string.IsNullOrWhiteSpace(gameRoot))
			throw new ArgumentException("Game root is required.", nameof(gameRoot));

		RuntimeEnvironment.SetPaths(gameRoot);
		RuntimeEnvironment.SetModes(debugMode: false, analysisMode: false);
		RuntimeEnvironment.SetAnalysisFiles([]);

		ConfigData.Instance.LoadConfig();
		JSONConfig.Load();
		Lang.LoadLanguageFiles();
		Lang.SetLanguage();

		WireRuntimeHostHooks();
	}

	public static void WireRuntimeHostHooks()
	{
		RuntimeHost.ProductVersionHook = static () => AssemblyData.EmueraVersionText;
		RuntimeHost.IsFontInstalledHook = static (fontName) => UiPlatformBridge.IsFontInstalled(fontName);
		RuntimeHost.LoadContentsHook = static (reload) => AppContents.LoadContents(reload);
		RuntimeHost.UnloadTempLoadedConstImagesHook = static () => AppContents.UnloadTempLoadedConstImageNames();
		RuntimeHost.UnloadTempLoadedGraphicsImagesHook = static () => AppContents.UnloadTempLoadedGraphicsImageNames();
		RuntimeHost.LoadImageHook = static (filePath) => ImgUtils.LoadImage(filePath);
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
			"save.invalid" => trerror.InvalidSaveDataFormat.Text,
			"save.invalid_num" => trerror.CanNotInterpretNumValue.Text,
			"save.not_impl" => trerror.NotImplement.Text,
			"preload.abnormal_encode" => trerror.AbnormalEncode.Text,
			"preload.file_locked" => trerror.FileUsingOtherProcess.Text,
			_ => null
		};
	}
}
