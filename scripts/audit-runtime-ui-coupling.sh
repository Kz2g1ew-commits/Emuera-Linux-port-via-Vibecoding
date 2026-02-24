#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

SCOPE=(
  "$ROOT_DIR/Emuera/Runtime"
)

# Current intentional UI coupling points in runtime-side code.
ALLOW_FILES=(
)

PATTERN='MinorShift\.Emuera\.UI'

mapfile -t hits < <(rg -n --no-heading --color=never "$PATTERN" "${SCOPE[@]}" -g"*.cs" || true)

status=0

if [[ ${#hits[@]} -eq 0 ]]; then
  echo "Runtime/UI coupling audit passed: no UI namespace references found in runtime scope."
else
  blocked=()
  for hit in "${hits[@]}"; do
    file="${hit%%:*}"
    allowed=false
    for af in "${ALLOW_FILES[@]}"; do
      if [[ "$file" == "$af" ]]; then
        allowed=true
        break
      fi
    done
    if [[ "$allowed" == false ]]; then
      blocked+=("$hit")
    fi
  done

  echo "Runtime/UI coupling audit summary:"
  echo "- Total hits   : ${#hits[@]}"
  echo "- Allowed hits : $((${#hits[@]} - ${#blocked[@]}))"
  echo "- Blocked hits : ${#blocked[@]}"

  if [[ ${#blocked[@]} -gt 0 ]]; then
    echo
    echo "Blocked Runtime -> UI references:"
    printf '%s\n' "${blocked[@]}"
    status=4
  else
    echo "Runtime/UI coupling audit passed (all hits are allowlisted)."
  fi
fi

# Runtime script layer should not cast RuntimeHost.GetCurrentProcess() to concrete Process.
PROCESS_CAST_PATTERN='RuntimeHost\.GetCurrentProcess\(\)\s+as\s+Process'
mapfile -t process_cast_hits < <(rg -n --no-heading --color=never "$PROCESS_CAST_PATTERN" "$ROOT_DIR/Emuera/Runtime/Script" -g"*.cs" || true)

if [[ ${#process_cast_hits[@]} -eq 0 ]]; then
  echo "Runtime process-cast audit passed: no RuntimeHost.GetCurrentProcess() as Process usage in runtime script scope."
else
  echo "Runtime process-cast audit summary:"
  echo "- Blocked hits : ${#process_cast_hits[@]}"
  printf '%s\n' "${process_cast_hits[@]}"
  if [[ "$status" -eq 0 ]]; then
    status=7
  fi
fi

# Runtime script layer must not depend on System.Drawing directly.
SCRIPT_SCOPE=(
  "$ROOT_DIR/Emuera/Runtime/Script"
)
ALLOW_DRAWING_FILES=(
)
DRAWING_PATTERN='^using System\.Drawing;'

mapfile -t drawing_hits < <(rg -n --no-heading --color=never "$DRAWING_PATTERN" "${SCRIPT_SCOPE[@]}" -g"*.cs" || true)
if [[ ${#drawing_hits[@]} -eq 0 ]]; then
  echo "Runtime drawing-coupling audit passed: no System.Drawing import found in runtime script scope."
  exit "$status"
fi

drawing_blocked=()
for hit in "${drawing_hits[@]}"; do
  file="${hit%%:*}"
  allowed=false
  for af in "${ALLOW_DRAWING_FILES[@]}"; do
    if [[ "$file" == "$af" ]]; then
      allowed=true
      break
    fi
  done
  if [[ "$allowed" == false ]]; then
    drawing_blocked+=("$hit")
  fi
done

echo "Runtime drawing-coupling audit summary:"
echo "- Total hits   : ${#drawing_hits[@]}"
echo "- Allowed hits : $((${#drawing_hits[@]} - ${#drawing_blocked[@]}))"
echo "- Blocked hits : ${#drawing_blocked[@]}"

if [[ ${#drawing_blocked[@]} -gt 0 ]]; then
  echo
  echo "Blocked runtime script -> System.Drawing imports:"
  printf '%s\n' "${drawing_blocked[@]}"
  if [[ "$status" -eq 0 ]]; then
    status=5
  else
    status=6
  fi
fi

if [[ ${#drawing_blocked[@]} -eq 0 ]]; then
  echo "Runtime drawing-coupling audit passed (all hits are allowlisted)."
fi

exit "$status"
