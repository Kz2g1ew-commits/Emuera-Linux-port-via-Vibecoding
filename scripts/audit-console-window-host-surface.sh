#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_FILE="$ROOT_DIR/Emuera/UI/Game/IConsoleWindowHost.cs"

mapfile -t hits < <(
  rg -n --pcre2 --no-heading --color=never \
    "^\s*(PictureBox|VScrollBar|RichTextBox|ToolTip)\s+\w+\s*\{" \
    "$TARGET_FILE" || true
)
mapfile -t api_hits < <(
  rg -n --no-heading --color=never \
    "System\\.Windows\\.Forms|DrawToolTipEventHandler|PopupEventHandler|TextFormatFlags" \
    "$TARGET_FILE" || true
)

if [[ ${#hits[@]} -eq 0 && ${#api_hits[@]} -eq 0 ]]; then
  echo "Console window-host surface audit passed: no direct WinForms control/event types exposed."
  exit 0
fi

echo "Console window-host surface audit failed: WinForms coupling remains in host surface." >&2
printf '%s\n' "${hits[@]}" >&2
printf '%s\n' "${api_hits[@]}" >&2
exit 1
