# Linux Porting Checklist (Stage 1)

## Applied changes

- `Emuera.RuntimeCore/Runtime/Utils/WinInput.cs`
  - Added OS guard for `GetKeyState`.
  - On non-Windows, returns a neutral value (`0`) instead of calling `user32.dll`.

- `Emuera.RuntimeCore/Runtime/Utils/WinmmTimer.cs`
  - Added platform-aware timer behavior.
  - Keeps `winmm` implementation on Windows.
  - Uses `Stopwatch` fallback on non-Windows.

- `Emuera/Runtime/Utils/WebPWrapper.cs`
  - Removed dependency on `kernel32!CopyMemory` by replacing it with managed unsafe memory copy.
  - Added `DllImportResolver` to map WebP native library names across platforms.
  - Linux candidates: `libwebp.so`, `libwebp.so.7`, `libwebp.so.6`.
  - Resolver now probes local directories first (`AppContext.BaseDirectory`, current directory, optional `EMUERA_LIBWEBP_DIR`) before global/system library lookup for better offline standalone behavior.

- `Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs`
  - Added runtime hooks (`DoEvents`, info message, yes/no prompt, product version).
  - Runtime layer can call these hooks without directly depending on WinForms APIs.
  - Added message resolver hook (`ResolveMessage`) so RuntimeCore can request host-localized text by key.
  - Added window-title hook (`SetWindowTitle`) so runtime variable writes can avoid direct global console access.
  - Added console-state hooks (`GetConsoleLineNo`, `GetConsoleClientHeight`) for escaped/div rendering paths without direct global mediator lookup.

- Runtime layer replacements
  - Replaced direct `Application.DoEvents` calls with `RuntimeHost.DoEvents`.
  - Replaced selected direct `MessageBox.Show` calls with `RuntimeHost.ShowInfo` / `RuntimeHost.ConfirmYesNo`.
  - Replaced `Application.ProductVersion` usage in `AssemblyData` with `RuntimeHost.GetProductVersion`.
  - Replaced `Preload` direct warning dispatch (`ParserMediator.Warn`) with host-injected warning hook (`Preload.WarnHook`), with `RuntimeHost.ShowInfo` fallback.
  - Decoupled `EraStreamReader` from `ParserMediator.RenameDic` static access by using constructor-injected rename dictionary.
  - Decoupled `EraStreamReader` from `Config.ReplaceContinuationBR` static access by using constructor-injected continuation replacement string.
  - `EraStreamReader.OpenOnCache` now uses `Preload.TryGetFileLines` and fails gracefully when a file is not cached.
  - Restored ERH rename/continuation behavior by passing rename dictionary and continuation replacement into ERH loader reader construction.
  - Moved `Dialog` abstraction to `Emuera.RuntimeCore/Runtime/Utils/Dialog.cs` and routed it through `RuntimeHost` hooks.

- `Emuera/Program.cs`
  - WinForms host now wires `RuntimeHost` hooks to actual UI operations.
  - WinForms host maps save-related message keys (`save.invalid`, `save.invalid_num`, `save.not_impl`) to `Lang.Error` texts.

- `Emuera.Cli` (new project)
  - Added a Linux-native .NET CLI bootstrap host (`net10.0`).
  - Supports `--game-dir`, `--exe-dir` (`--ExeDir`/`-exedir` compatible), `--verify-only`, `--preload-only`, `--scan-erh-only`, `--scan-erb-pp-only`, `--scan-erb-cont-only`, `--gate-only`, `--run-smoke-only`, `--strict-smoke-only`, `--strict-retries`, `--interactive-gui`, `--play-like`, `--run-engine`, `--launch-bundled`, `--launch-target`, and `--allow-pe-launch`.
  - Validates required game data folders (`CSV/`, `ERB/`) without WinForms dependency.
  - Game directory resolution order: `--game-dir` > `--exe-dir` > `EMUERA_GAME_DIR` > executable directory (`AppContext.BaseDirectory`) > current directory.
  - `--preload-only` executes RuntimeCore `Preload` to cache ERB/CSV data and reports target/cached/skipped counts + warning count + elapsed time.
  - Also reports extension-level stats (`.csv`, `.erb`, `.erh`, `.erd`, `.als`) for quick data quality checks.
  - `--scan-erh-only` performs preload first, then runs a lightweight ERH directive-prefix scan (non-comment, non-brace-block lines should start with `#`) and reports issue list.
  - `--scan-erb-pp-only` performs preload first, then validates ERB preprocessor block matching (`[IF]/[ELSEIF]/[ELSE]/[ENDIF]`, `[SKIPSTART]/[SKIPEND]`).
  - `--scan-erb-cont-only` performs preload first, then validates ERB continuation block structure (`{ ... }`) including unexpected end/nested start/unclosed block cases.
  - Static gate now also checks ERB entry-label presence (`@...`) and reports total label count + entry-like label count (`TITLE`, `SYSTEM_TITLE`, `EVENTFIRST`, `EVENTLOAD`, `START`) + boot profile status.
  - `--gate-only` runs preload + all static scans as one gate and returns non-zero (`3`) if issues are found.
  - `--run-smoke-only` checks runtime prerequisites (`CSV/GAMEBASE.CSV`, optional `emuera.config`, recommended `ERB/TITLE.ERB` + `ERB/SYSTEM.ERB`), inspects GAMEBASE metadata (title/author/version raw), and then runs preload + static gate (`4` for required-missing, `3` for gate issues, `5` for missing boot-entry profile).
  - `--strict-smoke-only` extends `--run-smoke-only` and fails on recommended-missing files (`6`) or preload warnings (`7`).
  - `--strict-retries` controls strict-mode retry count when preload warnings occur (default `2`).
  - `--interactive-gui` opens an interactive text-GUI launcher on Linux (menu-driven smoke/strict checks + bundled executable launch attempt).
- `--play-like` runs smoke first, then tries immediate bundled game launch; if auto-launch fails, it falls back to the interactive launcher menu.
- `--play-like` now exits with launch-failure code (`8`) instead of falling back to menu when stdin is non-interactive and auto-launch fails.
  - `--run-engine` runs the embedded runtime engine directly in terminal mode (smoke precheck + runtime init + input loop) without external launcher binary dependency.
  - `--run-engine` path now maps runtime output/style/html/image/display-history hooks and text-box/hotkey/mouse/CBG/background hooks to CLI-safe in-memory fallbacks so WinForms-only hook paths no longer break Linux terminal execution.
  - `--run-engine` now also maps runtime reload/title-error/output-log hooks (`RELOADERB` completion, `OUTPUTLOG`, title-error path, timer-stop/generation markers) to CLI behavior so script-side log/error flows no longer stay as no-op.
  - `--run-engine` CLI bridge now tracks tooltip/CtrlZ host hooks and temporary-line state (`IsLastLineTemporary`) so runtime-side tooltip/undo/log branches no longer silently collapse to static defaults.
  - `--run-engine` interactive input loop now handles key-only waits (`ENTER` / `ANYKEY` / `WAIT`) via direct key capture, reducing line-input-only behavior mismatch with WinForms.
  - `--run-engine` primitive input path (`INPUTMOUSEKEY`) now routes through `InputResult5` packets (`key:...`, `mouse:...`, `wheel:...`, CSV tuple) and records transient virtual-key state for `GETKEYSTATE` compatibility.
  - Linux/headless UI key parsing now uses shared `VirtualKeyMap` (WinForms-style key aliases + virtual-key constants + console-key mapping).
  - `--run-engine` now enforces interactive `InputRequest.Timelimit` for line/key input waits and marks timeout state (`IsTimeOut`) so TINPUT-style branches can follow timeout/default flow more closely.
  - `--launch-bundled` runs smoke checks and then launches a bundled executable from game root (returns non-zero if launch fails).
  - `--launch-target <file-or-abs-path>` prioritizes a specific executable candidate when used with `--launch-bundled`.
  - `--launch-target` now also supports `.exe`-to-stem matching (for example, targeting `Foo.exe` can resolve `Foo` Linux binary first when both exist).
  - `--allow-pe-launch` enables PE (`.exe`) launch fallback via `mono`/`wine`; default launch behavior is Linux-native candidate only.
  - Launcher now attempts to auto-apply execute permission (`chmod +x` equivalent via `File.SetUnixFileMode`) for Linux/script candidates before failing launch.
  - Running the Linux binary with no options now defaults to interactive launcher mode.
  - When stdin is redirected (non-interactive execution), no-option startup now falls back to smoke check mode and exits cleanly.
  - Wires `RuntimeHost` hooks for console-safe dialog/prompt behavior in Linux CLI mode.
  - Interactive launcher can try Windows PE executables via `mono` first (with `MONO_IOMAP=all`), then `wine` fallback when `--allow-pe-launch` is enabled.

- `Emuera.RuntimeCore` (new project)
  - Added a shared runtime/core library (`net10.0`).
  - Moved `RuntimeHost` into RuntimeCore and referenced it from both `Emuera` and `Emuera.Cli`.
  - Added `GameDataLayout` validator API used by CLI.
  - Moved `AssemblyData` (`Sys.cs`) into RuntimeCore as the first actual runtime utility extraction.
  - Moved `WinInput` and `WinmmTimer` into RuntimeCore (platform utility extraction).
  - Moved additional engine utilities: `CharStream`, `MTRandom(SFMT)`, `RegexFactory`, `LangManager`, `EmueraException`, `EraBinaryDataReader`, `EraBinaryDataWriter`, `EraDataStream`.
  - Moved `Preload` into RuntimeCore and replaced its warning path with `Preload.WarnHook` + `RuntimeHost.ResolveMessage`.
  - `Preload` no longer stores failed-read entries in cache (`null` line arrays are skipped).
  - Added cache snapshot access (`GetCachedFilePathsSnapshot`) and safe cache probing (`TryGetFileLines`) for host-side diagnostics.
  - Hardened `Preload` file-lock fallback path to avoid crashing on repeated `IOException` during fallback read.
  - `EraBinaryDataWriter` no longer reads `Config.Config` directly; compression flags are passed from caller.
  - `EraDataWriter` no longer reads `Config.Config.SaveEncode` directly; save encoding is passed from caller.
  - Added `InternalsVisibleTo(\"Emuera\")` for incremental extraction while preserving existing internal access patterns.

- Runtime/UI decoupling progress (Stage 1.5)
  - Added `Emuera/Runtime/Script/IScriptConsole.cs` as a script-loader/parser output abstraction.
  - `EmueraConsole` now implements `IScriptConsole`.
  - Migrated these runtime paths to depend on `IScriptConsole` instead of concrete `EmueraConsole`:
    - `ParserMediator` initialization/warning output
    - `ErhLoader`
    - `ErbLoader`
    - `ConstantData.LoadData`
    - `LogicalLineParser` parse entry points
  - This reduces WinForms type coupling in load/parse phase and prepares next extraction of `Process` execution loop.
  - Added `IDebugConsole` and switched `ProcessState` debug-trace dependency from `EmueraConsole` to `IDebugConsole`.
  - Added `IProcessConsole` and switched `Process` main constructor dependency from concrete `EmueraConsole` to `IProcessConsole`.
  - Promoted/normalized several console members (`IsRunning`, `MesSkip`, `noOutputLog`, `updatedGeneration`, `PrintErrorButton`) to interface-compatible surface for runtime decoupling.
  - Replaced runtime-side direct `Window` access (`ApplyTextBoxChanges`, text-box move/get/set, hotkey state, mouse pointing refresh) with `IProcessConsole` wrapper methods.
  - Removed runtime-layer `System.Windows.Forms` direct dependency in `FunctionMethodCreator`; WinForms cursor/window access now stays in `EmueraConsole` implementation.
  - Removed `ConsoleDivPart` direct `MainPicBox` dependency by using `IProcessConsole.ClientHeight`.
  - Removed `IProcessConsole` exposure of WinForms `MainWindow`; runtime/script layer no longer needs `MainWindow` type in the console abstraction.
  - Removed runtime direct `GlobalStatic.Console` references in variable assignment paths (`WINDOW_TITLE` now uses `ExpressionMediator.Console` or `RuntimeHost.SetWindowTitle`).
  - Switched array-evaluator runtime helpers (`SUMARRAY`, `MATCH`, `MAXARRAY`, `INRANGEARRAY`, `STRJOIN` path) to accept `ExpressionMediator` directly instead of reading `GlobalStatic.EMediator`.
  - Switched instruction argument parsing path to mediator-injection (`ArgumentParser.SetArgumentTo(..., exm)`) across process/loader/plugin/debug-command call sites.
  - Removed remaining `GlobalStatic.EMediator` runtime/debug references and deleted the global field; mediator access now comes from `Process.ExpressionMediator`.
  - Introduced `IRuntimeConsole` and switched `Process`/`ExpressionMediator` to depend on it (kept `IProcessConsole : IRuntimeConsole` for compatibility), preparing the next split between runtime-essential and host/UI-specific console members.
  - Started shrinking runtime surface by moving host-only members (`AlwaysRefresh`, `GetLineNo`, `MoveMouse`) back to `IProcessConsole`.
  - Continued runtime-surface trim by routing plugin-only host APIs (`PrintPlainwithSingleLine`, `ClearDisplay`, `forceStopTimer`) through `IProcessConsole` fallback checks, allowing their removal from `IRuntimeConsole`.
  - Moved tooltip host APIs (`CustomToolTip`, `SetToolTip*`) from `IRuntimeConsole` to `IProcessConsole` and switched instruction handlers to safe `IProcessConsole` casts.
  - Moved CBG/background host APIs (`AddBackgroundImage`, `CBG_*`, `ClearBackgroundImage`, `RemoveBackground`) from `IRuntimeConsole` to `IProcessConsole` and switched runtime call sites to `IProcessConsole`-cast paths.
  - Moved text-box/hotkey/mouse-pointing host APIs (`ApplyTextBoxChanges`, `Reset/Set/GetTextBox*`, `HotkeyState*`, `RefreshMousePointingState`) from `IRuntimeConsole` to `IProcessConsole` and switched runtime call sites to `IProcessConsole`-cast paths.
  - Moved additional host/UI-only render/input members (`EmptyLine`, `IsActive`, `ClientWidth/ClientHeight`, `Redraw`, `PointingSring`, `bgColor`, `GetDisplayLines`, `PopDisplayingLines`, `setRedrawTimer`, `bitmapCacheEnabledForNextLine`) from `IRuntimeConsole` to `IProcessConsole`; runtime access in function methods now goes through safe `AsProcessConsole(exm)` fallbacks.
  - Moved display-state internals (`PrintBuffer`, `DisplayLineList`, `EscapedParts`, `LastButtonGeneration`) from `IRuntimeConsole` to `IProcessConsole`; button-input instructions and display-line function paths now use safe `IProcessConsole` access.
  - Moved alignment control (`Alignment`) from `IRuntimeConsole` to `IProcessConsole`; title rendering / alignment instruction / current-alignment function now use `IProcessConsole`-safe paths.
  - Moved style-control surface (`StringStyle`, `UseUserStyle`, `UseSetColorStyle`, `SetStringStyle`, `SetBgColor`, `SetFont`) from `IRuntimeConsole` to `IProcessConsole`; process/instruction/function/plugin call sites now route through safe `IProcessConsole` checks with default-style fallbacks where needed.
  - Removed leftover `System.Drawing` import from `ProcessState` runtime script path and tightened audit guardrails:
    - `scripts/audit-runtime-ui-coupling.sh` now also fails when runtime script layer imports `System.Drawing` without explicit allowlist.
  - Added runtime color RGB cache fields in `Config` (`ForeColorRgb`, `BackColorRgb`, `FocusColorRgb`, `LogColorRgb`, `RikaiColorBackRgb`, `RikaiColorTextRgb`) and switched runtime reset-color instructions to RGB properties (`Config.ForeColorRgb`, `Config.BackColorRgb`) instead of `Color.ToArgb` calls.
  - Extended `PluginManager` with runtime-neutral helpers while keeping legacy API compatibility:
    - `SetBgColor(Color)` now routes through new `SetBgColorRgb(int)`.
    - Added `GetMouseX()` / `GetMouseY()` so plugins can avoid `System.Drawing.Point` when desired.
    - Added `GetMousePositionXY()` returning runtime-neutral `RuntimePoint` (`Emuera.RuntimeCore/Runtime/Utils/RuntimePoint.cs`).
    - Removed `PluginManager` dependency on `Color.ToArgb` (RGB extraction now uses channel fields).
  - Added runtime-neutral mouse position access to console bridge (`IProcessConsole.GetMousePositionXY`) and switched runtime-bridge mouse functions (`MOUSEX`, `MOUSEY`) to use `RuntimePoint`.
  - Added runtime-neutral mouse move bridge (`IProcessConsole.MoveMouseXY`) and exposed it in `PluginManager` as `MoveMouse(int x, int y)` to avoid `Point` dependency in plugin-side control paths.
  - Added runtime-neutral color value object `RuntimeColor` (`Emuera.RuntimeCore/Runtime/Utils/RuntimeColor.cs`) and exposed parallel runtime color fields in `Config` (`ForeColorRuntime`, `BackColorRuntime`, `FocusColorRuntime`, `LogColorRuntime`, `RikaiColorBackRuntime`, `RikaiColorTextRuntime`) to prepare full `System.Drawing.Color` decoupling.
  - Switched runtime reset-color instruction path to use runtime-neutral color objects (`Config.ForeColorRuntime`, `Config.BackColorRuntime`) and convert via `ToRgb24()` at the bridge edge.
  - Added runtime-neutral font descriptor `RuntimeFontSpec` (`Emuera.RuntimeCore/Runtime/Utils/RuntimeFontSpec.cs`) and exposed `Config.DefaultFontSpec` so runtime-side consumers can read font settings without requiring `System.Drawing.Font`.

