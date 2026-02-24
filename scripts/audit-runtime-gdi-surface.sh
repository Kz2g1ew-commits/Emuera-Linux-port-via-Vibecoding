#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_DIR="$ROOT_DIR/Emuera/Runtime"

# Runtime GDI dependencies that are still intentionally kept during staged porting.
# Keep this list short and shrink it over time.
ALLOWLIST=()

mapfile -t hits < <(rg -n "using[[:space:]]+System\\.Drawing|System\\.Drawing\\." "$TARGET_DIR" -g"*.cs" || true)

if (( ${#hits[@]} == 0 )); then
  echo "Runtime GDI surface audit passed: no System.Drawing references found in runtime scope."
  exit 0
fi

violations=()
for hit in "${hits[@]}"; do
  file_with_line="${hit%%:*}"
  rel_file="${file_with_line#$ROOT_DIR/}"

  allowed=false
  for allow in "${ALLOWLIST[@]}"; do
    if [[ "$rel_file" == "$allow" ]]; then
      allowed=true
      break
    fi
  done

  if [[ "$allowed" == false ]]; then
    violations+=("$hit")
  fi
done

if (( ${#violations[@]} > 0 )); then
  echo "Runtime GDI surface audit failed: unexpected System.Drawing usage in runtime scope." >&2
  printf '%s\n' "${violations[@]}" >&2
  exit 1
fi

echo "Runtime GDI surface audit passed: only allowlisted runtime files use System.Drawing."
