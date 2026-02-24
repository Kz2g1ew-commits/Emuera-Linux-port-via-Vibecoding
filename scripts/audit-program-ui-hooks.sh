#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROGRAM_FILE="$ROOT_DIR/Emuera/Program.cs"

if [[ ! -f "$PROGRAM_FILE" ]]; then
  echo "Program UI hook audit failed: missing file $PROGRAM_FILE" >&2
  exit 1
fi

mapfile -t direct_hits < <(rg -n --fixed-strings --no-heading --color=never "MessageBox.Show" "$PROGRAM_FILE" || true)
mapfile -t direct_do_events < <(rg -n --fixed-strings --no-heading --color=never "Application.DoEvents" "$PROGRAM_FILE" || true)
mapfile -t direct_clipboard < <(rg -n --fixed-strings --no-heading --color=never "Clipboard.SetDataObject" "$PROGRAM_FILE" || true)
hits=("${direct_hits[@]}" "${direct_do_events[@]}" "${direct_clipboard[@]}")

if [[ ${#hits[@]} -gt 0 ]]; then
  echo "Program UI hook audit failed: direct UI/clipboard calls remain in Program.cs." >&2
  printf '%s\n' "${hits[@]}" >&2
  exit 1
fi

required_patterns=(
  "RuntimeHost.DoEventsHook = UiPlatformBridge.DoEvents;"
  "RuntimeHost.ShowInfoHook = UiPlatformBridge.ShowInfo;"
  "RuntimeHost.ShowInfoWithCaptionHook = UiPlatformBridge.ShowInfo;"
  "RuntimeHost.ConfirmYesNoHook = UiPlatformBridge.ConfirmYesNo;"
  "RuntimeHost.SetClipboardTextHook = UiPlatformBridge.SetClipboardText;"
)

for pattern in "${required_patterns[@]}"; do
  if ! rg -n --fixed-strings --no-heading --color=never "$pattern" "$PROGRAM_FILE" >/dev/null; then
    echo "Program UI hook audit failed: missing required hook mapping: $pattern" >&2
    exit 1
  fi
done

echo "Program UI hook audit passed: Program.cs UI hooks use UiPlatformBridge."