- `Emuera/Emuera.csproj`
  - Added `EnableWindowsTargeting=true` to allow Windows-target build validation from Linux hosts.
  - Updated audio backend selection so non-Windows hosts use NAudio path by default.

## Build validation

Validated on Linux host with .NET 10 SDK using:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 NUGET_PACKAGES=/tmp/.nuget/packages \
  dotnet build /home//tw/emuera.em/Emuera/Emuera.csproj -c Debug-NAudio
```

Result:
- Build: success
- Errors: 0
- Warnings: many pre-existing analyzer warnings

`Emuera.Cli` validation:

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 NUGET_PACKAGES=/tmp/.nuget/packages \
  dotnet run --project /home//tw/emuera.em/Emuera.Cli/Emuera.Cli.csproj -- \
  --game-dir /home//tw/eraTWKR --verify-only
```

Output: game directory check passed (`CSV/`, `ERB/` detected).

Runtime preload smoke:

```bash
/home//tw/emuera.em/artifacts/linux-x64-cli/Emuera.Cli \
  --game-dir /home//tw/eraTWKR --preload-only
```

Output: preload completed with preload stats (example: target `1999`, cached `1999`, skipped `0`, warnings displayed) and extension-level breakdown.

ERH directive-prefix smoke:

```bash
/home//tw/emuera.em/artifacts/linux-x64-cli/Emuera.Cli \
  --game-dir /home//tw/eraTWKR --scan-erh-only
```

Output: ERH file count and issue count (example: `2` issues), with sample file:line diagnostics.

ERB preprocessor-block smoke:

```bash
/home//tw/emuera.em/artifacts/linux-x64-cli/Emuera.Cli \
  --game-dir /home//tw/eraTWKR --scan-erb-pp-only
```

Output: ERB file count, detected preprocessor directive count, and block-matching issue count.

ERB continuation-block smoke:

```bash
/home//tw/emuera.em/artifacts/linux-x64-cli/Emuera.Cli \
  --game-dir /home//tw/eraTWKR --scan-erb-cont-only
```

Output: ERB file count, continuation block count, and structural issue count.

Static gate smoke:

```bash
/home//tw/emuera.em/artifacts/linux-x64-cli/Emuera.Cli \
  --game-dir /home//tw/eraTWKR --gate-only
```

Output: preload + ERH/ERB static scan summary with `Total issues` and `Gate passed`.
Exit code: `0` when gate passes, `3` when any static issue exists.

Linux apphost publish (self-contained single-file):

```bash
/home//tw/emuera.em/scripts/publish-linux-cli.sh
```

- Script hardening:
  - publish path is guarded with a file lock (`flock`) to reduce concurrent publish collisions.
  - output directory is cleaned per retry attempt to reduce stale bundle state issues.

Output path:
- `/home//tw/emuera.em/artifacts/linux-x64-cli/Emuera.Cli`

Static gate helper script:

```bash
bash /home//tw/emuera.em/scripts/run-linux-static-gate.sh /home//tw/eraTWKR
```

Strict smoke helper script:

```bash
bash /home//tw/emuera.em/scripts/run-linux-strict-smoke.sh /home//tw/eraTWKR
```

CI-style Linux porting check (build + publish + gate + deploy-smoke):

```bash
bash /home//tw/emuera.em/scripts/ci-linux-porting.sh /home//tw/eraTWKR
```

CI script hardening:
- Build path now runs `restore -> clean -> build --no-restore` with retry wrapper to reduce intermittent lock/corrupt intermediate issues on Linux CI hosts.
- CI now includes offline standalone verification (`verify-linux-offline-standalone.sh`) after deploy.

CI strict mode (includes strict smoke):

```bash
bash /home//tw/emuera.em/scripts/ci-linux-porting.sh /home//tw/eraTWKR --strict
```

Offline standalone verification helper:

```bash
bash /home//tw/emuera.em/scripts/verify-linux-offline-standalone.sh /home//tw/eraTWKR
```

- Verifies deployed target binary exists and is executable.
- Verifies target binary format is Linux ELF.
- Verifies shared-library resolution via `ldd` (fails on unresolved `not found` entries).
- Verifies standalone publish shape by rejecting `libhostfxr` / `libhostpolicy` / `libcoreclr` dependencies.
- When target name ends with `.exe`, also verifies same-stem Linux executable exists and is executable.
- Verifies desktop launcher file exists and checks expected launch mode (`Exec=./Run-Emuera-Linux.sh --interactive-gui`, `Terminal=true`).
- Executes standalone binary in a near-empty environment (`env -i`, `DOTNET_ROOT=/nonexistent`) with `--verify-only`.
- Executes launcher in the same near-empty environment with `--verify-only` (non-interactive path).

Deploy standalone Linux binary into game directory:

```bash
/home//tw/emuera.em/scripts/deploy-linux-standalone.sh /home//tw/eraTWKR
```

- If target name ends with `.exe`, deploy now also emits a same-stem executable without extension (Linux-friendly launch path).
- Deploy now attempts to bundle `libwebp.so*` into game root (host discovery via `LIBWEBP_SO_PATH`, `ldconfig`, and common lib paths) and creates `libwebp.so` symlink when needed.

After deploy, a launcher script is also generated:
- `/home//tw/eraTWKR/Run-Emuera-Linux.sh` (runs `--play-like` by default)
  - Launcher exports `EMUERA_LIBWEBP_DIR` to game-root directory so bundled local WebP libraries are discovered first.
  - Launcher now switches to direct mode (no forced `--play-like`) when explicit check/control options are passed (`--verify-only`, `--run-smoke-only`, `--strict-smoke-only`, gate/scan options, `--interactive-gui`, `--play-like`, `--launch-bundled`).
  - In `--play-like`, if no actually launchable bundled target exists, CLI now enters launcher menu (or exits success in non-interactive stdin mode) instead of failing hard.
- `/home//tw/eraTWKR/Run-Emuera-Linux.desktop`
  - Desktop entry now launches `Run-Emuera-Linux.sh --interactive-gui` with `Terminal=true` so menu-driven launcher flow works from desktop environment.

Optional target file name:

```bash
/home//tw/emuera.em/scripts/deploy-linux-standalone.sh /home//tw/eraTWKR Emuera1824+v11+webp+test+fix.exe
```

Windows-style folder launch compatibility (Linux binary placed in game folder):

```bash
cp /home//tw/emuera.em/artifacts/linux-x64-cli/Emuera.Cli /home//tw/eraTWKR/Emuera1824+v11+webp+test+fix
chmod +x /home//tw/eraTWKR/Emuera1824+v11+webp+test+fix
(cd /home//tw/eraTWKR && ./Emuera1824+v11+webp+test+fix --run-smoke-only)
```


`Emuera.RuntimeCore` integration:
- `Emuera` now references RuntimeCore.
- `Emuera.Cli` now references RuntimeCore.
- RuntimeHost remains wired by WinForms host (`Program.cs`) but physically belongs to RuntimeCore.
- Window title flow moved toward host hooks: `GetWindowTitle/SetWindowTitle` migrated off `IRuntimeConsole` to `IProcessConsole` + `RuntimeHost` fallback (WinForms/CLI hooks both wired).
- Error-state visual hooks moved off `IRuntimeConsole`: `ThrowError`/`ThrowTitleError` now `IProcessConsole`-only with safe cast at runtime callsites.
- Render-surface hooks moved off `IRuntimeConsole`: `PrintHtml`/`PrintImg`/`PrintShape`/`PrintHTMLIsland`/`ClearHTMLIsland` are now `IProcessConsole`-only; script and plugin callsites use safe process-console casts.
- Additional host-only APIs moved off `IRuntimeConsole`: `ClearText`, `OutputLog`/`OutputSystemLog`, `getDefStBar`/`getStBar`/`setStBar` now live on `IProcessConsole`, with runtime fallbacks where needed.
- UI redraw hooks moved off `IRuntimeConsole`: `PrintFlush`/`RefreshStrings` are now `IProcessConsole`-only and all runtime/plugin callsites were converted to safe process-console access.
- Additional display-state hooks moved off `IRuntimeConsole`: `ResetStyle`, `ReloadErbFinished`, `LastLineIsEmpty`, and `LastLineIsTemporary` are now `IProcessConsole`-only; runtime checks now use safe casts.
- Temporary-line and generation-update paths were isolated to process console: `PrintTemporaryLine` and `updatedGeneration` moved to `IProcessConsole`, with `Process.SystemProc` using safe helper methods.
- Bar-rendering surface moved off `IRuntimeConsole`: `PrintBar` and `printCustomBar` are now `IProcessConsole`-only, and runtime/plugin callsites use safe process-console access.
- Button/error-line control paths moved off `IRuntimeConsole`: `PrintButton`, `PrintButtonC`, `PrintErrorButton`, and `deleteLine` are now `IProcessConsole`-only, with runtime and plugin callsites converted to safe process-console access.
- Logging suppression flag `noOutputLog` moved off `IRuntimeConsole` to `IProcessConsole` with safe runtime helper handling.
- Debug-output surface moved off `IRuntimeConsole`: `DebugPrint`, `DebugNewLine`, and `DebugClear` are now `IProcessConsole`-only with safe runtime/plugin callsites.
- Runtime/core boundary refinement: execution-critical members (`MesSkip`, `LineCount`, `IsTimeOut`, run/input/quit controls) are kept in slim `IRuntimeConsole`, while host/UI-specific rendering and window members stay in `IProcessConsole`.
- Added `IExecutionConsole` as an explicit execution-layer boundary (`IRuntimeConsole` base, `IProcessConsole` extension) and switched `Process` / `ExpressionMediator` to this layer to prepare eventual headless runtime-host implementations.
- Follow-up cleanup: runtime callsites that only needed execution state (`MesSkip`, `LineCount`, `IsTimeOut`) now read directly from execution console instead of `IProcessConsole` casts, further reducing UI-surface coupling inside script/system flow.
- `Process` and `ExpressionMediator` now depend on slim `IRuntimeConsole` again (execution essentials only: run-state/input/await/quit + script/debug output), while `IProcessConsole` remains the host/UI extension layer; this keeps engine loop dependencies narrower than full UI surface.
- `WINDOW_TITLE` variable write/read path now uses `RuntimeHost.SetWindowTitle` / `RuntimeHost.GetWindowTitle` directly (instead of runtime-side `IProcessConsole` title casts), further shrinking runtime-to-UI coupling in variable evaluation/token flow.
- `DRAWLINESTR` token read path now uses `RuntimeHost.ResolveDrawLineString(...)` hook instead of direct `IProcessConsole` casts; WinForms host provides formatted drawline string and CLI host falls back to config value.
- Added `RuntimeHost.FormatDrawLineString(...)` hook and switched runtime drawline-formatting callsites (`CUSTOMDRAWLINE` argument build, `CALLSHARP` argument build, `GETLINESTR`) from direct `IProcessConsole.getStBar(...)` casts to host hook dispatch.
- Added `RuntimeHost.InitializeDrawLineString(...)` hook and switched process init drawline baseline setup from direct `IProcessConsole.setStBar(...)` cast to host hook dispatch.
- During process init, script window-title application is now routed directly through `RuntimeHost.SetWindowTitle(...)` (no process-console cast in init path).
- Added `IProcessRuntimeConsole` (runtime-focused subset) and switched `Process` + `ExpressionMediator` runtime access to this smaller contract; `IProcessConsole` now extends it as host/UI superset. This narrows required implementation surface for future Linux runtime-host adapters.
- Decoupled runtime-facing display enums from UI layer: introduced `RuntimeDisplayLineAlignment` and `RuntimeRedrawMode` in runtime script layer, and mapped WinForms host (`EmueraConsole`) through explicit interface adapters. This removes additional runtime dependency on UI enum definitions.
- Added runtime text-style bridge type `RuntimeFontStyleFlags` and RGB-based style methods (`SetStringColorRgb`, `SetBackgroundColorRgb`, `SetStringStyleFlags`, `GetStringStyleFlags`) on `IProcessRuntimeConsole`.
- Switched runtime script/style paths (`Process.ScriptProc`, `RESETCOLOR`/`RESETBGCOLOR`/`FONT*` instructions) to the new RGB/style-flag interface path, reducing direct runtime reliance on UI-side `StringStyle.FontStyle` and widening portability for non-WinForms hosts.
- Added `RuntimeHost.TryResolveNamedColorRgb(...)` host hook path and switched runtime color-name validation/dispatch (`SETCOLORBYNAME`, `SETBGCOLORBYNAME`, argument-parse validation) to host-resolved RGB instead of runtime-side `Color.FromName(...)`.
- WinForms host and CLI host now both wire `RuntimeHost.ResolveNamedColorRgbHook` so existing script compatibility is preserved while runtime-core color-name lookup implementation stays host-extensible.
- Added RGB-based tooltip color bridge (`SetToolTipColorRgb`) and switched runtime `TOOLTIP_SETCOLOR` instruction path to RGB dispatch, removing another runtime-side `Color.FromArgb(...)` dependency.
- Removed now-unnecessary `using System.Drawing` references from runtime script paths where RGB bridge migration completed (`Process.ScriptProc`, `ArgumentBuilder`, `Instraction.Child`).
- Further narrowed runtime-facing console contract: removed `SetBgColor(Color)` / `SetStringStyle(Color|FontStyle)` from `IProcessRuntimeConsole`; kept UI-specific `SetBgColor(Color)` on `IProcessConsole` for plugin/UI-host compatibility.
- Added runtime-neutral config color accessors in `ConfigData` (`GetConfigRuntimeColor`, `GetConfigColorRgb24`) and switched ERB config color value export path to this bridge.
- `Config.SetConfig` color initialization now uses runtime-neutral color values first (`RuntimeColor`/RGB24), then derives legacy `System.Drawing.Color` mirrors for WinForms compatibility.
- Promoted runtime mouse hooks (`GetMousePositionXY`, `MoveMouseXY`) and debug output hooks (`DebugClear`, `DebugPrint`, `DebugNewLine`) into `IProcessRuntimeConsole`.
- Updated `PluginManager` to use `IProcessRuntimeConsole` for plain text/button/bar/font/bgcolor/flush/mouse/debug interactions where possible, reducing direct runtime dependency on `IProcessConsole`.
- In `Instraction.Child`, introduced runtime-console helper (`AsRuntimeConsole`) and switched runtime-capable instruction paths (`CUSTOMDRAWLINE`, debug print/clear, line delete/temp-line, RGB color reset, font style flags, reset style, input-time refresh) from `IProcessConsole` casts to `IProcessRuntimeConsole` casts where no UI-only API is needed.
- In `Creator.Method`, added runtime-console helper (`AsRuntimeConsole`) and switched runtime-capable method paths to runtime-side contracts:
  - `MOUSEX`/`MOUSEY` now use `IProcessRuntimeConsole.GetMousePositionXY`.
  - `GETSTYLE` now reads `RuntimeFontStyleFlags` via `IProcessRuntimeConsole.GetStringStyleFlags`.
  - `CURRENTALIGN` now reads runtime alignment through `IProcessRuntimeConsole.Alignment`.
  - `OUTPUTLOG` now routes through `IProcessRuntimeConsole.OutputLog`.
- Promoted additional host-state hooks (`IsActive`, `setRedrawTimer`) into `IProcessRuntimeConsole` and switched `Creator.Method` paths (`GETKEY*`, `ISACTIVE`, `SETANIMETIMER`) from `IProcessConsole` casts to `IProcessRuntimeConsole` casts.
- Added runtime style/color/font query hooks (`GetStringColorRgb`, `GetBackgroundColorRgb`, `GetFontName`) into `IProcessRuntimeConsole`.
- Switched `Creator.Method` retrieval paths (`GETCOLOR`, `GETBGCOLOR`, `GETFONT`) from `IProcessConsole` UI-style access (`StringStyle`/`bgColor`) to runtime-console query hooks.
- Promoted redraw-state query (`RuntimeRedrawMode Redraw`) into `IProcessRuntimeConsole` and switched `CURRENTREDRAW` method path to runtime-console access.
- Switched `LINEISEMPTY` method path from UI-side `EmptyLine` access to runtime-side `LastLineIsEmpty` access.
- Cleaned duplicated members from `IProcessConsole` that are now provided by `IProcessRuntimeConsole` (mouse XY/debug/redraw/activity), reducing interface overlap noise.
- Promoted runtime text-input/hotkey hooks (`GetTextBoxText`, `ChangeTextBox`, `HotkeyStateSet`, `HotkeyStateInit`) into `IProcessRuntimeConsole`, and switched `Creator.Method` textbox/hotkey paths to runtime-console casts.
- Replaced remaining UI-style dependency in `GraphicsDrawString` fallback font-style path: now resolves style via `IProcessRuntimeConsole.GetStringStyleFlags` instead of `IProcessConsole.StringStyle`.
- Promoted textbox-position and bitmap-cache hooks (`ResetTextBoxPos`, `SetTextBoxPos`, `bitmapCacheEnabledForNextLine`) into `IProcessRuntimeConsole`, and switched `MoveTextBox` / `BitmapCacheEnable` method paths to runtime-console casts.
- After this pass, `Creator.Method` direct `AsProcessConsole(...)` callsites were reduced to 14, now concentrated in UI-only surfaces (display-line/html extraction, client-size, mouse-button hover state, and CBG image/button rendering paths).
- Promoted client-size hooks (`ClientWidth`, `ClientHeight`) into `IProcessRuntimeConsole` and switched `CLIENTWIDTH`/`CLIENTHEIGHT` method path to runtime-console casts.
- After this pass, `Creator.Method` direct `AsProcessConsole(...)` callsites were reduced further to 13, now concentrated in UI-only surfaces:
  - display-line/html extraction (`GetDisplayLines`, `PopDisplayingLines`, `DisplayLineList`)
  - CBG rendering/button-map paths
  - mouse-hover button state refresh/read (`RefreshMousePointingState`, `PointingSring`)
