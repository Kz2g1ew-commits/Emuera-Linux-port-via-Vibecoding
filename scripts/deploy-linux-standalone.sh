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

# Use wide ambiguous width to match classic ERA map composition better.
export VTE_CJK_WIDTH="\${VTE_CJK_WIDTH:-2}"
# Keep runtime display-width model in sync with terminal ambiguous-width policy.
export EMUERA_CLI_AMBIGUOUS_WIDTH="\${EMUERA_CLI_AMBIGUOUS_WIDTH:-\$VTE_CJK_WIDTH}"

export EMUERA_NO_ANSI="\${EMUERA_NO_ANSI:-0}"
export EMUERA_CLI_DIRECT_KEY_CAPTURE="\${EMUERA_CLI_DIRECT_KEY_CAPTURE:-0}"
export EMUERA_CLI_FORCE_BLACK_BG="\${EMUERA_CLI_FORCE_BLACK_BG:-1}"
export EMUERA_CLI_OSC11_BG="\${EMUERA_CLI_OSC11_BG:-1}"
export EMUERA_CLI_PAD_MAP_SYMBOLS="\${EMUERA_CLI_PAD_MAP_SYMBOLS:-1}"
export EMUERA_CLI_USE_NATIVE_WCWIDTH="\${EMUERA_CLI_USE_NATIVE_WCWIDTH:-0}"
export EMUERA_DEFAULT_MODE="\${EMUERA_DEFAULT_MODE:-engine}"
export EMUERA_CLI_LAYOUT_WIDTH="\${EMUERA_CLI_LAYOUT_WIDTH:-}"
export EMUERA_CLI_LAYOUT_HEIGHT="\${EMUERA_CLI_LAYOUT_HEIGHT:-}"
export EMUERA_CLI_AUTO_RESIZE="\${EMUERA_CLI_AUTO_RESIZE:-1}"
if [[ "\${EMUERA_CLI_AUTO_RESIZE}" != "0" && -t 1 ]]; then
  if [[ "\${EMUERA_CLI_LAYOUT_WIDTH}" =~ ^[0-9]+$ ]] && [[ "\${EMUERA_CLI_LAYOUT_HEIGHT}" =~ ^[0-9]+$ ]]; then
    printf '\033[8;%s;%st' "\${EMUERA_CLI_LAYOUT_HEIGHT}" "\${EMUERA_CLI_LAYOUT_WIDTH}" >/dev/tty 2>/dev/null || true
  fi
fi

DEFAULT_TARGET="\${EMUERA_DEFAULT_TARGET:-}"

collect_targets() {
  local dir="\$1"
  local -a list=()
  local default_target="\$DEFAULT_TARGET"
  local default_path=""
  if [[ -n "\$default_target" ]]; then
    default_path="\$dir/\$default_target"
    if [[ -f "\$default_path" ]]; then
      list+=("\$default_target")
    fi
  fi

  while IFS= read -r name; do
    [[ -z "\$name" ]] && continue
    [[ -n "\$default_target" && "\$name" == "\$default_target" ]] && continue
    list+=("\$name")
  done < <(find "\$dir" -maxdepth 1 -type f \( -name 'Emuera*.exe' -o -name 'Emuera*' \) -printf '%f\n' 2>/dev/null | sort -u)

  printf '%s\n' "\${list[@]}"
}

choose_target_interactive() {
  local -a options=("\$@")
  local count="\${#options[@]}"
  if (( count == 0 )); then
    return 1
  fi
  if (( count == 1 )); then
    printf '%s\n' "\${options[0]}"
    return 0
  fi

  echo "Select executable:" >&2
  local i=1
  for opt in "\${options[@]}"; do
    echo "  [\$i] \$opt" >&2
    ((i++))
  done

  local selected
  while true; do
    read -rp "Enter number (1-\$count): " selected
    if [[ "\$selected" =~ ^[0-9]+$ ]] && (( selected >= 1 && selected <= count )); then
      printf '%s\n' "\${options[selected - 1]}"
      return 0
    fi
    echo "Invalid selection." >&2
  done
}

