#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CREATOR_FILE="$ROOT_DIR/Emuera/UI/Game/RuntimeBridge/Creator.Method.cs"

if [[ ! -f "$CREATOR_FILE" ]]; then
  echo "Runtime-bridge color/font hook audit failed: missing file $CREATOR_FILE" >&2
  exit 1
fi

mapfile -t direct_color_name < <(rg -n --fixed-strings --no-heading --color=never "Color.FromName(" "$CREATOR_FILE" || true)
mapfile -t direct_font_probe < <(rg -n --fixed-strings --no-heading --color=never "InstalledFontCollection" "$CREATOR_FILE" || true)
mapfile -t direct_font_registry < <(rg -n --fixed-strings --no-heading --color=never "FontRegistry" "$CREATOR_FILE" || true)
mapfile -t legacy_style_access < <(rg -n --no-heading --color=never "AsRuntimeConsole\(exm\)\?\.(GetStringColorRgb|GetBackgroundColorRgb|GetStringStyleFlags|GetFontName)|processConsole\?\.Alignment" "$CREATOR_FILE" || true)
hits=("${direct_color_name[@]}" "${direct_font_probe[@]}" "${direct_font_registry[@]}" "${legacy_style_access[@]}")

if [[ ${#hits[@]} -gt 0 ]]; then
  echo "Runtime-bridge color/font hook audit failed: direct platform probes remain in Creator.Method.cs." >&2
  printf '%s\n' "${hits[@]}" >&2
  exit 1
fi

required_patterns=(
  "RuntimeHost.TryResolveNamedColorRgb(colorName, out int rgb)"
  "RuntimeHost.IsFontInstalled(str)"
  "UiPlatformBridge.TryCreateFont(fontname, fontsize, fs, out styledFont)"
  "RuntimeHost.GetStringColorRgb()"
  "RuntimeHost.GetBackgroundColorRgb()"
  "RuntimeHost.GetStringStyleFlags()"
  "RuntimeHost.GetFontName()"
  "RuntimeHost.GetAlignment()"
  "RuntimeHost.GetRedrawMode()"
)

for pattern in "${required_patterns[@]}"; do
  if ! rg -n --fixed-strings --no-heading --color=never "$pattern" "$CREATOR_FILE" >/dev/null; then
    echo "Runtime-bridge color/font hook audit failed: missing required hook usage: $pattern" >&2
    exit 1
  fi
done

echo "Runtime-bridge color/font hook audit passed: Creator methods use RuntimeHost hooks."
