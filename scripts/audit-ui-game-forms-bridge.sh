#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_DIR="$ROOT_DIR/Emuera/UI/Game"
ALLOW_FILE="$TARGET_DIR/WinFormsBridge.cs"

mapfile -t hits < <(
  rg -n --no-heading --color=never \
    "using[[:space:]]+MinorShift\\.Emuera\\.Forms;|MinorShift\\.Emuera\\.Forms\\." \
    "$TARGET_DIR" -g"*.cs" || true
)

if [[ ${#hits[@]} -eq 0 ]]; then
  echo "UI/Game Forms bridge audit passed: no Forms namespace references found."
  exit 0
fi

blocked=()
for hit in "${hits[@]}"; do
  file="${hit%%:*}"
  if [[ "$file" != "$ALLOW_FILE" ]]; then
    blocked+=("$hit")
  fi
done

if [[ ${#blocked[@]} -eq 0 ]]; then
  echo "UI/Game Forms bridge audit passed: Forms namespace references are isolated to WinFormsBridge."
  exit 0
fi

echo "UI/Game Forms bridge audit failed: Forms namespace references found outside WinFormsBridge." >&2
printf '%s\n' "${blocked[@]}" >&2
exit 1
