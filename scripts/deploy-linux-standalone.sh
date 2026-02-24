#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME_DIR="${1:-}"
TARGET_NAME="${2:-}"
INSTALL_DESKTOP="${3:-}"

if [[ -z "$GAME_DIR" ]]; then
  echo "usage: $0 <game-dir> [target-file-name] [--install-desktop]" >&2
  exit 2
fi

if [[ ! -d "$GAME_DIR" ]]; then
  echo "game directory not found: $GAME_DIR" >&2
  exit 2
fi

copy_linux_libwebp() {
  local game_dir="$1"
  local picked=""
  local -a candidates=()

  if [[ -n "${LIBWEBP_SO_PATH:-}" && -f "${LIBWEBP_SO_PATH:-}" ]]; then
    candidates+=("$LIBWEBP_SO_PATH")
  fi

  if command -v ldconfig >/dev/null 2>&1; then
    while IFS= read -r so_path; do
      [[ -n "$so_path" && -f "$so_path" ]] && candidates+=("$so_path")
    done < <(ldconfig -p 2>/dev/null | awk '/libwebp\.so/ { print $NF }')
  fi

  candidates+=(
    "/usr/lib/x86_64-linux-gnu/libwebp.so.7"
    "/usr/lib/x86_64-linux-gnu/libwebp.so.6"
    "/usr/lib64/libwebp.so.7"
    "/usr/lib64/libwebp.so.6"
    "/usr/lib/libwebp.so.7"
    "/usr/lib/libwebp.so.6"
  )

  for src in "${candidates[@]}"; do
    if [[ -f "$src" ]]; then
      picked="$src"
      break
    fi
  done

  if [[ -z "$picked" ]]; then
    echo "warning: libwebp shared library not found on host; relying on target system library." >&2
    return 0
  fi

  local so_name
  so_name="$(basename "$picked")"
  cp -L "$picked" "$game_dir/$so_name"
  if [[ "$so_name" != "libwebp.so" ]]; then
    ln -sf "$so_name" "$game_dir/libwebp.so"
  fi
}

bash "$ROOT_DIR/scripts/publish-linux-cli.sh"

SOURCE_BIN="$ROOT_DIR/artifacts/linux-x64-cli/Emuera.Cli"
if [[ ! -x "$SOURCE_BIN" ]]; then
  echo "published binary not found: $SOURCE_BIN" >&2
  exit 1
fi

if [[ -z "$TARGET_NAME" ]]; then
  found_name="$(find "$GAME_DIR" -maxdepth 1 -type f -name 'Emuera*.exe' -printf '%f\n' | head -n 1 || true)"
  if [[ -n "$found_name" ]]; then
    TARGET_NAME="$found_name"
  else
    TARGET_NAME="EmueraLinux"
  fi
fi

TARGET_PATH="$GAME_DIR/$TARGET_NAME"
install_binary_safely() {
  local source_bin="$1"
  local target_path="$2"
  local target_dir
  target_dir="$(dirname "$target_path")"
  local tmp_path
  tmp_path="$(mktemp "$target_dir/.emuera.deploy.XXXXXX")"
  cp "$source_bin" "$tmp_path"
  chmod +x "$tmp_path"
  mv -f "$tmp_path" "$target_path"
}

install_binary_safely "$SOURCE_BIN" "$TARGET_PATH"
copy_linux_libwebp "$GAME_DIR"

if [[ "$TARGET_NAME" == *.exe ]]; then
  STEM_NAME="${TARGET_NAME%.exe}"
  if [[ -n "$STEM_NAME" ]]; then
    STEM_PATH="$GAME_DIR/$STEM_NAME"
    install_binary_safely "$TARGET_PATH" "$STEM_PATH"
  fi
fi

LAUNCHER_PATH="$GAME_DIR/Run-Emuera-Linux.sh"
cat > "$LAUNCHER_PATH" <<EOF
#!/usr/bin/env bash
set -euo pipefail
DIR="\$(cd "\$(dirname "\${BASH_SOURCE[0]}")" && pwd)"
export EMUERA_LIBWEBP_DIR="\$DIR"
if [[ -z "\${LANG:-}" ]]; then
  if command -v locale >/dev/null 2>&1 && locale -a 2>/dev/null | grep -Eqi '^ko_KR\.utf-?8$'; then
    export LANG="ko_KR.UTF-8"
  else
    export LANG="C.UTF-8"
  fi
fi
export LC_CTYPE="\${LC_CTYPE:-\$LANG}"

# Optional: if needed, user can set VTE_CJK_WIDTH=2 before launch.
# Do not force it by default because terminal/font behavior differs by environment.

export EMUERA_NO_ANSI="\${EMUERA_NO_ANSI:-0}"
export EMUERA_CLI_DIRECT_KEY_CAPTURE="\${EMUERA_CLI_DIRECT_KEY_CAPTURE:-0}"
export EMUERA_CLI_FORCE_BLACK_BG="\${EMUERA_CLI_FORCE_BLACK_BG:-1}"
export EMUERA_CLI_OSC11_BG="\${EMUERA_CLI_OSC11_BG:-1}"

direct_mode=0
for arg in "\$@"; do
  case "\$arg" in
    --verify-only|--run-smoke-only|--strict-smoke-only|--strict-retries|--strict-retries=*|--gate-only|--preload-only|--scan-erh-only|--scan-erb-pp-only|--scan-erb-cont-only|--interactive-gui|--play-like|--run-engine|--run-engine-gui|--gui-engine|--run-gui|--launch-bundled|--launch-target|--launch-target=*|--allow-pe-launch)
      direct_mode=1
      break
      ;;
  esac
done

if [[ "\$direct_mode" -eq 1 ]]; then
  exec "\$DIR/$TARGET_NAME" "\$@"
fi

# Default to terminal runtime gameplay mode.
exec "\$DIR/$TARGET_NAME" --run-engine "\$@"
EOF
chmod +x "$LAUNCHER_PATH"

DESKTOP_FILE_PATH="$GAME_DIR/Run-Emuera-Linux.desktop"
APP_NAME="Emuera Linux ($(basename "$GAME_DIR"))"
cat > "$DESKTOP_FILE_PATH" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=$APP_NAME
Comment=Run Emuera Linux standalone in this game folder
Exec=./Run-Emuera-Linux.sh
Path=$GAME_DIR
Terminal=true
Categories=Game;
StartupNotify=true
EOF
chmod +x "$DESKTOP_FILE_PATH"

if [[ "$INSTALL_DESKTOP" == "--install-desktop" ]]; then
  DESKTOP_INSTALL_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
  mkdir -p "$DESKTOP_INSTALL_DIR"
  cp "$DESKTOP_FILE_PATH" "$DESKTOP_INSTALL_DIR/emuera-$(basename "$GAME_DIR").desktop"
  if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$DESKTOP_INSTALL_DIR" >/dev/null 2>&1 || true
  fi
fi

echo "Deployed Linux standalone binary:"
echo "- Source: $SOURCE_BIN"
echo "- Target: $TARGET_PATH"
echo "- Launcher: $LAUNCHER_PATH"
echo "- Desktop : $DESKTOP_FILE_PATH"
if [[ "$INSTALL_DESKTOP" == "--install-desktop" ]]; then
  echo "- Installed desktop entry: ${XDG_DATA_HOME:-$HOME/.local/share}/applications/emuera-$(basename "$GAME_DIR").desktop"
fi
echo
echo "Smoke check:"
(cd "$GAME_DIR" && "./$TARGET_NAME" --run-smoke-only)
