#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_FILE="$ROOT_DIR/Emuera/UI/Framework/Forms/MainWindow.cs"

mapfile -t hits < <(
  rg -n --pcre2 --no-heading --color=never \
    "^\s*public\s+(PictureBox|VScrollBar|RichTextBox|ToolTip)\s+\w+\s*\{" \
    "$TARGET_FILE" || true
)

if [[ ${#hits[@]} -eq 0 ]]; then
  echo "MainWindow surface audit passed: no direct WinForms control properties exposed."
  exit 0
fi

echo "MainWindow surface audit failed: direct WinForms control properties are still exposed." >&2
printf '%s\n' "${hits[@]}" >&2
exit 1
