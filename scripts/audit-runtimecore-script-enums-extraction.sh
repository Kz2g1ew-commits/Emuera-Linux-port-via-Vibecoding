#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EMUERA_CSPROJ_FILE="$ROOT_DIR/Emuera/Emuera.csproj"

RUNTIMECORE_FUNCTION_ARG_TYPE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Statements.FunctionArgType.cs"
RUNTIMECORE_FUNCTION_CODE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Statements.FunctionCode.cs"
RUNTIMECORE_ARG_TYPE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Function.ArgType.cs"
RUNTIMECORE_DEFINED_NAME_TYPE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Data.DefinedNameType.cs"
RUNTIMECORE_VARIABLE_CODE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Variable.VariableCode.cs"
RUNTIMECORE_CONFIG_CODE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Config/ConfigCode.cs"
RUNTIMECORE_CHARACTER_STR_DATA_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Data.CharacterStrData.cs"
RUNTIMECORE_CHARACTER_INT_DATA_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Data.CharacterIntData.cs"
RUNTIMECORE_UDF_ARG_TYPE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Data.UserDifinedFunctionDataArgType.cs"

LEGACY_FUNCTION_ARG_TYPE_FILE="$ROOT_DIR/Emuera/Runtime/Script/Statements/FunctionArgType.cs"
LEGACY_FUNCTION_CODE_FILE="$ROOT_DIR/Emuera/Runtime/Script/Statements/BuiltInFunctionCode.cs"
LEGACY_FUNCTION_METHOD_FILE="$ROOT_DIR/Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs"
LEGACY_IDENTIFIER_DICTIONARY_FILE="$ROOT_DIR/Emuera/Runtime/Script/Data/IdentifierDictionary.cs"
LEGACY_VARIABLE_CODE_FILE="$ROOT_DIR/Emuera/Runtime/Script/Statements/Variable/VariableCode.cs"
LEGACY_CONFIG_CODE_FILE="$ROOT_DIR/Emuera/Runtime/Config/ConfigCode.cs"
LEGACY_CONSTANT_DATA_FILE="$ROOT_DIR/Emuera/Runtime/Script/Data/ConstantData.cs"
LEGACY_USER_DEFINED_FUNCTION_FILE="$ROOT_DIR/Emuera/Runtime/Script/Data/UserDefinedFunction.cs"

for file in \
  "$RUNTIMECORE_FUNCTION_ARG_TYPE_FILE" \
  "$RUNTIMECORE_FUNCTION_CODE_FILE" \
  "$RUNTIMECORE_ARG_TYPE_FILE" \
  "$RUNTIMECORE_DEFINED_NAME_TYPE_FILE" \
  "$RUNTIMECORE_VARIABLE_CODE_FILE" \
  "$RUNTIMECORE_CONFIG_CODE_FILE" \
  "$RUNTIMECORE_CHARACTER_STR_DATA_FILE" \
  "$RUNTIMECORE_CHARACTER_INT_DATA_FILE" \
  "$RUNTIMECORE_UDF_ARG_TYPE_FILE"
do
  if [[ ! -f "$file" ]]; then
    echo "RuntimeCore script-enums extraction audit failed: missing RuntimeCore file: $file" >&2
    exit 1
  fi
