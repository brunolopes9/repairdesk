# 94 — Reformulação do Mender (out/2026)

> **Porquê:** o Mender cresceu para dezenas de secções sobrepostas (Balcão ×3, Compras e Operação ×4, Catálogo & Stock ×4, Trabalhos, Reparações com muitas etapas, Preços, 3 relatórios com IVA mal calculado) e nunca foi usado no dia a dia. **Menos é mais.**
> Fonte de verdade do negócio: `Contexto/specs/SPEC_compras_stock_vendas_iva.md`, `Contexto/specs/SPEC_gestao_financeira.md` e os dois Excels na mesma pasta.

## Decisões do Bruno (2026-10-06)

| Tema | Decisão |
|---|---|
| Moloni / InvoiceXpress / faturação via API | **Eliminar código** por completo. Faturas de venda continuam a ser emitidas à mão no site do Moloni; o Mender regista o nº da fatura (+ PDF opcional). Integração de faturação volta mais tarde, passo a passo. |
| Loja online LopesTech shop | **Eliminar** (External API, checkout, ShopAi, imagens de estado, "mostrar na loja"). |
| Webhooks | **Eliminar**. |
| Manter | Agendamentos, Pedidos online, Tarefas, Campanhas, Auditoria, Automações (IA de faturas de compra + email ingest). |
| Dados em produção | Recomeçar limpo: manter Clientes, Fornecedores e utilizadores; backup antes; seed das compras a partir do Excel. |
| Email transacional | Resend com domínio `mender.pt`. |
| "Venda sem fatura" | **Nunca** implementar (risco fiscal — compras com IVA deduzido no NIF). |

"Remover" significa **apagar código e tabelas**, não esconder do menu.

## Estrutura alvo

Menu: Dashboard · **Vendas** · **Compras** (inclui Stock) · **Despesas** · Clientes · Agendamentos · Pedidos online · Tarefas · **IVA & Resultados** · Finanças pessoais (só dono) · Definições.

- **Compras** — documento de compra (fatura/encomenda) com linhas; cada linha é um **lote de stock** com custo, IVA de compra (do regime do fornecedor: Nacional 23% / UE autoliquidação 0%) e lucro desejado. Upload de PDF ou email → IA preenche → confirmar. Anti-duplicados por nº fatura/encomenda; documentos sem nº fatura (estado "fatura em falta").
- **Vendas** — um só ecrã para Reparação, Software/Website e Produto. Linhas: peça de um lote (preço sugerido pelo motor), serviço/mão de obra, produto. Reparação com 4 estados: Orçamento → À espera de peça → Pronta → Entregue & paga. Nº da fatura Moloni registado à mão. Snapshot de custo/IVA no momento da venda.
- **Despesas** — tudo o que não é para revenda; taxa de IVA, dedutível sim/não.
- **IVA & Resultados** — posição de IVA por trimestre (declaração periódica), "já vendido" vs "se vender o stock", balancete trimestral (vendas, IVA, custo, lucro bruto, contabilista, Seg. Social, lucro na conta), simulador de peça.
- **Finanças pessoais** — negócios configuráveis (com custo / só entradas; regime de IVA), despesas pessoais/gerais, fixas OK/EM FALTA, objetivo mensal, painel mensal. Permissão própria (só dono).

## Fases

| # | Fase | Estado |
|---|---|---|
| 1 | Login por email **ou** username + "Esqueci a palavra-passe" (Resend) | ✅ |
| 2 | Remover Moloni/faturação, loja, webhooks + migração de BD | ⏳ |
| 3 | Motor de IVA + Compras/Stock por lote + IA/email ligados ao modelo novo + seed Excel | ⏳ |
| 4 | Vendas unificadas (reparação simples) + limpeza Balcão/Trabalhos/Reparações/Preços/Catálogo | ⏳ |
| 5 | Despesas + IVA & Resultados + balancete trimestral | ⏳ |
| 6 | Finanças pessoais + import `Gestao_Financeira_v10.xlsx` | ⏳ |

Cada fase é deployável sozinha. O motor de IVA é validado pelos testes de aceitação do SPEC §6 (382,29 € IVA a pagar; 519 € lucro; −132,55 € saldo de IVA).

## Fase 1 — notas

- `POST /api/auth/login` aceita `{ login, password }` (email ou username); `{ email }` continua aceite para PWAs em cache.
- Username definido pelo próprio em *O meu perfil*: 3–32 caracteres `[a-z0-9._-]`, sem `@` (nunca colide com emails). Vazio = só email.
- `POST /api/auth/forgot-password` → resposta sempre 202 (não revela contas); email com link válido 1h.
- `POST /api/auth/reset-password` → muda a passe, desbloqueia a conta e revoga todas as sessões.
- Rate limit próprio `auth-reset` (5/15 min por IP), separado do login.
- **Produção:** definir `RESEND_API_KEY` no `.env.production` e verificar `mender.pt` no Resend (registos DNS SPF/DKIM). Sem key, o pedido responde 202 mas o email não sai (aviso no log).
