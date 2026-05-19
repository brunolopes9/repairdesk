# 50 - Orcamentos Moloni

## Objetivo

RepairDesk passa a suportar dois caminhos de orcamento:

- PDF Orcamento: documento proprio, rapido e branded LopesTech, sem ATCUD/certificacao AT.
- Orcamento Moloni: documento formal emitido via Moloni, com numeracao e certificacao do software Moloni.

## Fluxo

1. Na reparacao ou trabalho, o operador pode continuar a abrir o PDF proprio.
2. Se a faturacao Moloni estiver configurada, aparece `Emitir Orcamento Moloni`.
3. Antes de emitir, a UI pede confirmacao porque a operacao chama a conta Moloni real, ou sandbox se o tenant estiver em sandbox.
4. O documento Moloni fica guardado localmente em:
   - `EstimateExternalId`
   - `EstimateNumber`
   - `EstimatePdfUrl`
   - `EstimateEmittedAt`
5. Depois de aceite pelo cliente, o operador usa `Converter em Fatura`.
6. RepairDesk chama Moloni para converter o documento original em fatura e grava os campos de fatura ja existentes.

## Backend

- `IMoloniClient.InsertEstimateAsync` usa `POST /v1/estimates/insert/`.
- `IMoloniClient.GetEstimateStatusAsync` reutiliza `documents/getOne`.
- `IMoloniClient.ConvertEstimateToInvoiceAsync` isola a chamada `documentsToInvoice`, para que qualquer ajuste fino do payload fique concentrado no cliente Moloni.
- `ReparacaoService` e `TrabalhoService` registam audit log explicito para emitir e converter.
- Migration: `Sprint60EstimateFields`.

## Notas operacionais

- O PDF proprio nao foi removido.
- A conversao so e permitida quando existe `EstimateExternalId`.
- Se ja existir fatura local, a conversao e idempotente e devolve o estado atual.
- A sincronizacao futura pode usar `GetEstimateStatusAsync` para limpar documentos anulados ou marcar estados locais.
