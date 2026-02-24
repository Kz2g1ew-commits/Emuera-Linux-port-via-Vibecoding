#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if rg -n "\bIProcessConsole\b|\bIProcessUiConsole\b" "$ROOT_DIR/Emuera" -g"*.cs" >/dev/null 2>&1; then
  echo "RuntimeBridge legacy-process-console audit failed: IProcessConsole/IProcessUiConsole references remain." >&2
  exit 1
fi

mapfile -t runtime_hits < <(rg -n --no-heading --color=never "\bIProcessRuntimeConsole\b" "$ROOT_DIR/Emuera" "$ROOT_DIR/Emuera.RuntimeCore" -g"*.cs" || true)

ALLOW_RUNTIME_FILES=(
  "$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/IProcessRuntimeConsole.cs"
  "$ROOT_DIR/Emuera/UI/Game/EmueraConsole.cs"
)

blocked_runtime_hits=()
for hit in "${runtime_hits[@]}"; do
  file="${hit%%:*}"
  allowed=false
  for allow in "${ALLOW_RUNTIME_FILES[@]}"; do
    if [[ "$file" == "$allow" ]]; then
      allowed=true
      break
    fi
  done
  if [[ "$allowed" == false ]]; then
    blocked_runtime_hits+=("$hit")
  fi
done

if [[ ${#blocked_runtime_hits[@]} -gt 0 ]]; then
  echo "RuntimeBridge runtime-console audit failed: disallowed IProcessRuntimeConsole references remain." >&2
  printf "%s\n" "${blocked_runtime_hits[@]}" >&2
  exit 1
fi

echo "RuntimeBridge legacy-process-console audit passed: no IProcessConsole/IProcessUiConsole references found."
echo "RuntimeBridge runtime-console audit passed: IProcessRuntimeConsole usage is limited to runtime marker+host implementation."
