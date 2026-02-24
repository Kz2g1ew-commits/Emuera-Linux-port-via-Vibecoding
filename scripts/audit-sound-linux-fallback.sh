#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="$ROOT_DIR/Emuera/Runtime/Utils/Sound.NAudio.cs"

if ! [[ -f "$TARGET" ]]; then
  echo "Sound Linux fallback audit failed: missing file $TARGET" >&2
  exit 1
fi

if rg -n 'SynchronizationContext\.Current is null' "$TARGET" >/dev/null 2>&1; then
  echo "Sound Linux fallback audit failed: hard SynchronizationContext null-throw remains." >&2
  exit 1
fi

if ! rg -n 'OperatingSystem\.IsWindows\(\)' "$TARGET" >/dev/null 2>&1; then
  echo "Sound Linux fallback audit failed: platform guard for Windows-only audio path is missing." >&2
  exit 1
fi

if ! rg -n 'new DummyOut\(' "$TARGET" >/dev/null 2>&1; then
  echo "Sound Linux fallback audit failed: dummy output fallback path is missing." >&2
  exit 1
fi

if ! rg -n 'else if \(OperatingSystem\.IsWindows\(\)\)' "$TARGET" >/dev/null 2>&1; then
  echo "Sound Linux fallback audit failed: MediaFoundation reader path must remain Windows-only." >&2
  exit 1
fi

echo "Sound Linux fallback audit passed: non-Windows audio path has guarded fallback."
