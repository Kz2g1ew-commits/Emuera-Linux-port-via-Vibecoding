#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

RUNTIMECORE_SYSTEM_STATE_CODE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/SystemStateCode.cs"
RUNTIMECORE_BEGIN_TYPE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/BeginType.cs"
LEGACY_PROCESS_STATE_FILE="$ROOT_DIR/Emuera/Runtime/Script/Process.State.cs"

if [[ ! -f "$RUNTIMECORE_SYSTEM_STATE_CODE_FILE" ]]; then
  echo "RuntimeCore process-state-enums extraction audit failed: missing RuntimeCore SystemStateCode file." >&2
  exit 1
fi

if [[ ! -f "$RUNTIMECORE_BEGIN_TYPE_FILE" ]]; then
  echo "RuntimeCore process-state-enums extraction audit failed: missing RuntimeCore BeginType file." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script;" "$RUNTIMECORE_SYSTEM_STATE_CODE_FILE" >/dev/null; then
  echo "RuntimeCore process-state-enums extraction audit failed: SystemStateCode namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum SystemStateCode" "$RUNTIMECORE_SYSTEM_STATE_CODE_FILE" >/dev/null; then
  echo "RuntimeCore process-state-enums extraction audit failed: SystemStateCode enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script;" "$RUNTIMECORE_BEGIN_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore process-state-enums extraction audit failed: BeginType namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum BeginType" "$RUNTIMECORE_BEGIN_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore process-state-enums extraction audit failed: BeginType enum declaration missing." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "internal enum SystemStateCode" "$LEGACY_PROCESS_STATE_FILE" >/dev/null; then
  echo "RuntimeCore process-state-enums extraction audit failed: legacy SystemStateCode enum still declared in Process.State.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "internal enum BeginType" "$LEGACY_PROCESS_STATE_FILE" >/dev/null; then
  echo "RuntimeCore process-state-enums extraction audit failed: legacy BeginType enum still declared in Process.State.cs." >&2
  exit 1
fi

echo "RuntimeCore process-state-enums extraction audit passed: ProcessState enums are RuntimeCore-owned."
