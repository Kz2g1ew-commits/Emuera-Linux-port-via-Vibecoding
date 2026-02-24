#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_DIR="$ROOT_DIR/Emuera/UI/Game"

# UI/Game may keep WinForms calls only inside the bridge.
ALLOW_FILES=(
  "$TARGET_DIR/WinFormsBridge.cs"
  "$TARGET_DIR/UiPlatformBridge.cs"
)

# Direct static WinForms calls that should not appear in game logic/rendering files.
PATTERN='(using[[:space:]]+System\.Windows\.Forms;|MessageBox\.Show|TextRenderer\.|Application\.DoEvents|Control\.MousePosition|Cursor\.|Screen\.FromPoint|\bDrawToolTipEventArgs\b|\bPopupEventArgs\b|\bToolTip\b)'

mapfile -t hits < <(rg -n --pcre2 --no-heading --color=never "^(?!\\s*//).*$PATTERN" "$TARGET_DIR" -g"*.cs" || true)

if [[ ${#hits[@]} -eq 0 ]]; then
  echo "UI/Game WinForms bridge audit passed: no direct static WinForms calls found."
  exit 0
fi

blocked=()
for hit in "${hits[@]}"; do
  file="${hit%%:*}"
  allowed=false
  for af in "${ALLOW_FILES[@]}"; do
    if [[ "$file" == "$af" ]]; then
      allowed=true
      break
    fi
  done
  if [[ "$allowed" == false ]]; then
    blocked+=("$hit")
  fi
done

if [[ ${#blocked[@]} -eq 0 ]]; then
  echo "UI/Game WinForms bridge audit passed: all direct calls are in allowlisted files."
  exit 0
fi

echo "UI/Game WinForms bridge audit failed: direct WinForms calls found outside bridge." >&2
printf '%s\n' "${blocked[@]}" >&2
exit 1
