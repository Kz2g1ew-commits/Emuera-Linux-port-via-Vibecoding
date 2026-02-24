#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROGRAM="$ROOT_DIR/Emuera/Program.cs"
CLI_PROGRAM="$ROOT_DIR/Emuera.Cli/Program.cs"

for pattern in \
  'RuntimeHost\.HtmlLengthHook' \
  'RuntimeHost\.HtmlSubStringHook' \
  'RuntimeHost\.HtmlToPlainTextHook' \
  'RuntimeHost\.HtmlEscapeHook'
do
  if ! rg -n "$pattern" "$PROGRAM" >/dev/null 2>&1; then
    echo "Runtime-host HTML wiring audit failed: Program.cs missing $pattern assignment." >&2
    exit 1
  fi
done

for pattern in \
  'RuntimeHost\.HtmlLengthHook' \
  'RuntimeHost\.HtmlSubStringHook' \
  'RuntimeHost\.HtmlToPlainTextHook' \
  'RuntimeHost\.HtmlEscapeHook'
do
  if ! rg -n "$pattern" "$CLI_PROGRAM" >/dev/null 2>&1; then
    echo "Runtime-host HTML wiring audit failed: Emuera.Cli/Program.cs missing $pattern assignment." >&2
    exit 1
  fi
done

echo "Runtime-host HTML wiring audit passed: Program and CLI wire RuntimeHost HTML hooks."
