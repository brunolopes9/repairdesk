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
| 2 | Remover Moloni/faturação, loja (incl. Produtos/Catálogo e Molano), webhooks + migração de BD | ✅ |
| 3 | Motor de IVA + Compras/Stock por lote + IA/email ligados ao modelo novo + import do Excel (Sprint 556). *Perfil fiscal passou para a Fase 6, junto das Atividades.* | ✅ |
| 4 | Vendas unificadas (reparação simples) + limpeza Balcão/Trabalhos/Reparações/Preços/Catálogo. 4a ✅ (Sprint 557: Vendas novas, Balcão removido da UI); 4b ✅ (Sprint 558: caixa, Preços, Contagens, Kits, catálogo de Serviços e Trabalhos apagados); 4c ✅ (Sprint 559: Reparações antigas e Peças apagadas; extras passam para a Venda) | ✅ |
| 5 | Despesas + IVA & Resultados + balancete trimestral | ⏳ |
| 7 | Motor IRS + Segurança Social completo, recomendações por regras e agente no site (só explica o que o motor calcula) | ⏳ |
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

## Módulo fiscal (decisão 2026-10-06)

Objetivo: o Mender sabe o enquadramento de cada utilizador e calcula **IVA, IRS e Segurança Social**, avisa prazos e dá recomendações — sem substituir o contabilista.

### Princípios (obrigatórios)
1. **Números por código determinístico e testado, nunca pelo LLM.** O agente do site só chama o motor e explica o resultado, citando o artigo.
2. **Regras fiscais em dados versionados por ano** (`Fiscal/Rules/2026.json`): taxas de IVA, escalões de IRS, coeficientes do art. 31.º, dedução específica, IRS Jovem, taxas e bases da SS, limites (art. 53.º, regime simplificado), prazos. Cada valor tem artigo e link oficial. Mudar de ano = novo ficheiro; testes do ano anterior continuam verdes.
3. Casos duvidosos marcados **"confirmar com contabilista"** (ex.: coeficiente do CIRS 1519 — 0,35 vs 0,75).
4. Todo o resultado mostra "estimativa — não substitui o contabilista".
5. **Nunca recomendar gastar dinheiro para pagar menos imposto.** Recomendar só pedir fatura com NIF do que já se compra.

### Perfil fiscal (setup do tenant)
Upload do PDF da declaração de início/alteração de atividade → IA extrai os campos → utilizador confirma cada um (ou preenche à mão):
categoria (B), data de início, códigos CAE/CIRS (principal/secundários + datas), regime de IRS (simplificado/organizada), regime de IVA (isento art. 53.º / normal mensal / trimestral + data), operações intracomunitárias (bens / serviços), NIF ativo no VIES, IRS Jovem (ano de benefício), SS (isenção 1.º ano, ajuste ±25%), retenção na fonte.
As **atividades** do tenant são criadas a partir dos códigos do perfil (ex.: Informática = 62100/95102/47401; Trading = 1519).

### Classificação de cada documento (IVA)
| Situação | Tratamento |
|---|---|
| Venda em PT | IVA liquidado (23/13/6%) |
| Serviço a empresa UE com VAT válido | 0% · M40 · campo 7 + recapitulativa |
| Serviço a cliente fora da UE | 0% · M40 · campo 8 |
| Compra PT com NIF | IVA dedutível |
| Compra UE (bens ou serviços) com o VAT ID do utilizador | autoliquidação: liquida e deduz o mesmo valor (neutro) |
| Compra B2C / IVA OSS / fatura sem NIF | IVA **não** dedutível e não conta para a regra dos 15% |
| Moeda estrangeira | câmbio BCE da data da operação (API do BCE) |
| Exigibilidade (arts. 7.º–8.º CIVA) | a data da operação decide o período, não a data da fatura |

Correção a não esquecer: comprar peças na UE sem IVA **não** dá vantagem — a autoliquidação anula-se; na venda cobra-se 23% sobre o preço todo (SPEC §3.2).

### Cálculos
- **IRS (simplificado, art. 31.º CIRS):** rendimento × coeficiente por tipo (0,15 bens / 0,35 serviços / outros nos dados), redução 1.º/2.º ano (n.º 10) se aplicável, regra dos 15% (n.º 13: dedução específica + SS + despesas com NIF) com "faltam X € → custa ~Y € de IRS", IRS Jovem (art. 2.º-B), escalões do ano.
- **Segurança Social:** rendimento relevante trimestral = 70% serviços + 20% bens, ajuste ±25%, 21,4%, mínimo, isenção 12 meses, prazos (declaração trimestral, pagamento dia 10–20).
- **"Quanto guardar"** de cada recebimento para IVA, IRS e SS.