launcher_target="\${EMUERA_TARGET_BIN:-}"
force_select=0
engine_args=()
while (( \$# > 0 )); do
  case "\$1" in
    --target-bin)
      shift
      if (( \$# == 0 )); then
        echo "missing value for --target-bin" >&2
        exit 2
      fi
      launcher_target="\$1"
      ;;
    --target-bin=*)
      launcher_target="\${1#*=}"
      ;;
    --select-bin)
      force_select=1
      ;;
    --help-launcher)
      cat <<'USAGE'
Usage: ./Run-Emuera-Linux.sh [launcher-options] [engine-options]
Launcher options:
  --target-bin <file>    Run with a specific executable in this folder.
  --target-bin=<file>    Same as above.
  --select-bin           Show executable selection menu.
  --help-launcher        Show this help.
Environment:
  EMUERA_DEFAULT_TARGET  Optional preferred executable filename.
  EMUERA_DEFAULT_MODE    Default runtime mode (default: engine).
  VTE_CJK_WIDTH            Terminal ambiguous-width policy (default: 2).
  EMUERA_CLI_AMBIGUOUS_WIDTH Engine ambiguous-width policy (default: VTE_CJK_WIDTH).
  EMUERA_CLI_PAD_MAP_SYMBOLS  Pad map-art symbols (default: 1).
  EMUERA_CLI_USE_NATIVE_WCWIDTH  Use libc wcwidth for terminal column width (default: 0).
  EMUERA_CLI_LAYOUT_WIDTH  Optional fixed layout width hint (unset by default).
  EMUERA_CLI_LAYOUT_HEIGHT Optional fixed layout height hint (unset by default).
  EMUERA_CLI_AUTO_RESIZE   1 to request terminal resize when width/height are set (default: 1).
USAGE
      exit 0
      ;;
    *)
      engine_args+=("\$1")
      ;;
  esac
  shift
done

if [[ -n "\$launcher_target" ]]; then
  if [[ ! -f "\$DIR/\$launcher_target" ]]; then
    echo "target executable not found: \$launcher_target" >&2
    exit 2
  fi
else
  mapfile -t available_targets < <(collect_targets "\$DIR")
  if (( \${#available_targets[@]} == 0 )); then
    echo "no Emuera executable found in: \$DIR" >&2
    exit 1
  fi

  should_prompt=0
  if (( force_select == 1 )); then
    should_prompt=1
  fi

  if (( should_prompt == 1 )); then
    launcher_target="\$(choose_target_interactive "\${available_targets[@]}")"
  else
    launcher_target="\${available_targets[0]}"
    if (( \${#available_targets[@]} > 1 )); then
      echo "multiple executables found; using default: \$launcher_target" >&2
      echo "use --select-bin or --target-bin <file> to choose another." >&2
    fi
  fi
fi

target_path="\$DIR/\$launcher_target"
if [[ ! -x "\$target_path" ]]; then
  chmod +x "\$target_path" 2>/dev/null || true
fi
if [[ ! -x "\$target_path" ]]; then
  echo "target is not executable: \$launcher_target" >&2
  exit 1
fi

direct_mode=0
for arg in "\${engine_args[@]}"; do
  case "\$arg" in
    --verify-only|--run-smoke-only|--strict-smoke-only|--strict-retries|--strict-retries=*|--gate-only|--preload-only|--scan-erh-only|--scan-erb-pp-only|--scan-erb-cont-only|--interactive-gui|--play-like|--run-engine|--run-engine-gui|--gui-engine|--run-gui|--launch-bundled|--launch-target|--launch-target=*|--allow-pe-launch)
      direct_mode=1
      break
      ;;
  esac
done

if [[ "\$direct_mode" -eq 1 ]]; then
  exec "\$target_path" "\${engine_args[@]}"
fi

# Default to terminal runtime gameplay mode.
exec "\$target_path" --run-engine "\${engine_args[@]}"
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
