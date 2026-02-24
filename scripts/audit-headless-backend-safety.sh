#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="$ROOT_DIR/Emuera/UI/Game/HeadlessUiPlatformBackend.cs"

if rg -n '\bthrow\b' "$TARGET" >/dev/null 2>&1; then
  echo "Headless backend safety audit failed: throw usage found in HeadlessUiPlatformBackend." >&2
  exit 1
fi

if rg -n 'PlatformNotSupportedException' "$TARGET" >/dev/null 2>&1; then
  echo "Headless backend safety audit failed: PlatformNotSupportedException usage found in HeadlessUiPlatformBackend." >&2
  exit 1
fi

echo "Headless backend safety audit passed: no throw/PlatformNotSupportedException in headless backend."
