#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROGRAM_FILE="$ROOT_DIR/Emuera.Cli/Program.cs"
CONSOLE_FILE="$ROOT_DIR/Emuera.Cli/CliExecutionConsole.cs"

if [[ ! -f "$PROGRAM_FILE" ]]; then
  echo "CLI primitive-input audit failed: missing file $PROGRAM_FILE" >&2
  exit 1
fi

if [[ ! -f "$CONSOLE_FILE" ]]; then
  echo "CLI primitive-input audit failed: missing file $CONSOLE_FILE" >&2
  exit 1
fi

program_patterns=(
  "InputType.PrimitiveMouseKey => \"INPUT(PRIMITIVE)> \""
  "if (request.InputType != InputType.EnterKey &&"
  "request.InputType != InputType.Void)"
)

for pattern in "${program_patterns[@]}"; do
  if ! rg -n --fixed-strings --no-heading --color=never "$pattern" "$PROGRAM_FILE" >/dev/null; then
    echo "CLI primitive-input audit failed: missing pattern '$pattern' in Program.cs" >&2
    exit 1
  fi
done

if ! rg -n --fixed-strings --no-heading --color=never "TryBuildPrimitiveKeyPacket" "$CONSOLE_FILE" >/dev/null; then
  echo "CLI primitive-input audit failed: key-packet helper missing in CliExecutionConsole.cs" >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "TryParsePrimitiveKeySpec" "$CONSOLE_FILE" >/dev/null; then
  echo "CLI primitive-input audit failed: key-spec parser missing in CliExecutionConsole.cs" >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "TryParsePrimitiveModifier" "$CONSOLE_FILE" >/dev/null; then
  echo "CLI primitive-input audit failed: modifier parser missing in CliExecutionConsole.cs" >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "VirtualKeyMap.ComposeKeyData" "$CONSOLE_FILE" >/dev/null; then
  echo "CLI primitive-input audit failed: key-data composition path missing in CliExecutionConsole.cs" >&2
  exit 1
fi

echo "CLI primitive-input audit passed: primitive parser and line-input path are wired."