### Recomendações (motor de regras) e agente
Exemplos de regras: fatura estrangeira sem o VAT ID → pedir nova; perto do limite do art. 53.º ou do regime simplificado; faltam X € para a regra dos 15%; prazos (DP dia 20 do 2.º mês após o trimestre, recapitulativa dia 20, SS trimestral); venda a cliente UE sem M40; câmbio não registado.
O agente responde com base nos resultados do motor; sem regra para o caso → diz que não sabe e sugere o contabilista.

### Testes de referência (anonimizados)
Serviço a empresa checa: 269,70 USD ao câmbio BCE 1,1605 = 232,40 € · M40 · campo 7 + recapitulativa. Website em PT: 300 € + 69 € IVA. Compra Amazon com IVA OSS: não dedutível. Balancete e motor de IVA: testes dos SPECs.

## Fase 2 — notas (Sprint 555)

Removido (código + tabelas): Moloni/InvoiceXpress (emissão, anulação, recibos, orçamentos, OAuth, definições de faturação), Documentos ("Vendas · Faturas"), Avenças, Extrato e relatório de IVA (refeitos na Fase 5), tesouraria do Dashboard, API externa da loja + checkout + IA da loja + imagens de estado, Produtos/Modelos/Catálogo (telemóveis retail/dropshipping Molano), Webhooks (+ cron de garantias expiradas), flag "mostrar na loja" das peças, cron de faturas em dívida, preferência "emitir fatura no POS".

Mantido: `InvoiceNumber` + `InvoiceEmittedAt` em Reparação/Trabalho/Venda (registo manual do nº da fatura passada fora do Mender); lista "pagas sem fatura" passou a lembrete só de leitura; PDF de orçamento do Mender voltou aos Trabalhos.

Migração `Sprint555RemoveFaturacaoLojaWebhooks`: 9 tabelas + colunas Invoice*/Estimate*/Recibo*. Novo `PreMigrationBackup`: com `Backup:Enabled=true`, faz backup antes de aplicar migrações pendentes e aborta o arranque se o backup falhar.

## Fase 3 — notas (Sprint 556)

- **Fornecedor** passa a ter `RegimeIva` (Nacional / UE autoliquidação / Fora UE) e `Pais`. A migração
  copia o antigo `IntraUe` para `RegimeIva = UE` antes de apagar a coluna.
- **Compras** = `CompraDocumento` (fatura ou encomenda) + `CompraLinha` (= lote de stock). Guarda-se só o
  introduzido (quantidade, preço pago c/ IVA, taxa, lucro); tudo o resto vem do `IvaEngine`
  (SPEC compras §3, testado contra o dataset do Excel ao cêntimo: SPEC §6).
- Taxa de IVA na compra por defeito: 23% nacional, 0% UE. Lucro por defeito: 1 € películas/vidros, 5 € resto.
- **Anti-duplicados**: mesmo fornecedor + mesmo nº de fatura (maiúsculas indiferentes) ou encomenda
  repetida → 409 `compra_duplicada`; a UI mostra o aviso e permite "Gravar mesmo assim".
- **Reconciliação**: linhas + portes vs total impresso (tolerância 0,01 €); "fatura em falta" quando
  só há nº de encomenda — sem fatura o IVA não é dedutível.
- **Faturas recebidas (IA, upload/email)**: deixam de criar `Part`s. Botão **Compra** abre o editor já
  preenchido (linhas de portes → campo Portes; preço unitário = total da linha c/ IVA ÷ qtd) e grava via
  `POST /api/compras/de-fatura/{importId}`, que fecha a importação na mesma transação. Botão **Despesa**
  mantém-se para serviços/ferramentas/contas. Sem aprovação automática: a IA pode errar valores.
- **Import do Excel** (`POST /api/compras/importar-excel`, Admin): folhas Fornecedores/Faturas/Compras,
  idempotente. "Faturado = Sim" não marca venda — as vendas passam a ser registadas nas Vendas (Fase 4).
- Ainda por fazer na Fase 4: vendas a consumir lotes (snapshot do custo/IVA), remover `Part`/stock antigo,
  "Compras aprovadas" antigas (despesas de categoria stock) deixam de aparecer em Compras.

