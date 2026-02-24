#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNTIMECORE_CTRLZ_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Utils/CtrlZ.cs"
RUNTIMEHOST_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Utils/RuntimeHost.cs"
EMUERA_PROJECT_FILE="$ROOT_DIR/Emuera/Emuera.csproj"
PROGRAM_FILE="$ROOT_DIR/Emuera/Program.cs"
CLI_PROGRAM_FILE="$ROOT_DIR/Emuera.Cli/Program.cs"

if [[ ! -f "$RUNTIMECORE_CTRLZ_FILE" ]]; then
  echo "RuntimeCore CtrlZ extraction audit failed: missing $RUNTIMECORE_CTRLZ_FILE" >&2
  exit 1
fi

required_runtimehost_patterns=(
  "public static Func<bool> IsCtrlZEnabledHook { get; set; }"
  "public static Action<long[]> CaptureRandomSeedHook { get; set; }"
  "public static bool IsCtrlZEnabled()"
  "public static void CaptureRandomSeed(long[] seedBuffer)"
)

for pattern in "${required_runtimehost_patterns[@]}"; do
  if ! rg -n --fixed-strings --no-heading --color=never "$pattern" "$RUNTIMEHOST_FILE" >/dev/null; then
    echo "RuntimeCore CtrlZ extraction audit failed: RuntimeHost pattern missing: $pattern" >&2
    exit 1
  fi
done

if ! rg -n --fixed-strings --no-heading --color=never '<Compile Remove="Runtime/Utils/CtrlZ.cs" />' "$EMUERA_PROJECT_FILE" >/dev/null; then
  echo "RuntimeCore CtrlZ extraction audit failed: Emuera.csproj does not exclude legacy CtrlZ compile path." >&2
  exit 1
fi

required_program_patterns=(
  "RuntimeHost.IsCtrlZEnabledHook = static () => Config.Ctrl_Z_Enabled;"
  "RuntimeHost.CaptureRandomSeedHook = static (seedBuffer) => RuntimeGlobals.VEvaluator?.Rand.GetRand(seedBuffer);"
)

for pattern in "${required_program_patterns[@]}"; do
  if ! rg -n --fixed-strings --no-heading --color=never "$pattern" "$PROGRAM_FILE" >/dev/null; then
    echo "RuntimeCore CtrlZ extraction audit failed: Program host wiring missing: $pattern" >&2
    exit 1
  fi
done

required_cli_patterns=(
  "RuntimeHost.IsCtrlZEnabledHook = static () => false;"
  "RuntimeHost.CaptureRandomSeedHook = static (_) => { };"
)

for pattern in "${required_cli_patterns[@]}"; do
  if ! rg -n --fixed-strings --no-heading --color=never "$pattern" "$CLI_PROGRAM_FILE" >/dev/null; then
    echo "RuntimeCore CtrlZ extraction audit failed: CLI host wiring missing: $pattern" >&2
    exit 1
  fi
done

echo "RuntimeCore CtrlZ extraction audit passed: CtrlZ class and runtime host hooks are correctly extracted/wired."
