# RepairDesk + Moloni — Troubleshooting

Guia de erros comuns na integração Moloni e como resolvê-los. Útil para suporte aos tenants do beta.

---

## 1. Erro 503 Service Unavailable ao ligar Moloni

**Sintoma:** Modal "Ligar Moloni" devolve toast "Moloni rejeitou as credenciais (HTTP 503)" ou similar.

**Causa real:** A **sandbox Moloni (`api-sandbox.moloni.pt`)** está temporariamente indisponível.
Não é problema das tuas credenciais.

**Fix:**
1. Em `Definições → Faturação → 1. Registo da aplicação`
2. **Desmarca "Modo sandbox"**
3. Clica `Guardar credenciais`
4. Clica `Ligar Moloni` outra vez
5. Mete email + password Moloni → liga

Razão: estás a usar conta paga Flex, a sandbox não te dá nada que a produção não dê.
A sandbox só é útil para developers Moloni sem conta paga.

---

## 2. Erro 422 com "Moloni rejeitou as credenciais (HTTP 400)"

**Sintoma:** Toast "moloni_connect_failed" com erro HTTP 400 da Moloni.

**Causas possíveis:**
- Developer ID errado
- Client Secret errado
- Email/password Moloni errados
- Conta Moloni sem permissão para API (plano Solo ou Base)

**Fix:**
1. Confirma o **Developer ID** no painel Moloni → `Configurações → Developers → Configuração da API`
2. Se o Client Secret no RepairDesk estiver com placeholder `••••`, **recola** o Secret real
3. Tenta fazer login no `moloni.pt` com o email/password que estás a usar — confirma que funciona
4. Verifica o plano em `moloni.pt → Conta → Subscrição` — tem de ser **Flex (€10.90/mês)** ou superior

---

## 3. Conta sem acesso à API (plano insuficiente)

**Sintoma:** Após `Ligar Moloni`, alguma chamada devolve `permission_denied` ou similar.

**Causa:** A API Moloni só está disponível em planos **Flex (€10.90/mês)** e **Pro (€15.90/mês)**.
Planos Solo (€3.50) e Base (€6.49) **NÃO têm API**.

**Fix:**
1. Em `moloni.pt → Conta → Subscrição`
2. Faz upgrade para Flex (modalidade anual: €130/ano ≈ €10.83/mês)
3. Volta a `Ligar Moloni` no RepairDesk

---

## 4. Refresh token expirou (>14 dias sem actividade)

**Sintoma:** Toast "Refresh token Moloni rejeitado. Re-autentica em Definições → Faturação."

**Causa:** O `refresh_token` da Moloni tem validade de 14 dias. Se passarem 14 dias sem
nenhuma chamada à API (cada chamada renova o refresh), expira.

**Fix:**
1. Em `Definições → Faturação`
2. Secção 2: clica `Desligar` (limpa tokens expirados)
3. Clica `Ligar Moloni` outra vez → modal email/password
4. Pronto, ligado por mais 14 dias

**Como evitar:** Emitir pelo menos 1 fatura por mês mantém o refresh sempre fresco
(cada chamada à API renova ambos access_token e refresh_token).

---

## 5. "Sem séries" ao Sincronizar séries

**Sintoma:** Botão `Sincronizar` na secção 3 devolve `Sem séries disponíveis.`.

**Causa:** A conta Moloni ainda não comunicou nenhuma série à AT.

**Fix:** No painel Moloni:
1. `Configurações → Empresa → 5. Séries e n.º de cópias`
2. Confirma que existe pelo menos uma série activa (default: `M`)
3. Se não houver, `Tabelas → Séries → Nova série`
4. Voltar ao RepairDesk e clicar `Sincronizar` novamente

---

## 6. Erro "moloni_company_missing" ao emitir fatura

**Sintoma:** Toast "Configura o CompanyId da Moloni." ao clicar Emitir fatura.

**Causa:** O campo `Company ID` na secção 3 está vazio.

**Fix:**
1. No painel Moloni, abre a tua empresa. O ID está na URL (ex: `https://moloni.pt/companies/12345`)
2. Cola `12345` no campo `Company ID`
3. Clica `Guardar configuração`

**Alternativa:** Após `Ligar Moloni`, o RepairDesk tenta auto-descobrir e auto-selecciona
o `Company ID` se só houver 1 empresa na conta. Se tens várias empresas, escolhes manualmente.

---

## 7. Erro "moloni_product_missing" ao emitir fatura

**Sintoma:** Toast "Configura o produto/servico Moloni por defeito."

**Causa:** O `Produto/serviço ID` na secção avançada (3) está vazio. As linhas das faturas
Moloni precisam de referenciar um "artigo" existente no Moloni.

