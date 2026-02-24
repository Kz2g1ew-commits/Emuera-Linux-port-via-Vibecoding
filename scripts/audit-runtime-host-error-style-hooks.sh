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
  "ClearDisplay\\(|ResetStyle\\(|ThrowError\\(|forceStopTimer\\(" \
  "$INTERFACE_FILE" || true)

if [[ ${#interface_hits[@]} -gt 0 ]]; then
  echo "Legacy error/style/timer members are still exposed in IProcessRuntimeConsole:"
  printf '%s\n' "${interface_hits[@]}"
  STATUS=3
else
  echo "Runtime console interface audit passed: error/style/timer members removed."
fi

mapfile -t runtime_hits < <(rg -n --no-heading --color=never \
  "(ProcessConsole\\?\\.(ResetStyle|ThrowError)|AsRuntimeConsole\\(exm\\)\\?\\.ResetStyle|processConsole\\.(ClearDisplay|forceStopTimer))" \
  "${RUNTIME_SCOPE[@]}" -g"*.cs" || true)

if [[ ${#runtime_hits[@]} -gt 0 ]]; then
  echo "Runtime paths still use legacy process-console error/style/timer members:"
  printf '%s\n' "${runtime_hits[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=4
  fi
else
  echo "Runtime error/style/timer audit passed: runtime paths use RuntimeHost hooks."
fi

missing_runtimehost=()
required_runtimehost=(
  "public static Action ClearDisplayHook"
  "public static Action ResetStyleHook"
  "public static Action<bool> ThrowErrorHook"
  "public static Action ForceStopTimerHook"
  "public static void ClearDisplay\\(\\)"
  "public static void ResetStyle\\(\\)"
  "public static void ThrowError\\(bool playSound\\)"
  "public static void ForceStopTimer\\(\\)"
)

for pattern in "${required_runtimehost[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$RUNTIME_HOST_FILE"; then
    missing_runtimehost+=("$pattern")
  fi
done

if [[ ${#missing_runtimehost[@]} -gt 0 ]]; then
  echo "RuntimeHost is missing required error/style/timer hook surfaces:"
  printf '  %s\n' "${missing_runtimehost[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=5
  fi
else
  echo "RuntimeHost error/style/timer audit passed."
fi

missing_program_wiring=()
required_program_wiring=(
  "RuntimeHost.ClearDisplayHook ="
  "RuntimeHost.ResetStyleHook ="
  "RuntimeHost.ThrowErrorHook ="
  "RuntimeHost.ForceStopTimerHook ="
)

for pattern in "${required_program_wiring[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$PROGRAM_FILE"; then
    missing_program_wiring+=("$pattern")
  fi
done

if [[ ${#missing_program_wiring[@]} -gt 0 ]]; then
  echo "Program host wiring is missing required RuntimeHost error/style/timer hooks:"
  printf '  %s\n' "${missing_program_wiring[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=6
  fi
else
  echo "Program host error/style/timer wiring audit passed."
fi

missing_cli_wiring=()
for pattern in "${required_program_wiring[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$CLI_PROGRAM_FILE"; then
    missing_cli_wiring+=("$pattern")
  fi
done

if [[ ${#missing_cli_wiring[@]} -gt 0 ]]; then
  echo "CLI host wiring is missing required RuntimeHost error/style/timer hooks:"
  printf '  %s\n' "${missing_cli_wiring[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=7
  fi
else
  echo "CLI host error/style/timer wiring audit passed."
fi

exit "$STATUS"
