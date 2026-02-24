#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_DIR="$ROOT_DIR/Emuera/UI"

ALLOW_FILES=(
  "$ROOT_DIR/Emuera/UI/Game/WinFormsBridge.cs"
  "$ROOT_DIR/Emuera/UI/Game/UiPlatformBridge.cs"
)

mapfile -t hits_bridge < <(rg -n --fixed-strings --no-heading --color=never "WinFormsBridge." "$TARGET_DIR" -g"*.cs" || true)
mapfile -t hits_backend < <(rg -n --fixed-strings --no-heading --color=never "WinFormsUiPlatformBackend" "$TARGET_DIR" -g"*.cs" || true)
hits=("${hits_bridge[@]}" "${hits_backend[@]}")

if [[ ${#hits[@]} -eq 0 ]]; then
  echo "UI platform facade audit passed: no WinForms backend calls/references found."
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
  echo "UI platform facade audit passed: all WinForms backend calls/references are in allowlisted files."
  exit 0
fi

echo "UI platform facade audit failed: WinForms backend calls/references found outside facade files." >&2
printf '%s\n' "${blocked[@]}" >&2
exit 1
