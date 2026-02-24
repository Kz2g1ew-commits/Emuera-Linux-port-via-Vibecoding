#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_FILE="$ROOT_DIR/Emuera/UI/Game/UiPlatformBridge.cs"

if [[ ! -f "$TARGET_FILE" ]]; then
  echo "UI platform linux-backend audit failed: missing file $TARGET_FILE" >&2
  exit 1
fi

required_patterns=(
  "requested.Equals(\"linux-shell\", StringComparison.OrdinalIgnoreCase)"
  "requested.Equals(\"linux\", StringComparison.OrdinalIgnoreCase)"
  "requested.Equals(\"zenity\", StringComparison.OrdinalIgnoreCase)"
  "RuntimeInformation.IsOSPlatform(OSPlatform.Linux)"
)

for pattern in "${required_patterns[@]}"; do
  if ! rg -n --fixed-strings --no-heading --color=never "$pattern" "$TARGET_FILE" >/dev/null; then
    echo "UI platform linux-backend audit failed: missing required linux backend mapping: $pattern" >&2
    exit 1
  fi
done

linux_backend_returns=$(rg -n --fixed-strings --no-heading --color=never "return new LinuxShellUiPlatformBackend();" "$TARGET_FILE" | wc -l)
if [[ "$linux_backend_returns" -lt 2 ]]; then
  echo "UI platform linux-backend audit failed: expected LinuxShell backend return in both override/default paths." >&2
  exit 1
fi

echo "UI platform linux-backend audit passed: UiPlatformBridge defaults and overrides include LinuxShell backend."
