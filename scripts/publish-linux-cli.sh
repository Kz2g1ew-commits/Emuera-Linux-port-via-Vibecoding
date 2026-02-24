#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/Emuera.Cli/Emuera.Cli.csproj"
OUT_DIR="$ROOT_DIR/artifacts/linux-x64-cli"
RID="linux-x64"
LOCK_FILE="${TMPDIR:-/tmp}/emuera-linux-publish.lock"

DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/tmp}"
DOTNET_SKIP_FIRST_TIME_EXPERIENCE="${DOTNET_SKIP_FIRST_TIME_EXPERIENCE:-1}"
NUGET_PACKAGES="${NUGET_PACKAGES:-/tmp/.nuget/packages}"

DOTNET_CLI_HOME="$DOTNET_CLI_HOME" \
DOTNET_SKIP_FIRST_TIME_EXPERIENCE="$DOTNET_SKIP_FIRST_TIME_EXPERIENCE" \
NUGET_PACKAGES="$NUGET_PACKAGES" \
dotnet restore "$PROJECT" -r "$RID"

mkdir -p "$(dirname "$LOCK_FILE")"
exec 9>"$LOCK_FILE"
if ! flock -w 120 9; then
  echo "failed to acquire publish lock: $LOCK_FILE" >&2
  exit 1
fi

attempt=1
max_attempts=3
while (( attempt <= max_attempts )); do
  rm -rf "$OUT_DIR"
  if DOTNET_CLI_HOME="$DOTNET_CLI_HOME" \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE="$DOTNET_SKIP_FIRST_TIME_EXPERIENCE" \
    NUGET_PACKAGES="$NUGET_PACKAGES" \
    dotnet publish "$PROJECT" \
      -c Release \
      -r "$RID" \
      --self-contained true \
      /nodeReuse:false \
      /p:UseSharedCompilation=false \
      /p:PublishSingleFile=true \
      /p:DebugType=None \
      /p:DebugSymbols=false \
      -o "$OUT_DIR"; then
    break
  fi

  if (( attempt == max_attempts )); then
    echo "publish failed after $max_attempts attempts" >&2
    exit 1
  fi

  echo "publish attempt $attempt failed, retrying..." >&2
  sleep 1
  attempt=$((attempt + 1))
done

echo "Published Linux CLI executable to: $OUT_DIR"
echo "Run with: $OUT_DIR/Emuera.Cli --game-dir /path/to/game --run-smoke-only"
