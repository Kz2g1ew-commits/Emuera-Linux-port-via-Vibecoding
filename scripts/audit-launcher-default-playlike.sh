#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLI_FILE="$ROOT_DIR/Emuera.Cli/Program.cs"
HOST_FILE="$ROOT_DIR/Emuera/UI/Game/LinuxLauncherUiAppHost.cs"
DEPLOY_FILE="$ROOT_DIR/scripts/deploy-linux-standalone.sh"
VERIFY_FILE="$ROOT_DIR/scripts/verify-linux-offline-standalone.sh"

for file in "$CLI_FILE" "$HOST_FILE" "$DEPLOY_FILE" "$VERIFY_FILE"; do
  if [[ ! -f "$file" ]]; then
    echo "Launcher default play-like audit failed: missing file $file" >&2
    exit 1
  fi
done

required_cli_patterns=(
  'var defaultMode = Environment.GetEnvironmentVariable("EMUERA_DEFAULT_MODE");'
  'Console.WriteLine("No mode specified. Starting play-like launcher flow...");'
  'appExitCode = await LauncherUi.RunAsync(validation, strictRetries, autoPlay: true, allowPeLaunch: false);'
)

for pattern in "${required_cli_patterns[@]}"; do
  if ! rg -n --fixed-strings --no-heading --color=never "$pattern" "$CLI_FILE" >/dev/null; then
    echo "Launcher default play-like audit failed: CLI default-playlike pattern missing: $pattern" >&2
    exit 1
  fi
done

if ! rg -n --fixed-strings --no-heading --color=never 'argumentList.Add("--play-like");' "$HOST_FILE" >/dev/null; then
  echo "Launcher default play-like audit failed: Linux launcher host does not forward --play-like by default." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never 'Exec=./Run-Emuera-Linux.sh' "$DEPLOY_FILE" >/dev/null; then
  echo "Launcher default play-like audit failed: deploy script desktop launcher Exec is not default no-arg mode." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "'^Exec=\\./Run-Emuera-Linux\\.sh( --interactive-gui)?$'" "$VERIFY_FILE" >/dev/null; then
  echo "Launcher default play-like audit failed: verify script desktop Exec pattern guard is missing." >&2
  exit 1
fi

echo "Launcher default play-like audit passed: CLI/app-host/deploy/verify launcher defaults are aligned."
