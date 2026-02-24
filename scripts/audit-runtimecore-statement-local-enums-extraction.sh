#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

RUNTIMECORE_CASE_EXPR_TYPE_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Statements.CaseExpressionType.cs"
RUNTIMECORE_SORT_ORDER_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Statements.SortOrder.cs"
RUNTIMECORE_DT_OPTIONS_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Statements.DTOptions.cs"
RUNTIMECORE_ARGS_END_WITH_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Expression.ArgsEndWith.cs"
RUNTIMECORE_TERM_END_WITH_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Expression.TermEndWith.cs"

LEGACY_CASE_EXPRESSION_FILE="$ROOT_DIR/Emuera/Runtime/Script/Statements/CaseExpression.cs"
LEGACY_ARGUMENT_FILE="$ROOT_DIR/Emuera/Runtime/Script/Statements/Argument.cs"
LEGACY_EXPRESSION_PARSER_FILE="$ROOT_DIR/Emuera/Runtime/Script/Statements/Expression/ExpressionParser.cs"
LEGACY_CLIPBOARD_FILE="$ROOT_DIR/Emuera/Runtime/Script/Statements/Clipboard.cs"

for file in \
  "$RUNTIMECORE_CASE_EXPR_TYPE_FILE" \
  "$RUNTIMECORE_SORT_ORDER_FILE" \
  "$RUNTIMECORE_DT_OPTIONS_FILE" \
  "$RUNTIMECORE_ARGS_END_WITH_FILE" \
  "$RUNTIMECORE_TERM_END_WITH_FILE"
do
  if [[ ! -f "$file" ]]; then
    echo "RuntimeCore statement-local-enums extraction audit failed: missing RuntimeCore file: $file" >&2
    exit 1
  fi
done

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Statements;" "$RUNTIMECORE_CASE_EXPR_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: CaseExpressionType namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum CaseExpressionType" "$RUNTIMECORE_CASE_EXPR_TYPE_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: CaseExpressionType enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Statements;" "$RUNTIMECORE_SORT_ORDER_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: SortOrder namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum SortOrder" "$RUNTIMECORE_SORT_ORDER_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: SortOrder enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum CBTriggers" "$RUNTIMECORE_SORT_ORDER_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: CBTriggers enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Statements;" "$RUNTIMECORE_DT_OPTIONS_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: DTOptions namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum DTOptions" "$RUNTIMECORE_DT_OPTIONS_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: DTOptions enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Statements.Expression;" "$RUNTIMECORE_ARGS_END_WITH_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: ArgsEndWith namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum ArgsEndWith" "$RUNTIMECORE_ARGS_END_WITH_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: ArgsEndWith enum declaration missing." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Statements.Expression;" "$RUNTIMECORE_TERM_END_WITH_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: TermEndWith namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "internal enum TermEndWith" "$RUNTIMECORE_TERM_END_WITH_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: TermEndWith enum declaration missing." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "internal enum CaseExpressionType" "$LEGACY_CASE_EXPRESSION_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: legacy CaseExpressionType enum still declared in CaseExpression.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "internal enum SortOrder" "$LEGACY_ARGUMENT_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: legacy SortOrder enum still declared in Argument.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "enum DTOptions" "$LEGACY_ARGUMENT_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: legacy DTOptions enum still declared in Argument.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "internal enum CBTriggers" "$LEGACY_CLIPBOARD_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: legacy CBTriggers enum still declared in Clipboard.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "internal enum ArgsEndWith" "$LEGACY_EXPRESSION_PARSER_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: legacy ArgsEndWith enum still declared in ExpressionParser.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "internal enum TermEndWith" "$LEGACY_EXPRESSION_PARSER_FILE" >/dev/null; then
  echo "RuntimeCore statement-local-enums extraction audit failed: legacy TermEndWith enum still declared in ExpressionParser.cs." >&2
  exit 1
fi

echo "RuntimeCore statement-local-enums extraction audit passed: statement-local enums are RuntimeCore-owned."