- Introduced explicit UI-only bridge contract `IProcessUiConsole` (display-line/html extraction, CBG rendering/button-map, mouse-hover button state) and made `IProcessConsole` extend it.
- `Creator.Method` now routes all remaining UI-only paths through `AsUiConsole(...)`; generic `AsProcessConsole(...)` helper usage was removed.
- Added RuntimeHost CBG hooks (`CbgClear*`, `CbgSet*`) and switched `Creator.Method` CBG paths to host-hook calls.
- `Creator.Method` no longer uses `AsUiConsole(...)`; all previous CBG/display/html/mouse-hover query paths now route through runtime-side contracts (`IProcessRuntimeConsole`) or RuntimeHost hooks.
- Added RuntimeHost hooks for textbox-apply/background-image/tooltip operations, and switched `Instraction.Child` paths (`ApplyTextBoxChanges`, `SETBGIMAGE`/`REMOVEBGIMAGE`/`CLEARBGIMAGE`, tooltip color/delay/duration/font/custom/format/img) from `AsProcessConsole(...)` calls to RuntimeHost calls.
- Promoted print-buffer/button-count queries into runtime contract (`IProcessRuntimeConsole.IsPrintBufferEmpty`, `IProcessRuntimeConsole.CountInteractiveButtons(...)`) and switched `BINPUT`/`BINPUTS`/`ONEBINPUT`/`ONEBINPUTS` paths to runtime-console casts.
- Added RuntimeHost hooks for HTML/image/shape print paths (`PrintHtml`, `PrintHtmlIsland`, `ClearHtmlIsland`, `PrintImage`, `PrintShape`) and switched corresponding `Instraction.Child` paths from `AsProcessConsole(...)` to RuntimeHost calls.
- `Instraction.Child` no longer uses `AsProcessConsole(...)`; direct runtime-script dependence on `IProcessConsole` was removed from this file.
- `PluginManager` was switched off `IProcessConsole`:
  - `PrintPlainWithSingleLine` now uses runtime-console contract (`IProcessRuntimeConsole.PrintPlainSingleLine`).
  - `PrintHtml`/`PrintImage` now route through RuntimeHost hooks.
  - `ClearDisplay`/`ForceStopTimer` now use runtime-console contract.
- Added runtime-console members required by plugin/runtime paths:
  - `IProcessRuntimeConsole.IsPrintBufferEmpty`
  - `IProcessRuntimeConsole.PrintPlainSingleLine(string)`
  - `IProcessRuntimeConsole.ClearDisplay()`
  - `IProcessRuntimeConsole.forceStopTimer()`
  - `IProcessRuntimeConsole.CountInteractiveButtons(bool)`
- Runtime-layer grep check now reports no remaining `IProcessConsole` references under `Emuera/Runtime/**`; `IProcessConsole` remains only as a UI-bridge interface under `Emuera/UI/Game/RuntimeBridge`.

## RuntimeCore extraction progress (contracts stage)

- Moved runtime console contract files from `Emuera/Runtime/Script/*` into `Emuera.RuntimeCore/Runtime/Script/*`:
  - `IDebugConsole`, `IScriptConsole`, `IRuntimeConsole`, `IExecutionConsole`, `IProcessRuntimeConsole`
  - `RuntimeDisplayLineAlignment`, `RuntimeFontStyleFlags`, `RuntimeRedrawMode`
- Moved `InputRequest`/`InputType` from `Emuera/Runtime/InputRequest.cs` into `Emuera.RuntimeCore/Runtime/InputRequest.cs`.
- Added `Emuera.RuntimeCore/Properties/AssemblyInfo.cs` with `InternalsVisibleTo("Emuera")` so existing Windows host/runtime sources can continue compiling while contracts now live in RuntimeCore.
- Build and offline verification remained green after the contract move:
  - `dotnet build Emuera/Emuera.csproj -c Debug-NAudio` (warnings only, no errors)
  - `dotnet build Emuera.Cli/Emuera.Cli.csproj -c Release` (clean)
  - `scripts/audit-runtime-ui-coupling.sh` pass
  - `scripts/verify-linux-offline-standalone.sh /home//tw/eraTWKR` pass

## Runtime global-coupling reduction (Process stage)

- `ProcessState` no longer depends on `GlobalStatic.Process` to decide debug trace cleanup:
  - Added optional method-stack provider injection (`Func<int>`) to `ProcessState` constructor.
  - `Process` now constructs state via `new ProcessState(console, MethodStack)`.
  - `ProcessState.Clone()` preserves the same provider.
- `CalledFunction` character-ref range check no longer uses global variable storage:
  - Replaced `GlobalStatic.VariableData.CharacterList.Count` with `exm.VEvaluator.VariableData.CharacterList.Count`.
- `LabelDictionary` no longer reaches `GlobalStatic.IdentifierDictionary` directly:
  - Added constructor injection `LabelDictionary(IdentifierDictionary identifierDictionary)`.
  - Replaced local-size/default-size queries and resize calls with injected dictionary usage.
  - `Process.Initialize` now creates labels via `new LabelDictionary(idDic)`.
- `IProcessConsole` now avoids duplicate declarations for runtime-level members that already exist on `IProcessRuntimeConsole`:
  - Removed re-declarations of `CountInteractiveButtons`, `ClearDisplay`, `forceStopTimer`.
  - This removes interface-hiding warnings and clarifies runtime/UI contract ownership.
- `Instraction.Child` runtime-global usage was reduced further:
  - Replaced `GlobalStatic.VEvaluator` usage with `exm.VEvaluator`.
  - Replaced many `GlobalStatic.Process`/`IdentifierDictionary`/`LabelDictionary`/`GameBaseData`/`ConstantData` access paths with `exm.Process` and `exm.VEvaluator` context access where instruction-time context exists.
  - Parser-time `SetJumpTo(...)` paths were switched from `GlobalStatic.Process` to `RuntimeHost.GetCurrentProcess()`-based access (`CurrentRuntimeProcess` helper), removing remaining direct `GlobalStatic` access from `Instraction.Child.cs`.
- Added runtime host bridge for current process handle:
  - `RuntimeHost.GetCurrentProcessHook` + `RuntimeHost.GetCurrentProcess()` in `Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs`.
  - Wired in Windows host (`Emuera/Program.cs`) to return `GlobalStatic.Process`.
  - Wired in CLI host (`Emuera.Cli/Program.cs`) as null-safe no-op.
- Removed remaining `Process.cs` direct singleton publish writes:
  - `GlobalStatic.GameBaseData/ConstantData/VEvaluator/IdentifierDictionary/LabelDictionary` assignments were replaced by RuntimeHost publish calls:
    - `RuntimeHost.SetGameBaseData(...)`
    - `RuntimeHost.SetConstantData(...)`
    - `RuntimeHost.SetVariableEvaluator(...)`
    - `RuntimeHost.SetIdentifierDictionary(...)`
    - `RuntimeHost.SetLabelDictionary(...)`
  - Added matching RuntimeHost hooks and host wiring:
    - `SetGameBaseDataHook`, `SetConstantDataHook`, `SetVariableEvaluatorHook`, `SetIdentifierDictionaryHook`, `SetLabelDictionaryHook`
    - Windows host maps these to existing `GlobalStatic` compatibility fields.
    - CLI host keeps no-op mappings.
- Added runtime-global accessor bridge:
  - New `Emuera/Runtime/Utils/RuntimeGlobals.cs` that resolves runtime singleton-like data through `RuntimeHost` instead of direct `GlobalStatic` reads.
  - Extended `RuntimeHost` with cached/gettable object channels:
    - `Set/GetGameBaseData`
    - `Set/GetConstantData`
    - `Set/GetVariableEvaluator`
    - `Set/GetVariableData`
    - `Set/GetIdentifierDictionary`
    - `Set/GetLabelDictionary`
- Migrated parser/argument and nearby runtime layers off direct `GlobalStatic` access:
  - `ExpressionParser`
  - `ArgumentBuilder`
  - `LogicalLineParser`
  - `LexicalAnalyzer`
  - `VariableParser`
  - `UserDefinedFunction`
  - `UserDefinedVariable`
  - `ErbLoader`
  - `ErhLoader`
  - `StrForm`
  - `LogicalLine`
  - `CharacterData`
  - `VariableEvaluator` (now publishes variable data via `RuntimeHost.SetVariableData`)
- Current remaining direct `GlobalStatic.(ConstantData|IdentifierDictionary|...)` usage in runtime script scope is concentrated in `VariableData.cs` (plus one comment in `ConstantData.cs`), making `VariableData` the next primary decoupling target.
- `VariableData.cs` decoupling completed:
  - Replaced remaining runtime save/load map/xml/datatable and variable-token checks from `GlobalStatic.ConstantData` / `GlobalStatic.IdentifierDictionary` to `RuntimeGlobals.ConstantData` / `RuntimeGlobals.IdentifierDictionary`.
  - Runtime script scope now has no active `GlobalStatic.(ConstantData|IdentifierDictionary|LabelDictionary|VariableData|VEvaluator|GameBaseData)` references; only one historical comment remains in `ConstantData.cs`.
- Further runtime global-decoupling pass:
  - Replaced parser/runtime call-site scan-line lookups from `GlobalStatic.Process.GetScaningLine()` to `RuntimeGlobals.CurrentProcess?.GetScaningLine()` in:
    - `IdentifierDictionary`
    - `VariableLocal`
    - `ExpressionParser`
    - `LexicalAnalyzer`
  - Replaced parser-side scan-line assignment in `LogicalLineParser` with null-safe `RuntimeGlobals.CurrentProcess` assignment.
  - `CtrlZ` random-seed snapshot path now reads via `RuntimeGlobals.VEvaluator` instead of `GlobalStatic.VEvaluator`.
- Analysis-only unresolved-label counting moved off `GlobalStatic.tempDic`:
  - Added `RuntimeHost` analysis counter APIs:
    - `IncrementAnalysisLabelCounter`
    - `GetAnalysisLabelCounters`
    - `ClearAnalysisLabelCounters`
  - `ExpressionParser` now records unresolved labels through `RuntimeHost`.
  - `ErbLoader` now consumes/report counters through `RuntimeHost` and clears them per load pass.
- Current remaining `GlobalStatic` in runtime scope:
  - Debug stack tracking in `LogicalLine` (`#if DEBUG` only)
  - One historical comment in `ConstantData`
  - One commented trace line in `Process.State`
- After this pass, direct `GlobalStatic` references in `Process*.cs` are reduced and concentrated on remaining runtime globals (`ctrlZ`, dictionary caches, and shared data singletons), which are the next extraction targets.

Solution build check (Linux host):

```bash
DOTNET_CLI_HOME=/tmp DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 NUGET_PACKAGES=/tmp/.nuget/packages \
  dotnet build /home//tw/emuera.em/Emuera.sln -c Debug-NAudio
```

Result: success (`Emuera`, `Emuera.Cli`, `Emuera.RuntimeCore`, `EmueraPluginExample` all built).

## Remaining blockers for native Linux runtime

1. Runtime execution still coupled to large `IProcessConsole` surface
- `Process` can be constructed with an interface, but the current interface still carries many UI-era methods/types.
- CLI can run validation/smoke/static gates natively, but not full in-engine play loop yet.

2. Full UI replacement path
- `UI/Framework/Forms/*` remains WinForms-tied.
- Linux host path needs staged replacement:
  - shrink/split runtime-facing console contract,
  - add Linux text-console runtime host implementation,
  - optionally add GUI host later.

## Next implementation order

1. Create `Emuera.RuntimeCore` project
- Move script engine/runtime/config/data layers into OS-agnostic class library.

2. Keep current WinForms app as Windows host
- Reference `RuntimeCore`.

3. Add Linux host
- Minimal CLI host is now added and deployable.
- Next: connect CLI host to RuntimeCore `Process` execution loop using a reduced runtime console contract.
- Add GUI host later if needed.

4. Add CI matrix
- Windows build (existing host)
- Linux build (RuntimeCore + Linux host)

## Runtime environment decoupling progress (Stage 1.6)

- Added `Emuera.RuntimeCore/Runtime/RuntimeEnvironment.cs` to centralize runtime directory/mode state (`ExeDir`, `CsvDir`, `ErbDir`, `DatDir`, `DebugMode`, `AnalysisMode`, etc.).
- `Program.cs` now syncs startup path/mode/analysis-file state into `RuntimeEnvironment`.
- Replaced many `Program.*` references with `RuntimeEnvironment.*` across runtime paths (`Process*`, `Config`, parser/loader/statement helpers, plugin loader, JSON config, and utility/lang helpers), reducing entrypoint static coupling in runtime logic.
- Added RuntimeHost image-content hooks (`LoadContents`, `UnloadTempLoadedConstImages`, `UnloadTempLoadedGraphicsImages`) and switched `Process`, `ProcessState`, and `Process.SystemProc` to use these hooks instead of direct `AppContents` calls.
- WinForms host (`Program.cs`) now wires these hooks to `AppContents`; CLI host wires safe no-op fallbacks.
- Added RuntimeHost graphics/sprite hooks (`GetGraphics`, `GetSprite`, `CreateSpriteG`, `SpriteDispose`, `SpriteDisposeAll`, `CreateSpriteAnime`).
- Switched `FunctionMethodCreator` image/sprite function paths from direct `AppContents` calls to RuntimeHost hooks.
- WinForms host wires these hooks to `AppContents`; CLI host wires safe null/no-op defaults.
- Added `scripts/audit-runtime-ui-coupling.sh` to track Runtime -> UI namespace coupling (`MinorShift.Emuera.UI*`) under `Emuera/Runtime` with allowlist enforcement.
- Integrated Runtime/UI coupling audit into `scripts/ci-linux-porting.sh` right after platform-specific API audit.
- Reduced `GlobalStatic` usage inside WinForms console implementation where instance context already exists:
  - `EmueraConsole.Print`: switched bitmap-cache flag and warning raw-line print path to direct instance `process`/field usage.
  - `EmueraConsole`: switched mouse-input `RESULTS`, debug state save/restore, paint-line correction, and ctrl+z rewind load paths to direct `process.ExpressionMediator.VEvaluator` / `process` access (keeping `GlobalStatic.ctrlZ` compatibility store for now).
  - `ConsoleButtonString`: switched bitmap-cache ring operations from `GlobalStatic.Console` to the existing `parent` (`EmueraConsole`) reference.
  - `GraphicsImage.GDrawString`: removed direct dependency on `GlobalStatic.Console.StringStyle` by using config default font style fallback.
- Removed additional host-side global static dependencies:
  - Added `Emuera/UI/FontRegistry.cs` and migrated private-font registration/lookup from `GlobalStatic.Pfc` to `FontRegistry.Collection` in `Program`, `FontFactory`, `EmueraConsole`, and `FunctionMethodCreator`.
  - Replaced `GlobalStatic.ForceQuitAndRestart` with `EmueraConsole`-local static state (`forceQuitAndRestart`) and removed the field from `GlobalStatic`.
  - `GlobalStatic.(Pfc|ForceQuitAndRestart)` references are now fully removed from source.
- Reduced WinForms form-layer usage of `GlobalStatic` for process/console/evaluator access:
  - Added `EmueraConsole.CurrentProcess` / `CurrentEvaluator` accessors and `MainWindow.ConsoleHost`.
  - Replaced `MainWindow` mouse/input result write paths from `GlobalStatic.Process` / `GlobalStatic.VEvaluator` to `console.CurrentProcess` / `console.CurrentEvaluator`.
  - Replaced `DebugDialog` watch-eval and state-save paths from `GlobalStatic.Process` to injected `Process` instance (`emuera`).
  - Replaced `ConfigDialog` clipboard bridge updates from `GlobalStatic.Console` to `parent.ConsoleHost`.
  - Replaced `HtmlManager.HtmlLength` dependence on `GlobalStatic.Console.StrMeasure` with an internal fallback `StringMeasure` instance.
  - In these scopes, remaining `GlobalStatic` usage is now limited to `EmueraConsole` bootstrap publication (`GlobalStatic.Console = this`, `GlobalStatic.Process = process`).
- Reduced `GlobalStatic` usage in `UI/Game/RuntimeBridge/Creator.Method.cs` where `ExpressionMediator` context already exists:
  - Migrated function-enumeration source (`NoneventKeys`) to `exm.Process.LabelDictionary`.
  - Migrated HTML substring result destination to `exm.VEvaluator.RESULTS_ARRAY`.
  - Migrated DataTable helper result arrays to `exm.VEvaluator.RESULT_ARRAY` / `RESULTS_ARRAY`.
  - Migrated `UNICODE` warning-context reads from `GlobalStatic.Process` to `exm.Process`.
  - Migrated `MOUSESKIP` deprecation warning scan-line source to `RuntimeGlobals.CurrentProcess?.GetScaningLine()`.
