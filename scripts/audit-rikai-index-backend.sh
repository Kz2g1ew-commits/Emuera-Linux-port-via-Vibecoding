#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GENERATOR_FILE="$ROOT_DIR/Emuera/UI/Game/RikaiIndexGenerator.cs"
LINUX_BACKEND_FILE="$ROOT_DIR/Emuera/UI/Game/LinuxShellUiPlatformBackend.cs"
HEADLESS_BACKEND_FILE="$ROOT_DIR/Emuera/UI/Game/HeadlessUiPlatformBackend.cs"
WINFORMS_DIALOG_FILE="$ROOT_DIR/Emuera/UI/Framework/Forms/RikaiDialog.cs"

for required in "$GENERATOR_FILE" "$LINUX_BACKEND_FILE" "$HEADLESS_BACKEND_FILE" "$WINFORMS_DIALOG_FILE"; do
  if [[ ! -f "$required" ]]; then
    echo "Rikai index backend audit failed: missing file $required" >&2
    exit 1
  fi
done

if ! rg -n --fixed-strings --no-heading --color=never "TryGenerateAndSave(" "$GENERATOR_FILE" >/dev/null; then
  echo "Rikai index backend audit failed: generator helper method is missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "RikaiIndexGenerator.TryGenerateAndSave(" "$LINUX_BACKEND_FILE" >/dev/null; then
  echo "Rikai index backend audit failed: Linux backend is not wired to generator." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "RikaiIndexGenerator.TryGenerateAndSave(" "$HEADLESS_BACKEND_FILE" >/dev/null; then
  echo "Rikai index backend audit failed: headless backend is not wired to generator." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "RikaiIndexGenerator.TryGenerateAndSave(" "$WINFORMS_DIALOG_FILE" >/dev/null; then
  echo "Rikai index backend audit failed: WinForms dialog is not wired to generator." >&2
  exit 1
fi

echo "Rikai index backend audit passed: WinForms/Linux/headless paths share generator helper."
