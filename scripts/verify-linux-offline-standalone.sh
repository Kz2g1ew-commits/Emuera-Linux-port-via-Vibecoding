#!/usr/bin/env bash
set -euo pipefail

GAME_DIR="${1:?usage: verify-linux-offline-standalone.sh <game-dir> [binary-name] [launcher-name]}"
BINARY_NAME="${2:-}"
LAUNCHER_NAME="${3:-Run-Emuera-Linux.sh}"
DESKTOP_NAME="${4:-Run-Emuera-Linux.desktop}"

LAUNCHER="$GAME_DIR/$LAUNCHER_NAME"
DESKTOP_FILE="$GAME_DIR/$DESKTOP_NAME"

if [[ ! -f "$LAUNCHER" ]]; then
  echo "launcher not found: $LAUNCHER" >&2
  exit 1
fi
if [[ ! -x "$LAUNCHER" ]]; then
  echo "launcher is not executable: $LAUNCHER" >&2
  exit 1
fi

if [[ ! -f "$DESKTOP_FILE" ]]; then
  echo "desktop entry not found: $DESKTOP_FILE" >&2
  exit 1
fi

resolve_binary_from_launcher() {
  local launcher_path="$1"
  if [[ ! -f "$launcher_path" ]]; then
    return 1
  fi

  local candidate
  candidate="$(rg -n --no-heading --color=never -o 'exec "\$DIR/[^"]+"' "$launcher_path" | head -n 1 | sed -E 's|.*exec "\$DIR/([^"]+)".*|\1|')"
  if [[ -z "$candidate" ]]; then
    return 1
  fi

  printf '%s\n' "$candidate"
  return 0
}

resolve_binary_fallback() {
  local game_dir="$1"

  local candidate
  candidate="$(find "$game_dir" -maxdepth 1 -type f -name 'Emuera*.exe' -printf '%f\n' | head -n 1 || true)"
  if [[ -n "$candidate" ]]; then
    printf '%s\n' "$candidate"
    return 0
  fi

  candidate="$(find "$game_dir" -maxdepth 1 -type f -name 'Emuera*' ! -name '*.sh' ! -name '*.desktop' -perm -u+x -printf '%f\n' | head -n 1 || true)"
  if [[ -n "$candidate" ]]; then
    printf '%s\n' "$candidate"
    return 0
  fi

  return 1
}

if [[ -z "$BINARY_NAME" ]]; then
  BINARY_NAME="$(resolve_binary_from_launcher "$LAUNCHER" || true)"
fi

if [[ -z "$BINARY_NAME" ]]; then
  BINARY_NAME="$(resolve_binary_fallback "$GAME_DIR" || true)"
fi

if [[ -z "$BINARY_NAME" ]]; then
  echo "failed to resolve standalone binary name from launcher or game directory: $GAME_DIR" >&2
  exit 1
fi

TARGET="$GAME_DIR/$BINARY_NAME"

if [[ ! -f "$TARGET" ]]; then
  echo "standalone binary not found: $TARGET" >&2
  exit 1
fi
if [[ ! -x "$TARGET" ]]; then
  echo "standalone binary is not executable: $TARGET" >&2
  exit 1
fi

if ! rg -q '^Exec=\./Run-Emuera-Linux\.sh( --interactive-gui)?$' "$DESKTOP_FILE"; then
  echo "desktop entry Exec is unexpected: $DESKTOP_FILE" >&2
  exit 1
fi

if ! rg -q '^Terminal=true$' "$DESKTOP_FILE"; then
  echo "desktop entry Terminal mode is unexpected: $DESKTOP_FILE" >&2
  exit 1
fi

if ! file "$TARGET" | rg -q 'ELF 64-bit'; then
  echo "standalone binary is not linux ELF: $TARGET" >&2
  file "$TARGET" >&2 || true
  exit 1
fi

if command -v ldd >/dev/null 2>&1; then
  ldd_out="$(ldd "$TARGET" 2>&1 || true)"
  if printf '%s\n' "$ldd_out" | rg -q 'not found'; then
    echo "standalone binary has unresolved shared libraries: $TARGET" >&2
    printf '%s\n' "$ldd_out" >&2
    exit 1
  fi

  # self-contained publish should not require dotnet runtime host/coreclr libs
  if printf '%s\n' "$ldd_out" | rg -qi 'libhostfxr|libhostpolicy|libcoreclr'; then
    echo "standalone binary appears framework-dependent (dotnet host/coreclr dependency detected)." >&2
    printf '%s\n' "$ldd_out" >&2
    exit 1
  fi
else
  echo "warning: ldd not found; skipping shared-library dependency check." >&2
fi

if [[ "$BINARY_NAME" == *.exe ]]; then
  STEM_TARGET="$GAME_DIR/${BINARY_NAME%.exe}"
  if [[ ! -f "$STEM_TARGET" || ! -x "$STEM_TARGET" ]]; then
    echo "linux stem executable missing or not executable: $STEM_TARGET" >&2
    exit 1
  fi
fi

run_isolated() {
  local cmd=("$@")
  env -i \
    HOME="${TMPDIR:-/tmp}" \
    PATH="/usr/bin:/bin" \
    LANG="C.UTF-8" \
    LC_ALL="C.UTF-8" \
    DOTNET_ROOT="/nonexistent" \
    DOTNET_MULTILEVEL_LOOKUP="0" \
    "${cmd[@]}"
}

run_isolated "$TARGET" --game-dir "$GAME_DIR" --verify-only
run_isolated "$LAUNCHER" --verify-only
run_isolated "$LAUNCHER" --launch-target "$BINARY_NAME" --verify-only

echo "Offline standalone verification passed."
echo "- Binary  : $TARGET"
echo "- Launcher: $LAUNCHER"
echo "- Desktop : $DESKTOP_FILE"
