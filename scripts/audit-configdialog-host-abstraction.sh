#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_FILE="$ROOT_DIR/Emuera/UI/Framework/Forms/ConfigDialog.cs"

if rg -n --pcre2 --no-heading --color=never \
  "(MainWindow\\s+parent\\b|SetConfig\\(\\s*MainWindow\\s+)" \
  "$TARGET_FILE" >/dev/null; then
  echo "ConfigDialog host abstraction audit failed: ConfigDialog is still bound to MainWindow concrete type." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "IConfigDialogHost parent;" "$TARGET_FILE" >/dev/null; then
  echo "ConfigDialog host abstraction audit failed: IConfigDialogHost field binding is missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "SetConfig(IConfigDialogHost" "$TARGET_FILE" >/dev/null; then
  echo "ConfigDialog host abstraction audit failed: SetConfig(IConfigDialogHost ...) binding is missing." >&2
  exit 1
fi

echo "ConfigDialog host abstraction audit passed: ConfigDialog depends on IConfigDialogHost."
