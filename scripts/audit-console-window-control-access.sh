#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_DIR="$ROOT_DIR/Emuera/UI"

mapfile -t hits < <(
  rg -n --pcre2 --no-heading --color=never \
    "^(?!\\s*//).*(window\\.(ScrollBar|TextBox|MainPicBox|ToolTip)\\.)" \
    "$TARGET_DIR/Game/EmueraConsole.cs" \
    "$TARGET_DIR/Game/EmueraConsole.Print.cs" \
    "$TARGET_DIR/Framework/Forms/EmueraConsole.WindowAccess.cs" \
    "$TARGET_DIR/Framework/Forms/EmueraConsole.UiLoop.cs" \
    "$TARGET_DIR/Framework/Forms/EmueraConsole.ToolTip.cs" || true
)

if [[ ${#hits[@]} -eq 0 ]]; then
  echo "Console window-control access audit passed: no direct window.ScrollBar/TextBox/MainPicBox/ToolTip usage in EmueraConsole paths."
  exit 0
fi

echo "Console window-control access audit failed: direct window control usage found." >&2
printf '%s\n' "${hits[@]}" >&2
exit 1
