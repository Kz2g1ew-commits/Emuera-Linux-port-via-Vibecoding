#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNTIMECORE_DEBUG_IF_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/IDebugRuntimeProcess.cs"
RUNTIMECORE_PROCESS_IF_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/IRuntimeProcess.cs"
RUNTIMECORE_HOST_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs"
RUNTIME_ENGINE_FACTORY_FILE="$ROOT_DIR/Emuera.RuntimeEngine/RuntimeProcessFactory.cs"
CLI_CSPROJ_FILE="$ROOT_DIR/Emuera.Cli/Emuera.Cli.csproj"
CLI_PROGRAM_FILE="$ROOT_DIR/Emuera.Cli/Program.cs"
EMUERA_SCRIPT_DIR="$ROOT_DIR/Emuera/Runtime/Script"

if [[ ! -f "$RUNTIMECORE_DEBUG_IF_FILE" ]]; then
  echo "RuntimeCore process-contract extraction audit failed: missing IDebugRuntimeProcess in RuntimeCore." >&2
  exit 1
fi

if [[ ! -f "$RUNTIMECORE_PROCESS_IF_FILE" ]]; then
  echo "RuntimeCore process-contract extraction audit failed: missing IRuntimeProcess in RuntimeCore." >&2
  exit 1
fi

if [[ ! -f "$RUNTIME_ENGINE_FACTORY_FILE" ]]; then
  echo "RuntimeCore process-contract extraction audit failed: missing RuntimeEngine factory file." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "interface IDebugRuntimeProcess" "$EMUERA_SCRIPT_DIR" >/dev/null; then
  echo "RuntimeCore process-contract extraction audit failed: Emuera runtime script scope still defines IDebugRuntimeProcess." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "interface IRuntimeProcess" "$EMUERA_SCRIPT_DIR" >/dev/null; then
  echo "RuntimeCore process-contract extraction audit failed: Emuera runtime script scope still defines IRuntimeProcess." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "string EvaluateExpressionForDebugWatch(string expression);" "$RUNTIMECORE_DEBUG_IF_FILE" >/dev/null; then
  echo "RuntimeCore process-contract extraction audit failed: RuntimeCore IDebugRuntimeProcess missing EvaluateExpressionForDebugWatch contract." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "RuntimeDebugCommandResult ExecuteDebugCommand(string command, bool munchkin, bool outputDebugConsole);" "$RUNTIMECORE_PROCESS_IF_FILE" >/dev/null; then
  echo "RuntimeCore process-contract extraction audit failed: RuntimeCore IRuntimeProcess missing ExecuteDebugCommand contract." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "public static Func<IExecutionConsole, IRuntimeProcess> CreateRuntimeProcessHook { get; set; }" "$RUNTIMECORE_HOST_FILE" >/dev/null; then
  echo "RuntimeCore process-contract extraction audit failed: RuntimeHost CreateRuntimeProcessHook is not strongly typed to IRuntimeProcess." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "public static IRuntimeProcess CreateRuntimeProcess(IExecutionConsole console)" "$RUNTIMECORE_HOST_FILE" >/dev/null; then
  echo "RuntimeCore process-contract extraction audit failed: RuntimeHost CreateRuntimeProcess return type is not IRuntimeProcess." >&2
  exit 1
fi

if ! rg -n --no-heading --color=never "public static IRuntimeProcess\\? Create\\(IExecutionConsole console\\)" "$RUNTIME_ENGINE_FACTORY_FILE" >/dev/null; then
  echo "RuntimeCore process-contract extraction audit failed: RuntimeEngine factory signature mismatch." >&2
  exit 1
fi

if ! rg -n --no-heading --color=never "ProjectReference Include=\"..[/\\\\]Emuera.RuntimeEngine[/\\\\]Emuera.RuntimeEngine.csproj\"" "$CLI_CSPROJ_FILE" >/dev/null; then
  echo "RuntimeCore process-contract extraction audit failed: CLI project does not reference Emuera.RuntimeEngine." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "RuntimeHost.CreateRuntimeProcessHook = static (_) => null;" "$CLI_PROGRAM_FILE" >/dev/null; then
  echo "RuntimeCore process-contract extraction audit failed: CLI host still hardcodes a null CreateRuntimeProcessHook." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "RuntimeHost.CreateRuntimeProcessHook = RuntimeProcessResolver.TryCreate;" "$CLI_PROGRAM_FILE" >/dev/null; then
  echo "RuntimeCore process-contract extraction audit failed: CLI host does not wire RuntimeProcessResolver.Create hook." >&2
  exit 1
fi

echo "RuntimeCore process-contract extraction audit passed: runtime process contracts are RuntimeCore-owned."