- Removed legacy `GlobalStatic` singleton data caches that were no longer consumed:
  - In `GlobalStatic`, removed `GameBaseData`, `ConstantData`, `VariableData`, `VEvaluator`, `IdentifierDictionary`, `LabelDictionary`, and `tempDic`.
  - Updated WinForms host `Program` hook wiring to no-op publish hooks for these values (`SetGameBaseDataHook`, `SetConstantDataHook`, `SetVariableEvaluatorHook`, `SetVariableDataHook`, `SetIdentifierDictionaryHook`, `SetLabelDictionaryHook`) since runtime now resolves through `RuntimeHost`/`RuntimeGlobals`.
- Added a runtime-level console host hook in `Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs`:
  - `GetConsoleHostHook` + `GetConsoleHost()`.
  - This allows host bindings to access UI console capabilities through `RuntimeHost` instead of direct static field reach-through.
- Updated host wiring:
  - WinForms host (`Emuera/Program.cs`) now sets `RuntimeHost.GetConsoleHostHook` and routes runtime UI hooks through `CurrentConsoleHost` accessor.
  - CLI host (`Emuera.Cli/Program.cs`) now explicitly wires `GetConsoleHostHook` to `null` as a non-UI host default.
  - `GetCurrentProcessHook` now resolves process from runtime console host first (`(RuntimeHost.GetConsoleHost() as EmueraConsole)?.CurrentProcess`) with legacy fallback.
- Updated `EmueraConsole.Initialize` to publish runtime console host (`RuntimeHost.GetConsoleHostHook = () => this`) at bootstrap.
- Verification after this change set:
  - `dotnet build Emuera/Emuera.csproj -c Debug-NAudio` succeeded.
  - `dotnet build Emuera.Cli/Emuera.Cli.csproj -c Release` succeeded.
  - `scripts/audit-runtime-ui-coupling.sh` passed.
  - `scripts/verify-linux-offline-standalone.sh /home//tw/eraTWKR` passed.
- Follow-up decoupling:
  - Removed remaining `GlobalStatic.Console` / `GlobalStatic.Process` reads from `Emuera/Program.cs` host hook wiring.
  - `Program` now resolves host console/process exclusively through `RuntimeHost.GetConsoleHost()` and `RuntimeHost.GetCurrentProcessHook`.
  - `EmueraConsole.Initialize` now also publishes `RuntimeHost.GetCurrentProcessHook = () => process`.
- Remaining direct `GlobalStatic` runtime hot path is now primarily `ctrlZ` compatibility state and bootstrap publication lines in `EmueraConsole`.
- CtrlZ decoupling:
  - Moved rewind state ownership from `GlobalStatic.ctrlZ` to `EmueraConsole.CtrlZState`.
  - Rewired `RuntimeHost` CtrlZ hooks in `Program.cs` to use `CurrentConsoleHost?.CtrlZState`.
  - Removed `ctrlZ` field from `GlobalStatic`.
- Removed `GlobalStatic` runtime dependency entirely:
  - Deleted `Emuera/GlobalStatic.cs`.
  - Removed bootstrap assignments from `EmueraConsole.Initialize`.
  - Cleared stale `GlobalStatic` references in comments/docs within runtime/UI bridge files.
  - Runtime host state is now published through `RuntimeHost` hooks only.
- Added `scripts/audit-global-state.sh` and integrated it into `scripts/ci-linux-porting.sh`.
  - CI now fails if any `GlobalStatic.*` reference reappears under `Emuera/*.cs`.
- Reduced Linux CLI host dependency surface:
  - Removed `System.Drawing` dependency from `Emuera.Cli/Program.cs`.
  - CLI color-name resolution hook now returns `null` (non-GUI host behavior).
- CLI runtime color-name fallback added without `System.Drawing` dependency:
  - `Emuera.Cli/Program.cs` now resolves common named colors and `#RRGGBB` directly via `CliColorNames`.
  - Keeps runtime color functions usable on Linux CLI host even without WinForms/System.Drawing color resolver.
- Promoted RuntimeCore host interfaces to public API for external Linux host implementations:
  - `IScriptConsole`, `IDebugConsole`, `IRuntimeConsole`, `IExecutionConsole`, `IProcessRuntimeConsole`
  - `InputRequest`, `InputType`, `RuntimeRedrawMode`, `RuntimeDisplayLineAlignment`, `RuntimeFontStyleFlags`
  - `ScriptPosition` (from `EmueraException.cs`)
- This enables implementing runtime-compatible host consoles outside WinForms and is a prerequisite for Linux native GUI/host replacement.
- Reduced runtime plugin API dependency on `System.Drawing`:
  - `PluginManager` no longer exposes `SetBgColor(Color)` and `GetMousePosition(): Point`.
  - Added non-GDI alternatives:
    - `SetBgColor(RuntimeColor)`
    - `SetBgColorRgb(byte r, byte g, byte b)`
    - `GetMousePositionXY(): RuntimePoint`
- This shrinks host/runtime coupling to drawing-framework specific types and helps Linux host portability.
- Removed runtime config `System.Drawing` dependence from config model layer:
  - `Emuera/Runtime/Config/ConfigData.cs` color config items now store `RuntimeColor` instead of `Color`.
  - `Emuera/Runtime/Config/ConfigItem.cs` parses/serializes color values via `RuntimeColor` (`R,G,B`) with no `System.Drawing` import.
  - WinForms boundary conversion is now explicit in `UI/Framework/Forms/ConfigDialog.cs` (`Color <-> RuntimeColor`).
- Tightened runtime GDI audit allowlist:
  - Removed `Config.cs`, `ConfigData.cs`, `ConfigItem.cs` from `scripts/audit-runtime-gdi-surface.sh` allowlist.
- Split EvilMask utility GDI surface out of runtime core utility file:
  - `Emuera/Runtime/Utils/EvilMask/Utils.cs` is now GDI-free and marked `partial`.
  - Added `Emuera/UI/Game/EvilMask/Utils.Drawing.cs` for `Color`/`Bitmap`/`Icon` methods (`AddColorParam*`, `LoadImage`, `MakeIconFromBmpFile`).
  - Runtime GDI allowlist now reduced to only `Emuera/Runtime/Utils/EvilMask/Shape.cs`.
- Verification after this change set:
  - `dotnet build Emuera/Emuera.csproj -c Debug-NAudio` succeeded.
  - `dotnet build Emuera.Cli/Emuera.Cli.csproj -c Release` succeeded.
  - `scripts/audit-runtime-gdi-surface.sh` passed with reduced allowlist.
  - `scripts/ci-linux-porting.sh /home//tw/eraTWKR` passed (offline standalone deploy/verify included).
- Runtime GDI final cleanup milestone (current stage):
  - Moved `Emuera/Runtime/Utils/EvilMask/Shape.cs` to `Emuera/UI/Game/EvilMask/Shape.cs`.
  - Runtime-side `System.Drawing` references are now 0 under `Emuera/Runtime`.
  - `scripts/audit-runtime-gdi-surface.sh` allowlist is now empty and audit passes with:
    - `Runtime GDI surface audit passed: no System.Drawing references found in runtime scope.`
- Verification after Shape move:
  - `dotnet build Emuera/Emuera.csproj -c Debug-NAudio` succeeded.
  - `scripts/ci-linux-porting.sh /home//tw/eraTWKR` succeeded.
  - Offline standalone deploy/verify remained green.
- UI/Game WinForms direct-call consolidation milestone:
  - Added `Emuera/UI/Game/WinFormsBridge.cs` APIs for:
    - `ShowInfo(string)`, `ConfirmYesNo(string, string)`
    - `DrawText(Graphics, string, Font, Rectangle, Color, Color, TextFormatFlags)`
    - `MeasureText(string, Font, Size, TextFormatFlags)`
  - Replaced direct `MessageBox.Show`, `Application.DoEvents`, `TextRenderer.DrawText`, `TextRenderer.MeasureText` calls in:
    - `Emuera/UI/Game/EmueraConsole.cs`
    - `Emuera/UI/Game/HotkeyState.cs`
    - `Emuera/UI/Game/Rikaichan.cs` (init error path)
  - Added `scripts/audit-ui-game-winforms-bridge.sh` and wired it into `scripts/ci-linux-porting.sh`.
    - Audit rule: no direct `MessageBox.Show` / `TextRenderer.*` / `Application.DoEvents` in `Emuera/UI/Game` except `WinFormsBridge.cs`.
- UI/Game input-surface WinForms static-call reduction milestone:
  - Added cursor/mouse/screen helpers to `Emuera/UI/Game/WinFormsBridge.cs`:
    - `GetMousePositionInClient(Control)`
    - `IsPointInClient(Control, Point)`
    - `GetCursorPosition()`
    - `GetCursorHeight()`
    - `GetWorkingAreaHeightForPoint(Point)`
  - Replaced direct `Control.MousePosition`, `Cursor.*`, `Screen.FromPoint(...)` static calls in `Emuera/UI/Game/EmueraConsole.cs` to bridge calls.
  - Strengthened `scripts/audit-ui-game-winforms-bridge.sh` to also block direct:
    - `Control.MousePosition`, `Cursor.*`, `Screen.FromPoint(...)`
    - outside `WinFormsBridge.cs`.
  - Verified via:
    - `scripts/audit-ui-game-winforms-bridge.sh`
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- UI tooltip WinForms split milestone:
  - Moved tooltip-specific WinForms event/render/config code from:
    - `Emuera/UI/Game/EmueraConsole.cs`
    - to `Emuera/UI/Framework/Forms/EmueraConsole.ToolTip.cs` (partial class).
  - Added helper methods for tooltip interaction from game-render path:
    - `RemoveToolTip()`
    - `GetToolTipInitialDelay()`
    - `ShowToolTip(string, Point)`
  - `Emuera/UI/Game/EmueraConsole.cs` now uses these helpers from tooltip display path, reducing direct WinForms UI detail in game-layer file.
  - Strengthened `scripts/audit-ui-game-winforms-bridge.sh` to also flag `DrawToolTipEventArgs`, `PopupEventArgs`, and `ToolTip` usage in `Emuera/UI/Game` (except bridge allowlist).
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- EmueraConsole WinForms type-surface reduction milestone:
  - Reduced `Emuera/UI/Game/EmueraConsole.cs` direct WinForms type dependence:
    - Removed `using System.Windows.Forms` from `EmueraConsole.cs`.
    - `PressPrimitiveKey` signature changed from `Keys` to `int`.
    - `MouseDown` signature changed from `MouseButtons` to `int`.
    - `IsActive` no longer reads `Form.ActiveForm` directly; now uses `WinFormsBridge.IsAnyFormActive()`.
  - Replaced WinForms redraw timer usage:
    - Migrated `redrawTimer` from `System.Windows.Forms.Timer` to `System.Timers.Timer`.
    - Switched redraw callback to `Elapsed` and marshaled refresh via `window.Invoke(() => window.Refresh())`.
  - Updated caller bridge in `Emuera/UI/Framework/Forms/MainWindow.cs`:
    - Cast key/button enums to `int` when forwarding to `EmueraConsole`.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Hotkey input WinForms dependency reduction milestone:
  - `Emuera/UI/Game/HotkeyState.cs` now uses host-agnostic `int` key data path:
    - `keyToNumberRunInterpreter(KeyEventArgs)` -> `keyToNumberRunInterpreter(int keyData)`
    - removed direct `using System.Windows.Forms` from `HotkeyState.cs`.
  - Added key-name parsing bridge in `Emuera/UI/Game/WinFormsBridge.cs`:
    - `TryParseKeyCode(string keyName, out int keyCode)`
  - Updated callsite in `Emuera/UI/Framework/Forms/MainWindow.cs`:
    - passes `(int)e.KeyData` to hotkey interpreter.
  - Strengthened `scripts/audit-ui-game-winforms-bridge.sh`:
    - now also blocks direct `using System.Windows.Forms;` in `Emuera/UI/Game` (except allowlist files).
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Debug dialog/UI split milestone:
  - Moved debug dialog UI creation/lifecycle from `Emuera/UI/Game/EmueraConsole.cs` to
    `Emuera/UI/Framework/Forms/EmueraConsole.DebugDialog.cs` (partial class).
  - `EmueraConsole` core file now keeps debug trace/state logic while WinForms dialog construction remains in Forms layer.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- EmueraConsole Forms partial expansion milestone:
  - Added `Emuera/UI/Framework/Forms/EmueraConsole.WindowAccess.cs` and moved window-bound access methods out of game core file:
    - `GetMousePosition()`
    - `GetMousePositionXY()`
    - `MoveMouseXY(int, int)`
    - `LeaveMouse()`
    - `verticalScrollBarUpdate()`
  - Added `Emuera/UI/Framework/Forms/EmueraConsole.UiLoop.cs` and moved UI event-loop/activation surface:
    - `RefreshMousePointingState()`
    - `IsActive`
    - `Await(int)`
  - Refactored tooltip delayed-show flow:
    - Added `QueueToolTipShow(string)` in `Emuera/UI/Framework/Forms/EmueraConsole.ToolTip.cs`.
    - `Emuera/UI/Game/EmueraConsole.cs` now delegates delayed tooltip positioning/showing to Forms partial helper.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- UI platform façade consolidation milestone:
  - Added `Emuera/UI/Game/UiPlatformBridge.cs` as a game-layer platform façade.
  - Switched game-layer callsites from `WinFormsBridge` to `UiPlatformBridge` in:
    - `Emuera/UI/Game/HotkeyState.cs`
    - `Emuera/UI/Game/Rikaichan.cs`
    - `Emuera/UI/Game/ConsoleImagePart.cs`
    - `Emuera/UI/Game/ConsoleShapePart.cs`
    - `Emuera/UI/Game/ConsoleStyledString.cs`
    - `Emuera/UI/Game/EmueraConsole.Print.cs`
    - `Emuera/UI/Game/StringMeasure.cs`
    - `Emuera/UI/Game/EmueraConsole.cs` (comments/callsites aligned)
  - Updated `scripts/audit-ui-game-winforms-bridge.sh` allowlist to include:
    - `Emuera/UI/Game/UiPlatformBridge.cs`
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- UI platform bridge boundary hardening milestone:
  - Replaced remaining Forms partial direct `WinFormsBridge` calls with `UiPlatformBridge` in:
    - `Emuera/UI/Framework/Forms/EmueraConsole.UiLoop.cs`
    - `Emuera/UI/Framework/Forms/EmueraConsole.WindowAccess.cs`
    - `Emuera/UI/Framework/Forms/EmueraConsole.ToolTip.cs`
  - Added new audit script:
    - `scripts/audit-ui-platform-facade.sh`
    - rule: `WinFormsBridge.` calls are only allowed in:
      - `Emuera/UI/Game/WinFormsBridge.cs`
      - `Emuera/UI/Game/UiPlatformBridge.cs`
  - Wired new audit into CI:
    - `scripts/ci-linux-porting.sh`
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- UI/Game WinForms type-surface shrink milestone:
  - Updated game-layer platform façade signatures to reduce WinForms type exposure:
    - `UiPlatformBridge.GetMousePositionInClient(Control)` -> `GetMousePositionInClient(object)`
    - `UiPlatformBridge.IsPointInClient(Control, Point)` -> `IsPointInClient(object, Point)`
    - `UiPlatformBridge.DrawText(..., TextFormatFlags)` -> `DrawText(..., long format)`
    - `UiPlatformBridge.MeasureText(..., TextFormatFlags)` -> `MeasureText(..., long format)`
  - Updated `WinFormsBridge` implementation accordingly (casts are localized in bridge layer).
  - Updated tooltip Forms callsites to pass `(long)tooltip_format`.
  - Result: `Emuera/UI/Game` now has `using System.Windows.Forms;` only in `WinFormsBridge.cs`.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- UI backend pluggability milestone:
  - Added `Emuera/UI/Game/IUiPlatformBackend.cs`.
  - Converted WinForms implementation from static bridge to backend class:
    - `Emuera/UI/Game/WinFormsBridge.cs` now provides `WinFormsUiPlatformBackend : IUiPlatformBackend`.
  - Updated `Emuera/UI/Game/UiPlatformBridge.cs` to use replaceable backend instance:
    - default: `new WinFormsUiPlatformBackend()`
    - exposed: `UiPlatformBridge.Backend` setter/getter.
  - Strengthened `scripts/audit-ui-platform-facade.sh`:
    - now checks both `WinFormsBridge.` calls and `WinFormsUiPlatformBackend` references outside allowlisted files.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Non-Windows default backend milestone:
  - Added `Emuera/UI/Game/HeadlessUiPlatformBackend.cs` implementing `IUiPlatformBackend` for non-Windows/default fallback.
  - Updated `Emuera/UI/Game/UiPlatformBridge.cs` backend selection:
    - `EMUERA_UI_BACKEND=headless` -> headless backend
    - `EMUERA_UI_BACKEND=winforms` -> WinForms backend
    - default: Windows uses WinForms, non-Windows uses headless
  - Added `UiPlatformBridge.BackendName` for runtime backend diagnostics.
  - Purpose: remove implicit WinForms-only default from non-Windows runtime path while preserving WinForms behavior on Windows.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- RuntimeHost UI hook bridge migration milestone:
  - Updated `Emuera/Program.cs` RuntimeHost UI hooks to use `UiPlatformBridge` for:
    - `DoEventsHook`
    - `ShowInfoHook`
    - `ShowInfoWithCaptionHook`
    - `ConfirmYesNoHook`
  - Updated analysis error UI notifications in `Program.cs` to use `UiPlatformBridge.ShowInfo`.
  - Added platform-guarded `Application.SetCompatibleTextRenderingDefault(false)` (Windows only).
