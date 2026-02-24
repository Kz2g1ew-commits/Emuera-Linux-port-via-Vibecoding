#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME_DIR="${1:-$ROOT_DIR}"
EMUERA_PROJECT="$ROOT_DIR/Emuera/Emuera.csproj"
CLI_PROJECT="$ROOT_DIR/Emuera.Cli/Emuera.Cli.csproj"
STRICT_MODE="${2:-}"

run_build_retry() {
  local attempt=1
  local max_attempts=3
  while (( attempt <= max_attempts )); do
    if "$@"; then
      return 0
    fi
    if (( attempt == max_attempts )); then
      return 1
    fi
    echo "build attempt $attempt failed, retrying..." >&2
    sleep 1
    attempt=$((attempt + 1))
  done
}
run_build_retry env \
  DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp}" \
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}" \
  NUGET_PACKAGES="${NUGET_PACKAGES:-/tmp/.nuget/packages}" \
  dotnet restore "$EMUERA_PROJECT"

run_build_retry env \
  DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp}" \
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}" \
  NUGET_PACKAGES="${NUGET_PACKAGES:-/tmp/.nuget/packages}" \
  dotnet restore "$CLI_PROJECT"

run_build_retry env \
  DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp}" \
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}" \
  NUGET_PACKAGES="${NUGET_PACKAGES:-/tmp/.nuget/packages}" \
  dotnet clean "$EMUERA_PROJECT" -c Debug-NAudio

run_build_retry env \
  DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp}" \
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}" \
  NUGET_PACKAGES="${NUGET_PACKAGES:-/tmp/.nuget/packages}" \
  dotnet build "$EMUERA_PROJECT" -c Debug-NAudio --no-restore /p:UseSharedCompilation=false /nodeReuse:false

run_build_retry env \
  DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp}" \
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}" \
  NUGET_PACKAGES="${NUGET_PACKAGES:-/tmp/.nuget/packages}" \
  dotnet clean "$CLI_PROJECT" -c Debug

run_build_retry env \
  DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp}" \
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}" \
  NUGET_PACKAGES="${NUGET_PACKAGES:-/tmp/.nuget/packages}" \
  dotnet build "$CLI_PROJECT" -c Debug --no-restore /p:UseSharedCompilation=false /nodeReuse:false

bash "$ROOT_DIR/scripts/audit-linux-porting.sh"
bash "$ROOT_DIR/scripts/audit-runtime-ui-coupling.sh"
bash "$ROOT_DIR/scripts/audit-runtimebridge-legacy-process-console.sh"
bash "$ROOT_DIR/scripts/audit-runtime-gdi-surface.sh"
bash "$ROOT_DIR/scripts/audit-ui-game-winforms-bridge.sh"
bash "$ROOT_DIR/scripts/audit-ui-game-forms-bridge.sh"
bash "$ROOT_DIR/scripts/audit-ui-platform-facade.sh"
bash "$ROOT_DIR/scripts/audit-ui-platform-linux-backend.sh"
bash "$ROOT_DIR/scripts/audit-rikai-index-backend.sh"
bash "$ROOT_DIR/scripts/audit-linux-debugdialog-backend.sh"
bash "$ROOT_DIR/scripts/audit-launcher-default-playlike.sh"
bash "$ROOT_DIR/scripts/audit-cli-redirect-input-retry.sh"
bash "$ROOT_DIR/scripts/audit-cli-primitive-input-parser.sh"
bash "$ROOT_DIR/scripts/audit-cli-runtime-output-bridge.sh"
bash "$ROOT_DIR/scripts/audit-runtime-bridge-color-font-hooks.sh"
bash "$ROOT_DIR/scripts/audit-runtime-bridge-html-hooks.sh"
bash "$ROOT_DIR/scripts/audit-runtime-host-html-wiring.sh"
bash "$ROOT_DIR/scripts/audit-runtimecore-ctrlz-extraction.sh"
bash "$ROOT_DIR/scripts/audit-runtimecore-process-contract-extraction.sh"
bash "$ROOT_DIR/scripts/audit-runtimecore-parser-primitives-extraction.sh"
bash "$ROOT_DIR/scripts/audit-runtimecore-script-enums-extraction.sh"
bash "$ROOT_DIR/scripts/audit-runtimecore-circular-buffer-extraction.sh"
bash "$ROOT_DIR/scripts/audit-runtimecore-process-state-enums-extraction.sh"
bash "$ROOT_DIR/scripts/audit-runtimecore-statement-local-enums-extraction.sh"
bash "$ROOT_DIR/scripts/audit-runtime-host-log-hooks.sh"
bash "$ROOT_DIR/scripts/audit-runtime-host-console-output-hooks.sh"
bash "$ROOT_DIR/scripts/audit-runtime-host-refresh-hooks.sh"
bash "$ROOT_DIR/scripts/audit-runtime-host-error-style-hooks.sh"
bash "$ROOT_DIR/scripts/audit-headless-backend-safety.sh"
bash "$ROOT_DIR/scripts/audit-headless-backend-drawing-fallback.sh"
bash "$ROOT_DIR/scripts/audit-sound-linux-fallback.sh"
bash "$ROOT_DIR/scripts/audit-console-window-host-abstraction.sh"
bash "$ROOT_DIR/scripts/audit-console-window-host-surface.sh"
bash "$ROOT_DIR/scripts/audit-mainwindow-control-surface.sh"
bash "$ROOT_DIR/scripts/audit-configdialog-host-abstraction.sh"
bash "$ROOT_DIR/scripts/audit-configdialog-font-bridge.sh"
bash "$ROOT_DIR/scripts/audit-debugconfig-host-abstraction.sh"
bash "$ROOT_DIR/scripts/audit-console-debugdialog-bridge.sh"
bash "$ROOT_DIR/scripts/audit-debugdialog-console-host-abstraction.sh"
bash "$ROOT_DIR/scripts/audit-mainwindow-runtime-process-abstraction.sh"
bash "$ROOT_DIR/scripts/audit-console-window-control-access.sh"
bash "$ROOT_DIR/scripts/audit-program-ui-hooks.sh"
bash "$ROOT_DIR/scripts/audit-program-input-hooks.sh"
bash "$ROOT_DIR/scripts/audit-program-gdi-neutrality.sh"
bash "$ROOT_DIR/scripts/audit-program-winforms-entry.sh"
bash "$ROOT_DIR/scripts/audit-ui-app-host-neutrality.sh"
bash "$ROOT_DIR/scripts/audit-global-state.sh"
bash "$ROOT_DIR/scripts/publish-linux-cli.sh"
bash "$ROOT_DIR/scripts/run-linux-static-gate.sh" "$GAME_DIR"
bash "$ROOT_DIR/scripts/deploy-linux-standalone.sh" "$GAME_DIR"
bash "$ROOT_DIR/scripts/verify-linux-offline-standalone.sh" "$GAME_DIR"

if [[ "$STRICT_MODE" == "--strict" ]]; then
  bash "$ROOT_DIR/scripts/run-linux-strict-smoke.sh" "$GAME_DIR"
fi
