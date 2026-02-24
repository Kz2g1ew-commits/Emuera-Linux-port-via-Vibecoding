#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROGRAM="$ROOT_DIR/Emuera/Program.cs"
HOST_FILE="$ROOT_DIR/Emuera/UI/Framework/Forms/WinFormsAppUiHost.cs"

if rg -n '^\s*using\s+System\.Windows\.Forms\s*;' "$PROGRAM" >/dev/null 2>&1; then
  echo "Program WinForms entry audit failed: Program.cs should not import System.Windows.Forms directly." >&2
  exit 1
fi

if ! rg -n 'RunWindowsGuiHost\(\s*args\s*,\s*icon\s*\);' "$PROGRAM" >/dev/null 2>&1; then
  echo "Program WinForms entry audit failed: Main must delegate GUI startup to RunWindowsGuiHost(args, icon)." >&2
  exit 1
fi

if ! rg -n 'private static void RunWindowsGuiHost\(' "$PROGRAM" >/dev/null 2>&1; then
  echo "Program WinForms entry audit failed: RunWindowsGuiHost helper is missing." >&2
  exit 1
fi

if ! rg -n 'private static IUiAppHost CreateAppUiHost\(' "$PROGRAM" >/dev/null 2>&1; then
  echo "Program WinForms entry audit failed: CreateAppUiHost helper is missing." >&2
  exit 1
fi

if ! rg -n 'new Forms\.WinFormsAppUiHost\(\)' "$PROGRAM" >/dev/null 2>&1; then
  echo "Program WinForms entry audit failed: Program.cs must select WinFormsAppUiHost through the host factory." >&2
  exit 1
fi

if rg -n 'Application\.Run\(' "$PROGRAM" >/dev/null 2>&1; then
  echo "Program WinForms entry audit failed: Program.cs must not call Application.Run directly." >&2
  exit 1
fi

if rg -n 'new Forms\.MainWindow\(' "$PROGRAM" >/dev/null 2>&1; then
  echo "Program WinForms entry audit failed: Program.cs must not construct MainWindow directly." >&2
  exit 1
fi

if [[ ! -f "$HOST_FILE" ]]; then
  echo "Program WinForms entry audit failed: missing WinForms host file: $HOST_FILE" >&2
  exit 1
fi

if ! rg -n 'class WinFormsAppUiHost' "$HOST_FILE" >/dev/null 2>&1; then
  echo "Program WinForms entry audit failed: WinFormsAppUiHost class definition is missing." >&2
  exit 1
fi

if ! rg -n 'Application\.Run\(window\);' "$HOST_FILE" >/dev/null 2>&1; then
  echo "Program WinForms entry audit failed: WinForms run loop must be localized in WinFormsAppUiHost." >&2
  exit 1
fi

if ! rg -n 'new MainWindow\(' "$HOST_FILE" >/dev/null 2>&1; then
  echo "Program WinForms entry audit failed: MainWindow construction must be localized in WinFormsAppUiHost." >&2
  exit 1
fi

echo "Program WinForms entry audit passed: WinForms startup is localized in WinFormsAppUiHost."
