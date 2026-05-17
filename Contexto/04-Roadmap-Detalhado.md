# Roadmap Detalhado — Dor → Feature → Sprint

Última atualização: 2026-05-13

Este roadmap está ancorado nas dores reais identificadas em `03-Dores-Reais.md` (Reddit, Capterra, Bruno) e na análise crítica em `05-Reflexao-Critica.md`. **Não é** uma lista de features aspiracionais. **É** o caminho mais curto para resolver dores observadas com o menor desperdício de esforço.

---

## Princípios do roadmap

1. **Dor observada vence ideia teórica.** Se uma feature não mapeia para dor real, fica fora.
2. **Dogfooding antes de release.** Bruno usa cada feature pelo menos 2 semanas antes de exporta para beta.
3. **Quick wins primeiro.** Features que demoram <1 dia e resolvem dor real vão à frente.
4. **Anti-bloat.** Cortar antes de adicionar. Re-avaliar features após 3 meses.
5. **Honesto.** Sem dark patterns, sem features-marketing-fake (e.g., IA placebo).
6. **Verticalização.** Eletrónica primeiro. Outras categorias só quando houver tração.

---

## Legenda

- 🔴 **Crítico** — bloqueia adopção / dor severa
- 🟡 **Importante** — diferenciação clara vs concorrentes
- 🟢 **Nice** — qualidade de vida, polimento
- ⚫ **Adiar** — fora de scope curto/médio prazo

Esforço:
- **S** = <1 dia
- **M** = 1-3 dias
- **L** = 1-2 semanas
- **XL** = >2 semanas

---

## Sprint 14 — Dashboard honesto + Tenant settings (em curso)