done

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Statements;" "$RUNTIMECORE_FUNCTION_ARG_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: FunctionArgType RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "enum FunctionArgType" "$RUNTIMECORE_FUNCTION_ARG_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: FunctionArgType enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Statements;" "$RUNTIMECORE_FUNCTION_CODE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: FunctionCode RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "enum FunctionCode" "$RUNTIMECORE_FUNCTION_CODE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: FunctionCode enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Statements.Function;" "$RUNTIMECORE_ARG_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: ArgType RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "enum ArgType" "$RUNTIMECORE_ARG_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: ArgType enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera;" "$RUNTIMECORE_DEFINED_NAME_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: DefinedNameType RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "enum DefinedNameType" "$RUNTIMECORE_DEFINED_NAME_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: DefinedNameType enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Statements.Variable;" "$RUNTIMECORE_VARIABLE_CODE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: VariableCode RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "enum VariableCode" "$RUNTIMECORE_VARIABLE_CODE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: VariableCode enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Config;" "$RUNTIMECORE_CONFIG_CODE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: ConfigCode RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum ConfigCode" "$RUNTIMECORE_CONFIG_CODE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: ConfigCode enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum DisplayWarningFlag" "$RUNTIMECORE_CONFIG_CODE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: DisplayWarningFlag enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum TextDrawingMode" "$RUNTIMECORE_CONFIG_CODE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: TextDrawingMode enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Data;" "$RUNTIMECORE_CHARACTER_STR_DATA_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: CharacterStrData RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum CharacterStrData" "$RUNTIMECORE_CHARACTER_STR_DATA_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: CharacterStrData enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Data;" "$RUNTIMECORE_CHARACTER_INT_DATA_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: CharacterIntData RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum CharacterIntData" "$RUNTIMECORE_CHARACTER_INT_DATA_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: CharacterIntData enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Data;" "$RUNTIMECORE_UDF_ARG_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: UserDifinedFunctionDataArgType RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum UserDifinedFunctionDataArgType" "$RUNTIMECORE_UDF_ARG_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: UserDifinedFunctionDataArgType enum declaration missing." >&2
  exit 1
fi

if [[ -f "$LEGACY_FUNCTION_ARG_TYPE_FILE" ]] && ! rg -n --fixed-strings --no-heading --color=never "<Compile Remove=\"Runtime/Script/Statements/FunctionArgType.cs\" />" "$EMUERA_CSPROJ_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: legacy FunctionArgType file is not compile-removed." >&2
  exit 1
fi

if [[ -f "$LEGACY_FUNCTION_CODE_FILE" ]] && ! rg -n --fixed-strings --no-heading --color=never "<Compile Remove=\"Runtime/Script/Statements/BuiltInFunctionCode.cs\" />" "$EMUERA_CSPROJ_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: legacy BuiltInFunctionCode file is not compile-removed." >&2
  exit 1
fi

if [[ -f "$LEGACY_VARIABLE_CODE_FILE" ]] && ! rg -n --fixed-strings --no-heading --color=never "<Compile Remove=\"Runtime/Script/Statements/Variable/VariableCode.cs\" />" "$EMUERA_CSPROJ_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: legacy VariableCode file is not compile-removed." >&2
  exit 1
fi

if [[ -f "$LEGACY_CONFIG_CODE_FILE" ]] && ! rg -n --fixed-strings --no-heading --color=never "<Compile Remove=\"Runtime/Config/ConfigCode.cs\" />" "$EMUERA_CSPROJ_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: legacy ConfigCode file is not compile-removed." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "enum ArgType" "$LEGACY_FUNCTION_METHOD_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: legacy ArgType enum still declared in FunctionMethod.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "enum DefinedNameType" "$LEGACY_IDENTIFIER_DICTIONARY_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: legacy DefinedNameType enum still declared in IdentifierDictionary.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "internal enum CharacterStrData" "$LEGACY_CONSTANT_DATA_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: legacy CharacterStrData enum still declared in ConstantData.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "internal enum CharacterIntData" "$LEGACY_CONSTANT_DATA_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: legacy CharacterIntData enum still declared in ConstantData.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "internal enum UserDifinedFunctionDataArgType" "$LEGACY_USER_DEFINED_FUNCTION_FILE" >/dev/null; then
  echo "RuntimeCore script-enums extraction audit failed: legacy UserDifinedFunctionDataArgType enum still declared in UserDefinedFunction.cs." >&2
  exit 1
fi

echo "RuntimeCore script-enums extraction audit passed: core script/config/data enums are RuntimeCore-owned."
