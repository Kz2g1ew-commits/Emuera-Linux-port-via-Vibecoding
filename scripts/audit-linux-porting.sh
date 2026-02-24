#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Runtime-side audit scope only (exclude WinForms UI tree by design).
SCOPE=(
  "$ROOT_DIR/Emuera/Runtime"
  "$ROOT_DIR/Emuera.RuntimeCore"
  "$ROOT_DIR/Emuera.Cli"
)

# Allowed platform-specific touchpoints currently retained by design.
ALLOW_FILES=(
)

PATTERN='DllImport|LibraryImport|System\.Windows\.Forms|Microsoft\.Win32|user32|kernel32|winmm'

mapfile -t hits < <(rg -n --no-heading --color=never "$PATTERN" "${SCOPE[@]}" -g"*.cs" || true)

if [[ ${#hits[@]} -eq 0 ]]; then
  echo "Linux porting audit passed: no platform-specific runtime hits found."
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

echo "Linux porting audit summary:"
echo "- Total hits   : ${#hits[@]}"
echo "- Allowed hits : $((${#hits[@]} - ${#blocked[@]}))"
echo "- Blocked hits : ${#blocked[@]}"

if [[ ${#blocked[@]} -gt 0 ]]; then
  echo
  echo "Blocked platform-specific runtime references:"
  printf '%s\n' "${blocked[@]}"
  exit 3
fi

echo "Linux porting audit passed (all hits are allowlisted)."
