# 47 - InvoiceXpress Setup

## Decisao

InvoiceXpress fica como segundo provider real de faturacao, ao lado da Moloni. O RepairDesk escolhe o provider em runtime por tenant atraves de `TenantBillingSettings.Provider`.

Uso recomendado:

- `Moloni`: default para lojas que ja usam Moloni ou querem OAuth/auto-configuracao.
- `InvoiceXpress`: alternativa para lojas com conta InvoiceXpress existente, volumes baixos ou preferencia por API key simples.

Nota importante: a documentacao oficial InvoiceXpress usa `api_key` no query string, nao OAuth. Apesar de alguns exemplos internos falarem em header, a implementacao segue a API oficial para evitar um falso positivo em producao.

Fontes oficiais:

- API reference: https://docs.invoicexpress.com/
- Base URL e autenticacao: https://docs.invoicexpress.com/#section/Introduction
- Invoices: https://docs.invoicexpress.com/invoices
- PDF: https://docs.invoicexpress.com/generates-a-pdf

## Configuracao no RepairDesk

Em `Definicoes > Faturacao`:

1. Escolher `InvoiceXpress`.
2. Preencher `Account Name`.
   - Exemplo: se a conta abre em `https://lopestech.app.invoicexpress.com`, o Account Name e `lopestech`.
3. Preencher `API key`.
4. Confirmar o NIF da empresa em `Definicoes > Empresa`.
5. Definir `Tipo de documento por defeito`.
   - `Fatura simplificada` para B2C.
   - `Fatura` para clientes com NIF/empresas.
6. Sincronizar ou preencher manualmente a serie (`sequence_id`).
7. Se a loja esta isenta de IVA, preencher motivo de isencao, por exemplo `M01`.

## Campos usados

Reutilizamos `TenantBillingSettings` sem migration nova:

- `Provider = InvoiceXpress`
- `ApiKeyCipherText` = API key InvoiceXpress, cifrada
- `ClientId` = Account Name
- `DefaultDocumentType` = fatura simplificada/fatura
- `DefaultSerieId` = sequence id InvoiceXpress
- `ExemptionReason` = motivo de isencao quando IVA 0

Campos Moloni como `CompanyId`, `DefaultProductId`, `DefaultTaxId`, `DefaultPaymentMethodId`, `DefaultMaturityDateId` e `FallbackCustomerId` nao sao usados pelo InvoiceXpress.

## Fluxo tecnico

1. UI guarda settings do tenant.
2. Controllers continuam a chamar `IBillingProvider`.
3. `TenantBillingProvider` carrega settings do tenant.
4. `BillingProviderFactory.GetProvider(settings)` escolhe:
   - `MoloniBillingProvider`
   - `InvoiceXpressBillingProvider`
5. Provider cria payload InvoiceXpress:
   - `simplified_invoices.json` para fatura simplificada
   - `invoices.json` para fatura
   - `credit_notes.json` quando o cancelamento direto falha
6. `InvoiceExternalId` fica com prefixo do tipo de documento:
   - `simplified_invoices:123`
   - `invoices:456`
   - `credit_notes:789`

Este prefixo evita perder o tipo de documento se o tenant mudar o default no futuro.

## Cancelamento/anulacao

O RepairDesk tenta primeiro `change-state` para `canceled`.

Se a InvoiceXpress rejeitar o cancelamento direto, o RepairDesk emite uma Nota de Credito com `owner_invoice_id` a apontar para o documento original.

Limite conhecido: se um tenant mudar de provider e sobrescrever a API key antiga, o RepairDesk pode manter o historico local da fatura, mas pode deixar de conseguir anular esse documento no provider anterior sem reconfigurar credenciais.

## Teste manual com sandbox/conta real

1. Criar/usar conta InvoiceXpress de teste.
2. Obter API key no painel InvoiceXpress.
3. Configurar provider no RepairDesk.
4. Clicar `Testar emissao` para validar ligacao.
5. Criar cliente com NIF.
6. Criar reparacao paga.
7. Emitir fatura.
8. Confirmar no painel InvoiceXpress:
   - documento criado
   - cliente criado/associado
   - item criado nas linhas
   - PDF disponivel
9. Anular a fatura no RepairDesk e confirmar cancelamento ou Nota de Credito.

## Riscos

- A API key vai em query string por desenho da API oficial; nao deve ser logada em reverse proxies.
- O `sequence_id` tem de corresponder ao tipo de documento correto.
- O nome dos impostos (`IVA23`, `IVA13`, `IVA6`, `IVA0`) deve existir na conta InvoiceXpress.
- PDF pode demorar alguns segundos a ficar disponivel em contas reais; se acontecer, adicionar retry curto no client.