- Input backend unification milestone:
  - Added `GetKeyState(int)` to `IUiPlatformBackend`.
  - Implemented in:
    - `WinFormsUiPlatformBackend` (delegates to `WinInput.GetKeyState`)
    - `HeadlessUiPlatformBackend` (returns 0)
  - Updated `UiPlatformBridge` and `Program.cs` (`RuntimeHost.GetKeyStateHook`) to route through backend.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Program entrypoint decoupling hardening milestone:
  - Added `scripts/audit-program-ui-hooks.sh` and wired it into `scripts/ci-linux-porting.sh`.
  - Audit enforces in `Emuera/Program.cs`:
    - no direct `MessageBox.Show`
    - no direct `Application.DoEvents`
    - required RuntimeHost hook mappings to `UiPlatformBridge`
  - Routed Program-level key state hook through platform backend:
    - `RuntimeHost.GetKeyStateHook = UiPlatformBridge.GetKeyState`
    - added `GetKeyState(int)` to `IUiPlatformBackend`
    - implemented in `WinFormsUiPlatformBackend` and `HeadlessUiPlatformBackend`
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Clipboard backend unification milestone:
  - Added `SetClipboardText(string)` to `IUiPlatformBackend`.
  - Implemented in:
    - `WinFormsUiPlatformBackend` (uses `Clipboard.SetDataObject`)
    - `HeadlessUiPlatformBackend` (no-op)
  - Updated `UiPlatformBridge` and `Program.cs`:
    - `RuntimeHost.SetClipboardTextHook = UiPlatformBridge.SetClipboardText`
  - Strengthened `scripts/audit-program-ui-hooks.sh`:
    - now also blocks direct `Clipboard.SetDataObject` usage in `Emuera/Program.cs`
    - requires explicit hook mapping for `SetClipboardTextHook`.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Program input-hook guardrail milestone:
  - Added `scripts/audit-program-input-hooks.sh` and wired into `scripts/ci-linux-porting.sh`.
  - Audit enforces in `Emuera/Program.cs`:
    - no direct `WinInput.GetKeyState` usage
    - required mapping: `RuntimeHost.GetKeyStateHook = UiPlatformBridge.GetKeyState;`
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Program WinForms entry isolation milestone:
  - Refactored `Emuera/Program.cs` to localize WinForms startup in `RunWindowsGuiHost(args, icon)`:
    - `System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false)`
    - `ApplicationConfiguration.Initialize()`
    - `System.Windows.Forms.Application.Run(win)`
  - Removed direct `using System.Windows.Forms;` import from `Program.cs` and kept WinForms calls fully qualified within the localized method.
  - Added `scripts/audit-program-winforms-entry.sh` and wired into `scripts/ci-linux-porting.sh`.
  - Audit enforces:
    - `Program.cs` must not import `System.Windows.Forms`
    - GUI startup path must delegate through `RunWindowsGuiHost(args, icon)`
    - WinForms run loop call remains localized
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Program legacy WinForms reboot-path cleanup milestone:
  - Removed obsolete commented reboot/startup block in `Emuera/Program.cs` that still referenced legacy WinForms startup flow (`Application.Run`, `FormWindowState`).
  - Tightened `scripts/audit-program-winforms-entry.sh`:
    - fails if `Program.cs` references `FormWindowState`
    - fails on unqualified `Application.Run(...)` usage
  - Goal: keep active Program entrypoint surface minimal and prevent accidental re-introduction of WinForms-specific legacy flow in shared startup code.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Headless backend safety hardening milestone:
  - Updated `Emuera/UI/Game/HeadlessUiPlatformBackend.cs` to remove exception throws in non-essential mouse/cursor queries:
    - `GetMousePositionInClient` -> `Point.Empty`
    - `IsPointInClient` -> `false`
    - `GetCursorPosition` -> `Point.Empty`
    - `GetCursorHeight` -> `0`
    - `GetWorkingAreaHeightForPoint` -> `0`
  - Goal: prevent accidental crash paths on Linux/headless runtime when optional UI queries are invoked.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Headless interactive prompt behavior milestone:
  - Updated `Emuera/UI/Game/HeadlessUiPlatformBackend.cs`:
    - `ConfirmYesNo` now supports interactive console prompt (`[y/N]`) when stdin/stdout/stderr are TTY.
    - In non-interactive mode, logs explicit fallback and safely defaults to `No`.
    - `ShowInfo`/`ShowInfo(caption)` log format normalized for CLI diagnostics.
  - Goal: make Linux/headless runtime behavior closer to user intent during real play/CLI flows while keeping deterministic behavior in automation.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Headless backend regression guardrail milestone:
  - Added `scripts/audit-headless-backend-safety.sh`.
  - Audit enforces in `Emuera/UI/Game/HeadlessUiPlatformBackend.cs`:
    - no `throw` usage
    - no `PlatformNotSupportedException` usage
  - Wired audit into `scripts/ci-linux-porting.sh`.
  - Goal: prevent accidental reintroduction of crash-prone code paths in Linux/headless backend.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Headless clipboard integration milestone:
  - Updated `Emuera/UI/Game/HeadlessUiPlatformBackend.cs` clipboard behavior from no-op to best-effort external command integration:
    - Linux: `wl-copy` -> `xclip -selection clipboard` -> `xsel --clipboard --input`
    - macOS fallback: `pbcopy`
  - Clipboard helper writes via stdin pipe and returns success on zero exit code.
  - If helper tools are unavailable, emits a clear diagnostic line and continues safely.
  - Goal: reduce practical gameplay/tooling gap for Linux users without introducing platform-specific hard failures.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Linux graphical launcher UX milestone (`zenity` path):
  - Updated `Emuera.Cli/Program.cs` interactive launcher flow:
    - when `zenity` is available on Linux and stdin/stdout are interactive, launcher menu uses graphical dialogs.
    - menu supports smoke/strict checks and bundled game launch actions.
    - on missing `zenity` (or non-interactive IO), existing console menu path remains as fallback.
  - Goal: provide immediate Linux GUI launcher experience without waiting for full WinForms-equivalent renderer replacement.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Console launcher target-selection milestone:
  - Updated `Emuera.Cli/Program.cs` interactive text launcher (`RunAsync`) to support explicit executable selection before launch.
  - Added shared launchability filter helper (`GetLaunchableExecutableCandidates`) and reused it for both:
    - console selector (`SelectLaunchTargetInConsole`)
    - `zenity` selector (`SelectLaunchTargetWithZenity`)
  - Launcher behavior now aligns across text and graphical interactive paths:
    - excludes CLI host self-binary
    - filters non-launchable PE targets unless `--allow-pe-launch` and mono/wine availability are satisfied
    - defaults to first candidate when selection input is empty or invalid
  - Goal: improve Linux "actual game launch" parity for non-zenity environments (SSH/TTY/local terminal).
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Linux launcher option-routing hardening milestone:
  - Updated generated launcher template in `scripts/deploy-linux-standalone.sh` so direct pass-through mode now also matches:
    - `--strict-retries` / `--strict-retries=*`
    - `--launch-target` / `--launch-target=*`
    - `--allow-pe-launch`
  - Fixes incorrect fallback where these valid options could be wrapped by implicit `--play-like` behavior.
  - Updated `scripts/verify-linux-offline-standalone.sh` to validate launcher pass-through with:
    - `Run-Emuera-Linux.sh --launch-target <binary> --verify-only`
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- MainWindow concrete coupling reduction milestone:
  - Added `Emuera/UI/Framework/Forms/IConsoleWindowHost.cs` as a host abstraction for `EmueraConsole` window interactions.
  - Updated `Emuera/UI/Game/EmueraConsole.cs` to depend on `IConsoleWindowHost` instead of concrete `MainWindow`:
    - constructor: `EmueraConsole(IConsoleWindowHost parent)`
    - field: `readonly IConsoleWindowHost window`
  - Updated `Emuera/UI/Framework/Forms/MainWindow.cs` to implement `IConsoleWindowHost` and added explicit hotkey bridge methods:
    - `HotkeyStateSet(...)`
    - `HotkeyStateInit(...)`
  - Added regression guardrail audit `scripts/audit-console-window-host-abstraction.sh` and wired into `scripts/ci-linux-porting.sh`.
  - Goal: prepare for alternate Linux GUI host injection by removing direct `EmueraConsole` compile-time dependency on WinForms `MainWindow` concrete class.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Window-host API surface narrowing milestone (phase 2):
  - Extended `IConsoleWindowHost` with non-control-oriented window state accessors:
    - `ClientAreaWidth/ClientAreaHeight`
    - `ScrollValue/ScrollMaximum/ScrollEnabled`
    - `InputText/InputBackColorArgb`
  - Implemented these accessors in `MainWindow` with `DesignerSerializationVisibility.Hidden` to avoid WinForms designer serialization warnings.
  - Replaced direct `window.ScrollBar`/`window.TextBox` access in key `EmueraConsole` paths with host accessor usage:
    - `Emuera/UI/Game/EmueraConsole.cs`
    - `Emuera/UI/Game/EmueraConsole.Print.cs`
    - `Emuera/UI/Framework/Forms/EmueraConsole.WindowAccess.cs`
  - Added guardrail audit `scripts/audit-console-window-control-access.sh` and wired into CI to block reintroduction of direct `window.ScrollBar`/`window.TextBox` access in those paths.
  - Goal: progressively reduce WinForms control-surface coupling in runtime-adjacent console logic before introducing alternate Linux GUI host implementations.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Window-host ownership refinement milestone (phase 3):
  - Moved host abstraction definition from Forms layer to UI/Game layer:
    - new location: `Emuera/UI/Game/IConsoleWindowHost.cs`
  - Removed `EmueraConsole` dependency on `MinorShift.Emuera.Forms` namespace for host-contract types:
    - `Emuera/UI/Game/EmueraConsole.cs`
    - `Emuera/UI/Framework/Forms/EmueraConsole.ToolTip.cs`
  - Simplified tooltip host contract surface:
    - removed host method `GetToolTipText(...)` and routed tooltip text through popup event args.
    - kept delay control through `ToolTipInitialDelay` property only.
  - Reduced `MainWindow` direct WinForms control surface:
    - removed public control passthrough properties (`MainPicBox`, `ScrollBar`, `TextBox`, `ToolTip`).
    - updated internal call sites to use concrete controls directly where needed.
  - Updated `ConfigDialog` to read viewport size via host-style dimensions instead of control handle access:
    - replaced `parent.MainPicBox.Width/Height` with `parent.ClientAreaWidth/ClientAreaHeight`.
  - Added guardrail audit `scripts/audit-mainwindow-control-surface.sh` and wired it into `scripts/ci-linux-porting.sh`.
  - Hardened bridge audit regex in `scripts/audit-ui-game-winforms-bridge.sh` to avoid false positives on custom host event names (`ConsoleToolTipPopupEventArgs`).
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- UI/Game WinForms decoupling milestone (Rikaichan path):
  - Removed direct `MinorShift.Emuera.Forms` dependency from `Emuera/UI/Game/Rikaichan.cs`.
  - Added platform-facade API for dictionary index dialog handoff:
    - `IUiPlatformBackend.TryShowRikaiIndexDialog(byte[] edict, Action<byte[]> onIndexReady)`
    - `UiPlatformBridge.TryShowRikaiIndexDialog(...)`
  - Implemented WinForms-side dialog opening in `Emuera/UI/Game/WinFormsBridge.cs` using `RikaiDialog`.
  - Implemented headless fallback in `Emuera/UI/Game/HeadlessUiPlatformBackend.cs` returning `false`.
  - Updated Rikaichan init flow to disable feature gracefully with info message when index dialog backend is unavailable.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Debug dialog bridge decoupling milestone:
  - Added `Emuera/UI/Game/IUiDebugDialogHandle.cs`.
  - Extended platform-facade contract:
    - `IUiPlatformBackend.TryCreateDebugDialog(EmueraConsole, Process)`
    - `UiPlatformBridge.TryCreateDebugDialog(...)`
  - Implemented WinForms debug dialog adapter in `Emuera/UI/Game/WinFormsBridge.cs`:
    - moved `DebugDialog` creation/translation/ownership into bridge-side handle.
  - Implemented headless fallback in `Emuera/UI/Game/HeadlessUiPlatformBackend.cs` returning `null`.
  - Updated `Emuera/UI/Framework/Forms/EmueraConsole.DebugDialog.cs`:
    - removed direct `DebugDialog` construction and direct Forms dependency.
    - uses bridge handle for open/focus/update/close.
  - Updated `Emuera/UI/Framework/Forms/MainWindow.cs`:
    - replaced direct `console.DebugDialog` access with `IsDebugDialogCreated/UpdateDebugDialog/CloseDebugDialog`.
  - Added guardrail audit `scripts/audit-console-debugdialog-bridge.sh` and wired into CI.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- DebugConfig host abstraction milestone:
  - Added `Emuera/UI/Framework/Forms/IDebugConfigDialogHost.cs`.
  - Updated `Emuera/UI/Framework/Forms/DebugDialog.cs` to implement `IDebugConfigDialogHost`.
  - Updated `Emuera/UI/Framework/Forms/DebugConfigDialog.cs`:
    - `SetConfig(DebugDialog ...)` -> `SetConfig(IDebugConfigDialogHost ...)`
    - removed concrete `DebugDialog` field binding.
    - reads width/height/position through host interface.
  - Added guardrail audit `scripts/audit-debugconfig-host-abstraction.sh` and wired into CI.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Linux audio fallback hardening milestone:
  - Updated `Emuera/Runtime/Utils/Sound.NAudio.cs` to keep non-Windows paths from crashing on Windows-only audio APIs:
    - removed hard fail on missing `SynchronizationContext.Current` in mixer/device setup.
    - non-Windows output now falls back to `DummyOut` instead of `WasapiOut`.
    - unsupported non-Windows media decode path now fails safely (debug log + return) instead of throwing to gameplay flow.
  - Added regression guardrail `scripts/audit-sound-linux-fallback.sh` and wired it into `scripts/ci-linux-porting.sh`.
  - Goal: prevent Linux runtime/play sessions from failing due residual WASAPI/MediaFoundation assumptions while WinForms/Windows behavior remains intact.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Program Linux launcher-host selection milestone:
  - Added `Emuera/UI/Game/LinuxLauncherUiAppHost.cs` implementing `IUiAppHost`.
  - Updated `Emuera/Program.cs` host selection:
    - env override: `EMUERA_UI_HOST=linux-launcher|launcher`
    - default: on Linux, select `LinuxLauncherUiAppHost` instead of unsupported-host fallback.
  - Linux launcher host behavior:
    - resolves launcher target from `EMUERA_LINUX_UI_LAUNCHER` or bundled candidates (`Run-Emuera-Linux.sh`, `Emuera.Cli`).
    - forwards CLI args; if none, defaults to `--interactive-gui`.
  - Goal: move non-Windows GUI entry flow from “hard fail” to launchable host chain while full native renderer replacement is still in progress.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- UI app-host contract neutrality milestone:
  - Updated `Emuera/UI/Game/IUiAppHost.cs` to remove direct `System.Drawing.Icon` contract exposure:
    - `TryRun(string[] args, Icon icon)` -> `TryRun(string[] args, object appIcon)`
  - Updated host implementations:
    - `Emuera/UI/Framework/Forms/WinFormsAppUiHost.cs` now casts `appIcon` to `Icon` only inside WinForms host boundary.
    - `Emuera/UI/Game/LinuxLauncherUiAppHost.cs` and `Emuera/UI/Game/UnsupportedUiAppHost.cs` now use runtime-neutral signature and do not depend on `System.Drawing`.
  - Added regression guardrail `scripts/audit-ui-app-host-neutrality.sh` and wired into `scripts/ci-linux-porting.sh`.
  - Goal: keep shared UI-host contract portable for non-Windows GUI backends while localizing WinForms types to Forms layer.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Program GDI neutrality milestone (entry-host cleanup):
  - `Emuera/Program.cs` no longer directly references `System.Drawing` (`Color.FromName`, `Rectangle`, `Icon`) for runtime hook wiring.
  - Named-color resolution now routes through `UiPlatformBridge.ResolveNamedColorRgb(...)`.
  - Sprite-creation hook now routes through runtime-neutral rectangle bridge (`AppContents.CreateSpriteG(..., RuntimeRect)`).
  - Configured icon load path now routes through `UiPlatformBridge.LoadConfiguredIcon(...)`.
- Added `scripts/audit-program-gdi-neutrality.sh` and wired it into `scripts/ci-linux-porting.sh`.
  - CI now fails if direct Program-level GDI usage reappears in host bootstrap paths.
- Runtime bridge host-hook milestone (color/font runtime probes):
  - Added `RuntimeHost.IsFontInstalledHook` + `RuntimeHost.IsFontInstalled(...)` in `Emuera.RuntimeCore`.
  - WinForms host wiring now sets `RuntimeHost.IsFontInstalledHook = UiPlatformBridge.IsFontInstalled`.
  - CLI host wiring now sets `RuntimeHost.IsFontInstalledHook` to a non-GUI-safe default (`false`).
  - `CHECKFONT` and `COLORFROMNAME` runtime-bridge methods now use RuntimeHost hooks:
    - `RuntimeHost.IsFontInstalled(...)`
    - `RuntimeHost.TryResolveNamedColorRgb(...)`
  - Removed direct `InstalledFontCollection` / `Color.FromName` probing from these runtime-facing function paths.
