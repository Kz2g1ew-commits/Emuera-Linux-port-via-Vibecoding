#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_FILE="$ROOT_DIR/Emuera/UI/Game/EmueraConsole.cs"

if [[ ! -f "$TARGET_FILE" ]]; then
  echo "Console window-host abstraction audit failed: missing file $TARGET_FILE" >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "EmueraConsole(MainWindow" "$TARGET_FILE" >/dev/null; then
  echo "Console window-host abstraction audit failed: EmueraConsole constructor is still bound to MainWindow." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "readonly MainWindow window" "$TARGET_FILE" >/dev/null; then
  echo "Console window-host abstraction audit failed: EmueraConsole window field is still MainWindow." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "EmueraConsole(IConsoleWindowHost" "$TARGET_FILE" >/dev/null; then
  echo "Console window-host abstraction audit failed: IConsoleWindowHost constructor binding is missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "readonly IConsoleWindowHost window" "$TARGET_FILE" >/dev/null; then
  echo "Console window-host abstraction audit failed: IConsoleWindowHost field binding is missing." >&2
  exit 1
fi

echo "Console window-host abstraction audit passed: EmueraConsole depends on IConsoleWindowHost."
