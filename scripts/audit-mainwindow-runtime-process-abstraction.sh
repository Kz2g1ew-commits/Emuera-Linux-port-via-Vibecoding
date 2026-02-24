#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONSOLE_FILE="$ROOT_DIR/Emuera/UI/Game/EmueraConsole.cs"
CONSOLE_PRINT_FILE="$ROOT_DIR/Emuera/UI/Game/EmueraConsole.Print.cs"
MAINWINDOW_FILE="$ROOT_DIR/Emuera/UI/Framework/Forms/MainWindow.cs"
DEBUG_DIALOG_FILE="$ROOT_DIR/Emuera/UI/Framework/Forms/DebugDialog.cs"
PROCESS_INTERFACE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/IDebugRuntimeProcess.cs"
RUNTIME_PROCESS_INTERFACE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/IRuntimeProcess.cs"
EMUERA_CSPROJ_FILE="$ROOT_DIR/Emuera/Emuera.csproj"
LEGACY_PROCESS_INTERFACE_FILE="$ROOT_DIR/Emuera/Runtime/Script/IDebugRuntimeProcess.cs"
LEGACY_RUNTIME_PROCESS_INTERFACE_FILE="$ROOT_DIR/Emuera/Runtime/Script/IRuntimeProcess.cs"
PROCESS_FILE="$ROOT_DIR/Emuera/Runtime/Script/Process.cs"
PROGRAM_FILE="$ROOT_DIR/Emuera/Program.cs"

if ! rg -n --fixed-strings --no-heading --color=never "public void SetInputResultInteger(long idx, long value)" "$CONSOLE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: EmueraConsole.SetInputResultInteger wrapper is missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "public void SetInputResultString(long idx, string value)" "$CONSOLE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: EmueraConsole.SetInputResultString wrapper is missing." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "CurrentEvaluator" "$CONSOLE_FILE" "$MAINWINDOW_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: legacy CurrentEvaluator access still exists." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "CurrentProcess" "$MAINWINDOW_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: MainWindow still accesses CurrentProcess directly." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "console.inputReq" "$MAINWINDOW_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: MainWindow still accesses console.inputReq directly." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "GameProc.Process" "$MAINWINDOW_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: MainWindow still references GameProc.Process concrete type." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "GameProc.Process process;" "$CONSOLE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: EmueraConsole still stores GameProc.Process concrete type." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "new GameProc.Process(this)" "$CONSOLE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: EmueraConsole still creates GameProc.Process directly." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "RuntimeHost.CreateRuntimeProcessHook = static (console) => new GameProc.Process(console);" "$PROGRAM_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: Program runtime-process factory hook wiring is missing." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "process.gameBase" "$CONSOLE_PRINT_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: EmueraConsole UI log path still accesses process.gameBase directly." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "process.GetScaningLine(" "$CONSOLE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: EmueraConsole debug path still accesses process.GetScaningLine directly." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "process.getCurrentLine" "$CONSOLE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: EmueraConsole still accesses process.getCurrentLine directly." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "console.SetInputResultInteger(" "$MAINWINDOW_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: MainWindow does not use SetInputResultInteger wrapper." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "console.SetInputResultString(" "$MAINWINDOW_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: MainWindow does not use SetInputResultString wrapper." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never ".ExpressionMediator" "$CONSOLE_FILE" "$DEBUG_DIALOG_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: UI layer still accesses Process.ExpressionMediator directly." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "process.PrepareInstructionArguments(" "$CONSOLE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: EmueraConsole still calls process.PrepareInstructionArguments directly." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "process.DoDebugNormalFunction(" "$CONSOLE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: EmueraConsole still calls process.DoDebugNormalFunction directly." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "ExpressionMediator { get; }" "$PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IDebugRuntimeProcess still exposes ExpressionMediator directly." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "PrepareInstructionArguments(" "$PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IDebugRuntimeProcess still exposes PrepareInstructionArguments." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "InstructionLine" "$PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IDebugRuntimeProcess still leaks InstructionLine type." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "string EvaluateExpressionForDebugWatch(string expression);" "$PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IDebugRuntimeProcess missing EvaluateExpressionForDebugWatch abstraction." >&2
  exit 1
fi

if [[ -f "$LEGACY_PROCESS_INTERFACE_FILE" ]] && ! rg -n --fixed-strings --no-heading --color=never "<Compile Remove=\"Runtime/Script/IDebugRuntimeProcess.cs\" />" "$EMUERA_CSPROJ_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: legacy Emuera IDebugRuntimeProcess file is not compile-removed." >&2
  exit 1
fi

if [[ -f "$LEGACY_RUNTIME_PROCESS_INTERFACE_FILE" ]] && ! rg -n --fixed-strings --no-heading --color=never "<Compile Remove=\"Runtime/Script/IRuntimeProcess.cs\" />" "$EMUERA_CSPROJ_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: legacy Emuera IRuntimeProcess file is not compile-removed." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "public string EvaluateExpressionForDebugWatch(string expression)" "$PROCESS_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: Process missing EvaluateExpressionForDebugWatch bridge method." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "interface IRuntimeProcess : IDebugRuntimeProcess" "$RUNTIME_PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IRuntimeProcess interface is missing or not connected to IDebugRuntimeProcess." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "GameBase gameBase { get; }" "$RUNTIME_PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IRuntimeProcess still exposes GameBase concrete type." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "string GetScriptTitle();" "$RUNTIME_PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IRuntimeProcess is missing GetScriptTitle abstraction." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "string GetScriptVersionText();" "$RUNTIME_PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IRuntimeProcess is missing GetScriptVersionText abstraction." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "LogicalLine getCurrentLine { get; }" "$RUNTIME_PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IRuntimeProcess still exposes LogicalLine getCurrentLine." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "long GetCurrentLineMarker();" "$RUNTIME_PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IRuntimeProcess is missing GetCurrentLineMarker abstraction." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "public long GetCurrentLineMarker()" "$PROCESS_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: Process is missing GetCurrentLineMarker bridge method." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "LogicalLine GetScaningLine();" "$RUNTIME_PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IRuntimeProcess still exposes LogicalLine GetScaningLine." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "RuntimeScanningLineInfo GetScanningLineInfo();" "$RUNTIME_PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IRuntimeProcess is missing GetScanningLineInfo abstraction." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "RuntimeDebugCommandResult ExecuteDebugCommand(string command, bool munchkin, bool outputDebugConsole);" "$RUNTIME_PROCESS_INTERFACE_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: IRuntimeProcess is missing ExecuteDebugCommand abstraction." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal sealed partial class Process(IExecutionConsole view) : IRuntimeProcess" "$PROCESS_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: Process does not implement IRuntimeProcess." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "public RuntimeScanningLineInfo GetScanningLineInfo()" "$PROCESS_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: Process is missing GetScanningLineInfo bridge method." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "public RuntimeDebugCommandResult ExecuteDebugCommand(string command, bool munchkin, bool outputDebugConsole)" "$PROCESS_FILE" >/dev/null; then
  echo "MainWindow runtime-process abstraction audit failed: Process is missing ExecuteDebugCommand bridge method." >&2
  exit 1
fi

echo "MainWindow runtime-process abstraction audit passed: MainWindow/UI input, debug, and runtime-process creation paths use process abstraction methods."
