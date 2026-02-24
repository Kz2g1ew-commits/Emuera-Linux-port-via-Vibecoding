#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_FILE="$ROOT_DIR/Emuera/UI/Framework/Forms/DebugConfigDialog.cs"

if rg -n --pcre2 --no-heading --color=never \
  "(DebugDialog\\s+dd\\b|SetConfig\\(\\s*DebugDialog\\s+)" \
  "$TARGET_FILE" >/dev/null; then
  echo "DebugConfigDialog host abstraction audit failed: DebugConfigDialog is still bound to DebugDialog concrete type." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "IDebugConfigDialogHost dd;" "$TARGET_FILE" >/dev/null; then
  echo "DebugConfigDialog host abstraction audit failed: IDebugConfigDialogHost field binding is missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "SetConfig(IDebugConfigDialogHost" "$TARGET_FILE" >/dev/null; then
  echo "DebugConfigDialog host abstraction audit failed: SetConfig(IDebugConfigDialogHost ...) binding is missing." >&2
  exit 1
fi

echo "DebugConfigDialog host abstraction audit passed: DebugConfigDialog depends on IDebugConfigDialogHost."
