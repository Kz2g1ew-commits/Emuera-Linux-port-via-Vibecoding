#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HOST_INTERFACE="$ROOT_DIR/Emuera/UI/Game/IUiAppHost.cs"
UNSUPPORTED_HOST="$ROOT_DIR/Emuera/UI/Game/UnsupportedUiAppHost.cs"
LINUX_HOST="$ROOT_DIR/Emuera/UI/Game/LinuxLauncherUiAppHost.cs"

for file in "$HOST_INTERFACE" "$UNSUPPORTED_HOST" "$LINUX_HOST"; do
  if ! [[ -f "$file" ]]; then
    echo "UI app-host neutrality audit failed: missing file $file" >&2
    exit 1
  fi
done

if rg -n 'System\.Drawing' "$HOST_INTERFACE" >/dev/null 2>&1 || rg -n -w 'Icon' "$HOST_INTERFACE" >/dev/null 2>&1; then
  echo "UI app-host neutrality audit failed: IUiAppHost should not expose System.Drawing/Icon types." >&2
  exit 1
fi

if rg -n 'System\.Drawing' "$UNSUPPORTED_HOST" >/dev/null 2>&1 || rg -n -w 'Icon' "$UNSUPPORTED_HOST" >/dev/null 2>&1; then
  echo "UI app-host neutrality audit failed: UnsupportedUiAppHost should not depend on System.Drawing/Icon." >&2
  exit 1
fi

if rg -n 'System\.Drawing' "$LINUX_HOST" >/dev/null 2>&1 || rg -n -w 'Icon' "$LINUX_HOST" >/dev/null 2>&1; then
  echo "UI app-host neutrality audit failed: LinuxLauncherUiAppHost should not depend on System.Drawing/Icon." >&2
  exit 1
fi

if ! rg -n 'TryRun\(string\[\] args, object appIcon\)' "$HOST_INTERFACE" >/dev/null 2>&1; then
  echo "UI app-host neutrality audit failed: IUiAppHost should expose object-based appIcon signature." >&2
  exit 1
fi

echo "UI app-host neutrality audit passed: shared UI host contract is runtime-neutral."
