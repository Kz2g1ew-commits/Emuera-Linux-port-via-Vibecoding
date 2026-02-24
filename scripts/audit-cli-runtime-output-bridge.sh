#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROGRAM_FILE="$ROOT_DIR/Emuera.Cli/Program.cs"
BRIDGE_FILE="$ROOT_DIR/Emuera.Cli/CliRuntimeHostBridge.cs"
STATUS=0

if ! rg -q --no-heading --color=never \
  "CliRuntimeHostBridge\\.AttachExecutionConsole\\(executionConsole\\)" \
  "$PROGRAM_FILE"; then
  echo "CLI runtime output bridge audit failed: run-engine path does not attach CliRuntimeHostBridge."
  STATUS=3
else
  echo "CLI runtime output bridge audit passed: run-engine path attaches CliRuntimeHostBridge."
fi

required_bridge_patterns=(
  "RuntimeHost\\.PrintPlainHook ="
  "RuntimeHost\\.PrintHtmlHook ="
  "RuntimeHost\\.PrintHtmlIslandHook ="
  "RuntimeHost\\.PrintImageHook ="
  "RuntimeHost\\.PrintShapeHook ="
  "RuntimeHost\\.PrintButtonStringHook ="
  "RuntimeHost\\.PrintFlushHook ="
  "RuntimeHost\\.ClearDisplayHook ="
  "RuntimeHost\\.ReloadErbFinishedHook ="
  "RuntimeHost\\.IsLastLineTemporaryHook ="
  "RuntimeHost\\.ThrowErrorHook ="
  "RuntimeHost\\.ForceStopTimerHook ="
  "RuntimeHost\\.SetStringColorRgbHook ="
  "RuntimeHost\\.SetBackgroundColorRgbHook ="
  "RuntimeHost\\.SetStringStyleFlagsHook ="
  "RuntimeHost\\.SetAlignmentHook ="
  "RuntimeHost\\.GetConsoleClientWidthHook ="
  "RuntimeHost\\.GetKeyStateHook ="
  "RuntimeHost\\.CountInteractiveButtonsHook ="
  "RuntimeHost\\.MarkUpdatedGenerationHook ="
  "RuntimeHost\\.DisableOutputLogHook ="
  "RuntimeHost\\.OutputLogHook ="
  "RuntimeHost\\.OutputSystemLogHook ="
  "RuntimeHost\\.ThrowTitleErrorHook ="
  "RuntimeHost\\.GetDisplayLineTextHook ="
  "RuntimeHost\\.GetDisplayLineHtmlHook ="
  "RuntimeHost\\.PopDisplayLineHtmlHook ="
  "RuntimeHost\\.GetTextBoxTextHook ="
  "RuntimeHost\\.ChangeTextBoxHook ="
  "RuntimeHost\\.ResetTextBoxPosHook ="
  "RuntimeHost\\.SetTextBoxPosHook ="
  "RuntimeHost\\.ApplyTextBoxChangesHook ="
  "RuntimeHost\\.HotkeyStateSetHook ="
  "RuntimeHost\\.HotkeyStateInitHook ="
  "RuntimeHost\\.GetMousePositionXYHook ="
  "RuntimeHost\\.MoveMouseXYHook ="
  "RuntimeHost\\.SetBitmapCacheEnabledForNextLineHook ="
  "RuntimeHost\\.SetRedrawTimerHook ="
  "RuntimeHost\\.GetConsoleClientHeightHook ="
  "RuntimeHost\\.CbgClearHook ="
  "RuntimeHost\\.CbgClearRangeHook ="
  "RuntimeHost\\.CbgClearButtonHook ="
  "RuntimeHost\\.CbgClearButtonMapHook ="
  "RuntimeHost\\.CbgSetGraphicsHook ="
  "RuntimeHost\\.CbgSetButtonMapHook ="
  "RuntimeHost\\.CbgSetImageHook ="
  "RuntimeHost\\.CbgSetButtonImageHook ="
  "RuntimeHost\\.AddBackgroundImageHook ="
  "RuntimeHost\\.RemoveBackgroundImageHook ="
  "RuntimeHost\\.ClearBackgroundImageHook ="
  "RuntimeHost\\.SetToolTipColorRgbHook ="
  "RuntimeHost\\.SetToolTipDelayHook ="
  "RuntimeHost\\.SetToolTipDurationHook ="
  "RuntimeHost\\.SetToolTipFontNameHook ="
  "RuntimeHost\\.SetToolTipFontSizeHook ="
  "RuntimeHost\\.SetCustomToolTipHook ="
  "RuntimeHost\\.SetToolTipFormatHook ="
  "RuntimeHost\\.SetToolTipImageEnabledHook ="
  "RuntimeHost\\.IsCtrlZEnabledHook ="
  "RuntimeHost\\.CaptureRandomSeedHook ="
  "RuntimeHost\\.CtrlZAddInputHook ="
  "RuntimeHost\\.CtrlZOnSavePrepareHook ="
  "RuntimeHost\\.CtrlZOnSaveHook ="
  "RuntimeHost\\.CtrlZOnLoadHook ="
)

missing_bridge_patterns=()
for pattern in "${required_bridge_patterns[@]}"; do
  if ! rg -q --no-heading --color=never "$pattern" "$BRIDGE_FILE"; then
    missing_bridge_patterns+=("$pattern")
  fi
done

if [[ ${#missing_bridge_patterns[@]} -gt 0 ]]; then
  echo "CLI runtime output bridge audit failed: missing RuntimeHost hook wiring in CliRuntimeHostBridge:"
  printf '  %s\n' "${missing_bridge_patterns[@]}"
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=4
  fi
else
  echo "CLI runtime output bridge audit passed: required RuntimeHost hook wiring exists."
fi

if ! rg -q --no-heading --color=never "\\\\u001b\\[" "$BRIDGE_FILE"; then
  echo "CLI runtime output bridge audit failed: ANSI style/control path not found in CliRuntimeHostBridge."
  if [[ "$STATUS" -eq 0 ]]; then
    STATUS=5
  fi
else
  echo "CLI runtime output bridge audit passed: ANSI style/control path detected."
fi

exit "$STATUS"
