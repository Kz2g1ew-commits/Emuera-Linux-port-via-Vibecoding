#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STATUS=0

INTERFACE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/IProcessRuntimeConsole.cs"
RUNTIME_SCOPE=(
  "$ROOT_DIR/Emuera/Runtime/Script"
  "$ROOT_DIR/Emuera/Runtime/Utils/PluginSystem/PluginManager.cs"
)
RUNTIME_HOST_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs"
PROGRAM_FILE="$ROOT_DIR/Emuera/Program.cs"
CLI_PROGRAM_FILE="$ROOT_DIR/Emuera.Cli/Program.cs"

mapfile -t interface_hits < <(rg -n --no-heading --color=never \
  "PrintBar\\(|printCustomBar\\(|DebugPrint\\(|DebugNewLine\\(|DebugClear\\(|PrintTemporaryLine\\(" \
  "$INTERFACE_FILE" || true)

if [[ ${#interface_hits[@]} -gt 0 ]]; then
  echo "Legacy console-output members are still exposed in IProcessRuntimeConsole:"
  printf '%s\n' "${interface_hits[@]}"
  STATUS=3
else
  echo "Runtime console interface audit passed: console-output members removed."
fi

mapfile -t runtime_hits < <(rg -n --no-heading --color=never \
  "(ProcessConsole\\?\\.(PrintBar|printCustomBar|DebugPrint|DebugNewLine|DebugClear|PrintTemporaryLine)|AsRuntimeConsole\\(exm\\)\\?\\.(printCustomBar|DebugPrint|DebugNewLine|DebugClear|PrintTemporaryLine))" \
  "${RUNTIME_SCOPE[@]}" -g"*.cs" || true)

if [[ ${#runtime_hits[@]} -gt 0 ]]; then
  echo "Runtime paths still use legacy process-console console-output members:"
  printf '%s\n' "${runtime_hits[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=4
  fi
else
  echo "Runtime console-output audit passed: runtime paths use RuntimeHost output hooks."
fi

missing_runtimehost=()
required_runtimehost=(
  "public static Action PrintBarHook"
  "public static Action<string, bool> PrintCustomBarHook"
  "public static Action<string> DebugPrintHook"
  "public static Action DebugNewLineHook"
  "public static Action DebugClearHook"
  "public static Action<string> PrintTemporaryLineHook"
  "public static void PrintBar\\(\\)"
  "public static void PrintCustomBar\\(string barText, bool isConst\\)"
  "public static void DebugPrint\\(string text\\)"
  "public static void DebugNewLine\\(\\)"
  "public static void DebugClear\\(\\)"
  "public static void PrintTemporaryLine\\(string text\\)"
)

for pattern in "${required_runtimehost[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$RUNTIME_HOST_FILE"; then
    missing_runtimehost+=("$pattern")
  fi
done

if [[ ${#missing_runtimehost[@]} -gt 0 ]]; then
  echo "RuntimeHost is missing required console-output hook surfaces:"
  printf '  %s\n' "${missing_runtimehost[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=5
  fi
else
  echo "RuntimeHost console-output audit passed."
fi

missing_program_wiring=()
required_program_wiring=(
  "RuntimeHost.PrintBarHook ="
  "RuntimeHost.PrintCustomBarHook ="
  "RuntimeHost.DebugPrintHook ="
  "RuntimeHost.DebugNewLineHook ="
  "RuntimeHost.DebugClearHook ="
  "RuntimeHost.PrintTemporaryLineHook ="
)

for pattern in "${required_program_wiring[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$PROGRAM_FILE"; then
    missing_program_wiring+=("$pattern")
  fi
done

if [[ ${#missing_program_wiring[@]} -gt 0 ]]; then
  echo "Program host wiring is missing required RuntimeHost console-output hooks:"
  printf '  %s\n' "${missing_program_wiring[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=6
  fi
else
  echo "Program host console-output wiring audit passed."
fi

missing_cli_wiring=()
for pattern in "${required_program_wiring[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$CLI_PROGRAM_FILE"; then
    missing_cli_wiring+=("$pattern")
  fi
done

if [[ ${#missing_cli_wiring[@]} -gt 0 ]]; then
  echo "CLI host wiring is missing required RuntimeHost console-output hooks:"
  printf '  %s\n' "${missing_cli_wiring[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=7
  fi
else
  echo "CLI host console-output wiring audit passed."
fi

exit "$STATUS"