## Fase 4a — notas (Sprint 557)

- **Venda** evolui (não há entidade paralela): `Tipo` (Produto / Reparação / Serviço), `Estado` (Orçamento →
  À espera de peça → Pronta → Entregue & paga; Cancelada), `Equipamento` e `Problema` para reparações.
  Migração: `Status` → `Estado` (Pendente→Pronta, Paga→Entregue, Cancelada→Cancelada); `Origem` apagada;
  vendas antigas ficam `Tipo = Produto`.
- **Linhas**: serviço/mão de obra (IVA sobre o valor todo) ou unidade(s) de um **lote** (`CompraLinhaId`).
  Snapshot do custo pago e da taxa de IVA da compra na linha; refrescado ao passar a Entregue (SPEC §2.4).
  Contas pelo `IvaEngine.VendaPorTotal` (SPEC §3.3, inclui desconto).
- **Stock**: Orçamento não mexe; À espera de peça / Pronta / Entregue consomem (`QuantidadeVendida`);
  Cancelada devolve; editar liberta e volta a consumir na mesma transação. Entregue/Cancelada não se editam.
- **Fatura**: decisão do Bruno — avisar, não bloquear. Entregue sem nº → "Fatura por registar" (vista própria
  nas Vendas). Registo do nº via `PUT /api/vendas/{id}/fatura`. Cancelar uma venda entregue lembra a nota de crédito.
- Garantia legal automática (DL 84/2021) só em vendas de **Produto**.
- Menu novo: Dashboard · Vendas · Compras · Despesas · Clientes · Agendamentos · Pedidos online · Tarefas ·
  Relatórios · Auditoria · Definições. Balcão (POS + caixa + fecho Z) removido do frontend.
- Por decidir com o Bruno antes da 4c: que extras das Reparações antigas ficam (PDF entrada/entrega com
  assinatura, portal do cliente, fotos, garantias de reparação).

## Fase 4b — notas (Sprint 558)

Apagado (código + tabelas, migração `Sprint558RemoveCaixaPrecosTrabalhos`): caixa do Balcão (`CashMovements`,
`DailyClosings`, Z-reports), tabela de Preços (`PriceTableEntries`), Contagens físicas (`StockTakes`), Kits de
peças (`PartKits`), catálogo de Serviços (`ServiceItems`) e **Trabalhos** (software/websites passam a ser
Vendas do tipo Serviço). `Despesa.TrabalhoId` apagado; `RepairRequest.TrabalhoId` → `VendaId`
("Converter em orçamento" nos Pedidos online cria uma Venda de reparação em Orçamento).
O PDF de orçamento passou a ser da Venda (`GET /api/vendas/{id}/orcamento.pdf`). O Dashboard e o relatório
de negócio antigos deixaram de somar Trabalhos (contadores a 0) — serão refeitos na Fase 5.
Ficam para a 4c: `Part`/`PartMovimento`/SkuMapping e as Reparações antigas (dependem umas das outras).

## Fase 4c — notas (Sprint 559)

Decisão do Bruno (2026-10-07): apagar as Reparações antigas; **ficam** o PDF de entrada/entrega com assinatura,
o portal do cliente, as fotos e as garantias — agora ligados à **Venda do tipo Reparação**.

- Venda ganha `PublicSlug` (portal `/r/{slug}`, gerado nas reparações), `PrevistoPara` (ETA no portal e no
  calendário) e histórico `VendaEstadoLog` (linha temporal do portal). Novo estado **Em curso** (`EmCurso = 5`):
  é o estado de um orçamento aceite (no balcão ou pelo cliente no portal); a ordem do fluxo é
  Orçamento → Em curso → À espera de peça → Pronta → Entregue & paga.
- Renomeados e religados à Venda: `VendaFoto`, `VendaAssinatura`, `VendaComunicacao`; `Avaliacao`,
  `PushSubscription` e `InternalTask` passam a `VendaId`; `Garantia` só tem `VendaId` (`SourceType` distingue
  produto de reparação); `Payment` perde `ReparacaoId`.
- Garantia de reparação emitida ao entregar (dias/cobertura do tenant); garantia de produto como antes (DL 84/2021).
- Portal: aceitar o orçamento chama `VendaService.MudarEstado(EmCurso)` — consome as peças como no balcão;
  recusar cancela. MB Way pelo portal fica ligado à Venda (o pagamento confirmado avisa a loja; "Entregue & paga"
  continua a ser marcado ao levantar).
