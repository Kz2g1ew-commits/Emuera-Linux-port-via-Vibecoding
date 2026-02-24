#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EMUERA_CSPROJ_FILE="$ROOT_DIR/Emuera/Emuera.csproj"

RUNTIMECORE_CIRCULAR_BUFFER_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Statements.CircularBuffer.cs"
LEGACY_CIRCULAR_BUFFER_FILE="$ROOT_DIR/Emuera/Runtime/Script/Statements/CircularBuffer.cs"

if [[ ! -f "$RUNTIMECORE_CIRCULAR_BUFFER_FILE" ]]; then
  echo "RuntimeCore circular-buffer extraction audit failed: missing RuntimeCore CircularBuffer file." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Statements;" "$RUNTIMECORE_CIRCULAR_BUFFER_FILE" >/dev/null; then
  echo "RuntimeCore circular-buffer extraction audit failed: CircularBuffer RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "public interface ICircularBuffer<T>" "$RUNTIMECORE_CIRCULAR_BUFFER_FILE" >/dev/null; then
  echo "RuntimeCore circular-buffer extraction audit failed: ICircularBuffer declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "public class CircularBuffer<T> : ICircularBuffer<T>, IEnumerable<T>" "$RUNTIMECORE_CIRCULAR_BUFFER_FILE" >/dev/null; then
  echo "RuntimeCore circular-buffer extraction audit failed: CircularBuffer declaration missing." >&2
  exit 1
fi

if [[ -f "$LEGACY_CIRCULAR_BUFFER_FILE" ]] && ! rg -n --fixed-strings --no-heading --color=never "<Compile Remove=\"Runtime/Script/Statements/CircularBuffer.cs\" />" "$EMUERA_CSPROJ_FILE" >/dev/null; then
  echo "RuntimeCore circular-buffer extraction audit failed: legacy CircularBuffer file is not compile-removed." >&2
  exit 1
fi

echo "RuntimeCore circular-buffer extraction audit passed: CircularBuffer is RuntimeCore-owned."
