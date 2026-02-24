#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STATUS=0

INTERFACE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/IProcessRuntimeConsole.cs"
RUNTIME_SCOPE="$ROOT_DIR/Emuera/Runtime/Script"
RUNTIME_HOST_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs"
PROGRAM_FILE="$ROOT_DIR/Emuera/Program.cs"
CLI_PROGRAM_FILE="$ROOT_DIR/Emuera.Cli/Program.cs"

mapfile -t interface_hits < <(rg -n --no-heading --color=never \
  "noOutputLog|updatedGeneration|OutputLog\\(|OutputSystemLog\\(|ThrowTitleError\\(" \
  "$INTERFACE_FILE" || true)

if [[ ${#interface_hits[@]} -gt 0 ]]; then
  echo "Legacy log/title/generation members are still exposed in IProcessRuntimeConsole:"
  printf '%s\n' "${interface_hits[@]}"
  STATUS=3
else
  echo "Runtime console interface audit passed: legacy log/title/generation members removed."
fi

mapfile -t runtime_hits < <(rg -n --no-heading --color=never \
  "ProcessConsole\\?\\.(OutputLog|OutputSystemLog|ThrowTitleError)|ProcessConsole\\.(updatedGeneration|noOutputLog)" \
  "$RUNTIME_SCOPE" -g"*.cs" || true)

if [[ ${#runtime_hits[@]} -gt 0 ]]; then
  echo "Runtime script still uses legacy ProcessConsole log/title/generation members:"
  printf '%s\n' "${runtime_hits[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=4
  fi
else
  echo "Runtime script audit passed: log/title/generation paths use RuntimeHost hooks."
fi

missing_runtimehost=()
required_runtimehost=(
  "public static Action MarkUpdatedGenerationHook"
  "public static Action DisableOutputLogHook"
  "public static Func<string, bool, bool> OutputLogHook"
  "public static Func<string, bool> OutputSystemLogHook"
  "public static Action<bool> ThrowTitleErrorHook"
  "public static void MarkUpdatedGeneration\\(\\)"
  "public static void DisableOutputLog\\(\\)"
  "public static bool OutputLog\\(string filename, bool hideInfo\\)"
  "public static bool OutputSystemLog\\(string filename\\)"
  "public static void ThrowTitleError\\(bool error\\)"
)

for pattern in "${required_runtimehost[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$RUNTIME_HOST_FILE"; then
    missing_runtimehost+=("$pattern")
  fi
done

if [[ ${#missing_runtimehost[@]} -gt 0 ]]; then
  echo "RuntimeHost is missing required log/title/generation hook surfaces:"
  printf '  %s\n' "${missing_runtimehost[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=5
  fi
else
  echo "RuntimeHost audit passed: required log/title/generation hook surfaces exist."
fi

missing_program_wiring=()
required_program_wiring=(
  "RuntimeHost.MarkUpdatedGenerationHook ="
  "RuntimeHost.DisableOutputLogHook ="
  "RuntimeHost.OutputLogHook ="
  "RuntimeHost.OutputSystemLogHook ="
  "RuntimeHost.ThrowTitleErrorHook ="
)

for pattern in "${required_program_wiring[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$PROGRAM_FILE"; then
    missing_program_wiring+=("$pattern")
  fi
done

if [[ ${#missing_program_wiring[@]} -gt 0 ]]; then
  echo "Program host wiring is missing required RuntimeHost log/title/generation hooks:"
  printf '  %s\n' "${missing_program_wiring[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=6
  fi
else
  echo "Program host wiring audit passed."
fi

missing_cli_wiring=()
for pattern in "${required_program_wiring[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$CLI_PROGRAM_FILE"; then
    missing_cli_wiring+=("$pattern")
  fi
done

if [[ ${#missing_cli_wiring[@]} -gt 0 ]]; then
  echo "CLI host wiring is missing required RuntimeHost log/title/generation hooks:"
  printf '  %s\n' "${missing_cli_wiring[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=7
  fi
else
  echo "CLI host wiring audit passed."
fi

exit "$STATUS"
