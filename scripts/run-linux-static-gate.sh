#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BIN="$ROOT_DIR/artifacts/linux-x64-cli/Emuera.Cli"
GAME_DIR="${1:-$(pwd)}"

if [[ ! -x "$BIN" ]]; then
  echo "missing executable: $BIN" >&2
  echo "run scripts/publish-linux-cli.sh first" >&2
  exit 2
fi

"$BIN" --game-dir "$GAME_DIR" --gate-only