- PDFs na Venda: `orcamento.pdf`, `entrada.pdf`, `entrega.pdf`, `label.pdf` (QR do portal).
- Apagado: `Reparacao`, `ReparacaoEstadoLog`, etiquetas e cronómetro da reparação, `SignatureCapture` (duplicava a
  assinatura S551), diagnóstico guiado, campos de equipamento, `Part`/`PartMovimento`/`SkuMapping`,
  relatórios antigos (Negócio/Produtividade — substituídos por "IVA & Resultados" na Fase 5), Dashboard antigo e
  lembretes "reparações paradas"/"clientes por notificar". Despesas deixam de se ligar a reparações.
- Dashboard novo (`GET /api/dashboard`): faturado do mês, IVA a entregar e lucro (motor de IVA), despesas do mês,
  em curso por estado, alertas (faturas por registar, compras sem fatura/com diferença, faturas recebidas por
  aprovar), stock por lote e próximas entregas.
- Migração `Sprint559ReparacoesComoVendas`: limpa antes de mudar as FKs (avaliações e subscrições push das
  reparações antigas, garantias/pagamentos só de reparações, tarefas ficam sem ligação) — "recomeçar limpo",
  com backup automático antes.
- Preferências: modelos WhatsApp/push passam a usar os nomes dos estados da Venda; nomes antigos gravados são
  convertidos ao ler (`TenantPreferencesDefaults.EstadoAliases`).

## Faturas recebidas ↔ compras — notas (Sprint 560)

Problema: as compras importadas do Excel pela encomenda ficam com "fatura em falta"; quando a fatura chega,
"Faturas recebidas → Compra" criava uma compra **nova** (duplicado de stock). E o leitor antigo da Tudo4Mobile
(feito para o resumo de encomenda do site) lia as faturas CloudInvoice como lixo com confiança "alta" — a IA
nem era chamada.

- **Leitores determinísticos** (`InvoiceLayoutParsers`): faturas CloudInvoice (Tudo4Mobile e outros PT — nº,
  data de emissão, total, linhas c/ IVA, portes) e resumos de encomenda MobileSentrix (nº de encomenda, data,
  total, linhas multi-linha, envio). Só dão confiança alta se as linhas somarem o total; nota de crédito fica
  sempre para revisão. O leitor antigo da Tudo4Mobile já não diz "alta" sem nº e total.
- Aviso de documento: PDFs com "Documento emitido para fins de formação" mostram aviso para confirmar no
  e-Fatura (não baixa a confiança da leitura). Avisos passam a ir no DTO das faturas recebidas.
- **Correspondência** (`CompraFaturaService`), por fornecedor: mesmo nº de fatura → mesmo nº de encomenda →
  fatura em falta com data a ±10 dias. Bate certo = linhas da compra + portes da fatura = total da fatura
  (a mesma `IvaEngine.Reconciliar` da "Dif."). Sugestão: `associar` (uma só compra e bate certo), `rever`,
  `nova`, `duplicada` (2.ª cópia de fatura já documentada), `ilegivel`.
- **Associar** grava nº e data da fatura (a data do IVA é a da fatura), total, portes e IVA dos portes da fatura,
  e liga o PDF; fecha a importação na mesma transação; auditado com antes/depois. **Nunca mexe em linhas nem
  stock** — se ficar diferença, a correção é humana com o PDF à frente. Um resumo de encomenda documenta a
  compra mas ela continua "fatura em falta" (e pode ser substituído pela fatura verdadeira). Idempotente;
  índice único impede o mesmo PDF em duas compras.
- **Automático**: carregar vários PDFs (um a um, com progresso) e no fim `associar-automaticamente` liga só os
  casos inequívocos; o resto aparece na inbox com etiqueta e o botão **Ligar** (comparação lado a lado).
- **Nº visível**: `CompraDocumento.Numero` e `CompraLinha.Numero` (lote), sequenciais por tenant, nunca
  reutilizados (backfill na migração por data), pesquisáveis; mostrados em Documentos, editor, Stock e no
  seletor de lotes das vendas.
- Fornecedores procurados pelo nome sem distinguir maiúsculas (o Excel criou "Tudo4mobile", a fatura diz
  "Tudo4Mobile").
- Validado com o Excel real + 13 PDFs reais: 9 ligadas sozinhas (as 3 "Dif." ficam a zero — os totais/portes
  do Excel estavam errados, as linhas certas), 3 para rever, 1 compra nova que não estava no Excel.
