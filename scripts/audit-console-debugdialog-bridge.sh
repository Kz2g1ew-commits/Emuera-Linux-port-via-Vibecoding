#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_FILE="$ROOT_DIR/Emuera/UI/Framework/Forms/EmueraConsole.DebugDialog.cs"

if rg -n --no-heading --color=never \
  "using[[:space:]]+MinorShift\\.Emuera\\.Forms;|new[[:space:]]+DebugDialog\\(" \
  "$TARGET_FILE" >/dev/null; then
  echo "Console debug-dialog bridge audit failed: direct WinForms DebugDialog usage found in EmueraConsole." >&2
  rg -n --no-heading --color=never \
    "using[[:space:]]+MinorShift\\.Emuera\\.Forms;|new[[:space:]]+DebugDialog\\(" \
    "$TARGET_FILE" >&2 || true
  exit 1
fi

if ! rg -n --no-heading --color=never "UiPlatformBridge\\.TryCreateDebugDialog\\(" "$TARGET_FILE" >/dev/null; then
  echo "Console debug-dialog bridge audit failed: platform bridge creation call not found." >&2
  exit 1
fi

echo "Console debug-dialog bridge audit passed: EmueraConsole uses UiPlatformBridge for debug dialog creation."
