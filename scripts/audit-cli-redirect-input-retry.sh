#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="$ROOT_DIR/Emuera.Cli/Program.cs"

if [[ ! -f "$TARGET" ]]; then
  echo "CLI redirected-input retry audit failed: missing file $TARGET" >&2
  exit 1
fi

required_patterns=(
  "ResolveRedirectInvalidLimit()"
  "Invalid runtime input in redirected mode (attempt"
  "Redirected input retry limit reached"
  "continue;"
)

for pattern in "${required_patterns[@]}"; do
  if ! rg -n --fixed-strings --no-heading --color=never "$pattern" "$TARGET" >/dev/null; then
    echo "CLI redirected-input retry audit failed: missing pattern '$pattern' in Program.cs" >&2
    exit 1
  fi
done

echo "CLI redirected-input retry audit passed: redirected stdin now retries with bounded limit."
