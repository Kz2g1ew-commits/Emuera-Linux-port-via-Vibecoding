#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROGRAM_FILE="$ROOT_DIR/Emuera/Program.cs"

if [[ ! -f "$PROGRAM_FILE" ]]; then
  echo "Program GDI neutrality audit failed: missing file $PROGRAM_FILE" >&2
  exit 1
fi

mapfile -t direct_gdi_import < <(rg -n --fixed-strings --no-heading --color=never "using System.Drawing;" "$PROGRAM_FILE" || true)
mapfile -t direct_color_parse < <(rg -n --fixed-strings --no-heading --color=never "Color.FromName" "$PROGRAM_FILE" || true)
mapfile -t direct_rectangle < <(rg -n --fixed-strings --no-heading --color=never "new Rectangle(" "$PROGRAM_FILE" || true)

hits=("${direct_gdi_import[@]}" "${direct_color_parse[@]}" "${direct_rectangle[@]}")
if [[ ${#hits[@]} -gt 0 ]]; then
  echo "Program GDI neutrality audit failed: Program.cs still contains direct GDI usage." >&2
  printf '%s\n' "${hits[@]}" >&2
  exit 1
fi

required_patterns=(
  "RuntimeHost.ResolveNamedColorRgbHook = UiPlatformBridge.ResolveNamedColorRgb;"
  "AppContents.CreateSpriteG(name, g, rect);"
  "object icon = UiPlatformBridge.LoadConfiguredIcon(Config.EmueraIcon);"
)

for pattern in "${required_patterns[@]}"; do
  if ! rg -n --fixed-strings --no-heading --color=never "$pattern" "$PROGRAM_FILE" >/dev/null; then
    echo "Program GDI neutrality audit failed: missing bridge mapping: $pattern" >&2
    exit 1
  fi
done

echo "Program GDI neutrality audit passed: Program.cs uses UI bridge for GDI-sensitive paths."