- Added `scripts/audit-runtime-bridge-color-font-hooks.sh` and wired it into `scripts/ci-linux-porting.sh`.
  - CI now fails if these runtime bridge methods regress back to direct platform probes.
- Headless drawing-fallback hardening milestone:
  - Updated `Emuera/UI/Game/HeadlessUiPlatformBackend.cs` to avoid direct `System.Drawing` rendering/measurement calls in non-Windows fallback paths:
    - `DrawText*`/`DrawText(...)` now no-op safely in headless mode.
    - `MeasureText*` now use internal heuristic size estimation (`EstimateTextSize`) instead of `Graphics.MeasureString`/`Bitmap` paths.
  - Added regression guardrail `scripts/audit-headless-backend-drawing-fallback.sh` and wired it into `scripts/ci-linux-porting.sh`.
  - Goal: keep Linux headless fallback path resilient when `System.Drawing` runtime support is partial/unavailable.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- ConfigDialog font-bridge consolidation milestone:
  - Updated `Emuera/UI/Framework/Forms/ConfigDialog.cs` to use `UiPlatformBridge.GetInstalledFontNames()` for both initial and manual font-list refresh paths.
  - Removed direct `InstalledFontCollection`/`PrivateFontCollection` probing from ConfigDialog.
  - Added `UiPlatformBridge.GetInstalledFontNames()` with safe fallback behavior when system font enumeration is unavailable.
  - Added regression guardrail `scripts/audit-configdialog-font-bridge.sh` and wired it into `scripts/ci-linux-porting.sh`.
  - Goal: keep font discovery path backend-driven and reduce WinForms form-layer platform probes.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- RuntimeBridge legacy interface cleanup milestone:
  - Removed unused aggregate bridge interfaces:
    - `Emuera/UI/Game/RuntimeBridge/IProcessConsole.cs`
    - `Emuera/UI/Game/RuntimeBridge/IProcessUiConsole.cs`
  - Updated `EmueraConsole` to implement runtime-facing contracts directly:
    - `IExecutionConsole`
    - `IProcessRuntimeConsole`
  - Added regression guardrail `scripts/audit-runtimebridge-legacy-process-console.sh` and wired it into `scripts/ci-linux-porting.sh`.
  - Goal: reduce leftover runtime/UI bridge surface and keep runtime boundary focused on active interfaces only.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Runtime bridge HTML-hook decoupling milestone:
  - Added RuntimeHost HTML hooks in `Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs`:
    - `HtmlLengthHook` / `HtmlLength(...)`
    - `HtmlSubStringHook` / `HtmlSubString(...)`
    - `HtmlToPlainTextHook` / `HtmlToPlainText(...)`
    - `HtmlEscapeHook` / `HtmlEscape(...)`
  - Updated runtime-facing function creator (`Emuera/UI/Game/RuntimeBridge/Creator.Method.cs`) to use RuntimeHost HTML hooks instead of direct `HtmlManager` calls for:
    - HTML length/sub-string functions
    - HTML plain-text conversion
    - HTML escape conversion
  - Updated focus-color getter path to runtime-neutral value (`Config.FocusColorRuntime`) instead of legacy `Color.ToArgb`.
  - Wired host behavior:
    - WinForms host (`Emuera/Program.cs`) maps hooks to `HtmlManager`.
    - CLI host (`Emuera.Cli/Program.cs`) maps hooks to lightweight fallback implementation (`CliHtmlFallback`).
  - Added regression guardrails:
    - `scripts/audit-runtime-bridge-html-hooks.sh`
    - `scripts/audit-runtime-host-html-wiring.sh`
  - Wired both audits into `scripts/ci-linux-porting.sh`.
  - Goal: keep runtime-side HTML semantics host-pluggable and reduce direct UI-layer coupling in runtime bridge paths.
  - Verified via:
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Parser-time process-coupling reduction milestone (`SetJumpTo` path):
  - Updated `Emuera/Runtime/Script/Statements/Instraction.Child.cs` to remove direct parse-time dependency on `RuntimeHost.GetCurrentProcess() as Process` for const-target pre-resolution.
  - `CALLF` / `TRYCALLF` `SetJumpTo` now resolve function methods via runtime-global dictionaries:
    - `RuntimeGlobals.IdentifierDictionary`
    - `RuntimeGlobals.LabelDictionary`
  - `CALL` const `SetJumpTo` no longer requires concrete `Process`; now uses dictionary-based call resolution.
  - `GOTO` const `SetJumpTo` now resolves `$` labels via `RuntimeGlobals.LabelDictionary` instead of current-process cast.
  - Extended `CalledFunction` (`Emuera/Runtime/Script/Process.CalledFunction.cs`) with dictionary-based overload:
    - `CallFunction(LabelDictionary labelDictionary, string label, LogicalLine retAddress)`
    - existing `CallFunction(Process, ...)` now delegates to the overload.
  - Goal: shrink parser/bootstrap reliance on concrete process instance and reduce a blocker for moving execution-loop/runtime script layers into RuntimeCore.
  - Verified via:
    - `dotnet build Emuera/Emuera.csproj -c Debug-NAudio`
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Loader process-coupling reduction milestone (`ErhLoader`/`ErbLoader` path):
  - Updated `Emuera/Runtime/Script/Loader/ErbLoader.cs` constructor to remove concrete `Process` dependency:
    - `ErbLoader(IScriptConsole, ExpressionMediator, Process)` -> `ErbLoader(IScriptConsole, ExpressionMediator)`
    - replaced scanline assignment/reset from `parentProcess.scaningLine` to `RuntimeGlobals.SetCurrentScanningLine(...)`.
  - Updated `Emuera/Runtime/Script/Loader/ErhLoader.cs` constructor to remove concrete `Process` dependency:
    - `ErhLoader(IScriptConsole, IdentifierDictionary, Process)` -> `ErhLoader(IScriptConsole, IdentifierDictionary, ExpressionMediator, VariableData)`
    - `DimLineWC` now receives injected `ExpressionMediator`.
    - user-defined variable creation now uses injected `VariableData` directly instead of `parentProcess.VEvaluator.VariableData`.
  - Updated callsites in `Emuera/Runtime/Script/Process.cs` to use the new loader constructors.
  - Goal: reduce runtime script-loader reliance on `GameProc.Process` and prepare the remaining execution-loop extraction to RuntimeCore.
  - Verified via:
    - `dotnet build Emuera/Emuera.csproj -c Debug-NAudio`
    - `scripts/audit-runtime-ui-coupling.sh`
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Runtime scanline access decoupling milestone (`RuntimeGlobals` path):
  - Updated `Emuera/Runtime/Utils/RuntimeGlobals.cs` to remove direct `Process` cast path from scanline access:
    - removed `RuntimeHost.GetCurrentProcess() as Process` usage for scanline get/set.
    - introduced runtime-bound scanline accessors:
      - `BindCurrentScanningLineAccessors(Func<LogicalLine>, Action<LogicalLine>)`
      - `CurrentScanningLine` now reads through bound getter.
      - `SetCurrentScanningLine(...)` now writes through bound setter.
  - Updated `Emuera/Runtime/Script/Process.cs` initialization to bind scanline accessors:
    - `RuntimeGlobals.BindCurrentScanningLineAccessors(GetScaningLine, SetScaningLine)`.
  - Goal: reduce `RuntimeGlobals` concrete-process dependency and keep scanline flow on runtime-side hooks, improving readiness for execution-loop relocation.
  - Verified via:
    - `dotnet build Emuera/Emuera.csproj -c Debug-NAudio`
    - `scripts/audit-runtime-ui-coupling.sh`
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- Debug dialog process/console host abstraction milestone:
  - Added runtime-facing debug process contract:
    - `Emuera/Runtime/Script/IDebugRuntimeProcess.cs`
  - Updated debug dialog bridge path to consume `IDebugRuntimeProcess` instead of concrete `Process`:
    - `Emuera/UI/Game/IUiPlatformBackend.cs`
    - `Emuera/UI/Game/UiPlatformBridge.cs`
    - `Emuera/UI/Game/WinFormsBridge.cs`
    - `Emuera/UI/Game/HeadlessUiPlatformBackend.cs`
    - `Emuera/UI/Framework/Forms/DebugDialog.cs`
  - Added debug dialog console host contract:
    - `Emuera/UI/Game/IDebugDialogConsoleHost.cs`
  - Updated debug dialog creation/evaluation path to consume `IDebugDialogConsoleHost` instead of concrete `EmueraConsole`.
  - Updated `EmueraConsole` binding:
    - class now implements `IDebugDialogConsoleHost`
    - explicit host property mapping added for `IsInProcess`.
  - Added regression guardrail:
    - `scripts/audit-debugdialog-console-host-abstraction.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: remove concrete process/console type coupling from debug dialog bridge so alternate Linux UI hosts can bind runtime/debug surfaces with smaller contracts.
  - Verified via:
    - `dotnet build Emuera/Emuera.csproj -c Debug-NAudio`
    - `scripts/audit-debugdialog-console-host-abstraction.sh`
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- MainWindow input-process abstraction milestone:
  - Updated WinForms console input bridge:
    - `Emuera/UI/Game/EmueraConsole.cs`
    - added wrapper methods:
      - `SetInputResultInteger(long idx, long value)`
      - `SetInputResultString(long idx, string value)`
      - `ClearInputTimeLimit()`
  - Updated MainWindow input-result write path:
    - `Emuera/UI/Framework/Forms/MainWindow.cs` now writes mouse/input result fields through `EmueraConsole` wrapper methods:
      - `SetInputResultInteger(...)`
      - `SetInputResultString(...)`
    - removed direct `CurrentEvaluator.RESULT_ARRAY/RESULTS_ARRAY` access from MainWindow.
    - removed direct `CurrentProcess` access from MainWindow.
    - replaced direct `console.inputReq.Timelimit` write with `console.ClearInputTimeLimit()` helper call.
  - Removed unnecessary console exposure:
    - `Emuera/UI/Game/EmueraConsole.cs` no longer exposes `CurrentEvaluator` / `CurrentProcess`.
  - Added regression guardrail:
    - `scripts/audit-mainwindow-runtime-process-abstraction.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: keep MainWindow input bridge bounded to `EmueraConsole` wrapper methods only, reducing runtime-process compile-time coupling and preparing alternate Linux UI host bindings.
  - Verified via:
    - `dotnet build Emuera/Emuera.csproj -c Debug-NAudio`
    - `scripts/audit-mainwindow-runtime-process-abstraction.sh`
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR`
- RuntimeHost log/title/generation hook extraction milestone:
  - Added RuntimeHost hook surfaces in `Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs`:
    - `MarkUpdatedGenerationHook` / `MarkUpdatedGeneration()`
    - `DisableOutputLogHook` / `DisableOutputLog()`
    - `OutputLogHook` / `OutputLog(...)`
    - `OutputSystemLogHook` / `OutputSystemLog(...)`
    - `ThrowTitleErrorHook` / `ThrowTitleError(...)`
  - Runtime script paths now use RuntimeHost hooks instead of `IProcessRuntimeConsole` members:
    - `Emuera/Runtime/Script/Process.SystemProc.cs`
    - `Emuera/UI/Game/RuntimeBridge/Creator.Method.cs` (`OUTPUTLOG` function path)
  - Host wiring updates:
    - `Emuera/Program.cs` wires WinForms console behavior to the new hooks.
    - `Emuera.Cli/Program.cs` wires non-UI safe defaults for the same hooks.
  - Reduced runtime console bridge surface:
    - removed legacy members from `Emuera.RuntimeCore/Runtime/Script/IProcessRuntimeConsole.cs`:
      - `noOutputLog`
      - `updatedGeneration`
      - `OutputLog(...)`
      - `OutputSystemLog(...)`
      - `ThrowTitleError(...)`
  - Added regression guardrail:
    - `scripts/audit-runtime-host-log-hooks.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: keep runtime-side logging/title error and generation update flows host-pluggable through RuntimeHost so future Linux runtime host implementations require a smaller process-console contract.
- RuntimeHost console-output hook extraction milestone:
  - Added RuntimeHost console-output hook surfaces in `Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs`:
    - `PrintBarHook` / `PrintBar()`
    - `PrintCustomBarHook` / `PrintCustomBar(...)`
    - `DebugPrintHook` / `DebugPrint(...)`
    - `DebugNewLineHook` / `DebugNewLine()`
    - `DebugClearHook` / `DebugClear()`
    - `PrintTemporaryLineHook` / `PrintTemporaryLine(...)`
  - Runtime paths switched from direct `IProcessRuntimeConsole` members to RuntimeHost hooks:
    - `Emuera/Runtime/Script/Process.SystemProc.cs`
    - `Emuera/Runtime/Script/Process.ScriptProc.cs`
    - `Emuera/Runtime/Script/Statements/Instraction.Child.cs`
    - `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs`
  - Host wiring updates:
    - `Emuera/Program.cs` wires WinForms behavior to the new hooks.
    - `Emuera.Cli/Program.cs` wires non-UI safe no-op defaults.
  - Reduced runtime console bridge surface:
    - removed members from `Emuera.RuntimeCore/Runtime/Script/IProcessRuntimeConsole.cs`:
      - `PrintBar()`
      - `printCustomBar(...)`
      - `DebugPrint(...)`
      - `DebugNewLine()`
      - `DebugClear()`
      - `PrintTemporaryLine(...)`
  - Added regression guardrail:
    - `scripts/audit-runtime-host-console-output-hooks.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: keep runtime rendering/debug text output host-pluggable and further shrink the process-console contract required by a future Linux runtime host.
- RuntimeHost refresh/delete hook extraction milestone:
  - Added RuntimeHost refresh/delete hook surfaces in `Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs`:
    - `DeleteLineHook` / `DeleteLine(...)`
    - `PrintFlushHook` / `PrintFlush(...)`
    - `RefreshStringsHook` / `RefreshStrings(...)`
  - Runtime paths switched from direct `IProcessRuntimeConsole` refresh/delete members to RuntimeHost hooks:
    - `Emuera/Runtime/Script/Process.cs` (initial load-label refresh path)
    - `Emuera/Runtime/Script/Process.SystemProc.cs`
    - `Emuera/Runtime/Script/Process.ScriptProc.cs`
    - `Emuera/Runtime/Script/Statements/Instraction.Child.cs`
    - `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs` (`FlushConsole`)
  - Host wiring updates:
    - `Emuera/Program.cs` wires WinForms behavior to the new hooks.
    - `Emuera.Cli/Program.cs` wires non-UI safe no-op defaults.
  - Reduced runtime console bridge surface:
    - removed members from `Emuera.RuntimeCore/Runtime/Script/IProcessRuntimeConsole.cs`:
      - `deleteLine(...)`
      - `PrintFlush(...)`
      - `RefreshStrings(...)`
  - Added regression guardrail:
    - `scripts/audit-runtime-host-refresh-hooks.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: keep runtime-side redraw/flush/delete-line flow host-pluggable while further shrinking process-console requirements for a future Linux runtime host.
