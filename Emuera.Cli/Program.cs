using System.CommandLine;
using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.RuntimeCore.Bootstrap;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.RuntimeEngine;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

var runtimeTitle = string.Empty;
CliConsoleEncoding.TryConfigure();

RuntimeHost.DoEventsHook = static () => { };
RuntimeHost.ShowInfoHook = static (message) => Console.Error.WriteLine(message);
RuntimeHost.ShowInfoWithCaptionHook = static (message, caption) =>
{
    if (string.IsNullOrWhiteSpace(caption))
        Console.Error.WriteLine(message);
    else
        Console.Error.WriteLine($"[{caption}] {message}");
};
RuntimeHost.ConfirmYesNoHook = static (message, caption) =>
{
    if (!Console.IsInputRedirected)
    {
        if (!string.IsNullOrWhiteSpace(caption))
            Console.Error.WriteLine($"[{caption}] {message}");
        else
            Console.Error.WriteLine(message);

        Console.Error.Write("Proceed? [y/N]: ");
        var input = Console.ReadLine();
        return string.Equals(input?.Trim(), "y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(input?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }

    return false;
};
RuntimeHost.SetClipboardTextHook = static (_) => { };
RuntimeHost.SetWindowTitleHook = (title) =>
{
    runtimeTitle = title ?? string.Empty;
};
RuntimeHost.GetWindowTitleHook = () => runtimeTitle;
RuntimeHost.GetConsoleLineNoHook = static () => 0;
RuntimeHost.GetConsoleClientHeightHook = static () => 0;
RuntimeHost.ResolveMessageHook = static (_) => null;
RuntimeHost.ResolveDrawLineStringHook = static (fallback) => fallback;
RuntimeHost.FormatDrawLineStringHook = static (raw) => raw;
RuntimeHost.InitializeDrawLineStringHook = static (_) => { };
RuntimeHost.HtmlTagSplitHook = static (raw) => CliHtmlFallback.HtmlTagSplit(raw);
RuntimeHost.HtmlLengthHook = static (raw) => CliHtmlFallback.HtmlLength(raw);
RuntimeHost.HtmlSubStringHook = static (raw, width) => CliHtmlFallback.HtmlSubString(raw, width);
RuntimeHost.HtmlToPlainTextHook = static (raw) => CliHtmlFallback.HtmlToPlainText(raw);
RuntimeHost.HtmlEscapeHook = static (raw) => CliHtmlFallback.Escape(raw);
RuntimeHost.PlayErrorToneHook = static () => { };
RuntimeHost.PlayNotifyToneHook = static () => { };
RuntimeHost.ResolveNamedColorRgbHook = static (colorName) =>
{
    if (CliColorNames.TryResolveRgb24(colorName, out var rgb))
        return rgb;
    return null;
};
RuntimeHost.IsFontInstalledHook = static (_) => false;
RuntimeHost.GetDisplayLineTextHook = static (_) => string.Empty;
RuntimeHost.GetDisplayLineHtmlHook = static (_) => string.Empty;
RuntimeHost.PopDisplayLineHtmlHook = static () => string.Empty;
RuntimeHost.GetPointingButtonInputHook = static () => string.Empty;
RuntimeHost.CbgClearHook = static () => { };
RuntimeHost.CbgClearRangeHook = static (_, _) => { };
RuntimeHost.CbgClearButtonHook = static () => { };
RuntimeHost.CbgClearButtonMapHook = static () => { };
RuntimeHost.CbgSetGraphicsHook = static (_, _, _, _) => false;
RuntimeHost.CbgSetButtonMapHook = static (_) => false;
RuntimeHost.CbgSetImageHook = static (_, _, _, _) => false;
RuntimeHost.CbgSetButtonImageHook = static (_, _, _, _, _, _, _) => false;
RuntimeHost.ApplyTextBoxChangesHook = static () => { };
RuntimeHost.AddBackgroundImageHook = static (_, _, _) => { };
RuntimeHost.RemoveBackgroundImageHook = static (_) => { };
RuntimeHost.ClearBackgroundImageHook = static () => { };
RuntimeHost.SetToolTipColorRgbHook = static (_, _) => { };
RuntimeHost.SetToolTipDelayHook = static (_) => { };
RuntimeHost.SetToolTipDurationHook = static (_) => { };
RuntimeHost.SetToolTipFontNameHook = static (_) => { };
RuntimeHost.SetToolTipFontSizeHook = static (_) => { };
RuntimeHost.SetCustomToolTipHook = static (_) => { };
RuntimeHost.SetToolTipFormatHook = static (_) => { };
RuntimeHost.SetToolTipImageEnabledHook = static (_) => { };
RuntimeHost.IsCtrlZEnabledHook = static () => false;
RuntimeHost.CaptureRandomSeedHook = static (_) => { };
RuntimeHost.CtrlZAddInputHook = static (_) => { };
RuntimeHost.CtrlZOnSavePrepareHook = static (_) => { };
RuntimeHost.CtrlZOnSaveHook = static () => { };
RuntimeHost.CtrlZOnLoadHook = static (_) => { };
RuntimeHost.PrintHtmlHook = static (_, _) => { };
RuntimeHost.PrintHtmlIslandHook = static (_) => { };
RuntimeHost.ClearHtmlIslandHook = static () => { };
RuntimeHost.PrintImageHook = static (_, _, _, _, _, _) => { };
RuntimeHost.PrintShapeHook = static (_, _) => { };
RuntimeHost.SetUseUserStyleHook = static (_) => { };
RuntimeHost.SetUseSetColorStyleHook = static (_) => { };
RuntimeHost.SetBackgroundColorRgbHook = static (_) => { };
RuntimeHost.GetBackgroundColorRgbHook = static () => 0;
RuntimeHost.SetStringColorRgbHook = static (_) => { };
RuntimeHost.GetStringColorRgbHook = static () => 0;
RuntimeHost.SetStringStyleFlagsHook = static (_) => { };
RuntimeHost.GetStringStyleFlagsHook = static () => RuntimeFontStyleFlags.Regular;
RuntimeHost.SetFontHook = static (_) => { };
RuntimeHost.GetFontNameHook = static () => string.Empty;
RuntimeHost.SetAlignmentHook = static (_) => { };
RuntimeHost.GetAlignmentHook = static () => RuntimeDisplayLineAlignment.LEFT;
RuntimeHost.SetRedrawHook = static (_) => { };
RuntimeHost.GetRedrawModeHook = static () => RuntimeRedrawMode.None;
RuntimeHost.PrintButtonStringHook = static (_, _) => { };
RuntimeHost.PrintButtonLongHook = static (_, _) => { };
RuntimeHost.PrintButtonCStringHook = static (_, _, _) => { };
RuntimeHost.PrintButtonCLongHook = static (_, _, _) => { };
RuntimeHost.PrintPlainSingleLineHook = static (_) => { };
RuntimeHost.PrintErrorButtonHook = static (_, _, _) => { };
RuntimeHost.PrintPlainHook = static (_) => { };
RuntimeHost.ClearTextHook = static () => { };
RuntimeHost.ReloadErbFinishedHook = static () => { };
RuntimeHost.IsLastLineEmptyHook = static () => false;
RuntimeHost.IsLastLineTemporaryHook = static () => false;
RuntimeHost.IsPrintBufferEmptyHook = static () => true;
RuntimeHost.CountInteractiveButtonsHook = static (_) => 0;
RuntimeHost.GetConsoleClientWidthHook = static () => 0;
RuntimeHost.IsConsoleActiveHook = static () => false;
RuntimeHost.GetMousePositionXYHook = static () => new RuntimePoint(0, 0);
RuntimeHost.MoveMouseXYHook = static (_, _) => false;
RuntimeHost.SetBitmapCacheEnabledForNextLineHook = static (_) => { };
RuntimeHost.SetRedrawTimerHook = static (_) => { };
RuntimeHost.GetTextBoxTextHook = static () => string.Empty;
RuntimeHost.ChangeTextBoxHook = static (_) => { };
RuntimeHost.ResetTextBoxPosHook = static () => { };
RuntimeHost.SetTextBoxPosHook = static (_, _, _) => { };
RuntimeHost.HotkeyStateSetHook = static (_, _) => { };
RuntimeHost.HotkeyStateInitHook = static (_) => { };
RuntimeHost.PrintBarHook = static () => { };
RuntimeHost.PrintCustomBarHook = static (_, _) => { };
RuntimeHost.DeleteLineHook = static (_) => { };
RuntimeHost.ClearDisplayHook = static () => { };
RuntimeHost.ResetStyleHook = static () => { };
RuntimeHost.ThrowErrorHook = static (_) => { };
RuntimeHost.ForceStopTimerHook = static () => { };
RuntimeHost.DebugPrintHook = static (_) => { };
RuntimeHost.DebugNewLineHook = static () => { };
RuntimeHost.DebugClearHook = static () => { };
RuntimeHost.PrintTemporaryLineHook = static (_) => { };
RuntimeHost.PrintFlushHook = static (_) => { };
RuntimeHost.RefreshStringsHook = static (_) => { };
RuntimeHost.MarkUpdatedGenerationHook = static () => { };
RuntimeHost.DisableOutputLogHook = static () => { };
RuntimeHost.OutputLogHook = static (_, _) => false;
RuntimeHost.OutputSystemLogHook = static (_) => false;
RuntimeHost.ThrowTitleErrorHook = static (_) => { };
RuntimeHost.SetGameBaseDataHook = static (_) => { };
RuntimeHost.SetConstantDataHook = static (_) => { };
RuntimeHost.SetVariableEvaluatorHook = static (_) => { };
RuntimeHost.SetVariableDataHook = static (_) => { };
RuntimeHost.SetIdentifierDictionaryHook = static (_) => { };
RuntimeHost.SetLabelDictionaryHook = static (_) => { };
RuntimeHost.GetConsoleHostHook = static () => null;
RuntimeHost.CreateRuntimeProcessHook = RuntimeProcessResolver.TryCreate;
RuntimeHost.GetKeyStateHook = static (_) => 0;
RuntimeHost.LoadImageHook = static (_) => null;
RuntimeHost.OpenUrlHook = static (url) =>
{
    if (string.IsNullOrWhiteSpace(url))
        return false;

    try
    {
        var psi = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
        Process.Start(psi);
        return true;
    }
    catch
    {
        Console.Error.WriteLine($"Open URL failed: {url}");
        return false;
    }
};
RuntimeHost.LoadContentsHook = static (_) => null;
RuntimeHost.UnloadTempLoadedConstImagesHook = static () => { };
RuntimeHost.UnloadTempLoadedGraphicsImagesHook = static () => { };
RuntimeHost.GetGraphicsHook = static (_) => null;
RuntimeHost.GetSpriteHook = static (_) => null;
RuntimeHost.CreateSpriteGHook = static (_, _, _) => { };
RuntimeHost.SpriteDisposeHook = static (_) => { };
RuntimeHost.SpriteDisposeAllHook = static (_) => 0;
RuntimeHost.CreateSpriteAnimeHook = static (_, _, _) => { };

var gameDirOption = new Option<DirectoryInfo?>(
    name: "--game-dir",
    description: "Game data root directory (expects CSV/ and ERB/ folders)."
);

gameDirOption.AddAlias("-g");

var exeDirOption = new Option<DirectoryInfo?>(
    name: "--exe-dir",
    description: "Compatibility option. Base directory to resolve game data (same intent as Windows --ExeDir)."
);

exeDirOption.AddAlias("-exedir");
exeDirOption.AddAlias("-EXEDIR");
exeDirOption.AddAlias("--ExeDir");

var verifyOnlyOption = new Option<bool>(
    name: "--verify-only",
    description: "Only verify directory structure and exit."
);

var preloadOnlyOption = new Option<bool>(
    name: "--preload-only",
    description: "Run runtime preload (CSV/ERB cache load) and exit."
);

var scanErhOnlyOption = new Option<bool>(
    name: "--scan-erh-only",
    description: "Run preload and scan ERH files for directive-prefix validity (#...)."
);

var scanErbPpOnlyOption = new Option<bool>(
    name: "--scan-erb-pp-only",
    description: "Run preload and scan ERB preprocessor block matching ([IF]/[ENDIF], [SKIPSTART]/[SKIPEND])."
);

var scanErbContOnlyOption = new Option<bool>(
    name: "--scan-erb-cont-only",
    description: "Run preload and scan ERB continuation blocks ({ ... }) for structural validity."
);

var gateOnlyOption = new Option<bool>(
    name: "--gate-only",
    description: "Run preload + ERH/ERB static scans as a single gate and return non-zero on issues."
);

var runSmokeOnlyOption = new Option<bool>(
    name: "--run-smoke-only",
    description: "Run runtime prerequisite checks + preload + static gate and return non-zero on failures."
);

var strictSmokeOnlyOption = new Option<bool>(
    name: "--strict-smoke-only",
    description: "Run smoke checks in strict mode (fails on recommended-missing files or preload warnings)."
);

var strictRetriesOption = new Option<int>(
    name: "--strict-retries",
    description: "Retry count for strict smoke when preload warnings are detected.",
    getDefaultValue: () => 2
);

var runEngineOption = new Option<bool>(
    name: "--run-engine",
    description: "Run the embedded runtime engine directly in terminal mode."
);

var runEngineGuiOption = new Option<bool>(
    name: "--run-engine-gui",
    description: "Run the embedded runtime engine with a Linux GUI dialog frontend (zenity)."
);
runEngineGuiOption.AddAlias("--gui-engine");
runEngineGuiOption.AddAlias("--run-gui");

var interactiveGuiOption = new Option<bool>(
    name: "--interactive-gui",
    description: "Launch interactive Linux text-GUI style launcher."
);

var playLikeOption = new Option<bool>(
    name: "--play-like",
    description: "Run game-like launcher flow after smoke checks."
);

var launchBundledOption = new Option<bool>(
    name: "--launch-bundled",
    description: "Run smoke checks, then launch a bundled executable from the game folder."
);

var launchTargetOption = new Option<string?>(
    name: "--launch-target",
    description: "Preferred executable file name (or absolute path) to launch with --launch-bundled."
);

var allowPeLaunchOption = new Option<bool>(
    name: "--allow-pe-launch",
    description: "Allow launching Windows PE executables (.exe) via mono/wine fallback."
);

var root = new RootCommand("Emuera Linux CLI bootstrap host")
{
    gameDirOption,
    exeDirOption,
    verifyOnlyOption,
    preloadOnlyOption,
    scanErhOnlyOption,
    scanErbPpOnlyOption,
    scanErbContOnlyOption,
    gateOnlyOption,
    runSmokeOnlyOption,
    strictSmokeOnlyOption,
    strictRetriesOption,
    runEngineOption,
    runEngineGuiOption,
    interactiveGuiOption,
    playLikeOption,
    launchBundledOption,
    launchTargetOption,
    allowPeLaunchOption,
};

var appExitCode = 0;

root.SetHandler(async context =>
{
    var parse = context.ParseResult;
    var gameDir = parse.GetValueForOption(gameDirOption);
    var exeDir = parse.GetValueForOption(exeDirOption);
    var verifyOnly = parse.GetValueForOption(verifyOnlyOption);
    var preloadOnly = parse.GetValueForOption(preloadOnlyOption);
    var scanErhOnly = parse.GetValueForOption(scanErhOnlyOption);
    var scanErbPpOnly = parse.GetValueForOption(scanErbPpOnlyOption);
    var scanErbContOnly = parse.GetValueForOption(scanErbContOnlyOption);
    var gateOnly = parse.GetValueForOption(gateOnlyOption);
    var runSmokeOnly = parse.GetValueForOption(runSmokeOnlyOption);
    var strictSmokeOnly = parse.GetValueForOption(strictSmokeOnlyOption);
    var strictRetries = Math.Max(0, parse.GetValueForOption(strictRetriesOption));
    var runEngine = parse.GetValueForOption(runEngineOption);
    var runEngineGui = parse.GetValueForOption(runEngineGuiOption);
    var interactiveGui = parse.GetValueForOption(interactiveGuiOption);
    var playLike = parse.GetValueForOption(playLikeOption);
    var launchBundled = parse.GetValueForOption(launchBundledOption);
    var launchTarget = parse.GetValueForOption(launchTargetOption);
    var allowPeLaunch = parse.GetValueForOption(allowPeLaunchOption);

    var candidates = new List<(string Source, string Dir)>();
    if (gameDir != null)
        candidates.Add(("--game-dir", gameDir.FullName));
    if (exeDir != null)
        candidates.Add(("--exe-dir", exeDir.FullName));

    var envGameDir = Environment.GetEnvironmentVariable("EMUERA_GAME_DIR");
    if (!string.IsNullOrWhiteSpace(envGameDir))
        candidates.Add(("EMUERA_GAME_DIR", envGameDir.Trim()));

    candidates.Add(("AppContext.BaseDirectory", AppContext.BaseDirectory));
    candidates.Add(("CurrentDirectory", Directory.GetCurrentDirectory()));

    var tried = new List<(string Source, string Root, GameDataValidationResult Validation)>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    GameDataValidationResult? validation = null;
    string selectedSource = string.Empty;
    foreach (var candidate in candidates)
    {
        var fullPath = Path.GetFullPath(candidate.Dir);
        if (!seen.Add(fullPath))
            continue;

        var candidateValidation = GameDataLayout.Validate(fullPath);
        tried.Add((candidate.Source, fullPath, candidateValidation));
        if (candidateValidation.IsValid)
        {
            validation = candidateValidation;
            selectedSource = candidate.Source;
            break;
        }
    }

    if (validation == null || !validation.IsValid)
    {
        Console.Error.WriteLine("Invalid game directory. Could not locate a valid CSV/ERB layout.");
        foreach (var attempt in tried)
        {
            Console.Error.WriteLine($"- Tried ({attempt.Source}): {attempt.Root}");
            foreach (var missingPath in attempt.Validation.MissingPaths)
                Console.Error.WriteLine($"  missing: {missingPath}");
        }
        appExitCode = 2;
        return;
    }

    Console.WriteLine("Game directory check passed.");
    Console.WriteLine($"- Source: {selectedSource}");
    Console.WriteLine($"- Root: {validation.RootDir}");
    Console.WriteLine($"- CSV : {validation.CsvDir}");
    Console.WriteLine($"- ERB : {validation.ErbDir}");

    if (runEngineGui)
    {
        appExitCode = await CliZenityEngineRunner.RunAsync(validation, strictSmokeOnly, strictRetries);
        return;
    }

    if (runEngine)
    {
        appExitCode = await CliEngineRunner.RunAsync(validation, strictSmokeOnly, strictRetries);
        return;
    }

    if (launchBundled)
    {
        appExitCode = await LauncherUi.LaunchBundledAsync(validation, strictRetries, strictSmokeOnly, launchTarget, allowPeLaunch);
        return;
    }

    if (interactiveGui || playLike)
    {
        appExitCode = await LauncherUi.RunAsync(validation, strictRetries, playLike, allowPeLaunch);
        return;
    }

    if (verifyOnly)
        return;

    if (preloadOnly)
    {
        var result = await RuntimePreloadBootstrap.PreloadAsync(validation.CsvDir, validation.ErbDir);
        Console.WriteLine("Runtime preload completed.");
        Console.WriteLine($"- Target files: {result.CandidateFileCount}");
        Console.WriteLine($"- Cached files: {result.CachedFileCount}");
        Console.WriteLine($"- Skipped     : {result.SkippedFileCount}");
        Console.WriteLine($"- Warnings    : {result.WarningCount}");
        Console.WriteLine($"- Elapsed ms  : {result.ElapsedMilliseconds}");
        foreach (var warning in result.WarningSamples.Take(10))
            Console.WriteLine($"  [PRELOAD] {warning}");
        if (result.ExtensionStats.Count > 0)
        {
            Console.WriteLine("- By extension:");
            foreach (var stat in result.ExtensionStats)
            {
                Console.WriteLine($"  {stat.Extension}: target={stat.CandidateCount}, cached={stat.CachedCount}, skipped={stat.SkippedCount}");
            }
        }
        return;
    }

    if (scanErhOnly)
    {
        var preload = await RuntimePreloadBootstrap.PreloadAsync(validation.CsvDir, validation.ErbDir);
        Console.WriteLine("Runtime preload completed for ERH scan.");
        Console.WriteLine($"- Cached files: {preload.CachedFileCount}");
        Console.WriteLine($"- Warnings    : {preload.WarningCount}");

        var scan = RuntimePreloadBootstrap.ScanErhDirectivePrefixes();
        Console.WriteLine("ERH directive-prefix scan completed.");
        Console.WriteLine($"- ERH files   : {scan.ErhFileCount}");
        Console.WriteLine($"- Issues      : {scan.IssueCount}");

        foreach (var issue in scan.Issues.Take(10))
        {
            Console.WriteLine($"  {issue.FilePath}:{issue.LineNo + 1} {issue.Message}");
        }
        return;
    }

    if (scanErbPpOnly)
    {
        var preload = await RuntimePreloadBootstrap.PreloadAsync(validation.CsvDir, validation.ErbDir);
        Console.WriteLine("Runtime preload completed for ERB PP scan.");
        Console.WriteLine($"- Cached files: {preload.CachedFileCount}");
        Console.WriteLine($"- Warnings    : {preload.WarningCount}");

        var scan = RuntimePreloadBootstrap.ScanErbPreprocessorBlocks();
        Console.WriteLine("ERB preprocessor scan completed.");
        Console.WriteLine($"- ERB files   : {scan.ErbFileCount}");
        Console.WriteLine($"- Directives  : {scan.DirectiveCount}");
        Console.WriteLine($"- Issues      : {scan.IssueCount}");

        foreach (var issue in scan.Issues.Take(20))
        {
            Console.WriteLine($"  {issue.FilePath}:{issue.LineNo + 1} {issue.Message}");
        }
        return;
    }

    if (scanErbContOnly)
    {
        var preload = await RuntimePreloadBootstrap.PreloadAsync(validation.CsvDir, validation.ErbDir);
        Console.WriteLine("Runtime preload completed for ERB continuation scan.");
        Console.WriteLine($"- Cached files: {preload.CachedFileCount}");
        Console.WriteLine($"- Warnings    : {preload.WarningCount}");

        var scan = RuntimePreloadBootstrap.ScanErbContinuationBlocks();
        Console.WriteLine("ERB continuation scan completed.");
        Console.WriteLine($"- ERB files   : {scan.ErbFileCount}");
        Console.WriteLine($"- Blocks      : {scan.BlockCount}");
        Console.WriteLine($"- Issues      : {scan.IssueCount}");

        foreach (var issue in scan.Issues.Take(20))
        {
            Console.WriteLine($"  {issue.FilePath}:{issue.LineNo + 1} {issue.Message}");
        }
        return;
    }

    if (gateOnly)
    {
        var gate = await RuntimePreloadBootstrap.RunStaticGateAsync(validation.CsvDir, validation.ErbDir);
        Console.WriteLine("Static gate completed.");
        Console.WriteLine($"- Preload cached: {gate.Preload.CachedFileCount}/{gate.Preload.CandidateFileCount}");
        Console.WriteLine($"- Preload warn  : {gate.Preload.WarningCount}");
        Console.WriteLine($"- ERH issues    : {gate.Erh.IssueCount}");
        Console.WriteLine($"- ERB PP issues : {gate.ErbPreprocessor.IssueCount}");
        Console.WriteLine($"- ERB CONT issue: {gate.ErbContinuation.IssueCount}");
        Console.WriteLine($"- ERB labels    : {gate.ErbEntryLabels.LabelCount} (entry-like: {gate.ErbEntryLabels.EntryLikeLabelCount})");
        if (gate.ErbEntryLabels.FoundEntryLikeLabels.Count > 0)
            Console.WriteLine($"- Entry labels  : {string.Join(", ", gate.ErbEntryLabels.FoundEntryLikeLabels.Take(8))}");
        Console.WriteLine($"- Boot profile  : {(gate.BootProfile.IsValid ? "OK" : "WARN")} ({gate.BootProfile.Detail})");
        Console.WriteLine($"- ERB label errs: {gate.ErbEntryLabels.IssueCount}");
        Console.WriteLine($"- Total issues  : {gate.TotalIssueCount}");
        Console.WriteLine($"- Gate passed   : {gate.Passed}");

        if (!gate.Passed)
        {
            foreach (var issue in gate.Erh.Issues.Take(10))
            {
                Console.WriteLine($"  [ERH] {issue.FilePath}:{issue.LineNo + 1} {issue.Message}");
            }
            foreach (var issue in gate.ErbPreprocessor.Issues.Take(10))
            {
                Console.WriteLine($"  [ERB-PP] {issue.FilePath}:{issue.LineNo + 1} {issue.Message}");
            }
            foreach (var issue in gate.ErbContinuation.Issues.Take(10))
            {
                Console.WriteLine($"  [ERB-CONT] {issue.FilePath}:{issue.LineNo + 1} {issue.Message}");
            }
            foreach (var issue in gate.ErbEntryLabels.Issues.Take(10))
            {
                Console.WriteLine($"  [ERB-LABEL] {issue.FilePath}:{issue.LineNo + 1} {issue.Message}");
            }
            appExitCode = 3;
        }
        return;
    }

    if (runSmokeOnly || strictSmokeOnly)
    {
        var prereq = GameDataLayout.CheckRuntimePrerequisites(validation.RootDir, validation.CsvDir, validation.ErbDir);
        Console.WriteLine("Runtime prerequisite check completed.");
        Console.WriteLine($"- GAMEBASE.CSV : {(prereq.MissingRequiredPaths.Count == 0 ? "OK" : "MISSING")}");
        Console.WriteLine($"- emuera.config: {(prereq.MissingRecommendedPaths.Count == 0 ? "OK" : "MISSING (recommended)")}");
        Console.WriteLine($"- TITLE.ERB    : {(File.Exists(prereq.TitleErbPath) ? "OK" : "MISSING (recommended)")}");
        Console.WriteLine($"- SYSTEM.ERB   : {(File.Exists(prereq.SystemErbPath) ? "OK" : "MISSING (recommended)")}");

        if (prereq.MissingRequiredPaths.Count > 0)
        {
            foreach (var missing in prereq.MissingRequiredPaths)
                Console.WriteLine($"  [REQ] missing: {missing}");
            appExitCode = 4;
            return;
        }
        foreach (var missing in prereq.MissingRecommendedPaths)
        {
            Console.WriteLine($"  [REC] missing: {missing}");
        }
        if (strictSmokeOnly && prereq.MissingRecommendedPaths.Count > 0)
        {
            appExitCode = 6;
            return;
        }

        var gameBase = GameBaseInspector.Inspect(prereq.GamebaseCsvPath);
        Console.WriteLine("GAMEBASE inspection completed.");
        Console.WriteLine($"- Read ok      : {gameBase.ReadSucceeded}");
        Console.WriteLine($"- Parsed keys  : {gameBase.ParsedKeyCount}");
        Console.WriteLine($"- Title        : {gameBase.Title ?? "(missing)"}");
        Console.WriteLine($"- Author       : {gameBase.Author ?? "(missing)"}");
        Console.WriteLine($"- Version raw  : {gameBase.VersionRaw ?? "(missing)"}");
        foreach (var warning in gameBase.Warnings.Take(10))
        {
            Console.WriteLine($"  [GAMEBASE] {warning}");
        }

        var gate = await RuntimePreloadBootstrap.RunStaticGateAsync(validation.CsvDir, validation.ErbDir);
        if (strictSmokeOnly && gate.Preload.WarningCount > 0)
        {
            for (var attempt = 1; attempt <= strictRetries; attempt++)
            {
                Console.WriteLine($"Strict retry {attempt}/{strictRetries} due to preload warnings...");
                await Task.Delay(100);
                gate = await RuntimePreloadBootstrap.RunStaticGateAsync(validation.CsvDir, validation.ErbDir);
                if (gate.Preload.WarningCount == 0)
                    break;
            }
        }

        Console.WriteLine("Run smoke gate completed.");
        Console.WriteLine($"- Preload cached: {gate.Preload.CachedFileCount}/{gate.Preload.CandidateFileCount}");
        Console.WriteLine($"- Preload warn  : {gate.Preload.WarningCount}");
        foreach (var warning in gate.Preload.WarningSamples.Take(10))
            Console.WriteLine($"  [PRELOAD] {warning}");
        Console.WriteLine($"- ERH issues    : {gate.Erh.IssueCount}");
        Console.WriteLine($"- ERB PP issues : {gate.ErbPreprocessor.IssueCount}");
        Console.WriteLine($"- ERB CONT issue: {gate.ErbContinuation.IssueCount}");
        Console.WriteLine($"- ERB labels    : {gate.ErbEntryLabels.LabelCount} (entry-like: {gate.ErbEntryLabels.EntryLikeLabelCount})");
        if (gate.ErbEntryLabels.FoundEntryLikeLabels.Count > 0)
            Console.WriteLine($"- Entry labels  : {string.Join(", ", gate.ErbEntryLabels.FoundEntryLikeLabels.Take(8))}");
        Console.WriteLine($"- Boot profile  : {(gate.BootProfile.IsValid ? "OK" : "FAIL")} ({gate.BootProfile.Detail})");
        Console.WriteLine($"- ERB label errs: {gate.ErbEntryLabels.IssueCount}");
        Console.WriteLine($"- Total issues  : {gate.TotalIssueCount}");
        Console.WriteLine($"- Smoke passed  : {gate.Passed}");
        if (!gate.Passed)
        {
            appExitCode = 3;
            return;
        }
        if (strictSmokeOnly && gate.Preload.WarningCount > 0)
        {
            appExitCode = 7;
            return;
        }
        if (!gate.BootProfile.IsValid)
        {
            appExitCode = 5;
        }
        return;
    }

    if (Console.IsInputRedirected)
    {
        Console.WriteLine("No mode specified and stdin is redirected. Running smoke checks by default...");
        appExitCode = await LauncherUi.RunSmokeOnlyAsync(validation, strict: false, strictRetries);
        return;
    }

    var defaultMode = Environment.GetEnvironmentVariable("EMUERA_DEFAULT_MODE");
    var useInteractiveDefault =
        string.Equals(defaultMode, "interactive", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(defaultMode, "menu", StringComparison.OrdinalIgnoreCase);
    var usePlayLikeDefault =
        string.Equals(defaultMode, "play-like", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(defaultMode, "playlike", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(defaultMode, "autoplay", StringComparison.OrdinalIgnoreCase);
    var useEngineDefault =
        string.IsNullOrWhiteSpace(defaultMode) ||
        string.Equals(defaultMode, "engine", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(defaultMode, "run-engine", StringComparison.OrdinalIgnoreCase);

    if (useInteractiveDefault)
    {
        Console.WriteLine("No mode specified. Entering interactive launcher...");
        appExitCode = await LauncherUi.RunAsync(validation, strictRetries, autoPlay: false, allowPeLaunch: false);
        return;
    }

    if (usePlayLikeDefault)
    {
        Console.WriteLine("No mode specified. Starting play-like launcher flow...");
        appExitCode = await LauncherUi.RunAsync(validation, strictRetries, autoPlay: true, allowPeLaunch: false);
        return;
    }

    if (useEngineDefault)
    {
        Console.WriteLine("No mode specified. Starting embedded runtime engine mode...");
        appExitCode = await CliEngineRunner.RunAsync(validation, strictSmokeOnly, strictRetries);
        return;
    }

    Console.WriteLine($"No mode specified. Unknown EMUERA_DEFAULT_MODE='{defaultMode}'. Starting embedded runtime engine mode...");
    appExitCode = await CliEngineRunner.RunAsync(validation, strictSmokeOnly, strictRetries);
});

var commandResult = await root.InvokeAsync(args);
return appExitCode != 0 ? appExitCode : commandResult;

internal static class CliColorNames
{
    private static readonly Dictionary<string, int> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = 0x000000,
        ["silver"] = 0xC0C0C0,
        ["gray"] = 0x808080,
        ["white"] = 0xFFFFFF,
        ["maroon"] = 0x800000,
        ["red"] = 0xFF0000,
        ["purple"] = 0x800080,
        ["fuchsia"] = 0xFF00FF,
        ["green"] = 0x008000,
        ["lime"] = 0x00FF00,
        ["olive"] = 0x808000,
        ["yellow"] = 0xFFFF00,
        ["navy"] = 0x000080,
        ["blue"] = 0x0000FF,
        ["teal"] = 0x008080,
        ["aqua"] = 0x00FFFF,
        ["orange"] = 0xFFA500,
        ["pink"] = 0xFFC0CB,
        ["brown"] = 0xA52A2A,
        ["gold"] = 0xFFD700,
        ["cyan"] = 0x00FFFF,
        ["magenta"] = 0xFF00FF,
    };

    public static bool TryResolveRgb24(string? colorName, out int rgb24)
    {
        rgb24 = 0;
        if (string.IsNullOrWhiteSpace(colorName))
            return false;

        var raw = colorName.Trim();
        if (raw.StartsWith('#'))
        {
            var hex = raw[1..];
            if (hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            {
                rgb24 = rgb & 0xFFFFFF;
                return true;
            }
            return false;
        }

        if (NamedColors.TryGetValue(raw, out var named))
        {
            rgb24 = named;
            return true;
        }

        return false;
    }
}

internal static class CliHtmlFallback
{
    private static readonly Regex ButtonRegex = new(
        "<button\\b(?<attrs>[^>]*)>(?<inner>.*?)</button>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ButtonValueRegex = new(
        "(?:^|\\s)value\\s*=\\s*(?:\"(?<dq>[^\"]*)\"|'(?<sq>[^']*)'|(?<bare>[^\\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string[]? HtmlTagSplit(string? raw)
    {
        if (raw == null)
            return null;
        if (raw.Length == 0)
            return [string.Empty];

        var parts = new List<string>();
        var segmentStart = 0;
        var inTag = false;

        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (ch == '<')
            {
                if (!inTag)
                {
                    if (i > segmentStart)
                        parts.Add(raw.Substring(segmentStart, i - segmentStart));
                    segmentStart = i;
                    inTag = true;
                    continue;
                }

                // Nested tag start before closing previous tag: treat as malformed.
                return null;
            }

            if (ch == '>' && inTag)
            {
                parts.Add(raw.Substring(segmentStart, i - segmentStart + 1));
                segmentStart = i + 1;
                inTag = false;
            }
        }

        if (inTag)
            return null;

        if (segmentStart < raw.Length)
            parts.Add(raw.Substring(segmentStart));

        if (parts.Count == 0)
            return [string.Empty];

        return parts.ToArray();
    }

    public static int HtmlLength(string? raw)
    {
        return CliTextDisplayWidth.GetDisplayWidth(HtmlToPlainText(raw));
    }

    public static string[] HtmlSubString(string? raw, int width)
    {
        var plain = HtmlToPlainText(raw);
        if (string.IsNullOrEmpty(plain))
            return [string.Empty, string.Empty];

        if (width <= 0)
            return [string.Empty, plain];
        if (width >= CliTextDisplayWidth.GetDisplayWidth(plain))
            return [plain, string.Empty];

        return CliTextDisplayWidth.SplitByDisplayWidth(plain, width);
    }

    public static string HtmlToPlainText(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var normalized = ReplaceButtonTags(raw);
        return StripTags(normalized);
    }

    private static string ReplaceButtonTags(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.IndexOf("<button", StringComparison.OrdinalIgnoreCase) < 0)
            return raw;

        return ButtonRegex.Replace(raw, static match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var innerHtml = match.Groups["inner"].Value;
            var innerText = StripTags(innerHtml)
                .Replace('\r', ' ')
                .Replace('\n', ' ');
            var value = TryExtractButtonValue(attrs);
            if (string.IsNullOrEmpty(value))
                return innerText;
            return $"{innerText}<{value}>";
        });
    }

    private static string? TryExtractButtonValue(string attrs)
    {
        if (string.IsNullOrWhiteSpace(attrs))
            return null;

        var match = ButtonValueRegex.Match(attrs);
        if (!match.Success)
            return null;

        if (match.Groups["dq"].Success)
            return match.Groups["dq"].Value;
        if (match.Groups["sq"].Success)
            return match.Groups["sq"].Value;
        if (match.Groups["bare"].Success)
            return match.Groups["bare"].Value;
        return null;
    }

    private static string StripTags(string raw)
    {
        var input = raw.AsSpan();
        var builder = new System.Text.StringBuilder(raw.Length);
        var inTag = false;
        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch == '<')
            {
                inTag = true;
                continue;
            }
            if (ch == '>')
            {
                inTag = false;
                continue;
            }
            if (!inTag)
                builder.Append(ch);
        }

        return builder.ToString();
    }

    public static string Escape(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;
        return raw
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}

internal static class CliConsoleEncoding
{
    public static void TryConfigure()
    {
        try
        {
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            if (Console.OutputEncoding.CodePage != utf8NoBom.CodePage)
                Console.OutputEncoding = utf8NoBom;
            if (!Console.IsInputRedirected && Console.InputEncoding.CodePage != utf8NoBom.CodePage)
                Console.InputEncoding = utf8NoBom;
        }
        catch
        {
            // Best-effort only.
        }
    }
}

internal static class CliEngineRunner
{
    public static async Task<int> RunAsync(GameDataValidationResult validation, bool strict, int strictRetries)
    {
        Console.WriteLine("Runtime engine mode: running smoke checks...");
        var smoke = await LauncherUi.RunSmokeOnlyAsync(validation, strict, strictRetries);
        if (smoke != 0)
        {
            Console.Error.WriteLine($"Runtime engine launch blocked by smoke exit code: {smoke}");
            return smoke;
        }

        return await RunAfterSmokeAsync(validation);
    }

    public static async Task<int> RunAfterSmokeAsync(GameDataValidationResult validation)
    {
        if (validation == null)
            return 2;

        try
        {
            RuntimeEngineBootstrap.InitializeForCli(validation.RootDir);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Runtime bootstrap failed: {ex.Message}");
            return 9;
        }

        var executionConsole = new CliExecutionConsole();
        CliRuntimeHostBridge.AttachExecutionConsole(executionConsole);
        IRuntimeProcess? runtimeProcess;
        try
        {
            runtimeProcess = RuntimeHost.CreateRuntimeProcess(executionConsole);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Runtime process creation failed: {ex.Message}");
            return 9;
        }

        if (runtimeProcess == null)
        {
            Console.Error.WriteLine("Runtime process creation failed: process factory returned null.");
            return 9;
        }

        executionConsole.AttachProcess(runtimeProcess);

        using var initLogStream = new MemoryStream();
        using var initLogWriter = new StreamWriter(initLogStream) { AutoFlush = true };

        bool initialized;
        try
        {
            initialized = await runtimeProcess.Initialize(initLogWriter);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Runtime initialization failed: {ex}");
            return 10;
        }

        if (!initialized)
        {
            Console.Error.WriteLine("Runtime initialization did not complete.");
            return 10;
        }

        runtimeProcess.BeginTitle();

        while (true)
        {
            executionConsole.BeginRunning();
            runtimeProcess.DoScript();

            if (executionConsole.IsTerminated)
                return executionConsole.HasRuntimeError ? 11 : 0;

            if (!executionConsole.IsWaitingInput)
            {
                Console.Error.WriteLine("Runtime loop paused without an input request.");
                return 12;
            }

            if (!TryHandleInput(executionConsole))
            {
                executionConsole.Quit();
                return 0;
            }
        }
    }

    private static bool TryHandleInput(CliExecutionConsole executionConsole)
    {
        var request = executionConsole.PendingRequest;
        if (request == null)
            return false;

        var prompt = BuildPrompt(request);
        var redirectedInput = Console.IsInputRedirected;
        var redirectedInvalidLimit = ResolveRedirectInvalidLimit();
        var redirectedInvalidCount = 0;
        DateTimeOffset? timeoutDeadline = null;
        if (!redirectedInput && request.Timelimit > 0)
            timeoutDeadline = DateTimeOffset.UtcNow.AddMilliseconds(request.Timelimit);
        while (true)
        {
            // Ensure pending runtime output (buttons/options) is visible before reading input.
            RuntimeHost.RefreshStrings(false);

            string? rawInput = null;
            var usedDirectInput = false;
            var timedOut = false;
            var directInput = string.Empty;
            var retry = false;
            var remainingTimeoutMs = GetRemainingTimeoutMs(timeoutDeadline);
            if (!redirectedInput &&
                CliTerminalMouseInput.TryRead(
                    request,
                    prompt,
                    executionConsole,
                    remainingTimeoutMs,
                    out var terminalResult))
            {
                usedDirectInput = terminalResult.UsedDirectInput;
                if (terminalResult.Cancelled)
                    return false;
                if (terminalResult.Retry)
                    continue;
                timedOut = terminalResult.TimedOut;
                rawInput = terminalResult.Input;
            }
            else
            {
                if (!redirectedInput)
                    Console.Write(CliRuntimeHostBridge.DecorateInputPrompt(prompt));

                if (!redirectedInput &&
                    IsDirectKeyCaptureEnabled() &&
                    TryReadDirectInteractiveInput(
                        request,
                        out directInput,
                        out retry,
                        out timedOut,
                        remainingTimeoutMs))
                {
                    usedDirectInput = true;
                    if (retry)
                        continue;
                    rawInput = directInput;
                }
                else if (!redirectedInput &&
                    TryReadLineInteractiveInput(
                        request,
                        out var lineInput,
                        out timedOut,
                        remainingTimeoutMs))
                {
                    rawInput = lineInput;
                }
                else
                {
                    rawInput = Console.ReadLine();
                }
            }

            if (timedOut)
            {
                executionConsole.MarkTimedOut();
                EmitTimeoutMessage(executionConsole, request);
                timeoutDeadline = null;
                if (executionConsole.TrySubmitInput(string.Empty, timedOut: true))
                    return true;
                Console.WriteLine("Input timed out. Default value was not accepted; enter a value.");
                continue;
            }

            if (rawInput == null)
            {
                Console.Error.WriteLine("Input stream closed. Ending runtime session.");
                return false;
            }

            if (!redirectedInput && !usedDirectInput)
                CliRuntimeHostBridge.RecordInputText(rawInput);

            if (executionConsole.TrySubmitInput(rawInput))
                return true;

            if (redirectedInput)
            {
                redirectedInvalidCount++;
                Console.Error.WriteLine($"Invalid runtime input in redirected mode (attempt {redirectedInvalidCount}).");
                if (redirectedInvalidCount >= redirectedInvalidLimit)
                {
                    Console.Error.WriteLine($"Redirected input retry limit reached ({redirectedInvalidLimit}).");
                    return false;
                }
                continue;
            }

            Console.WriteLine("Invalid input. Try again.");
        }
    }

    private static string BuildPrompt(InputRequest request)
    {
        return request.InputType switch
        {
            InputType.EnterKey => "[ENTER] ",
            InputType.AnyKey => "[ANYKEY] ",
            InputType.Void => "[WAIT] ",
            InputType.IntValue or InputType.IntButton => "INPUT(INT)> ",
            InputType.StrValue or InputType.StrButton => "INPUT(STR)> ",
            InputType.AnyValue => "INPUT(ANY)> ",
            InputType.PrimitiveMouseKey => "INPUT(PRIMITIVE)> ",
            _ => "> ",
        };
    }

    private static bool TryReadDirectInteractiveInput(
        InputRequest request,
        out string rawInput,
        out bool retry,
        out bool timedOut,
        long timeoutMilliseconds = -1)
    {
        rawInput = string.Empty;
        retry = false;
        timedOut = false;

        if (Console.IsInputRedirected)
            return false;

        if (request.InputType != InputType.EnterKey &&
            request.InputType != InputType.AnyKey &&
            request.InputType != InputType.Void)
        {
            return false;
        }

        ConsoleKeyInfo keyInfo;
        if (timeoutMilliseconds == 0)
        {
            Console.WriteLine();
            timedOut = true;
            return true;
        }

        if (timeoutMilliseconds > 0)
        {
            var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    keyInfo = Console.ReadKey(intercept: true);
                    break;
                }

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    Console.WriteLine();
                    timedOut = true;
                    return true;
                }

                Thread.Sleep(8);
            }
        }
        else
        {
            keyInfo = Console.ReadKey(intercept: true);
        }

        CliRuntimeHostBridge.RecordConsoleKey(keyInfo);
        Console.WriteLine();

        if (request.InputType == InputType.EnterKey)
        {
            if (keyInfo.Key != ConsoleKey.Enter)
            {
                Console.WriteLine("Press ENTER to continue.");
                retry = true;
                return true;
            }

            rawInput = string.Empty;
            return true;
        }

        if (request.InputType == InputType.AnyKey || request.InputType == InputType.Void)
        {
            rawInput = FormatKeyInput(keyInfo);
            return true;
        }
        return false;
    }

    private static bool TryReadLineInteractiveInput(
        InputRequest request,
        out string rawInput,
        out bool timedOut,
        long timeoutMilliseconds = -1)
    {
        rawInput = string.Empty;
        timedOut = false;

        if (Console.IsInputRedirected || timeoutMilliseconds < 0)
            return false;

        if (request.InputType == InputType.EnterKey ||
            request.InputType == InputType.AnyKey ||
            request.InputType == InputType.Void)
        {
            return false;
        }

        if (timeoutMilliseconds == 0)
        {
            Console.WriteLine();
            timedOut = true;
            return true;
        }

        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(intercept: true);
                CliRuntimeHostBridge.RecordConsoleKey(keyInfo);
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    rawInput = buffer.ToString();
                    return true;
                }

                if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Length--;
                        Console.Write("\b \b");
                    }
                    continue;
                }

                if (!char.IsControl(keyInfo.KeyChar) && keyInfo.KeyChar != '\0')
                {
                    buffer.Append(keyInfo.KeyChar);
                    Console.Write(keyInfo.KeyChar);
                }
                continue;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                Console.WriteLine();
                timedOut = true;
                return true;
            }

            Thread.Sleep(8);
        }
    }

    private static long GetRemainingTimeoutMs(DateTimeOffset? timeoutDeadline)
    {
        if (!timeoutDeadline.HasValue)
            return -1;

        var remaining = timeoutDeadline.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return 0;

        return (long)Math.Ceiling(remaining.TotalMilliseconds);
    }

    private static void EmitTimeoutMessage(CliExecutionConsole executionConsole, InputRequest request)
    {
        if (executionConsole == null || request == null)
            return;
        if (request.TimeUpMes == null)
            return;
        executionConsole.PrintSingleLine(request.TimeUpMes);
    }

    private static string FormatKeyInput(ConsoleKeyInfo keyInfo)
    {
        if (keyInfo.Key == ConsoleKey.Enter)
            return string.Empty;
        if (!char.IsControl(keyInfo.KeyChar) && keyInfo.KeyChar != '\0')
            return keyInfo.KeyChar.ToString();
        return keyInfo.Key.ToString();
    }

    private static int ResolveRedirectInvalidLimit()
    {
        const int fallback = 200;
        var raw = Environment.GetEnvironmentVariable("EMUERA_CLI_REDIRECT_INVALID_LIMIT");
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;
        if (!int.TryParse(raw, out var parsed))
            return fallback;
        return parsed <= 0 ? fallback : parsed;
    }

    private static bool IsDirectKeyCaptureEnabled()
    {
        var env = Environment.GetEnvironmentVariable("EMUERA_CLI_DIRECT_KEY_CAPTURE");
        if (!string.IsNullOrWhiteSpace(env))
        {
            if (env.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                env.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                env.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return true;
            if (env.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                env.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                env.Equals("no", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        var term = Environment.GetEnvironmentVariable("TERM");
        if (string.IsNullOrWhiteSpace(term) || term.Equals("dumb", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}

internal static class LauncherUi
{
    public static Task<int> RunSmokeOnlyAsync(GameDataValidationResult validation, bool strict, int strictRetries)
    {
        return RunSmokeAsync(validation, strict, strictRetries);
    }

    public static async Task<int> LaunchBundledAsync(
        GameDataValidationResult validation,
        int strictRetries,
        bool strict,
        string? preferredExecutable,
        bool allowPeLaunch)
    {
        var smoke = await RunSmokeAsync(validation, strict, strictRetries);
        if (smoke != 0)
        {
            Console.Error.WriteLine($"Launch blocked by smoke exit code: {smoke}");
            return smoke;
        }

        if (TryLaunchBundledExecutable(validation.RootDir, preferredExecutable, allowPeLaunch, out var launchedPath, out var launchError))
        {
            Console.WriteLine($"Launched executable: {launchedPath}");
            return 0;
        }

        Console.Error.WriteLine($"Launch failed: {launchError}");
        return 8;
    }

    public static async Task<int> RunAsync(GameDataValidationResult validation, int strictRetries, bool autoPlay, bool allowPeLaunch)
    {
        var hasInteractiveStdin = !Console.IsInputRedirected;

        if (autoPlay)
        {
            Console.WriteLine("Play-like mode: running smoke checks before launch...");
            var smoke = await RunSmokeAsync(validation, strict: false, strictRetries: strictRetries);
            if (smoke != 0)
            {
                Console.WriteLine($"Launch blocked by smoke exit code: {smoke}");
                return smoke;
            }
            Console.WriteLine("Smoke checks passed.");
            if (!HasViableLaunchCandidate(validation.RootDir, preferredExecutable: null, allowPeLaunch))
            {
                Console.WriteLine("No launchable bundled executable candidates were found.");
                if (!hasInteractiveStdin)
                {
                    Console.WriteLine("No interactive stdin detected. Finishing play-like smoke pass without auto launch.");
                    return 0;
                }
                Console.WriteLine("Falling back to embedded runtime engine mode...");
                return await CliEngineRunner.RunAfterSmokeAsync(validation);
            }
            else
            {
                if (TryLaunchBundledExecutable(validation.RootDir, preferredExecutable: null, allowPeLaunch, out var launchedPath, out var launchError))
                {
                    Console.WriteLine($"Game process launched: {launchedPath}");
                    return 0;
                }

                Console.WriteLine($"Auto launch failed: {launchError}");
                if (!hasInteractiveStdin)
                {
                    Console.WriteLine("No interactive stdin detected. Exiting play-like mode with launch failure.");
                    return 8;
                }
                Console.WriteLine("Falling back to embedded runtime engine mode...");
                return await CliEngineRunner.RunAfterSmokeAsync(validation);
            }
        }

        if (CanUseZenityLauncher())
            return await RunZenityAsync(validation, strictRetries, autoPlay, allowPeLaunch);

        while (true)
        {
            PrintHeader(validation.RootDir);
            var prereq = GameDataLayout.CheckRuntimePrerequisites(validation.RootDir, validation.CsvDir, validation.ErbDir);
            var gameBase = GameBaseInspector.Inspect(prereq.GamebaseCsvPath);
            Console.WriteLine($"Title   : {gameBase.Title ?? "(unknown)"}");
            Console.WriteLine($"Author  : {gameBase.Author ?? "(unknown)"}");
            Console.WriteLine($"Version : {gameBase.VersionRaw ?? "(unknown)"}");
            Console.WriteLine();
            Console.WriteLine("[1] Runtime smoke check");
            Console.WriteLine("[2] Strict smoke check");
            Console.WriteLine("[3] New game (launch if executable found)");
            Console.WriteLine("[4] Load game (launch if executable found)");
            Console.WriteLine("[0] Exit");
            Console.Write("> ");

            var input = Console.ReadLine()?.Trim();
            if (input == null)
            {
                Console.WriteLine("No interactive stdin detected. Exiting launcher.");
                return autoPlay ? 8 : 0;
            }
            if (input == "0")
                return 0;

            if (input == "1")
            {
                var code = await RunSmokeAsync(validation, strict: false, strictRetries: strictRetries);
                PrintResult("Smoke", code);
                WaitForEnter();
                continue;
            }

            if (input == "2")
            {
                var code = await RunSmokeAsync(validation, strict: true, strictRetries: strictRetries);
                PrintResult("Strict smoke", code);
                WaitForEnter();
                continue;
            }

            if (input is "3" or "4")
            {
                var smoke = await RunSmokeAsync(validation, strict: false, strictRetries: strictRetries);
                if (smoke != 0)
                {
                    Console.WriteLine("Launch blocked: smoke gate failed.");
                    WaitForEnter();
                    continue;
                }

                var launchSelection = SelectLaunchTargetInConsole(validation.RootDir, allowPeLaunch);
                if (launchSelection.Cancelled)
                {
                    WaitForEnter();
                    continue;
                }

                if (TryLaunchBundledExecutable(validation.RootDir, launchSelection.PreferredExecutable, allowPeLaunch, out var launchedPath, out var launchError))
                {
                    Console.WriteLine($"Game process launched: {launchedPath}");
                }
                else
                {
                    Console.WriteLine($"No launchable bundled executable found. {launchError}");
                    Console.WriteLine("Falling back to embedded runtime engine mode...");
                    var engineCode = await CliEngineRunner.RunAfterSmokeAsync(validation);
                    if (engineCode != 0)
                    {
                        Console.WriteLine($"Embedded runtime engine exited with code: {engineCode}");
                    }
                }
                WaitForEnter();
                continue;
            }

            Console.WriteLine("Invalid menu.");
            WaitForEnter();
        }
    }

    private static async Task<int> RunZenityAsync(GameDataValidationResult validation, int strictRetries, bool autoPlay, bool allowPeLaunch)
    {
        while (true)
        {
            var prereq = GameDataLayout.CheckRuntimePrerequisites(validation.RootDir, validation.CsvDir, validation.ErbDir);
            var gameBase = GameBaseInspector.Inspect(prereq.GamebaseCsvPath);
            var title = gameBase.Title ?? "(unknown)";
            var author = gameBase.Author ?? "(unknown)";
            var version = gameBase.VersionRaw ?? "(unknown)";
            var message = $"Game root: {validation.RootDir}\nTitle: {title}\nAuthor: {author}\nVersion: {version}\n\nSelect an action.";

            var selection = ShowZenityMenu(
                "Emuera Linux Launcher",
                message,
                ("1", "Runtime smoke check"),
                ("2", "Strict smoke check"),
                ("3", "New game (launch if executable found)"),
                ("4", "Load game (launch if executable found)"),
                ("0", "Exit")
            );

            if (selection == null || selection == "0")
                return 0;

            if (selection == "1")
            {
                var code = await RunSmokeAsync(validation, strict: false, strictRetries: strictRetries);
                ShowZenityResult("Smoke", code);
                continue;
            }

            if (selection == "2")
            {
                var code = await RunSmokeAsync(validation, strict: true, strictRetries: strictRetries);
                ShowZenityResult("Strict smoke", code);
                continue;
            }

            if (selection is "3" or "4")
            {
                var smoke = await RunSmokeAsync(validation, strict: false, strictRetries: strictRetries);
                if (smoke != 0)
                {
                    ShowZenityError("Launch blocked", $"Launch blocked: smoke gate failed (exit code {smoke}).");
                    continue;
                }

                var launchSelection = SelectLaunchTargetWithZenity(validation.RootDir, allowPeLaunch);
                if (launchSelection.Cancelled)
                    continue;

                if (TryLaunchBundledExecutable(validation.RootDir, launchSelection.PreferredExecutable, allowPeLaunch, out var launchedPath, out var launchError))
                {
                    ShowZenityInfo("Launch started", $"Game process launched:\n{launchedPath}");
                }
                else
                {
                    ShowZenityInfo("Launch fallback", $"No launchable bundled executable found.\n{launchError}\n\nStarting embedded runtime engine mode.");
                    var engineCode = await CliEngineRunner.RunAfterSmokeAsync(validation);
                    if (engineCode != 0)
                        ShowZenityError("Runtime engine exit", $"Embedded runtime engine exited with code: {engineCode}");
                }
                continue;
            }
        }
    }

    private static async Task<int> RunSmokeAsync(GameDataValidationResult validation, bool strict, int strictRetries)
    {
        var prereq = GameDataLayout.CheckRuntimePrerequisites(validation.RootDir, validation.CsvDir, validation.ErbDir);
        if (prereq.MissingRequiredPaths.Count > 0)
            return 4;
        if (strict && prereq.MissingRecommendedPaths.Count > 0)
            return 6;

        var gate = await RuntimePreloadBootstrap.RunStaticGateAsync(validation.CsvDir, validation.ErbDir);
        if (strict && gate.Preload.WarningCount > 0)
        {
            for (var attempt = 1; attempt <= strictRetries; attempt++)
            {
                await Task.Delay(100);
                gate = await RuntimePreloadBootstrap.RunStaticGateAsync(validation.CsvDir, validation.ErbDir);
                if (gate.Preload.WarningCount == 0)
                    break;
            }
        }

        if (!gate.Passed)
            return 3;
        if (strict && gate.Preload.WarningCount > 0)
            return 7;
        if (!gate.BootProfile.IsValid)
            return 5;
        return 0;
    }

    private static bool TryLaunchBundledExecutable(
        string rootDir,
        string? preferredExecutable,
        bool allowPeLaunch,
        out string launchedPath,
        out string error)
    {
        launchedPath = string.Empty;
        error = string.Empty;
        var candidates = GetBundledExecutableCandidates(rootDir, preferredExecutable);
        if (candidates.Count == 0)
        {
            error = "No bundled executable candidates matched launch filters.";
            return false;
        }

        var launchErrors = new List<string>();
        var wine = FindExecutableInPath("wine");
        var mono = FindExecutableInPath("mono");

        foreach (var candidate in candidates)
        {
            var kind = DetectBinaryKind(candidate);
            if (kind == BinaryKind.Unknown)
                continue;
            if ((kind == BinaryKind.Elf || kind == BinaryKind.Script) && IsLikelyCliHostBinary(candidate))
            {
                launchErrors.Add($"{Path.GetFileName(candidate)}: skipped CLI host binary");
                continue;
            }
            try
            {
                if (kind == BinaryKind.Pe)
                {
                    if (!allowPeLaunch)
                    {
                        launchErrors.Add($"{Path.GetFileName(candidate)}: PE launch disabled (use --allow-pe-launch)");
                        continue;
                    }
                    var launched = false;
                    if (!string.IsNullOrWhiteSpace(mono))
                    {
                        var psiMono = new ProcessStartInfo
                        {
                            FileName = mono,
                            WorkingDirectory = rootDir,
                            UseShellExecute = false,
                        };
                        psiMono.ArgumentList.Add(candidate);
                        psiMono.Environment["MONO_IOMAP"] = "all";
                        Process.Start(psiMono);
                        launchedPath = candidate;
                        error = string.Empty;
                        launched = true;
                    }

                    if (!launched && !string.IsNullOrWhiteSpace(wine))
                    {
                        var psiWine = new ProcessStartInfo
                        {
                            FileName = wine,
                            WorkingDirectory = rootDir,
                            UseShellExecute = false,
                        };
                        psiWine.ArgumentList.Add(candidate);
                        Process.Start(psiWine);
                        launchedPath = candidate;
                        error = string.Empty;
                        launched = true;
                    }

                    if (launched)
                        return true;
                    continue;
                }

                if (!HasExecutePermission(candidate) &&
                    !EnsureExecutePermission(candidate, out var chmodError))
                {
                    launchErrors.Add($"{Path.GetFileName(candidate)}: missing execute permission ({chmodError})");
                    continue;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    WorkingDirectory = rootDir,
                    UseShellExecute = false,
                };
                Process.Start(psi);
                launchedPath = candidate;
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                launchErrors.Add($"{Path.GetFileName(candidate)}: {ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(mono) || !string.IsNullOrWhiteSpace(wine))
            error = "Candidate executables failed to start. " + string.Join(" | ", launchErrors.Take(4));
        else
            error = "Candidate executables failed to start and neither 'mono' nor 'wine' is installed for Windows PE files. " + string.Join(" | ", launchErrors.Take(4));
        return false;
    }

    private static (bool Cancelled, string? PreferredExecutable) SelectLaunchTargetWithZenity(string rootDir, bool allowPeLaunch)
    {
        var launchable = GetLaunchableExecutableCandidates(rootDir, allowPeLaunch);
        if (launchable.Count == 0)
            return (false, null);

        if (launchable.Count == 1)
            return (false, launchable[0].Name);

        var args = new List<string>
        {
            "--list",
            "--title", "Select executable",
            "--text", "Choose bundled executable to launch.",
            "--column", "File",
            "--column", "Kind",
            "--column", "Path"
        };
        foreach (var item in launchable)
        {
            args.Add(item.Name);
            args.Add(item.KindLabel);
            args.Add(item.Path);
        }

        var result = RunZenity(args);
        if (!result.Success)
            return (true, null);

        var selected = result.Output.Trim();
        if (string.IsNullOrWhiteSpace(selected))
            return (true, null);
        var selectedName = selected.Split('|', StringSplitOptions.TrimEntries)[0];
        return (false, selectedName);
    }

    private static (bool Cancelled, string? PreferredExecutable) SelectLaunchTargetInConsole(string rootDir, bool allowPeLaunch)
    {
        var launchable = GetLaunchableExecutableCandidates(rootDir, allowPeLaunch);
        if (launchable.Count == 0)
            return (false, null);
        if (launchable.Count == 1)
            return (false, launchable[0].Name);

        Console.WriteLine();
        Console.WriteLine("Select executable to launch:");
        for (var i = 0; i < launchable.Count; i++)
        {
            var item = launchable[i];
            Console.WriteLine($"[{i + 1}] {item.Name} ({item.KindLabel})");
        }
        Console.WriteLine("[0] Cancel");
        Console.Write("Launch target [1]: ");

        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(input))
            return (false, launchable[0].Name);
        if (input == "0")
            return (true, null);
        if (!int.TryParse(input, out var index) || index < 1 || index > launchable.Count)
            return (false, launchable[0].Name);
        return (false, launchable[index - 1].Name);
    }

    private static List<(string Path, string KindLabel, string Name)> GetLaunchableExecutableCandidates(string rootDir, bool allowPeLaunch)
    {
        var candidates = GetBundledExecutableCandidates(rootDir, preferredExecutable: null);
        if (candidates.Count == 0)
            return [];

        var wine = FindExecutableInPath("wine");
        var mono = FindExecutableInPath("mono");
        var peLaunchAvailable = !string.IsNullOrWhiteSpace(mono) || !string.IsNullOrWhiteSpace(wine);

        var launchable = new List<(string Path, string KindLabel, string Name)>();
        foreach (var candidate in candidates)
        {
            var kind = DetectBinaryKind(candidate);
            if (kind == BinaryKind.Unknown)
                continue;
            if ((kind == BinaryKind.Elf || kind == BinaryKind.Script) && IsLikelyCliHostBinary(candidate))
                continue;
            if (kind == BinaryKind.Pe && (!allowPeLaunch || !peLaunchAvailable))
                continue;

            var kindLabel = kind switch
            {
                BinaryKind.Elf => "ELF",
                BinaryKind.Script => "SCRIPT",
                BinaryKind.Pe => "PE",
                _ => "UNKNOWN"
            };
            launchable.Add((candidate, kindLabel, Path.GetFileName(candidate)));
        }

        return launchable;
    }

    private static bool HasViableLaunchCandidate(string rootDir, string? preferredExecutable, bool allowPeLaunch)
    {
        if (!string.IsNullOrWhiteSpace(preferredExecutable))
            return GetLaunchableExecutableCandidates(rootDir, allowPeLaunch)
                .Any(item => string.Equals(item.Name, preferredExecutable, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(item.Path, Path.GetFullPath(Path.IsPathRooted(preferredExecutable) ? preferredExecutable : Path.Combine(rootDir, preferredExecutable)), StringComparison.OrdinalIgnoreCase));

        return GetLaunchableExecutableCandidates(rootDir, allowPeLaunch).Count > 0;
    }

    private static List<string> GetBundledExecutableCandidates(string rootDir, string? preferredExecutable)
    {
        var selfPath = Environment.ProcessPath;
        var selfFullPath = string.IsNullOrWhiteSpace(selfPath) ? null : Path.GetFullPath(selfPath);
        var selfFileName = string.IsNullOrWhiteSpace(selfFullPath) ? null : Path.GetFileName(selfFullPath);
        var selfStem = string.IsNullOrWhiteSpace(selfFullPath) ? null : Path.GetFileNameWithoutExtension(selfFullPath);

        var candidates = Directory.GetFiles(rootDir)
            .Where(path =>
            {
                var full = Path.GetFullPath(path);
                if (!string.IsNullOrWhiteSpace(selfFullPath) &&
                    string.Equals(full, selfFullPath, StringComparison.OrdinalIgnoreCase))
                    return false;

                var name = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(name))
                    return false;
                if (!string.IsNullOrWhiteSpace(selfFileName) &&
                    string.Equals(name, selfFileName, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (name.StartsWith("Run-Emuera-Linux", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (name.Contains("Emuera.Cli", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (!string.IsNullOrWhiteSpace(selfStem) &&
                    string.Equals(Path.GetFileNameWithoutExtension(name), selfStem, StringComparison.OrdinalIgnoreCase))
                    return false;
                return name.Contains("Emuera", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("XEra", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(path => DetectBinaryKind(path) == BinaryKind.Elf)
            .ThenByDescending(path => DetectBinaryKind(path) == BinaryKind.Script)
            .ThenByDescending(path => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(preferredExecutable))
            return candidates;

        var preferredPath = Path.IsPathRooted(preferredExecutable)
            ? Path.GetFullPath(preferredExecutable)
            : Path.GetFullPath(Path.Combine(rootDir, preferredExecutable));

        var preferredPaths = new List<string> { preferredPath };
        var preferredExt = Path.GetExtension(preferredPath);
        if (string.Equals(preferredExt, ".exe", StringComparison.OrdinalIgnoreCase))
        {
            var noExt = Path.Combine(
                Path.GetDirectoryName(preferredPath) ?? rootDir,
                Path.GetFileNameWithoutExtension(preferredPath));
            preferredPaths.Add(Path.GetFullPath(noExt));
        }

        var preferredIndex = candidates.FindIndex(path =>
        {
            var full = Path.GetFullPath(path);
            return preferredPaths.Any(pref =>
                string.Equals(full, pref, StringComparison.OrdinalIgnoreCase));
        });

        if (preferredIndex >= 0)
        {
            var preferred = candidates[preferredIndex];
            candidates.RemoveAt(preferredIndex);
            candidates.Insert(0, preferred);
        }

        return candidates;
    }

    private static bool IsLikelyCliHostBinary(string path)
    {
        const string marker = "Emuera Linux CLI bootstrap host";
        const int maxScanBytes = 4 * 1024 * 1024;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var len = (int)Math.Min(maxScanBytes, fs.Length);
            if (len <= 0)
                return false;
            var buffer = new byte[len];
            var read = fs.Read(buffer, 0, len);
            if (read <= 0)
                return false;
            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
            return text.Contains(marker, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasExecutePermission(string path)
    {
        if (OperatingSystem.IsWindows())
            return true;
        try
        {
            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static bool EnsureExecutePermission(string path, out string error)
    {
        error = string.Empty;
        if (OperatingSystem.IsWindows())
            return true;
        try
        {
            var mode = File.GetUnixFileMode(path);
            var execMask = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if ((mode & execMask) != 0)
                return true;
            File.SetUnixFileMode(path, mode | execMask);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static BinaryKind DetectBinaryKind(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> header = stackalloc byte[4];
            var read = fs.Read(header);
            if (read >= 4 && header[0] == 0x7f && header[1] == (byte)'E' && header[2] == (byte)'L' && header[3] == (byte)'F')
                return BinaryKind.Elf;
            if (read >= 2 && header[0] == (byte)'M' && header[1] == (byte)'Z')
                return BinaryKind.Pe;
            if (read >= 2 && header[0] == (byte)'#' && header[1] == (byte)'!')
                return BinaryKind.Script;
        }
        catch
        {
            // Non-fatal detection failure.
        }

        return BinaryKind.Unknown;
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

    private static bool CanUseZenityLauncher()
    {
        if (!OperatingSystem.IsLinux())
            return false;
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
            return false;
        return !string.IsNullOrWhiteSpace(FindExecutableInPath("zenity"));
    }

    private static string? ShowZenityMenu(string title, string text, params (string Key, string Label)[] items)
    {
        var args = new List<string>
        {
            "--list",
            "--title", title,
            "--text", text,
            "--column", "Key",
            "--column", "Action"
        };
        foreach (var item in items)
        {
            args.Add(item.Key);
            args.Add(item.Label);
        }

        var result = RunZenity(args);
        if (!result.Success)
            return null;
        return string.IsNullOrWhiteSpace(result.Output)
            ? null
            : result.Output.Trim();
    }

    private static void ShowZenityResult(string label, int code)
    {
        var text = $"{label} exit code: {code}\nStatus: {(code == 0 ? "PASS" : "FAIL")}";
        if (code == 0)
            ShowZenityInfo($"{label} result", text);
        else
            ShowZenityError($"{label} result", text);
    }

    private static void ShowZenityInfo(string title, string text)
    {
        if (RunZenity(new List<string> { "--info", "--title", title, "--text", text }).Success)
            return;
        Console.WriteLine($"[{title}] {text}");
    }

    private static void ShowZenityError(string title, string text)
    {
        if (RunZenity(new List<string> { "--error", "--title", title, "--text", text }).Success)
            return;
        Console.Error.WriteLine($"[{title}] {text}");
    }

    private static (bool Success, string Output) RunZenity(List<string> arguments)
    {
        var zenity = FindExecutableInPath("zenity");
        if (string.IsNullOrWhiteSpace(zenity))
            return (false, string.Empty);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = zenity,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            foreach (var argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode == 0, output);
        }
        catch
        {
            return (false, string.Empty);
        }
    }

    private enum BinaryKind
    {
        Unknown,
        Elf,
        Pe,
        Script,
    }

    private static void PrintHeader(string rootDir)
    {
        Console.Clear();
        Console.WriteLine("==============================================");
        Console.WriteLine(" Emuera Linux Launcher (Interactive GUI Stage)");
        Console.WriteLine("==============================================");
        Console.WriteLine($"Game root: {rootDir}");
        Console.WriteLine();
    }

    private static void PrintResult(string label, int code)
    {
        Console.WriteLine($"{label} exit code: {code}");
        Console.WriteLine(code == 0 ? "Status: PASS" : "Status: FAIL");
    }

    private static void WaitForEnter()
    {
        Console.WriteLine();
        Console.Write("Press Enter to continue...");
        Console.ReadLine();
    }
}