**Dores que resolve:**
- Dashboard mostra -312€ enganador (Bruno, dor G7)
- Não há sítio onde meter logo/IBAN para PDFs (Bruno, dor G8)
- PDF orçamento sem logo é amador (Bruno, queixa visual)
- "Software desadaptado, gastam tempo a contornar" (Reddit, dor padrão #1)

| Feature | Prio | Esforço | Status |
|---|---|---|---|
| Dashboard financeiro reestruturado (Lucro Realizado vs Investimento vs Pendente) | 🔴 | L | In progress |
| Breakdown por categoria (Reparações, Websites, Software, Junta, Hardware, Serviços) | 🟡 | M | Pending |
| Página `/settings` com Tenant settings (logo upload, NIF, IBAN, CAE, morada, telefone) | 🔴 | M | Pending |
| PDF orçamento profissional (logo, NIF, CAE, IBAN, T&Cs) | 🔴 | M | Done parcial |
| Remover scroll infinito do dashboard (grid layout) | 🟡 | S | Pending |

**Critério de saída:** Bruno consegue gerar PDF de orçamento que pode mandar a cliente sem corar.

---

## Sprint 15 — Dashboard profissional + UX guard rails (NOVO)

**Dores que resolve:**
- Dashboard actual é básico (Bruno, feedback 2026-05-15)
- Falta tendência, comparação, alertas, drill-down
- Trabalhos concluídos não-pagos passam despercebidos (caso Junta de Freguesia)
- Despesas órfãs (sem trabalho linked) inflacionam "Investimento Stock" injustamente
- "Quando confio num número, tem que ser correcto" — Bruno

| Feature | Prio | Esforço | Status |
|---|---|---|---|
| **Gráfico de evolução mensal** (6 meses) — line chart Lucro/Receita | 🔴 | M | Pending |
| **Comparação com período anterior** (Δ% nos cards: "+12% vs mês passado") | 🔴 | M | Pending |
| **Card "Receita Pendente" clicável** → abre lista de trabalhos não pagos | 🔴 | S | Pending |
| **Card "Investimento Stock" clicável** → abre lista de despesas órfãs (sem trabalho linked) | 🔴 | S | Pending |
| **Alertas inline no dashboard**: "⚠️ 5 trabalhos concluídos não pagos = €X" / "💡 12 despesas sem trabalho associado" | 🔴 | M | Pending |
| **Fluxo de caixa simplificado** (entrou / saiu / saldo) | 🟡 | M | Pending |
| **Filtro por categoria no dashboard** (ver só Reparações, ou só Websites, etc.) | 🟡 | S | Pending |
| **Top 5 reparações mais lucrativas do período** | 🟡 | S | Pending |
| **Export CSV/Excel do dashboard** | 🟢 | M | Pending |
| **UX guard rails**: ao concluir trabalho, perguntar "Pago? Sim/Parcial/Não". Ao criar despesa, sugerir trabalho/reparação. | 🔴 | M | Pending |

**Critério de saída:** dashboard responde a "como vou financeiramente este mês vs o mês passado?" em 5 segundos, sem ambiguidade. Bruno consegue substituir o Excel pessoal pelo dashboard.

---

## Sprint 16 — Portal cliente público (QR / Uber-style)

**Dores que resolve:**
- "Cliente não sabe estado, telefona 5x por dia" (Reddit, dor padrão #3)
- "Comunicação dispersa entre WhatsApp/SMS/telefone" (Reddit, dor padrão #6)
- Apple/Mercedes/Uber Eats têm tracking lindo, oficinas mandam SMS feio (ChatGPT, conselho B6)
- Bruno quer cliente ver estado sem ter que perguntar (G3)

| Feature | Prio | Esforço | Status |
|---|---|---|---|
| Backend: campo PublicSlug + endpoint `/api/public/repair/{slug}` (rate-limited) | 🔴 | M | Pending |
| Frontend: rota `/r/{slug}` mobile-first | 🔴 | L | Pending |
| QR code no PDF orçamento (QRCoder) | 🟡 | S | Pending |
| Linguagem client-friendly ("Em análise" não "Diagnostico") | 🟡 | S | Pending |
| Botão WhatsApp dentro do portal ("Falar com a loja") | 🟢 | S | Pending |
| Aprovar/recusar orçamento sem login | 🟡 | M | Pending |

**Critério de saída:** Bruno mete QR num iPhone reparado, cliente abre no telemóvel, vê estado, aprova orçamento. Sem instalar nada.

---

## Sprint 16 — Anti-scroll & paginação real

**Dores que resolve:**
- "Software é lento com 1000+ reparações" (Capterra dor PC Repair Tracker)
- "Scroll infinito mata produtividade B2B" (Bruno, intuição correcta)
- Listas crescem e UX degrada-se

| Feature | Prio | Esforço | Status |
|---|---|---|---|
| Paginação server-side em `/reparacoes` (já tem mas validar) | 🔴 | S | Done |
| Kanban view de reparações (drag-drop entre estados) | 🟡 | L | Pending |
| Filtros avançados sticky (estado, cliente, data, categoria) | 🟡 | M | Pending |
| Search com debounce 300ms | 🟡 | S | Pending |
| URL state (filtros no querystring) | 🟢 | S | Pending |

**Critério de saída:** Bruno encontra qualquer reparação em <5 segundos mesmo com 500+ rows simuladas.

---

## Sprint 17 — Estados granulares + Aguarda Peça

**Dores que resolve:**
- "Não sei distinguir 'à espera de peça' de 'a diagnosticar'" (dor implícita)
- "Cliente pergunta porque demora — preciso de dizer 'a peça vem da China'" (Reddit oficinas)
- Estados actuais demasiado grosseiros para workflow real

| Feature | Prio | Esforço | Status |
|---|---|---|---|
| Refactor enum RepairStatus (Aguarda_Peca, Orcamento_Aprovado, etc.) | 🔴 | M | Pending |
| Migration EF Core mapping estados antigos → novos | 🔴 | S | Pending |
| Cancelado com sub-razão (cliente, técnico, peça indisponível) | 🟡 | S | Pending |
| Estados visíveis no portal público ≠ estados internos (mapeamento) | 🟡 | S | Pending |
| Atualizar Kanban, badges, filtros frontend | 🔴 | M | Pending |

**Critério de saída:** Workflow real refletido em estados. Cliente externo vê linguagem clara, técnico vê detalhe.

---

## Sprint 18 — WhatsApp Business + Templates inteligentes

**Dores que resolve:**
- "SMS feios e caros, queria WhatsApp" (Reddit padrão #6)
- "Mensagem 30 dias depois ajudaria reviews" (Codex sugestão)
- Bruno já manda WhatsApp manual — automatizar

| Feature | Prio | Esforço | Status |
|---|---|---|---|
| Templates contextuais por estado (Recebido, Pronto, Entregue, Aguarda Peça) | 🟡 | M | Done parcial |
| Integração WhatsApp Business API (Meta Cloud API ou similar) | 🟡 | L | Pending |
| Histórico de mensagens enviadas (timeline da reparação) | 🟢 | S | Pending |
| Opt-in claro do cliente (RGPD) | 🔴 | S | Pending |
| Follow-up automático aos 30 dias (com opt-out) | 🟢 | M | Pending |

**Critério de saída:** Cliente recebe WhatsApp quando reparação muda de estado. Bruno deixa de mandar manualmente.

---

## Sprint 19 — Stock e fornecedores (versão simples)

**Dores que resolve:**
- "Quanto stock tenho realmente? E quanto vale?" (Bruno + Reddit Spanish appliance)
- "Quem é o melhor fornecedor por taxa de defeito?" (Codex sugestão A9)
- Hoje só temos despesas linked a reparações, sem inventário standalone

| Feature | Prio | Esforço | Status |
|---|---|---|---|
| Entidade Peca (nome, sku, fornecedor, custo, qty_stock, qty_min) | 🟡 | M | Pending |
| Mover Despesa para usar Peca em vez de string livre | 🟡 | M | Pending |
| Página `/stock` com listagem + alerta de mínimo | 🟡 | M | Pending |
| Ranking fornecedores (taxa devolução, lead time médio) | 🟢 | L | Pending |
| Importação CSV de stock inicial | 🟡 | M | Pending |

**Critério de saída:** Bruno regista compras, ao usar em reparação debita stock, vê valor total inventariado.

---

## Sprint 20 — Beta com 2-3 lojas amigas

**Dores que resolve:**
- Validação externa — nem tudo o que Bruno usa serve para outras lojas
- "Onboarding fácil ou abandonam" (Reddit/Capterra padrão)

| Feature | Prio | Esforço | Status |
|---|---|---|---|
| Importação CSV de clientes e reparações (de Excel ou outro sistema) | 🔴 | M | Pending |
| Setup wizard inicial (logo, NIF, primeira reparação demo) | 🔴 | M | Pending |
| Página `/help` com vídeos curtos por feature | 🟡 | L | Pending |
| Modo "demo data" (apagar tudo e começar) | 🟢 | S | Pending |
| Convidar 2-3 lojas amigas (irmãos, conhecidos do Bruno) | 🔴 | XL | Pending |

**Critério de saída:** Outra loja consegue fazer setup sozinha em <30min sem ligar ao Bruno.

---

## Sprint 21 — Integração com Provider PT Certificado (REVISTO 2026-05-15)

**⚠️ NOTA:** sprint refeito após research formal do Codex em `10-Compliance-PT.md`. A versão anterior assumia que emissão própria por software não certificado era legal em Isenção Art. 53 — não é. Razão: DL 28/2019 art. 4.º n.º 1 b) obriga certificação se a empresa **usar programa informático de faturação**, qualquer que seja o volume ou regime.

**Dores que resolve:**
- "Tenho que ir ao Portal das Finanças manualmente" (Bruno, dor recorrente)
- "Software não emite documento legal" (Reddit padrão)
- Compliance fiscal PT real (não atalhos)

**Estratégia:**
- RepairDesk continua a emitir apenas documentos **não fiscais** (orçamentos, fichas, garantias) com label "Este documento não serve de fatura"
- Fatura legal é emitida por **provider certificado externo** integrado via API: Moloni, InvoiceXpress, Cleverlance ou Vendus
- O dono da loja escolhe provider em Definições → "Faturação"
- RepairDesk envia para o provider quando o utilizador clica "Emitir fatura"

| Feature | Prio | Esforço | Status |
|---|---|---|---|
| Research formal de providers PT certificados (custos, APIs, KYC) | 🔴 | M | Pending — pode ser delegado a Codex |
| Settings de tenant: campo "Provider de faturação" (Nenhum/Moloni/InvoiceXpress/etc.) + credenciais API | 🔴 | M | Pending |
| Integração com **um** provider (provavelmente Moloni — mais usado em PT) | 🔴 | XL | Pending |
| Botão "Emitir fatura via [provider]" em reparação/trabalho pago | 🔴 | M | Pending |
| Sync: numeração e ATCUD vêm do provider | 🟡 | M | Pending |
| Documentação Definições → "Faturação" explicando porque não emite directamente | 🔴 | S | Pending |
| ⚫ Certificação de módulo próprio (Fase 3, 24+ meses, ~50-100k€) | ⚫ | XL | Adiado |
| ⚫ SAFT-PT export próprio | ⚫ | L | Adiado (vem do provider) |

**Critério de saída:** Bruno emite uma fatura legítima através do RepairDesk (com numeração e ATCUD reais), e ela aparece automaticamente no e-Fatura do Portal das Finanças. Sem ele sair do RepairDesk.

---

## Sprint 22 — Reviews Google + Marketing automation

**Dores que resolve:**
- "Como peço reviews sem ser chato?" (Reddit padrão)
- "Reviews são marketing barato" (Codex sugestão A10)
- Lojas pequenas não têm marketing — precisam de truques operacionais

| Feature | Prio | Esforço | Status |
|---|---|---|---|
| Funil automático: após entrega + 24h → WhatsApp com link Google Reviews | 🟡 | M | Pending |
| Configuração do link Google da loja em Settings | 🟡 | S | Pending |
| Métricas: quantos pedidos → reviews recebidas (estimativa) | 🟢 | M | Pending |
| Sugestão de texto personalizável | 🟢 | S | Pending |

**Critério de saída:** Bruno triplica reviews em 2 meses sem trabalho extra.

---

## Horizon 2 (3-6 meses)

Features que **podem** aparecer mas não bloqueiam beta. Re-avaliar quando chegar a altura.

| Feature | Esforço | Pre-requisito | Notas |
|---|---|---|---|
| MBWay integração (Easypay/SIBS) | L | KYC + €30/mês | Só vale com 20+ clientes pagantes |
| Pagamento Stripe/Mollie para SaaS subscriptions | M | Decisão pricing | Necessário para vender |
| Multi-loja (uma conta, várias localizações) | XL | Beta com 2 lojas | Cf. dor #8 Reddit |
| App mobile (iOS + Android nativos) | XL | PWA insuficiente | RO App tem, é diferencial |
| Open API pública + webhooks | L | Auth tokens | Para integradores |
| Ranking interno por funcionário (produtividade) | M | Multi-user | Cuidado: pressão tóxica |
| Bin locations / warehouse | M | Stock funcional | Útil para lojas grandes |
| 2-way SMS / VoIP | L | Operadora SMS | RO App tem |
| Importação Excel com IA (parse automático) | L | LLM API | Útil onboarding |

---

## Horizon 3 (6-12 meses)

Features que dependem de dados ou tração que ainda não temos.

| Feature | Pré-requisito | Notas |
|---|---|---|
| Biblioteca interna de avarias (sintoma → solução) | 1000+ reparações | Codex sugestão A8 — long-term value real |
| Passaporte do equipamento por IMEI | 10+ lojas no SaaS | Network effect |
| **IMEI ↔ autoridades (PSP/MAI/Interpol)** | Parceria institucional | **Ideia Bruno — ver secção dedicada abaixo** |
| IA previsão de peças necessárias | 5000+ reparações | Necessita histórico |
| Marketplace de peças B2B | Confiança no produto | Risco regulatório |
| App cliente nativa (iOS/Android) | Validação portal PWA | Custo dev/manutenção alto |
| Self-hostable (Docker compose público) | Decisão estratégica | Risco vs benefício |

---

## Feature destaque: IMEI ↔ Autoridades (PSP/MAI/Interpol)

**Ideia Bruno (2026-05-15):** registar IMEI/serial de equipamento que entra na loja e cruzar automaticamente com bases de dados de equipamentos reportados como roubados (PSP, MAI, Interpol I-24/7 Stolen Mobile Telephone Database, GSMA Device Registry / CheckMEND). Se aparecer match, alertar técnico **antes** de aceitar o trabalho.

**Por que esta ideia é potente:**
- **Diferenciação extrema:** nenhum concorrente PT (nem global) faz isto bem
- **PR enorme:** "O primeiro software que ajuda a devolver telemóveis roubados aos donos"
- **Bem público:** reduz mercado paralelo de equipamentos roubados em PT
- **Confiança das autoridades:** abre porta para parcerias institucionais
- **Marketing orgânico:** lojas que usam ganham reputação de "honestas"

**Mas é complicado. Realismo:**

### Viabilidade técnica
- **GSMA CheckMEND / Device Check:** existe, é pago (~£0.10-0.50 por consulta), tem API. **Caminho mais realista.**
- **APIs PSP/MAI:** **não existem publicamente** em PT. Precisa de protocolo institucional.
- **Interpol I-24/7:** acesso restrito a autoridades policiais, não a privados.
- **Bases agregadas (Stolen911, DeviceLost, etc.):** existem mas qualidade variável.

### Viabilidade legal
- **RGPD:** processamento de IMEI é dado pessoal indireto. Base legal:
  - Art. 6.º (1) (e) — interesse público (prevenção crime)
  - Art. 6.º (1) (f) — interesse legítimo da loja (não comprar bens roubados)
- **Consentimento do cliente:** obrigatório informar no recibo de entrada que IMEI será verificado
- **Falsos positivos:** risco real (IMEI clonado, reportado e devolvido sem actualizar BD, transferências de propriedade). Precisamos de UX que **alerta** mas **não acusa**.
- **Reporting às autoridades:** se houver match, a obrigação legal é **denunciar** (art. 244.º CP). Software deve facilitar, não substituir.

### Caminho proposto (sprint 25+ / horizon 3)

**Fase A (sprint 25-27):** registo interno robusto
- Campo IMEI obrigatório no recibo de entrada de telemóveis
- Validação Luhn (algoritmo de validação do IMEI)
- Histórico interno: mesmo IMEI já entrou cá antes? Alertar.
- Botão "Imprimir recibo com IMEI registado" (prova legal de boa-fé da loja)

**Fase B (sprint 30+):** integração GSMA CheckMEND
- Subscrição GSMA + API integration
- Consulta automática ao registar IMEI
- Resultado: ✅ Limpo / ⚠️ Watch-list / ❌ Reportado roubado
- UX que respeita presunção de inocência (cliente pode estar inocente)
- Logs imutáveis das consultas (auditoria)

**Fase C (horizon 4):** parceria com PSP/MAI
- Contactar PSP / GNS para protocolo de cooperação
- Pode envolver suporte do Governo (Programa Internet Segura, etc.)
- Whitelist do RepairDesk como "lojas confiáveis"
- Eventualmente: portal de consulta directa

### Riscos a gerir
- Lojas resistirem se sentirem "vigiadas" — vender como **protecção** (não comprar mercadoria com problema legal)
- Custo CheckMEND pode comer margem em lojas pequenas — incluir no Pro/Enterprise, não Starter
- Falsos positivos podem gerar conflito loja-cliente — UX cuidadosa
- Dependência GSMA: se mudarem termos, ficamos expostos

### Próximo passo
Task #68 criada: research formal de viabilidade. **Não implementar antes desta análise estar feita.**

---

## Horizon 4 (12+ meses) — Wishful

- Expansão geográfica (ES, BR, FR) com SAFT/IVA locais
- Acquisition / partnership com distribuidores de peças
- Funcionalidades vertical-específicas (bicicletas, electrodomésticos, etc.)
- IA conversacional com cliente (chat 24/7)

---

## Anti-Roadmap — Features que NÃO vamos fazer

Decisão consciente de NÃO construir, para manter foco:

| ❌ Feature | Razão |
|---|---|
| Cleaning services / HVAC / Alfaiataria | Fora de vertical foco (eletrónica) |
| Sapatos / Joalharia / Bordados | Workflow muito diferente, mercado pequeno PT |
| Loja online (e-commerce frontend) | Já existe Shopify/Woo, não competimos aí |
| Sistema de ponto / RH | Há produtos dedicados (Sage, Primavera) |
| ERP completo | Scope creep mortal |
| Vídeos antes/depois (storage) | Bandwidth caro, raro real-world |
| Assinatura digital com tablet | Pouco usado em PT, fricção UX |
| Login automático Portal AT com .env | Inseguro, RGPD, refusado |
| IA placebo (sem dados) | Honestidade > marketing |
| Dark patterns / lock-in | Princípio fundador |

---

## Métricas de sucesso por horizon

**Horizon 1 (até 6 meses):**
- 2-3 lojas beta a usar regularmente
- Bruno usa todo o sistema dele sem Excel paralelo
- Zero perdas de dados / incidentes críticos
- Tempo médio de criar reparação <90 segundos
- NPS interno (Bruno + beta lojas) >7/10

**Horizon 2 (6-12 meses):**
- 10-20 lojas pagantes
- €500-1500 MRR
- Churn <10% anual
- Tickets de suporte <30min/cliente/mês

**Horizon 3 (12-24 meses):**
- 50-100 lojas pagantes
- €2500-5000 MRR
- Bruno consegue pagar-se ordenado decente
- Talvez contratar primeiro funcionário

---

## Revisões deste roadmap

Re-avaliar a cada **6 sprints** (~3 meses). Perguntas para a revisão:

1. Quais features deste roadmap foram usadas pelas lojas?
2. Quais foram pedidas mas não estão aqui?
3. Quais aqui não foram pedidas por ninguém?
4. Estamos a entregar valor ou só a construir?
5. Algum concorrente fez algo grande que muda a nossa estratégia?
6. Bruno está esgotado? (sustentabilidade pessoal > velocidade)
