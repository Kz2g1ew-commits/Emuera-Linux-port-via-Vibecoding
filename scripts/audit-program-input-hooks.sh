#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROGRAM_FILE="$ROOT_DIR/Emuera/Program.cs"

if [[ ! -f "$PROGRAM_FILE" ]]; then
  echo "Program input hook audit failed: missing file $PROGRAM_FILE" >&2
  exit 1
fi

mapfile -t direct_hits < <(rg -n --fixed-strings --no-heading --color=never "WinInput.GetKeyState" "$PROGRAM_FILE" || true)
if [[ ${#direct_hits[@]} -gt 0 ]]; then
  echo "Program input hook audit failed: direct WinInput usage remains in Program.cs." >&2
  printf "%s\n" "${direct_hits[@]}" >&2
  exit 1
fi

required_patterns=(
  "RuntimeHost.GetKeyStateHook = UiPlatformBridge.GetKeyState;"
  "RuntimeHost.PrintButtonStringHook = static (text, input) => CurrentConsoleHost?.PrintButton(text, input);"
  "RuntimeHost.PrintButtonLongHook = static (text, input) => CurrentConsoleHost?.PrintButton(text, input);"
  "RuntimeHost.PrintButtonCStringHook = static (text, input, isRight) => CurrentConsoleHost?.PrintButtonC(text, input, isRight);"
  "RuntimeHost.PrintButtonCLongHook = static (text, input, isRight) => CurrentConsoleHost?.PrintButtonC(text, input, isRight);"
  "RuntimeHost.PrintPlainSingleLineHook = static (text) => CurrentConsoleHost?.PrintPlainSingleLine(text);"
  "RuntimeHost.PrintErrorButtonHook = static (text, position, level) => CurrentConsoleHost?.PrintErrorButton(text, position, level);"
  "RuntimeHost.PrintPlainHook = static (text) => CurrentConsoleHost?.PrintPlain(text);"
  "RuntimeHost.ClearTextHook = static () => CurrentConsoleHost?.ClearText();"
  "RuntimeHost.ReloadErbFinishedHook = static () => CurrentConsoleHost?.ReloadErbFinished();"
  "RuntimeHost.IsLastLineEmptyHook = static () => CurrentConsoleHost?.LastLineIsEmpty ?? false;"
  "RuntimeHost.IsLastLineTemporaryHook = static () => CurrentConsoleHost?.LastLineIsTemporary ?? false;"
  "RuntimeHost.IsPrintBufferEmptyHook = static () => CurrentConsoleHost?.IsPrintBufferEmpty ?? true;"
  "RuntimeHost.CountInteractiveButtonsHook = static (integerOnly) => CurrentConsoleHost?.CountInteractiveButtons(integerOnly) ?? 0;"
  "RuntimeHost.GetConsoleClientWidthHook = static () => CurrentConsoleHost?.ClientWidth ?? 0;"
  "RuntimeHost.IsConsoleActiveHook = static () => CurrentConsoleHost?.IsActive ?? false;"
  "RuntimeHost.GetMousePositionXYHook = static () => CurrentConsoleHost?.GetMousePositionXY() ?? new RuntimePoint(0, 0);"
  "RuntimeHost.MoveMouseXYHook = static (x, y) => CurrentConsoleHost?.MoveMouseXY(x, y) ?? false;"
  "RuntimeHost.SetBitmapCacheEnabledForNextLineHook = static (enabled) =>"
  "RuntimeHost.SetRedrawTimerHook = static (tickCount) => CurrentConsoleHost?.setRedrawTimer(tickCount);"
  "RuntimeHost.GetTextBoxTextHook = static () => CurrentConsoleHost?.GetTextBoxText() ?? string.Empty;"
  "RuntimeHost.ChangeTextBoxHook = static (text) => CurrentConsoleHost?.ChangeTextBox(text);"
  "RuntimeHost.ResetTextBoxPosHook = static () => CurrentConsoleHost?.ResetTextBoxPos();"
  "RuntimeHost.SetTextBoxPosHook = static (xOffset, yOffset, width) => CurrentConsoleHost?.SetTextBoxPos(xOffset, yOffset, width);"
  "RuntimeHost.HotkeyStateSetHook = static (key, value) => CurrentConsoleHost?.HotkeyStateSet(key, value);"
  "RuntimeHost.HotkeyStateInitHook = static (key) => CurrentConsoleHost?.HotkeyStateInit(key);"
)

for pattern in "${required_patterns[@]}"; do
  if ! rg -n --fixed-strings --no-heading --color=never "$pattern" "$PROGRAM_FILE" >/dev/null; then
    echo "Program input hook audit failed: missing required hook mapping: $pattern" >&2
    exit 1
  fi
done

echo "Program input hook audit passed: Program.cs input hooks use UiPlatformBridge/RuntimeHost mappings."
