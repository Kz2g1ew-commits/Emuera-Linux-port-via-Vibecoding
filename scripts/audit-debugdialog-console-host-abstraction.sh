#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEBUG_DIALOG_FILE="$ROOT_DIR/Emuera/UI/Framework/Forms/DebugDialog.cs"
BACKEND_FILE="$ROOT_DIR/Emuera/UI/Game/IUiPlatformBackend.cs"
BRIDGE_FILE="$ROOT_DIR/Emuera/UI/Game/UiPlatformBridge.cs"
WINFORMS_BRIDGE_FILE="$ROOT_DIR/Emuera/UI/Game/WinFormsBridge.cs"
CONSOLE_FILE="$ROOT_DIR/Emuera/UI/Game/EmueraConsole.cs"
HOST_FILE="$ROOT_DIR/Emuera/UI/Game/IDebugDialogConsoleHost.cs"

if rg -n --pcre2 --no-heading --color=never \
  "(\\bEmueraConsole\\b.*mainConsole|SetParent\\(\\s*EmueraConsole\\s+)" \
  "$DEBUG_DIALOG_FILE" >/dev/null; then
  echo "DebugDialog console-host abstraction audit failed: DebugDialog is still bound to EmueraConsole concrete type." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "IDebugDialogConsoleHost mainConsole;" "$DEBUG_DIALOG_FILE" >/dev/null; then
  echo "DebugDialog console-host abstraction audit failed: IDebugDialogConsoleHost field binding is missing in DebugDialog." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "SetParent(IDebugDialogConsoleHost" "$DEBUG_DIALOG_FILE" >/dev/null; then
  echo "DebugDialog console-host abstraction audit failed: SetParent(IDebugDialogConsoleHost, ...) binding is missing." >&2
  exit 1
fi

if rg -n --pcre2 --no-heading --color=never "TryCreateDebugDialog\\(\\s*EmueraConsole\\s+" "$BACKEND_FILE" "$BRIDGE_FILE" "$WINFORMS_BRIDGE_FILE" >/dev/null; then
  echo "DebugDialog console-host abstraction audit failed: debug-dialog backend bridge still exposes EmueraConsole concrete type." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "TryCreateDebugDialog(IDebugDialogConsoleHost" "$BACKEND_FILE" >/dev/null; then
  echo "DebugDialog console-host abstraction audit failed: IUiPlatformBackend signature is missing IDebugDialogConsoleHost." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "TryCreateDebugDialog(IDebugDialogConsoleHost" "$BRIDGE_FILE" >/dev/null; then
  echo "DebugDialog console-host abstraction audit failed: UiPlatformBridge signature is missing IDebugDialogConsoleHost." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "TryCreateDebugDialog(IDebugDialogConsoleHost" "$WINFORMS_BRIDGE_FILE" >/dev/null; then
  echo "DebugDialog console-host abstraction audit failed: WinForms bridge signature is missing IDebugDialogConsoleHost." >&2
  exit 1
fi

if [[ ! -f "$HOST_FILE" ]]; then
  echo "DebugDialog console-host abstraction audit failed: missing host interface file $HOST_FILE" >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "IDebugDialogConsoleHost" "$CONSOLE_FILE" >/dev/null; then
  echo "DebugDialog console-host abstraction audit failed: EmueraConsole is not bound to IDebugDialogConsoleHost." >&2
  exit 1
fi

echo "DebugDialog console-host abstraction audit passed: debug-dialog path depends on IDebugDialogConsoleHost."
