#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="$ROOT_DIR/Emuera/UI/Game/RuntimeBridge/Creator.Method.cs"

if rg -n 'HtmlManager\.' "$TARGET" >/dev/null 2>&1; then
  echo "Runtime-bridge HTML hook audit failed: direct HtmlManager usage found in Creator.Method." >&2
  exit 1
fi

if ! rg -n 'RuntimeHost\.HtmlLength\(' "$TARGET" >/dev/null 2>&1; then
  echo "Runtime-bridge HTML hook audit failed: RuntimeHost.HtmlLength hook usage missing." >&2
  exit 1
fi

if ! rg -n 'RuntimeHost\.HtmlSubString\(' "$TARGET" >/dev/null 2>&1; then
  echo "Runtime-bridge HTML hook audit failed: RuntimeHost.HtmlSubString hook usage missing." >&2
  exit 1
fi

if ! rg -n 'RuntimeHost\.HtmlToPlainText\(' "$TARGET" >/dev/null 2>&1; then
  echo "Runtime-bridge HTML hook audit failed: RuntimeHost.HtmlToPlainText hook usage missing." >&2
  exit 1
fi

if ! rg -n 'RuntimeHost\.HtmlEscape\(' "$TARGET" >/dev/null 2>&1; then
  echo "Runtime-bridge HTML hook audit failed: RuntimeHost.HtmlEscape hook usage missing." >&2
  exit 1
fi

echo "Runtime-bridge HTML hook audit passed: Creator methods use RuntimeHost HTML hooks."