**Fix:**
1. No painel Moloni: `Tabelas → Artigos → Novo Artigo`
2. Cria `Serviço de reparação` (tipo: Serviço, IVA: 23% se regime normal)
3. Após salvar, abre o artigo — o ID está na URL
4. Cola o ID em `Definições → Faturação → secção 3 (avançada) → Produto/serviço ID`

**Auto-discovery (futuro #C14):** quando o Sprint 45 do Codex aterrar, vai existir botão
"Auto-configurar tudo" que cria automaticamente este produto se não existir.

---

## 8. Fatura emitida não aparece no portal e-Fatura

**Sintoma:** Bruno (ou o cliente) confere o e-Fatura ao fim do dia e não vê a fatura.

**Causa possível 1:** Comunicação AT em diferimento configurado para >0 dias.
**Fix:** No painel Moloni → `Configurações → Empresa → Comunicação AT`, definir `Diferimento = 0 dias`.

**Causa possível 2:** Subutilizador AT sem permissão WSE (Webservice).
**Fix:** No Portal das Finanças → Gestão de Utilizadores → editar o subutilizador `263758141/1` → activar permissão `WSE — Comunicação de documentos`.

**Causa possível 3:** Houve falha temporária do webservice AT. A Moloni reagenda.
**Fix:** Painel Moloni → `A. Tributária → Comunicação automática → Estado dos documentos`.

---

## 9. Cliente final não recebe a fatura (PDF)

**Sintoma:** Cliente diz que não chegou email com a fatura.

**Por desenho actual:** O RepairDesk **não envia** automaticamente a fatura PDF por email.
Guarda o `InvoicePdfUrl` em DB e o operador pode partilhar manualmente (link assinado Moloni).

**Workaround MVP:**
- Após emitir, abrir detalhe da reparação
- Clicar `Ver fatura` → abre PDF Moloni
- Copiar link e enviar por WhatsApp/email

**Roadmap:** Sprint futuro adiciona envio automático por email do tenant
(usando provider de email configurado).

---

## 10. Erro de IVA: "M02 incompatível com taxa não-zero"

**Sintoma:** Moloni rejeita fatura com erro a indicar incompatibilidade do código de isenção.

**Causa:** Tens `Motivo isenção = M02` (Art. 53) **mas** o produto tem IVA 23%.
Os 2 não podem coexistir.

**Fix:**
- Se tens regime **normal IVA**: deixa **Motivo isenção vazio** e usa Tax ID com IVA 23%
- Se tens regime **isenção art. 53**: deixa Tax ID vazio (ou IVA 0%) e mete `M02` no motivo

Em `Definições → Empresa → Regime fiscal` confirma qual estás. O RepairDesk esconde
automaticamente o campo "Motivo isenção" quando o regime é Normal IVA.

---

## 11. CORS warning nos logs (não bloqueante)

**Sintoma:** Logs do api mostram `CORS policy execution failed. Request origin http://localhost does not have permission`.

**Causa:** O frontend é servido por nginx (porta 80) e o api responde a `Authorization` headers
sem necessitar de preflight CORS, mas o middleware loga sempre warning.

**Impacto:** Nenhum (request continua). É só ruído no log.

**Fix opcional:** Adicionar `localhost` à CORS allowlist em `Program.cs`. Sprint baixa prioridade.

---

## 12. Como confirmar que o refresh automático está a funcionar

**Verificação manual:**
1. Liga Moloni às 10:00
2. Não faças nada relacionado com Moloni durante 1h05min
3. Às 11:05, emite uma fatura de teste
4. **Espera-se:** sucesso (o token novo é obtido automaticamente)
5. Confere logs api: deve aparecer linha `Moloni access token refreshed for tenant ...`

Se aparecer `Refresh token Moloni rejeitado (HTTP 400): Invalid refresh token`:
- Algum processo externo invalidou o refresh (mudaste password Moloni? regeneraste Client Secret?)
- Re-liga Moloni (Sprint 42 modal)

---

## Tabela rápida de status HTTP

| HTTP | Significado | Acção |
|---|---|---|
| 200 | OK | Nada |
| 400 + `error: invalid_token` | Access token expirou | Auto-refresh trata |
| 400 + `error: invalid_grant` | Refresh token expirou ou credenciais erradas | Re-ligar Moloni |
| 400 + `error: invalid_client` | Developer ID ou Client Secret errados | Confirmar no painel |
| 401 Unauthorized | Token rejeitado | Auto-refresh trata |
| 422 (do RepairDesk) | Validação interna falhou | Ler mensagem do erro |
| 500/503 (da Moloni) | Servidor Moloni em baixo | Esperar / desligar sandbox |
| 502 (do nginx) | API container não está a correr | `docker compose up -d api` |
