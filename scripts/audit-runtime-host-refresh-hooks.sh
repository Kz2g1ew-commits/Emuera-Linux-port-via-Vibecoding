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
  "deleteLine\\(|PrintFlush\\(|RefreshStrings\\(" \
  "$INTERFACE_FILE" || true)

if [[ ${#interface_hits[@]} -gt 0 ]]; then
  echo "Legacy refresh/delete members are still exposed in IProcessRuntimeConsole:"
  printf '%s\n' "${interface_hits[@]}"
  STATUS=3
else
  echo "Runtime console interface audit passed: refresh/delete members removed."
fi

mapfile -t runtime_hits < <(rg -n --no-heading --color=never \
  "(ProcessConsole\\?\\.(deleteLine|PrintFlush|RefreshStrings)|AsRuntimeConsole\\(exm\\)\\?\\.(deleteLine|RefreshStrings)|processConsole\\?\\.(PrintFlush|RefreshStrings))" \
  "${RUNTIME_SCOPE[@]}" -g"*.cs" || true)

if [[ ${#runtime_hits[@]} -gt 0 ]]; then
  echo "Runtime paths still use legacy process-console refresh/delete members:"
  printf '%s\n' "${runtime_hits[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=4
  fi
else
  echo "Runtime refresh/delete audit passed: runtime paths use RuntimeHost hooks."
fi

missing_runtimehost=()
required_runtimehost=(
  "public static Action<int> DeleteLineHook"
  "public static Action<bool> PrintFlushHook"
  "public static Action<bool> RefreshStringsHook"
  "public static void DeleteLine\\(int lineCount\\)"
  "public static void PrintFlush\\(bool force\\)"
  "public static void RefreshStrings\\(bool forcePaint\\)"
)

for pattern in "${required_runtimehost[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$RUNTIME_HOST_FILE"; then
    missing_runtimehost+=("$pattern")
  fi
done

if [[ ${#missing_runtimehost[@]} -gt 0 ]]; then
  echo "RuntimeHost is missing required refresh/delete hook surfaces:"
  printf '  %s\n' "${missing_runtimehost[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=5
  fi
else
  echo "RuntimeHost refresh/delete audit passed."
fi

missing_program_wiring=()
required_program_wiring=(
  "RuntimeHost.DeleteLineHook ="
  "RuntimeHost.PrintFlushHook ="
  "RuntimeHost.RefreshStringsHook ="
)

for pattern in "${required_program_wiring[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$PROGRAM_FILE"; then
    missing_program_wiring+=("$pattern")
  fi
done

if [[ ${#missing_program_wiring[@]} -gt 0 ]]; then
  echo "Program host wiring is missing required RuntimeHost refresh/delete hooks:"
  printf '  %s\n' "${missing_program_wiring[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=6
  fi
else
  echo "Program host refresh/delete wiring audit passed."
fi

missing_cli_wiring=()
for pattern in "${required_program_wiring[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$CLI_PROGRAM_FILE"; then
    missing_cli_wiring+=("$pattern")
  fi
done

if [[ ${#missing_cli_wiring[@]} -gt 0 ]]; then
  echo "CLI host wiring is missing required RuntimeHost refresh/delete hooks:"
  printf '  %s\n' "${missing_cli_wiring[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=7
  fi
else
  echo "CLI host refresh/delete wiring audit passed."
fi

exit "$STATUS"
