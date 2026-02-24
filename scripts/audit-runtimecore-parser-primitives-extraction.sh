#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EMUERA_CSPROJ_FILE="$ROOT_DIR/Emuera/Emuera.csproj"

RUNTIMECORE_SUBWORD_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Parser.SubWord.cs"
RUNTIMECORE_WORD_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Parser.Word.cs"
RUNTIMECORE_WORD_COLLECTION_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Parser.WordCollection.cs"
RUNTIMECORE_OPERATOR_FILE="$ROOT_DIR/Emuera.RuntimeCore/Runtime/Script/Expression.OperatorCode.cs"

LEGACY_SUBWORD_FILE="$ROOT_DIR/Emuera/Runtime/Script/Parser/SubWord.cs"
LEGACY_WORD_FILE="$ROOT_DIR/Emuera/Runtime/Script/Parser/Word.cs"
LEGACY_WORD_COLLECTION_FILE="$ROOT_DIR/Emuera/Runtime/Script/Parser/WordCollection.cs"
LEGACY_OPERATOR_FILE="$ROOT_DIR/Emuera/Runtime/Script/Statements/Expression/OperatorCode.cs"
LEGACY_LEXICAL_ANALYZER_FILE="$ROOT_DIR/Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs"

for file in \
  "$RUNTIMECORE_SUBWORD_FILE" \
  "$RUNTIMECORE_WORD_FILE" \
  "$RUNTIMECORE_WORD_COLLECTION_FILE" \
  "$RUNTIMECORE_OPERATOR_FILE"
do
  if [[ ! -f "$file" ]]; then
    echo "RuntimeCore parser-primitives extraction audit failed: missing RuntimeCore file: $file" >&2
    exit 1
  fi
done

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Parser;" "$RUNTIMECORE_SUBWORD_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: SubWord RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Parser;" "$RUNTIMECORE_WORD_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: Word RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Parser;" "$RUNTIMECORE_WORD_COLLECTION_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: WordCollection RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "namespace MinorShift.Emuera.Runtime.Script.Statements.Expression;" "$RUNTIMECORE_OPERATOR_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: OperatorCode RuntimeCore namespace mismatch." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "enum LexEndWith" "$RUNTIMECORE_WORD_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: LexEndWith enum declaration missing in RuntimeCore parser primitives." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "enum FormStrEndWith" "$RUNTIMECORE_WORD_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: FormStrEndWith enum declaration missing in RuntimeCore parser primitives." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "enum StrEndWith" "$RUNTIMECORE_WORD_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: StrEndWith enum declaration missing in RuntimeCore parser primitives." >&2
  exit 1
fi

if ! rg -n --fixed-strings --no-heading --color=never "enum LexAnalyzeFlag" "$RUNTIMECORE_WORD_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: LexAnalyzeFlag enum declaration missing in RuntimeCore parser primitives." >&2
  exit 1
fi

if [[ -f "$LEGACY_SUBWORD_FILE" ]] && ! rg -n --fixed-strings --no-heading --color=never "<Compile Remove=\"Runtime/Script/Parser/SubWord.cs\" />" "$EMUERA_CSPROJ_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: legacy SubWord file is not compile-removed." >&2
  exit 1
fi

if [[ -f "$LEGACY_WORD_FILE" ]] && ! rg -n --fixed-strings --no-heading --color=never "<Compile Remove=\"Runtime/Script/Parser/Word.cs\" />" "$EMUERA_CSPROJ_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: legacy Word file is not compile-removed." >&2
  exit 1
fi

if [[ -f "$LEGACY_WORD_COLLECTION_FILE" ]] && ! rg -n --fixed-strings --no-heading --color=never "<Compile Remove=\"Runtime/Script/Parser/WordCollection.cs\" />" "$EMUERA_CSPROJ_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: legacy WordCollection file is not compile-removed." >&2
  exit 1
fi

if [[ -f "$LEGACY_OPERATOR_FILE" ]] && ! rg -n --fixed-strings --no-heading --color=never "<Compile Remove=\"Runtime/Script/Statements/Expression/OperatorCode.cs\" />" "$EMUERA_CSPROJ_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: legacy OperatorCode file is not compile-removed." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "enum LexEndWith" "$LEGACY_LEXICAL_ANALYZER_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: legacy LexEndWith enum still declared in LexicalAnalyzer.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "enum FormStrEndWith" "$LEGACY_LEXICAL_ANALYZER_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: legacy FormStrEndWith enum still declared in LexicalAnalyzer.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "enum StrEndWith" "$LEGACY_LEXICAL_ANALYZER_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: legacy StrEndWith enum still declared in LexicalAnalyzer.cs." >&2
  exit 1
fi

if rg -n --fixed-strings --no-heading --color=never "enum LexAnalyzeFlag" "$LEGACY_LEXICAL_ANALYZER_FILE" >/dev/null; then
  echo "RuntimeCore parser-primitives extraction audit failed: legacy LexAnalyzeFlag enum still declared in LexicalAnalyzer.cs." >&2
  exit 1
fi

echo "RuntimeCore parser-primitives extraction audit passed: parser token/operator primitives are RuntimeCore-owned."
