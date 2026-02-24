#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="$ROOT_DIR/Emuera/UI/Game/HeadlessUiPlatformBackend.cs"

if rg -n '(\.MeasureString\(|\.DrawString\(|Graphics\.FromImage\(|new[[:space:]]+Bitmap\()' "$TARGET" >/dev/null 2>&1; then
  echo "Headless backend drawing fallback audit failed: direct System.Drawing rendering/measurement calls found." >&2
  exit 1
fi

echo "Headless backend drawing fallback audit passed: no direct System.Drawing rendering/measurement calls in headless backend."
