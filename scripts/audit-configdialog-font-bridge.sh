#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="$ROOT_DIR/Emuera/UI/Framework/Forms/ConfigDialog.cs"

if rg -n '(InstalledFontCollection|PrivateFontCollection)' "$TARGET" >/dev/null 2>&1; then
  echo "ConfigDialog font bridge audit failed: direct font collection probing remains in ConfigDialog." >&2
  exit 1
fi

if ! rg -n 'UiPlatformBridge\.GetInstalledFontNames\(\)' "$TARGET" >/dev/null 2>&1; then
  echo "ConfigDialog font bridge audit failed: UiPlatformBridge.GetInstalledFontNames usage is missing." >&2
  exit 1
fi

echo "ConfigDialog font bridge audit passed: font enumeration uses UiPlatformBridge."