- RuntimeHost error/style/timer hook extraction milestone:
  - Added RuntimeHost error/style/timer hook surfaces in `Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs`:
    - `ClearDisplayHook` / `ClearDisplay()`
    - `ResetStyleHook` / `ResetStyle()`
    - `ThrowErrorHook` / `ThrowError(...)`
    - `ForceStopTimerHook` / `ForceStopTimer()`
  - Runtime paths switched from direct `IProcessRuntimeConsole` members to RuntimeHost hooks:
    - `Emuera/Runtime/Script/Process.cs` (error handling)
    - `Emuera/Runtime/Script/Process.SystemProc.cs`
    - `Emuera/Runtime/Script/Statements/Instraction.Child.cs`
    - `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs` (`ClearDisplay`, `ForceStopTimer`)
  - Host wiring updates:
    - `Emuera/Program.cs` wires WinForms behavior to the new hooks.
    - `Emuera.Cli/Program.cs` wires non-UI safe no-op defaults.
  - Reduced runtime console bridge surface:
    - removed members from `Emuera.RuntimeCore/Runtime/Script/IProcessRuntimeConsole.cs`:
      - `ClearDisplay()`
      - `ResetStyle()`
      - `ThrowError(...)`
      - `forceStopTimer()`
  - Added regression guardrail:
    - `scripts/audit-runtime-host-error-style-hooks.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: keep runtime error/style/timer control host-pluggable and further reduce process-console coupling for Linux-native runtime hosts.

- RuntimeHost style/font/alignment/redraw hook extraction milestone:
  - Added RuntimeHost style-surface hook APIs in `Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs`:
    - `SetUseUserStyleHook` / `SetUseUserStyle(...)`
    - `SetUseSetColorStyleHook` / `SetUseSetColorStyle(...)`
    - `SetBackgroundColorRgbHook` / `GetBackgroundColorRgbHook`
    - `SetStringColorRgbHook` / `GetStringColorRgbHook`
    - `SetStringStyleFlagsHook` / `GetStringStyleFlagsHook`
    - `SetFontHook` / `GetFontNameHook`
    - `SetAlignmentHook` / `GetAlignmentHook`
    - `SetRedrawHook` / `SetRedraw(...)`
  - Runtime paths switched from direct `IProcessRuntimeConsole` style members to RuntimeHost hooks:
    - `Emuera/Runtime/Script/Process.ScriptProc.cs`
    - `Emuera/Runtime/Script/Process.SystemProc.cs`
    - `Emuera/Runtime/Script/Statements/ExpressionMediator.cs`
    - `Emuera/Runtime/Script/Statements/Instraction.Child.cs`
    - `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs`
    - `Emuera/UI/Game/RuntimeBridge/Creator.Method.cs`
  - Host wiring updates:
    - `Emuera/Program.cs` wires WinForms behavior to the new style hooks.
    - `Emuera.Cli/Program.cs` wires headless-safe defaults for the same hook surfaces.
  - Reduced runtime console bridge surface:
    - removed members from `Emuera.RuntimeCore/Runtime/Script/IProcessRuntimeConsole.cs`:
      - `Alignment`
      - `UseUserStyle`
      - `UseSetColorStyle`
      - `Set/GetBackgroundColorRgb(...)`
      - `Set/GetStringColorRgb(...)`
      - `Set/GetStringStyleFlags(...)`
      - `SetFont(...)`
      - `GetFontName()`
      - `SetRedraw(...)`
      - `Redraw`
  - Guardrail updates:
    - strengthened `scripts/audit-runtime-bridge-color-font-hooks.sh` to reject legacy creator-side style access and require RuntimeHost getter usage (`GetStringColorRgb/GetBackgroundColorRgb/GetStringStyleFlags/GetFontName/GetAlignment/GetRedrawMode`).
  - Goal: keep runtime style/color/font/alignment control host-pluggable and shrink remaining runtime-side UI contract for Linux-native runtime hosts.

- RuntimeHost input/button/mouse/textbox hook extraction milestone:
  - Added RuntimeHost input-surface hook APIs in `Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs`:
    - `PrintButton*Hook` / `PrintButton*()`
    - `PrintPlainSingleLineHook` / `PrintPlainSingleLine(...)`
    - `PrintErrorButtonHook` / `PrintErrorButton(...)`
    - `PrintPlainHook` / `PrintPlain(...)`
    - `ClearTextHook` / `ClearText()`
    - `ReloadErbFinishedHook` / `ReloadErbFinished()`
    - `IsLastLineEmptyHook` / `IsLastLineTemporaryHook` / `IsPrintBufferEmptyHook`
    - `CountInteractiveButtonsHook`
    - `GetConsoleClientWidthHook` / `IsConsoleActiveHook`
    - `GetMousePositionXYHook` / `MoveMouseXYHook`
    - `SetBitmapCacheEnabledForNextLineHook`
    - `SetRedrawTimerHook`
    - `GetTextBoxTextHook` / `ChangeTextBoxHook`
    - `ResetTextBoxPosHook` / `SetTextBoxPosHook`
    - `HotkeyStateSetHook` / `HotkeyStateInitHook`
  - Runtime paths switched from direct `IProcessRuntimeConsole` members to RuntimeHost hooks:
    - `Emuera/Runtime/Script/Process.cs`
    - `Emuera/Runtime/Script/Process.SystemProc.cs`
    - `Emuera/Runtime/Script/Process.ScriptProc.cs`
    - `Emuera/Runtime/Script/Statements/Instraction.Child.cs`
    - `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs`
    - `Emuera/UI/Game/RuntimeBridge/Creator.Method.cs`
  - Host wiring updates:
    - `Emuera/Program.cs` wires WinForms behavior to new input/button/mouse/textbox hooks.
    - `Emuera.Cli/Program.cs` wires headless-safe defaults.
  - Reduced runtime console bridge surface:
    - `Emuera.RuntimeCore/Runtime/Script/IProcessRuntimeConsole.cs` is now an empty marker interface.
  - Guardrail updates:
    - strengthened `scripts/audit-runtimebridge-legacy-process-console.sh` to fail if `IProcessRuntimeConsole` leaks outside allowlisted host/marker files.
  - Goal: remove runtime-script dependency on process-console member surface and keep host-specific input/render behavior behind RuntimeHost for Linux-native runtime-host split.

- Linux UI backend progression milestone:
  - Added `Emuera/UI/Game/LinuxShellUiPlatformBackend.cs` as a non-WinForms Linux backend for `IUiPlatformBackend`.
  - Linux shell backend behavior:
    - keeps drawing/measurement path runtime-safe via headless fallback,
    - upgrades dialog/confirm flow to `zenity` when available (`ShowInfo`, `ConfirmYesNo`) with console fallback,
    - keeps clipboard integration through Linux helpers (`wl-copy`/`xclip`/`xsel`) with existing fallback semantics.
  - Updated `Emuera/UI/Game/UiPlatformBridge.cs` default backend selection:
    - Windows => `WinFormsUiPlatformBackend`
    - Linux => `LinuxShellUiPlatformBackend`
    - Other platforms => `HeadlessUiPlatformBackend`
    - Added explicit `EMUERA_UI_BACKEND` aliases: `linux-shell`, `linux`, `zenity`.
  - Added regression guardrail:
    - `scripts/audit-ui-platform-linux-backend.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: move Linux runtime UX from pure headless fallback toward practical desktop interaction without reintroducing WinForms coupling.

- Linux launcher default-behavior refinement milestone:
  - Updated CLI no-argument default path in `Emuera.Cli/Program.cs`:
    - interactive TTY now defaults to play-like launcher flow (`autoPlay: true`) instead of menu-first flow.
    - menu-first default is still available via `EMUERA_DEFAULT_MODE=interactive|menu`.
    - redirected stdin behavior is unchanged (still defaults to smoke mode).
    - aligned Zenity fallback-menu exit behavior with console menu path (user menu exit now returns `0`).
  - Updated Linux app-host forwarder default in `Emuera/UI/Game/LinuxLauncherUiAppHost.cs`:
    - when no args are supplied, launcher now forwards `--play-like` by default.
  - Updated standalone desktop launcher template in `scripts/deploy-linux-standalone.sh`:
    - desktop entry now uses `Exec=./Run-Emuera-Linux.sh` (script default handles play-like fallback/menu transition).
  - Updated offline verification in `scripts/verify-linux-offline-standalone.sh`:
    - accepts both desktop Exec forms:
      - `Exec=./Run-Emuera-Linux.sh`
      - `Exec=./Run-Emuera-Linux.sh --interactive-gui`
  - Added regression guardrail:
    - `scripts/audit-launcher-default-playlike.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: make Linux standalone startup behavior closer to Windows "double-click to play" while preserving explicit menu mode and CI safety checks.

- RuntimeCore utility extraction milestone (`CtrlZ`):
  - Moved `CtrlZ` implementation from:
    - `Emuera/Runtime/Utils/CtrlZ.cs`
    - to `Emuera.RuntimeCore/Runtime/Utils/CtrlZ.cs`.
  - RuntimeHost now provides CtrlZ-related runtime-neutral hooks:
    - `IsCtrlZEnabledHook` / `IsCtrlZEnabled()`
    - `CaptureRandomSeedHook` / `CaptureRandomSeed(long[] seedBuffer)`
  - Host wiring updates:
    - WinForms host (`Emuera/Program.cs`) maps:
      - enable-check to `Config.Ctrl_Z_Enabled`
      - random-seed capture to `RuntimeGlobals.VEvaluator?.Rand.GetRand(...)`
    - CLI host (`Emuera.Cli/Program.cs`) maps safe defaults:
      - CtrlZ disabled
      - random-seed capture no-op
  - Build-surface update:
    - `Emuera/Emuera.csproj` excludes legacy compile path (`Runtime/Utils/CtrlZ.cs`) so runtime utility source of truth is RuntimeCore.
  - Added regression guardrail:
    - `scripts/audit-runtimecore-ctrlz-extraction.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: continue migrating runtime utility layer from WinForms host assembly toward shared RuntimeCore for Linux-native runtime-host progression.

- Runtime process/UI decoupling milestone (`ExpressionMediator` access reduction):
  - Removed UI-layer direct `Process.ExpressionMediator` access from:
    - `Emuera/UI/Game/EmueraConsole.cs`
    - `Emuera/UI/Framework/Forms/DebugDialog.cs`
  - Expanded runtime-process abstraction surface in `IDebugRuntimeProcess`:
    - added `SetResultString(...)`
    - added `RestoreRandomSeed(...)`
    - added `PrepareInstructionArguments(...)`
    - added `EvaluateExpressionForDebugWatch(...)`
    - removed direct `ExpressionMediator` property exposure.
  - Added corresponding bridge methods in `Emuera/Runtime/Script/Process.cs` and switched UI call sites to those methods.
  - Strengthened regression guardrail in `scripts/audit-mainwindow-runtime-process-abstraction.sh`:
    - now fails if UI files directly access `.ExpressionMediator`
    - validates new interface + process bridge method presence.
  - Goal: narrow UI dependence on runtime-internal evaluator types to make future `Process` extraction into RuntimeCore safer and more incremental.

- Runtime process/UI decoupling milestone (`Process` concrete-type exposure reduction):
  - Added `IRuntimeProcess` abstraction in:
    - `Emuera/Runtime/Script/IRuntimeProcess.cs`
  - Updated `Process` implementation surface:
    - `Emuera/Runtime/Script/Process.cs` now implements `IRuntimeProcess`
    - added bridge properties/methods needed by UI-facing process access (`NeedWaitToEventComEnd` bridge, runtime script metadata getters).
  - Replaced concrete process storage in UI:
    - `Emuera/UI/Game/EmueraConsole.cs` now stores `IRuntimeProcess` instead of `GameProc.Process`.
  - Reduced UI dependency on runtime concrete `GameBase` type:
    - `Emuera/UI/Game/EmueraConsole.Print.cs` now uses process bridge methods (`GetScriptTitle`, `GetScriptVersionText`) instead of `process.gameBase`.
  - Reduced UI dependency on runtime `LogicalLine` debug-scan type:
    - `Emuera/UI/Game/EmueraConsole.cs` now uses `RuntimeScanningLineInfo` via `GetScanningLineInfo()` instead of `process.GetScaningLine()`.
  - Strengthened regression guardrail in `scripts/audit-mainwindow-runtime-process-abstraction.sh`:
    - fails when UI log path references `process.gameBase`
    - fails when UI debug path references `process.GetScaningLine()`
    - validates `IRuntimeProcess` abstraction members and `Process` implementation.
  - Goal: continue shrinking UI compile-time dependency on runtime concrete implementation details before moving process loop/code into RuntimeCore.

- Runtime process/UI decoupling milestone (`Process` creation-path abstraction):
  - Added runtime-process creation hook in RuntimeCore host bridge:
    - `Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs`
    - `CreateRuntimeProcessHook`
    - `CreateRuntimeProcess(IExecutionConsole console)`
  - Switched UI process bootstrap path:
    - `Emuera/UI/Game/EmueraConsole.cs` no longer directly calls `new GameProc.Process(this)`.
    - Process instance is now resolved through `RuntimeHost.CreateRuntimeProcess(...)` and validated as `IRuntimeProcess`.
  - Wired WinForms host factory ownership:
    - `Emuera/Program.cs` now sets `RuntimeHost.CreateRuntimeProcessHook = static (console) => new GameProc.Process(console);`.
  - Wired CLI safe default:
    - `Emuera.Cli/Program.cs` sets `RuntimeHost.CreateRuntimeProcessHook = static (_) => null;`.
  - Strengthened regression guardrail in `scripts/audit-mainwindow-runtime-process-abstraction.sh`:
    - fails if `EmueraConsole` directly creates `GameProc.Process`
    - fails if WinForms host factory hook wiring is missing.
  - Goal: move runtime `Process` construction ownership out of UI view class and keep host-specific runtime wiring in bootstrap layer.

- Runtime process/UI decoupling milestone (`LogicalLine` current-line exposure reduction):
  - Reduced UI dependency on runtime `LogicalLine` object identity for input-generation flow.
  - Updated `IRuntimeProcess`:
    - removed `LogicalLine getCurrentLine { get; }` from UI-facing process abstraction.
    - added `long GetCurrentLineMarker()` bridge method.
  - Updated `Process` bridge implementation:
    - `Emuera/Runtime/Script/Process.cs` now exposes `GetCurrentLineMarker()` via runtime object marker (`RuntimeHelpers.GetHashCode`).
  - Updated `EmueraConsole` input-generation tracking:
    - replaced `LogicalLine lastInputLine` comparison with marker-based comparison (`lastInputLineMarker`).
  - Strengthened regression guardrail in `scripts/audit-mainwindow-runtime-process-abstraction.sh`:
    - fails if UI still references `process.getCurrentLine`
    - fails if `IRuntimeProcess` still exposes `LogicalLine getCurrentLine`
    - validates `GetCurrentLineMarker` abstraction + process bridge implementation.
  - Goal: keep UI process contract free from runtime statement object types and prepare further runtime extraction to non-Windows host projects.

- Runtime process/UI decoupling milestone (`DebugCommand` bridge extraction):
  - Reduced UI dependency on runtime statement parser/argument internals in debug-command path.
  - Updated `IDebugRuntimeProcess`:
    - removed `PrepareInstructionArguments(...)` from UI-facing debug interface.
    - removed `InstructionLine` type leakage from debug process contract.
  - Added debug-command execution bridge in `IRuntimeProcess`:
    - `RuntimeDebugCommandResult ExecuteDebugCommand(string command, bool munchkin, bool outputDebugConsole)`.
  - Updated `Process`:
    - moved debug-command parse/validate/execute flow into `Process.ExecuteDebugCommand(...)`.
    - UI now receives only bridge result (`DisplayCommand`, `ShouldEchoCommand`) and no longer touches instruction-level runtime APIs.
  - Updated `EmueraConsole`:
    - replaced direct debug-command instruction handling with `process.ExecuteDebugCommand(...)` call.
  - Strengthened regression guardrail in `scripts/audit-mainwindow-runtime-process-abstraction.sh`:
    - fails if UI calls `process.PrepareInstructionArguments(...)` or `process.DoDebugNormalFunction(...)` directly.
    - fails if `IDebugRuntimeProcess` exposes `PrepareInstructionArguments(...)` or `InstructionLine`.
    - validates new `ExecuteDebugCommand(...)` abstraction on interface and process implementation.
  - Goal: keep debug console workflow on runtime-process abstraction boundary and reduce UI compile-time dependency on runtime statement types.

- RuntimeCore extraction progress (process-contract stage):
  - Moved process abstraction contracts from `Emuera` to `Emuera.RuntimeCore`:
    - `Emuera.RuntimeCore/Runtime/Script/IDebugRuntimeProcess.cs`
    - `Emuera.RuntimeCore/Runtime/Script/IRuntimeProcess.cs`
    - includes bridge data contracts:
      - `RuntimeScanningLineInfo`
      - `RuntimeDebugCommandResult`
  - Updated `Emuera/Emuera.csproj`:
    - compile-removes legacy source files:
      - `Runtime/Script/IDebugRuntimeProcess.cs`
      - `Runtime/Script/IRuntimeProcess.cs`
  - Updated `scripts/audit-mainwindow-runtime-process-abstraction.sh`:
    - process-interface checks now target RuntimeCore contract paths.
    - added guard to ensure legacy Emuera-side contract files are compile-removed when still present.
  - Promoted process contracts and factory surface to typed RuntimeCore API:
    - `IDebugRuntimeProcess` and `IRuntimeProcess` are now `public`.
    - `RuntimeScanningLineInfo` and `RuntimeDebugCommandResult` are now `public` bridge records.
    - `RuntimeHost.CreateRuntimeProcessHook` is now strongly typed:
      - `Func<IExecutionConsole, IRuntimeProcess>`
    - `RuntimeHost.CreateRuntimeProcess(...)` now returns `IRuntimeProcess`.
    - `EmueraConsole` process bootstrap no longer uses `as IRuntimeProcess` cast path.
  - Added regression guardrail:
    - `scripts/audit-runtimecore-process-contract-extraction.sh`
    - wired into `scripts/ci-linux-porting.sh`
    - now also enforces strongly-typed `RuntimeHost.CreateRuntimeProcess*` signatures.
  - Extracted parser token/operator primitives into RuntimeCore:
    - `Emuera.RuntimeCore/Runtime/Script/Parser.SubWord.cs`
    - `Emuera.RuntimeCore/Runtime/Script/Parser.Word.cs`
    - `Emuera.RuntimeCore/Runtime/Script/Parser.WordCollection.cs`
    - `Emuera.RuntimeCore/Runtime/Script/Expression.OperatorCode.cs`
    - legacy Emuera-side source paths are now compile-removed in `Emuera/Emuera.csproj`:
      - `Runtime/Script/Parser/SubWord.cs`
      - `Runtime/Script/Parser/Word.cs`
      - `Runtime/Script/Parser/WordCollection.cs`
      - `Runtime/Script/Statements/Expression/OperatorCode.cs`
  - Added regression guardrail:
    - `scripts/audit-runtimecore-parser-primitives-extraction.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Verification after this change set:
    - `dotnet build Emuera/Emuera.csproj -c Debug-NAudio` succeeded.
    - `dotnet build Emuera.Cli/Emuera.Cli.csproj -c Debug` succeeded.
    - `scripts/audit-mainwindow-runtime-process-abstraction.sh` passed.
    - `scripts/audit-runtime-ui-coupling.sh` passed.
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR` passed (publish/deploy/offline verify included).
  - Goal: make runtime-process contracts RuntimeCore-owned ahead of moving larger `Process` execution/loader pieces out of WinForms host assembly.
- RuntimeCore script enum extraction (latest)
  - Moved `FunctionArgType`, `FunctionCode`, and `VariableCode` enum definitions into `Emuera.RuntimeCore` for continued runtime-core ownership expansion.
  - `Emuera/Emuera.csproj` now compile-removes legacy enum files (`FunctionArgType.cs`, `BuiltInFunctionCode.cs`, `VariableCode.cs`) so `Emuera` consumes RuntimeCore-owned definitions.
  - Added `scripts/audit-runtimecore-script-enums-extraction.sh` and wired it into `scripts/ci-linux-porting.sh`.
