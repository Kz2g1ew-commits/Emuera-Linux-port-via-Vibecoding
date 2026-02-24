#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LINUX_BACKEND="$ROOT_DIR/Emuera/UI/Game/LinuxShellUiPlatformBackend.cs"
HEADLESS_BACKEND="$ROOT_DIR/Emuera/UI/Game/HeadlessUiPlatformBackend.cs"
HANDLE_FILE="$ROOT_DIR/Emuera/UI/Game/TextDebugDialogHandle.cs"

for required in "$LINUX_BACKEND" "$HEADLESS_BACKEND" "$HANDLE_FILE"; do
  if [[ ! -f "$required" ]]; then
    echo "Linux debug-dialog backend audit failed: missing file $required" >&2
    exit 1
  fi
done

if ! rg -n --fixed-strings --no-heading --color=never "new TextDebugDialogHandle(console, \"linux-shell\")" "$LINUX_BACKEND" >/dev/null; then
  echo "Linux debug-dialog backend audit failed: LinuxShell backend is not wired to text debug handle." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "new TextDebugDialogHandle(console, \"headless\")" "$HEADLESS_BACKEND" >/dev/null; then
  echo "Linux debug-dialog backend audit failed: headless backend is not wired to text debug handle." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "debug-dialog.snapshot.txt" "$HANDLE_FILE" >/dev/null; then
  echo "Linux debug-dialog backend audit failed: debug snapshot output path is missing." >&2
  exit 1
fi

echo "Linux debug-dialog backend audit passed: Linux/headless debug dialog fallback is wired."
