#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_DIR="$ROOT_DIR/Emuera"

mapfile -t hits < <(rg -n "GlobalStatic\\." "$TARGET_DIR" -g"*.cs" || true)

if (( ${#hits[@]} > 0 )); then
  echo "Global state audit failed: GlobalStatic references remain." >&2
  printf '%s\n' "${hits[@]}" >&2
  exit 1
fi

echo "Global state audit passed: no GlobalStatic references found."
