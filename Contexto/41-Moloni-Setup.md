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
4. **Plano necessário — CONFIRMADO em 2026-05-18 na página oficial de planos:**
   - Solo (3,50€/mês) — ❌ SEM API
   - Base (6,49€/mês) — ❌ SEM API
   - **Flex (10,90€/mês + IVA = ~13,41€/mês) — primeiro plano com API e e-commerce** ✅
   - Pro (15,90€/mês + IVA) — ✅ tem API
   - Trial: 30 dias grátis com qualquer plano.
   - **Não existe plano gratuito perpétuo com API.** O "free tier 50 docs/mês" mencionado em documentação antiga é trial, não permanente.

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

- Confirmado em 2026-05-18: integração RepairDesk ↔ Moloni exige plano Flex (~13€/mês com IVA) ou superior.
- Para tenants em plano Solo/Base sem API: o RepairDesk deve degradar graciosamente — mostrar mensagem "Faturação automática requer plano Moloni Flex ou superior. Emite manualmente no painel Moloni e regista o número aqui."
- Adicionar campo `InvoiceNumberManual` na entidade Reparacao/Trabalho para permitir registo de fatura emitida manualmente.
- O RepairDesk deve mostrar erro claro quando a Moloni responder com erro de permissão/plano insuficiente.

## Alternativas a considerar (Sprint futuro)

Se preço Moloni Flex for barreira para tenants pequenos:
- **InvoiceXpress** — também certificado AT, verificar tabela de planos com API.
- **Vendus** — provider PT certificado, planos a partir de ~€8/mês.
- **Faturalo** — opções low-cost.
- Estratégia ideal: RepairDesk suporta `IBillingProvider` multi-implementação (já está abstraído via `BillingProvider` enum no Sprint 40 do Codex). Adicionar `InvoiceXpressBillingProvider` etc. permite ao tenant escolher.

## Notas técnicas

- Segredos ficam cifrados at-rest via `IDataProtector`.
- O endpoint é idempotente: se a fatura já existe, devolve a mesma fatura sem emitir duplicado.
- `MOLONI_SANDBOX=true` força endpoint sandbox.
- Produção usa `https://api.moloni.pt/v1`.
- Sandbox usa `https://api-sandbox.moloni.pt/v1`.

## Refresh automático de tokens (Sprint 41)

Implementado em 2026-05-18. O `MoloniClient` agora tenta a chamada com o access token actual; se a Moloni rejeitar com erro `invalid_token` (ou HTTP 401), faz refresh automático via `/v1/grant/?grant_type=refresh_token`, actualiza `ApiKeyCipherText` e `RefreshTokenCipherText` cifrados em DB, e tenta uma vez. Resultado: o operador só precisa de colar o conjunto inicial (Developer ID, Client Secret, Access Token, Refresh Token) e o sistema mantém-se autenticado durante os 14 dias de validade do refresh token. Quando o refresh também expirar, mostra erro claro pedindo re-autenticação.

## Riscos / próximos passos

1. OAuth2 completo (authorization_code flow): para SaaS multi-tenant, ideal seria um botão "Ligar Moloni" que redirecciona o utilizador para o Moloni autorizar — sem partilhar password com o RepairDesk. Implementar quando primeiro tenant externo aparecer.
2. Cliente fiscal completo: RepairDesk hoje só tem nome/telefone/email/NIF, sem morada. Para clientes empresariais pode ser melhor sincronizar/criar cliente Moloni com morada completa.
3. IDs internos Moloni: produto, taxa, método pagamento e cliente fallback são obrigatórios para emissão fiável. Melhorar com botões de sincronização dedicados.
4. Certificação: a legalidade do documento depende da Moloni e da conta do tenant estar bem configurada.
