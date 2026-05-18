# RepairDesk + Moloni — Setup Path A

Data: 2026-05-18

## Decisão

Path A fiscal: RepairDesk não é software certificado AT por si. Cada tenant liga a sua própria conta Moloni e o RepairDesk chama a API Moloni em nome do tenant para emitir documentos legais.

Docs oficiais usadas:
- Autenticação OAuth2: https://www.moloni.pt/dev/autenticacao/
- Uso da API com `json=true`: https://www.moloni.pt/dev/utilizacao/
- Faturas: https://www.moloni.pt/dev/documents/invoices/insert/
- Faturas simplificadas: https://www.moloni.pt/dev/documents/simplified-invoices/insert/
- PDF assinado: https://www.moloni.pt/dev/documents/documents/getpdflink/
- Empresas: https://www.moloni.pt/dev/company/company/getone/

## Como criar conta Moloni

1. Criar conta Moloni e empresa.
2. Activar ambiente sandbox para testes.
3. Criar/confirmar:
   - Série de documentos.
   - Produto/serviço genérico, por exemplo `Serviço de reparação`.
   - Taxa IVA usada pela loja.
   - Método de pagamento.
   - Cliente fallback, por exemplo `Consumidor final`.
4. O plano gratuito foi considerado como referência de 50 documentos/mês, mas antes de beta deve ser confirmado no site Moloni: `{{TODO confirmar plano gratuito actual}}`.

## Configuração no RepairDesk

Em `Definições > Faturação`:

- Provider: `Moloni`.
- Modo sandbox: ligado enquanto testas.
- Access token/API key: para MVP, colar o `access_token` da API Moloni sandbox.
- Company ID: ID da empresa Moloni.
- Tipo documento: `Fatura simplificada` ou `Fatura`.
- Série Moloni: usar `Sincronizar séries` para listar e guardar a série.
- Produto/serviço ID: ID do produto/serviço usado nas linhas das faturas.
- Tax ID IVA: ID da taxa IVA Moloni.
- Método pagamento ID: obrigatório para faturas simplificadas.
- Cliente fallback ID: usado quando o cliente não tem NIF ou não existe na Moloni.
- Motivo isenção: por exemplo `M02` para Art. 53, confirmar com contabilista.

## Como testar conexão

1. Guardar as definições.
2. Clicar `Testar conexão`.
3. O RepairDesk chama `companies/getOne` com `company_id`.
4. Se a Moloni devolver erro, o operador vê a mensagem no toast/API ProblemDetails.

## Como emitir

1. Marcar Reparação ou Trabalho como `Pago`.
2. Abrir o detalhe.
3. Clicar `Emitir fatura via Moloni`.
4. O RepairDesk:
   - valida que está pago;
   - encontra cliente por NIF na Moloni quando possível;
   - usa `FallbackCustomerId` se não houver cliente Moloni;
   - cria a fatura;
   - pede link PDF assinado;
   - grava `InvoiceProvider`, `InvoiceExternalId`, `InvoiceNumber`, `InvoicePdfUrl`, `InvoiceEmittedAt`.

## Limites e upgrade

- Confirmar mensalmente os limites comerciais Moloni antes de vender a beta.
- Quando a oficina ultrapassar o limite gratuito, o upgrade acontece no Moloni, não no RepairDesk.
- O RepairDesk deve mostrar erro claro quando a Moloni responder com limite/plano insuficiente.

## Notas técnicas

- Segredos ficam cifrados at-rest via `IDataProtector`.
- O endpoint é idempotente: se a fatura já existe, devolve a mesma fatura sem emitir duplicado.
- `MOLONI_SANDBOX=true` força endpoint sandbox.
- Produção usa `https://api.moloni.pt/v1`.
- Sandbox usa `https://api-sandbox.moloni.pt/v1`.

## Riscos / próximos passos

1. OAuth2 completo: a API clássica Moloni usa access/refresh tokens. MVP aceita access token manual; antes de produção deve haver flow de refresh automático.
2. Cliente fiscal completo: RepairDesk hoje só tem nome/telefone/email/NIF, sem morada. Para clientes empresariais pode ser melhor sincronizar/criar cliente Moloni com morada completa.
3. IDs internos Moloni: produto, taxa, método pagamento e cliente fallback são obrigatórios para emissão fiável. Melhorar com botões de sincronização dedicados.
4. Certificação: a legalidade do documento depende da Moloni e da conta do tenant estar bem configurada.