- RuntimeCore circular-buffer extraction (latest)
  - Moved `ICircularBuffer<T>` / `CircularBuffer<T>` implementation into `Emuera.RuntimeCore`.
  - `Emuera/Emuera.csproj` now compile-removes legacy `Runtime/Script/Statements/CircularBuffer.cs`.
  - Added `scripts/audit-runtimecore-circular-buffer-extraction.sh` and wired it into `scripts/ci-linux-porting.sh`.
- RuntimeCore process-state enum extraction (latest)
  - Extracted `SystemStateCode` and `BeginType` from `Process.State.cs` into RuntimeCore:
    - `Emuera.RuntimeCore/Runtime/Script/SystemStateCode.cs`
    - `Emuera.RuntimeCore/Runtime/Script/BeginType.cs`
  - `Emuera/Runtime/Script/Process.State.cs` now consumes RuntimeCore-owned enums (legacy enum declarations removed).
  - Added `scripts/audit-runtimecore-process-state-enums-extraction.sh` and wired it into `scripts/ci-linux-porting.sh`.
- RuntimeCore statement-local enum extraction (latest)
  - Extracted statement/expression-local enums into RuntimeCore:
    - `CaseExpressionType` -> `Emuera.RuntimeCore/Runtime/Script/Statements.CaseExpressionType.cs`
    - `SortOrder` -> `Emuera.RuntimeCore/Runtime/Script/Statements.SortOrder.cs`
    - `ArgsEndWith` -> `Emuera.RuntimeCore/Runtime/Script/Expression.ArgsEndWith.cs`
    - `TermEndWith` -> `Emuera.RuntimeCore/Runtime/Script/Expression.TermEndWith.cs`
  - Removed legacy enum declarations from:
    - `Emuera/Runtime/Script/Statements/CaseExpression.cs`
    - `Emuera/Runtime/Script/Statements/Argument.cs`
    - `Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs`
  - Added `scripts/audit-runtimecore-statement-local-enums-extraction.sh` and wired it into `scripts/ci-linux-porting.sh`.

- RuntimeCore lexical-parser enum extraction (latest)
  - Extracted `LexEndWith`, `FormStrEndWith`, `StrEndWith`, and `LexAnalyzeFlag` enum ownership from `Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs` to RuntimeCore parser primitives (`Emuera.RuntimeCore/Runtime/Script/Parser.Word.cs`).
  - Removed legacy enum declarations from `LexicalAnalyzer.cs` so runtime parser code now consumes RuntimeCore-owned definitions.
  - Extended `scripts/audit-runtimecore-parser-primitives-extraction.sh` with regression checks for:
    - RuntimeCore enum presence
    - legacy `LexicalAnalyzer.cs` enum declaration removal

- RuntimeCore statement-local enum extraction (clipboard trigger follow-up)
  - Extracted CBTriggers from Emuera/Runtime/Script/Statements/Clipboard.cs to RuntimeCore statement enum surface (Emuera.RuntimeCore/Runtime/Script/Statements.SortOrder.cs).
  - ClipboardProcessor now consumes RuntimeCore-owned CBTriggers declaration only (legacy nested enum removed).
  - Extended scripts/audit-runtimecore-statement-local-enums-extraction.sh with CBTriggers presence/removal guardrails.

- RuntimeCore config-enum extraction (latest)
  - Moved runtime config enum set from `Emuera/Runtime/Config/ConfigCode.cs` into RuntimeCore:
    - `Emuera.RuntimeCore/Runtime/Config/ConfigCode.cs`
  - `Emuera/Emuera.csproj` now compile-removes legacy `Runtime/Config/ConfigCode.cs` so host/runtime code consumes RuntimeCore-owned config enums.
  - Extended `scripts/audit-runtimecore-script-enums-extraction.sh` to verify RuntimeCore config enum ownership and compile-remove wiring.

- RuntimeCore statement-local enum extraction (DT option follow-up)
  - Extracted `DTOptions` from `SpDtColumnOptions` nested declaration in `Emuera/Runtime/Script/Statements/Argument.cs` to RuntimeCore:
    - `Emuera.RuntimeCore/Runtime/Script/Statements.DTOptions.cs`
  - Updated DT column option consumers to use RuntimeCore-owned enum:
    - `Emuera/Runtime/Script/Statements/ArgumentBuilder.cs`
    - `Emuera/Runtime/Script/Statements/Instraction.Child.cs`
  - Extended `scripts/audit-runtimecore-statement-local-enums-extraction.sh` with `DTOptions` presence/removal guardrails.

- RuntimeCore function-arg enum extraction (ArgType follow-up)
  - Extracted function argument bit-flag enum `ArgType` from `FunctionMethod` nested declaration to RuntimeCore:
    - `Emuera.RuntimeCore/Runtime/Script/Function.ArgType.cs`
  - `FunctionMethod` now consumes RuntimeCore-owned `ArgType` declaration (legacy nested enum removed).
  - Extended `scripts/audit-runtimecore-script-enums-extraction.sh` with RuntimeCore `ArgType` presence and legacy removal checks.

- RuntimeCore script-data enum extraction (identifier-name type follow-up)
  - Extracted `DefinedNameType` from `IdentifierDictionary` nested declaration to RuntimeCore:
    - `Emuera.RuntimeCore/Runtime/Script/Data.DefinedNameType.cs`
  - `IdentifierDictionary` now consumes RuntimeCore-owned `DefinedNameType` declaration (legacy nested enum removed).
  - Extended `scripts/audit-runtimecore-script-enums-extraction.sh` with RuntimeCore `DefinedNameType` presence and legacy removal checks.

- CLI runtime-process bootstrap follow-up
  - Replaced hardcoded null runtime-process factory wiring in CLI host:
    - from `RuntimeHost.CreateRuntimeProcessHook = static (_) => null;`
    - to `RuntimeHost.CreateRuntimeProcessHook = RuntimeProcessResolver.TryCreate;`
  - Added `Emuera.Cli/RuntimeProcessResolver.cs`:
    - resolves optional runtime-engine assembly (`Emuera.RuntimeEngine.dll` by default, or `EMUERA_RUNTIME_ENGINE_ASSEMBLY` path override)
    - binds static factory method contract:
      - type: `MinorShift.Emuera.RuntimeEngine.RuntimeProcessFactory`
      - method: `Create(IExecutionConsole) -> IRuntimeProcess`
  - Extended `scripts/audit-runtimecore-process-contract-extraction.sh` to guard against regression back to hardcoded null CLI process factory.

- Linux case-sensitive filesystem compatibility follow-up (runtime loader stability)
  - Added cross-platform file-search utility in RuntimeCore:
    - `Emuera.RuntimeCore/Runtime/Utils/RuntimeFileSearch.cs`
    - provides:
      - case-insensitive glob file enumeration (`GetFiles(...)`)
      - case-insensitive single file resolution (`ResolveFilePath(...)`)
      - case-insensitive directory resolution (`ResolveDirectoryPath(...)`)
  - Applied case-insensitive glob resolution to runtime loaders/config paths that previously assumed Windows case-insensitive behavior:
    - `Emuera/Runtime/Config/Config.cs`
    - `Emuera/Runtime/Script/Loader/ErhLoader.cs`
    - `Emuera/Runtime/Script/Data/ConstantData.cs`
    - `Emuera/Runtime/Script/Statements/Variable/VariableEvaluator.cs`
    - `Emuera/Runtime/Utils/EvilMask/Lang.cs`
    - `Emuera/Runtime/Utils/PluginSystem/PluginManager.cs`
  - Applied case-insensitive single-file path resolution to fixed-name runtime boot files:
    - `Emuera/Runtime/Script/Process.cs`
      - `_Replace.csv`, `_Rename.csv`, `GAMEBASE.CSV`, `macro.txt`
    - `Emuera/Runtime/Config/ConfigData.cs`
      - `_default.config`, `_fixed.config`, `default.config`, `fixed.config`
    - `Emuera/Runtime/Script/KeyMacro.cs`
      - `macro.txt`
  - Updated game-layout bootstrap checks to be case-insensitive for both folder and file probes:
    - `Emuera.RuntimeCore/Bootstrap/GameDataLayout.cs`
      - `CSV`/`ERB` directory detection now resolves case-insensitively.
      - runtime prerequisite checks (`GAMEBASE.CSV`, `emuera.config`, `TITLE.ERB`, `SYSTEM.ERB`) now resolve case-insensitively.
  - Practical runtime impact verified on Linux game pack (`/home//tw/eraTWKR`):
    - embedded runtime mode now passes title-start flow without the previous fatal startup path (`ADDCHARA` / unresolved core CSV dictionaries due filename case mismatch).
    - deployed standalone binary (`Emuera1824+v11+webp+test+fix.exe`) now reaches title menu and accepts runtime input in `--run-engine` mode.
  - Verification:
    - `dotnet build Emuera.Cli/Emuera.Cli.csproj -c Debug --no-restore` succeeded.
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR` passed (publish/deploy/offline verify included).
    - runtime manual check:
      - `/home//tw/eraTWKR/Emuera1824+v11+webp+test+fix.exe --game-dir /home//tw/eraTWKR --run-engine`

- Linux case-sensitive filesystem compatibility follow-up (config/runtime deep-path hardening)
  - Strengthened RuntimeCore path resolution to handle nested directory case mismatches:
    - `Emuera.RuntimeCore/Runtime/Utils/RuntimeFileSearch.cs`
    - `ResolveDirectoryPath(...)` now resolves parent segments recursively before matching the current segment.
    - `ResolveFilePath(...)` now resolves parent directory via `ResolveDirectoryPath(...)` first.
    - `GetFiles(...)` now resolves target directory path before glob enumeration.
  - Hardened runtime config path handling to avoid stale/static startup path capture and case-sensitive misses:
    - `Emuera/Runtime/Config/ConfigData.cs`
    - replaced static `configPath`/`configdebugPath` fields with runtime-resolved helpers:
      - `ResolveConfigPath()`
      - `ResolveDebugConfigPath()`
    - `LoadConfig`/`SaveConfig` and `LoadDebugConfig`/`SaveDebugConfig` now use resolved paths per call.
  - Hardened JSON side-config path handling with runtime/case-insensitive resolution:
    - `Emuera/Runtime/Config/JSON/JSONConfig.cs`
    - replaced static `_configFilePath` with `ResolveConfigPath()` per call for `Load`/`Save`.
  - Hardened runtime raw-line error lookup path handling:
    - `Emuera/Runtime/Script/Process.cs`
    - `getRawTextFormFilewithLine(...)` now resolves `.erb` / `.csv` paths through `RuntimeFileSearch.ResolveFilePath(...)`.
  - Hardened GAMEBASE load/inspection path handling:
    - `Emuera/Runtime/Script/Data/GameBase.cs`
    - `Emuera.RuntimeCore/Bootstrap/GameBaseInspector.cs`
    - both now resolve GAMEBASE path via `RuntimeFileSearch.ResolveFilePath(...)` before file checks/read.
  - Hardened runtime-engine font probing fallback on non-Windows hosts:
    - `Emuera.RuntimeEngine/Compat/UiPlatformBridge.cs`
    - `TryCreateFont(...)` now exits early on non-Windows before attempting `System.Drawing.Font` construction.
  - Verification:
    - `dotnet build Emuera.RuntimeCore/Emuera.RuntimeCore.csproj -c Debug --no-restore` succeeded.
    - `dotnet build Emuera.RuntimeEngine/Emuera.RuntimeEngine.csproj -c Debug --no-restore` succeeded.
    - `dotnet build Emuera.Cli/Emuera.Cli.csproj -c Debug --no-restore` succeeded.
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR` passed (publish/deploy/offline verify included).

- Linux UI backend progression follow-up (Rikai index generation fallback hardening)
  - Extracted dictionary index generation logic from WinForms dialog into shared helper:
    - `Emuera/UI/Game/RikaiIndexGenerator.cs`
    - shared method: `TryGenerateAndSave(...)`
  - Updated WinForms dialog path to consume shared generator:
    - `Emuera/UI/Framework/Forms/RikaiDialog.cs`
    - background worker now delegates generation/persistence to `RikaiIndexGenerator`.
  - Implemented Linux/headless backend fallback generation path for missing `.ind`:
    - `Emuera/UI/Game/LinuxShellUiPlatformBackend.cs`
    - `Emuera/UI/Game/HeadlessUiPlatformBackend.cs`
    - both now generate `{Config.RikaiFilename}.ind` and invoke callback instead of returning unavailable by default.
  - Added regression guardrail:
    - `scripts/audit-rikai-index-backend.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: keep Rikaichan startup path functional on non-Windows backends without WinForms dialog dependency.

- Linux UI backend progression follow-up (debug dialog fallback hardening)
  - Added backend-neutral text debug dialog handle:
    - `Emuera/UI/Game/TextDebugDialogHandle.cs`
    - writes snapshot to `debug-dialog.snapshot.txt` under debug/base directory and updates via backend handle lifecycle.
  - Wired non-Windows backends to return a real debug dialog handle instead of `null`:
    - `Emuera/UI/Game/LinuxShellUiPlatformBackend.cs`
    - `Emuera/UI/Game/HeadlessUiPlatformBackend.cs`
  - Improved headless key parser parity:
    - `Emuera/UI/Game/HeadlessUiPlatformBackend.cs`
    - `TryParseKeyCode(...)` now accepts both numeric key codes and `ConsoleKey` names.
  - Added regression guardrail:
    - `scripts/audit-linux-debugdialog-backend.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: keep debug workflow usable on Linux/headless runtime paths without WinForms debug window dependency.

- Linux CLI runtime progression follow-up (redirected input retry hardening)
  - Updated runtime engine input loop for redirected stdin mode:
    - `Emuera.Cli/Program.cs`
    - invalid redirected input no longer aborts immediately; now retries by consuming subsequent lines.
    - added bounded retry guard via `EMUERA_CLI_REDIRECT_INVALID_LIMIT` (default: `200`).
  - Added regression guardrail:
    - `scripts/audit-cli-redirect-input-retry.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: improve scripted/non-interactive play validation stability when redirected input stream includes transient invalid lines.

- Linux CLI runtime progression follow-up (runtime output/style bridge hardening)
  - Added CLI runtime-host bridge for `--run-engine` mode:
    - `Emuera.Cli/CliRuntimeHostBridge.cs`
    - wires runtime output/style hooks to terminal-visible behavior (plain text, buttons, flush, bars, temporary lines, debug/error output).
    - adds ANSI-aware style/control fallback for supported terminals (alignment, color/style flags, clear/delete line handling).
    - adds HTML/image/shape fallback rendering path for terminal mode:
      - `RuntimeHost.PrintHtmlHook`, `PrintHtmlIslandHook`, `PrintImageHook`, `PrintShapeHook`
      - HTML now prints as plain text (with HTML history capture), image/shape paths emit readable terminal tokens.
    - adds display-line history hooks for runtime compatibility:
      - `RuntimeHost.GetDisplayLineTextHook`, `GetDisplayLineHtmlHook`, `PopDisplayLineHtmlHook`, `GetPointingButtonInputHook`
      - keeps bounded in-memory history for runtime script queries in CLI play mode.
  - Updated runtime engine path to attach bridge before process creation:
    - `Emuera.Cli/Program.cs`
    - `CliRuntimeHostBridge.AttachExecutionConsole(executionConsole);`
  - Added regression guardrail:
    - `scripts/audit-cli-runtime-output-bridge.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Goal: reduce no-op output paths in Linux embedded runtime play mode and improve terminal-side gameplay parity.

- Linux CLI runtime progression follow-up (primitive input parsing hardening)
  - Updated primitive input prompt path in `--run-engine`:
    - `Emuera.Cli/Program.cs`
    - `InputType.PrimitiveMouseKey` now uses line-input path (with timeout handling) instead of key-only direct capture.
  - Extended primitive packet parser in CLI execution console:
    - `Emuera.Cli/CliExecutionConsole.cs`
    - key packet input now accepts WinForms-style key names and modifier expressions:
      - examples: `A`, `ENTER`, `ctrl+z`, `key:f5:ctrl:shift`
    - existing packet forms remain supported:
      - `key:<code>[:keyData]`
      - `wheel:<delta>[:x[:y]]`
      - `mouse:<button>[:x[:y[:map[:result5]]]]`
      - CSV packet form (`r0,r1,r2,r3,r4,r5`)
  - Runtime impact:
    - Linux terminal runtime can express broader primitive input combinations without WinForms key/mouse event objects.
  - Small compatibility cleanup:
    - `Emuera.Cli/CliRuntimeHostBridge.cs` now prefers `EMUERA_CTRLZ_ENABLED` and keeps legacy `EMUERA_CTRZ_ENABLED` as fallback alias.
  - Added regression guardrail:
    - `scripts/audit-cli-primitive-input-parser.sh`
    - wired into `scripts/ci-linux-porting.sh`
  - Verification:
    - `dotnet build /home//tw/emuera.em/Emuera.Cli/Emuera.Cli.csproj -c Debug --no-restore` passed.
    - `scripts/audit-cli-runtime-output-bridge.sh` passed.
    - `scripts/audit-cli-redirect-input-retry.sh` passed.
    - `scripts/ci-linux-porting.sh /home//tw/eraTWKR` passed (publish/deploy/offline verify included).
