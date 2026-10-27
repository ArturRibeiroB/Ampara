#!/usr/bin/env bash
# Compara o openapi.json gerado no build com o da última release (INF-21).
# - Escreve as mudanças do contrato no resumo do run.
# - Em push de tag vX.Y.Z: falha se houver quebra de contrato sem subir a versão maior (em 0.x, a menor).
# - No resto (push e pull request): quebra de contrato vira só aviso.
set -euo pipefail

NOVO="artifacts/openapi/openapi.json"
BASE="base-openapi.json"
OASDIFF="tufin/oasdiff:v1.34.1"

if ! gh release download --repo "$GITHUB_REPOSITORY" --pattern openapi.json --output "$BASE" 2>/dev/null; then
  echo "Nenhuma release anterior com openapi.json: comparação do contrato pulada." >> "$GITHUB_STEP_SUMMARY"
  exit 0
fi

VERSAO_BASE="$(gh release view --repo "$GITHUB_REPOSITORY" --json tagName --jq .tagName)"
oasdiff() { docker run --rm -v "$PWD:/specs" "$OASDIFF" "$@"; }

{
  echo "## Contrato da API em relação a ${VERSAO_BASE}"
  echo
  oasdiff changelog "/specs/$BASE" "/specs/$NOVO" --format markdown || true
} >> "$GITHUB_STEP_SUMMARY"

QUEBRAS="$(oasdiff breaking "/specs/$BASE" "/specs/$NOVO" --fail-on ERR --format text 2>&1)" && exit 0

echo "$QUEBRAS"
if [[ "${GITHUB_REF:-}" != refs/tags/v* ]]; then
  echo "::warning title=Quebra de contrato::Há quebra de contrato em relação a ${VERSAO_BASE}. O próximo release precisa subir a versão maior e avisar no CHANGELOG."
  exit 0
fi

IFS=. read -r MAIOR_BASE MENOR_BASE _ <<< "${VERSAO_BASE#v}"
IFS=. read -r MAIOR_NOVA MENOR_NOVA _ <<< "${GITHUB_REF_NAME#v}"
if (( MAIOR_NOVA > MAIOR_BASE )) || (( MAIOR_BASE == 0 && MAIOR_NOVA == 0 && MENOR_NOVA > MENOR_BASE )); then
  echo "Quebra de contrato com a versão maior subindo (${VERSAO_BASE} → ${GITHUB_REF_NAME}): ok."
  exit 0
fi

echo "::error title=Quebra de contrato::${GITHUB_REF_NAME} quebra o contrato de ${VERSAO_BASE} sem subir a versão maior."
exit 1
