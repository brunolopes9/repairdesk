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

Menu: Dashboard · **Vendas** · **Compras** (inclui Stock) · **Despesas** · Clientes · Agendamentos · Pedidos online · Tarefas · **IVA & Resultados** · Finanças (só dono) · Definições.

- **Compras** — documento de compra (fatura/encomenda) com linhas; cada linha é um **lote de stock** com custo, IVA de compra (do regime do fornecedor: Nacional 23% / UE autoliquidação 0%) e lucro desejado. Upload de PDF ou email → IA preenche → confirmar. Anti-duplicados por nº fatura/encomenda; documentos sem nº fatura (estado "fatura em falta").
- **Vendas** — um só ecrã para Reparação, Software/Website e Produto. Linhas: peça de um lote (preço sugerido pelo motor), serviço/mão de obra, produto. Reparação com 4 estados: Orçamento → À espera de peça → Pronta → Entregue & paga. Nº da fatura Moloni registado à mão. Snapshot de custo/IVA no momento da venda.
- **Despesas** — tudo o que não é para revenda; taxa de IVA, dedutível sim/não.
- **IVA & Resultados** — posição de IVA por trimestre (declaração periódica), "já vendido" vs "se vender o stock", balancete trimestral (vendas, IVA, custo, lucro bruto, contabilista, Seg. Social, lucro na conta), simulador de peça.
- **Atividades** — cada tenant cria as suas atividades (no caso do Bruno: *Informática* — CAE 62100/95102/47401 — e *Trading* — CIRS 1519). Cada venda e cada despesa pertence a uma atividade. Nada é hardcoded: outra loja pode ter só uma atividade.
- **Finanças** (só dono) — resultado por atividade, despesas pessoais/gerais, fixas OK/EM FALTA, objetivo mensal, painel mensal e "quanto guardar" para Segurança Social e IRS. Permissão própria.

## Fases

| # | Fase | Estado |
|---|---|---|
| 1 | Login por email **ou** username + "Esqueci a palavra-passe" (Resend) | ✅ |
| 2 | Remover Moloni/faturação, loja, webhooks + migração de BD | ⏳ |
| 3 | Motor de IVA + Compras/Stock por lote + IA/email ligados ao modelo novo + seed Excel | ⏳ |
| 4 | Vendas unificadas (reparação simples) + limpeza Balcão/Trabalhos/Reparações/Preços/Catálogo | ⏳ |
| 5 | Despesas + IVA & Resultados + balancete trimestral | ⏳ |
| 6 | Atividades + Finanças + import `Gestao_Financeira_v10.xlsx` (só Informática, Trading, despesas e fixas) | ⏳ |

Cada fase é deployável sozinha. O motor de IVA é validado pelos testes de aceitação do SPEC §6 (382,29 € IVA a pagar; 519 € lucro; −132,55 € saldo de IVA).

## Fase 1 — notas

- `POST /api/auth/login` aceita `{ login, password }` (email ou username); `{ email }` continua aceite para PWAs em cache.
- Username definido pelo próprio em *O meu perfil*: 3–32 caracteres `[a-z0-9._-]`, sem `@` (nunca colide com emails). Vazio = só email.
- `POST /api/auth/forgot-password` → resposta sempre 202 (não revela contas); email com link válido 1h.
- `POST /api/auth/reset-password` → muda a passe, desbloqueia a conta e revoga todas as sessões.
- Rate limit próprio `auth-reset` (5/15 min por IP), separado do login.
- **Produção:** definir `RESEND_API_KEY` no `.env.production` e verificar `mender.pt` no Resend (registos DNS SPF/DKIM). Sem key, o pedido responde 202 mas o email não sai (aviso no log).

## Atividades e IVA por tipo de operação (decisão 2026-10-06)

O Bruno é trabalhador independente (Categoria B, regime simplificado, IVA normal trimestral) com duas atividades: **Informática** (reparações, websites, venda de peças) e **Trading** (payouts de prop firms). Fonte: relatório fiscal do Bruno de 04-10-2026 (não versionado, tem dados pessoais).

**Camisolas ficam fora do Mender.** O Mender só regista atividade declarada. O import do Excel de gestão ignora a folha Camisolas.

O tratamento de IVA de uma linha de venda depende de **o que se vende** (bem vs serviço) e **a quem** (país e tipo do cliente), não da atividade:

| Operação | IVA | Declaração periódica |
|---|---|---|
| Bem ou serviço a cliente em PT | 23% | liquidado normal |
| Serviço B2B a empresa da UE com VAT válido (ex.: FTMO, CZ) | 0% · M40 "IVA – autoliquidação" | campo 7 + declaração recapitulativa |
| Serviço B2B a empresa fora da UE (ex.: 5ers IL, FundingPips) | 0% · M40 | campo 8 |
| Compra de serviço/bem à UE ou fora (challenges, peças UE) | autoliquidação 23% liquidado = 23% deduzido | neutro, mas declarado |

Consequências no modelo:
- **Cliente** ganha país + tipo (particular/empresa) + VAT; o Mender sugere o tratamento de IVA e o motivo (M40) na venda.
- **Linha de venda** distingue *bem* (peça, produto) de *serviço* (mão de obra, website, payout). Relevante para o IRS (coeficiente 0,15 bens / 0,35 serviços) e Segurança Social (20% bens / 70% serviços da base).
- **Despesas** ganham regime de IVA como os fornecedores (nacional / UE autoliquidação / fora UE autoliquidação).
- Payouts em USD: guarda-se o valor em euros recebido + moeda/valor original como nota.
- "Quanto guardar" (Fase 6): Segurança Social = 21,4% × (70% serviços + 20% bens) × (1 − ajuste configurável, ex. 25%); IRS = estimativa por coeficiente, % configurável. Sempre marcado como estimativa — a contabilista faz as contas finais.
