# Estado Actual do RepairDesk — 2026-05-17

**Single source of truth.** Único sítio onde se vê tudo o que está implementado vs o que falta. Actualizar a cada 3-5 sprints, não em cada commit.

Última actualização: **fim Sprint 35**. Próximo bloco a entrar: backup automático (#C6), audit log + RGPD UI (#C7), observability (#C8) — todos delegados ao Codex.

Referência cruzada: para critérios objectivos de beta-ready, ver [`34-Beta-Launch-Criteria.md`](34-Beta-Launch-Criteria.md).

---

## 1. Produto a funcionar (`docker compose up`)

Stack: `api` (.NET 10), `web` (React + nginx), `db` (SQL Server 2022), `cache` (Redis 7). Todos containers, hot-reload em dev.

### Operação diária ✅

- **CRUD completo** Clientes / Reparações / Trabalhos / Despesas
- **Stock de peças** (Sprint 31) — SKU, fornecedor, localização, mínimo, alertas low-stock, movimentos com motivo
- **Peças usadas em reparação** — autocomplete, decremento automático de stock, recálculo de custo
- **State machine reparação** — Orçamento → Recebido → Diagnóstico → Aguarda Peça → Em Reparação → Reparado → Entregue (+ Cancelado), transições validadas backend+frontend, logs imutáveis
- **3-tier lock** — Aberto / Frozen (Concluído NãoPago) / Locked (Concluído Pago)
- **IMEI Fase A** — validação Luhn + histórico ("este IMEI já cá entrou X vezes")
- **Search/filtros** — equipamento, IMEI, cliente, NIF, estado

### Vistas
- **Lista** clássica paginada (sem scroll infinito)
- **Kanban** drag-drop entre 6 colunas, toggle persistido em localStorage

### Dashboard financeiro honesto
- **3 zonas visuais** (Sprint 30) — Precisa de atenção / Hoje na oficina / Saúde do negócio
- **KPIs separados** — Lucro Realizado vs Receita Pendente vs Investimento Stock (sem auto-engano)
- **Δ% vs período anterior** verde/vermelho em cada KPI
- **Tendência 6 meses** — SVG nativo, barras receita+custo, linha lucro, margem média
- **Top reparações lucrativas** + **Top clientes**
- **Alertas inline** — itens por cobrar (clicáveis), despesas órfãs, reparações paradas >7 dias
- **Lucro por categoria** com margem %
- **Avaliações + NPS** — distribuição estrelas, comentários recentes

### Portal cliente público (Uber-style) — `/r/{slug}`
- Sem login, rate-limited 30/min/IP
- Timeline visual + linguagem cliente-friendly ("Em análise" não "Diagnostico")
- Aprovar/recusar orçamento sem login
- Botões WhatsApp + telefone para a loja
- Fotos antes/depois (visibilidade configurável por foto)
- Health Score 0-100 com pontos a destacar
- DTO público reduzido (sem custos internos)
- Avaliação 1-5 ★ + funil Google Reviews honesto (4-5★ → Google, 1-3★ internas)

### Garantia digital — `/g/{slug}`
- Auto-emitida ao Entregar reparação
- Página pública permanente com QR
- Cobertura/exclusões/dias configuráveis em Definições
- Cliente nunca mais perde garantia em papel

### PDF orçamento profissional
- Logo + NIF + CAE + morada + IBAN + brand color
- Tabela peças vs mão-de-obra
- QR para portal cliente
- Termos e condições configuráveis

### Fotos antes/durante/depois (Sprint 29)
- Upload com legenda + tipo
- **Antes** e **Depois** visíveis no portal; **Durante** privadas
- Storage abstraído via `IPhotoStorage`:
  - **LocalFileSystem** (default em dev)
  - **CloudflareR2** (Sprint 35) — S3-compat, EU, zero egress, selector via env `Storage:Provider`

### Diagnóstico guiado
- Templates por tipo (Smartphone/Tablet/Laptop/Desktop/Smartwatch)
- Checklist visual com 4 estados por item (OK / Marginal / Avaria / N/T)
- Health Score em tempo real ponderado
- Cor verde/âmbar/vermelho

### Tabela de preços partilhada
- Por marca/modelo/serviço
- Tempo estimado em minutos
- Sugestões automáticas no form de nova reparação

### Onboarding wizard (Sprint 32) — `/bemvindo`
- 5 passos: empresa → cliente → reparação → tour dashboard → equipa
- Demo data em cada passo (cria cliente/reparação fake que podes apagar)
- Preview live no Step 1 (mostra o orçamento PDF)
- Tour interactivo com Popover (Step 4)
- "Saltar por agora" em cada passo
- Auto-redirect para `/bemvindo` se `Tenant.OnboardingCompletado=false`

### UI primitives (Sprint 33)
- `Button`, `StatusBadge`, `PageHeader`, `EmptyState`, `Skeleton` + variantes — aplicados em 7+ páginas

### UX guard rails
- Modal "Foi pago?" ao Concluir/Entregar
- Sugestão "Associar a reparação?" ao criar despesa
- Sidebar colapsável (hover/pin) + dark mode 3-states (light/dark/system)
- Toasts globais (sonner) em todas as acções

### Import / Export
- CSV de clientes (drag-drop, preview, dedupe NIF)
- CSV de reparações (cria/reaproveita clientes, parser PT)
- CSV de stock (Sprint 31)
- Export UTF-8 BOM Excel-friendly

### Multi-tenant
- Global query filter por TenantId (claim JWT)
- Soft-delete em todas as entidades
- Identity + JWT Bearer + refresh tokens com revogação

### CI/CD (Sprint 34)
- `ci.yml` (build + test + lint + security em PR/push main)
- `deploy-staging.yml` (push main → SSH staging)
- `deploy-production.yml` (tag `v*.*.*` → approval manual)
- Dependabot (.NET + npm + Docker + GitHub Actions)
- CHANGELOG.md (Keep a Changelog format)
- gitleaks + npm audit + dotnet audit + CodeQL (continue-on-error)

### Qualidade
- 50+/50 testes backend a passar
- Frontend `npm run build` sem erros
- Frontend `npm run lint` sem warnings
- Multi-tenant isolation testado em `TenantIsolationTests`

---

## 2. Documentação estratégica (`Contexto/`)

39 docs co-localizados com o código. Índice em [`00-Index.md`](00-Index.md).

### Estratégia núcleo
✅ 02 Concorrentes · 03 Dores reais · 04 Roadmap · 05 Reflexão crítica · 06 Prompts Codex · 09 Customer Acquisition

### Decisões tecnológicas/legais (Codex pesquisou + Bruno aprovou)
✅ 07 Pricing · 10 Compliance PT · 11-15 WhatsApp/Onboarding/IMEI/Storage/Provider · 16 RGPD · 17 Hosting · 18 Backup · 19 Monitoring · 20 Suporte · 21 Certificação AT · 22 Tabela preços PT · 23 Plano fiscal pessoal · 24 PWA Offline · 25 Distribuidores peças · 26 Brand · 27 Plano testes · 28 Performance · 29 Privacy by design · 30 Release · 31 Sales playbook · 32 Audit UX/UI · 33 CI/CD setup

### Decisões fechadas pelo Bruno (2026-05-17)
✅ **34 Beta launch criteria** — MUST/SHOULD/NICE-have, timeline 6-8 semanas
✅ **35 Faturação** — **Path A: Moloni/InvoiceXpress**, implementação sprint 39
✅ **36 Vídeo demo script** — 90s, 8 cenas, pronto para gravar

### Pendente para o Codex
- ⏳ 08 Pagamentos comparação — adiar até cobrar SaaS (~6 meses)

---

## 3. O que falta para beta (per [`34-Beta-Launch-Criteria.md`](34-Beta-Launch-Criteria.md))

### 🔴 MUST-HAVE bloqueadores
1. **Backup automático** SQL Server + R2 — prompt Codex #C6 pronto
2. **Audit log + RGPD UI** (export Art. 20, hard-delete) — prompt Codex #C7 pronto
3. **Observability** (Serilog + correlation IDs + health checks) — prompt Codex #C8 pronto
4. **Faturação integrada** (Moloni / InvoiceXpress) — prompt em `35-Faturacao-Decisao-Final.md`, sprint 39
5. **Página de privacidade pública** (RGPD)
6. **Hosting produção** — VPS Hetzner + domínio + SSL + deploy CI/CD

### 🟡 SHOULD-HAVE antes do beta
7. WhatsApp Business automático (templates já em `11-WhatsApp-Templates.md`)
8. Print etiquetas térmicas 80mm
9. Vídeo demo 90s **gravado** (script pronto)
10. Landing actualizada com screenshots

### 🟢 Nice-to-have / pós-beta
- IMEI Fase B (GSMA CheckMEND) — diferenciador, parceria
- PWA offline mode
- Multi-loja por tenant
- App mobile nativa
- i18n EN

---

## 4. Decisões em aberto

| Decisão | Quem | Quando | Notas |
|---|---|---|---|
| Provider hosting (Hetzner vs OVH) | Bruno | esta semana | Cf. `17-Hosting-Deployment.md` |
| Domínio público (`repairdesk.lopestech.pt`?) | Bruno | esta semana | Bruno já tem lopestech.pt |
| Moloni vs InvoiceXpress | Bruno | antes sprint 39 | Doc 35 recomenda Moloni |
| Pricing tier inicial em beta | Bruno | antes 1ª oficina | Cf. `07-Pricing-Proposta.md` (€19/39/89) |
| Rebrand do nome RepairDesk | Bruno | depois 5-10 clientes | Cf. `05-Reflexao-Critica.md` |
| Sociedade Lda vs nome individual | Bruno + contabilista | quando ultrapassar €15k/ano | Hoje Isenção Art. 53 |

---

## 5. Menu de próximos passos para o Bruno

### Esta semana
- [ ] Mandar Codex executar #C6 (backup) quando voltar
- [ ] Decidir Moloni vs InvoiceXpress (criar conta sandbox para testar API)
- [ ] Decidir hosting (Hetzner CX21 ~€5/mês é o ponto de partida)

### Próxima semana
- [ ] Mandar Codex #C7 (audit log + RGPD UI)
- [ ] Comprar domínio e apontar para VPS
- [ ] Gravar primeira tentativa do vídeo demo (Loom basta)

### Semana 3
- [ ] Mandar Codex #C8 (observability)
- [ ] Página de privacidade pública (texto + rota)
- [ ] Tenant demo com dados credíveis para mostrar

### Semana 4-6
- [ ] Convidar 1ª oficina amiga (informalmente, sem pressão)
- [ ] Acompanhar 2 semanas via WhatsApp directo
- [ ] Iterar feedback real
- [ ] Decidir GA ou polir mais

---

## 6. Sprints concluídos

| Sprint | Conteúdo | Data |
|---|---|---|
| 0 | Foundation Clean Architecture + Docker | 2026-04-15 |
| 1-13 | Auth, CRUD core, base PDF, dashboard básico | 2026-04 a 05-13 |
| 14 | Tenant settings + Dashboard financeiro + PDF profissional | 2026-05-15 |
| 15 | Dashboard tendência + Δ% + drill-down + top reparações | 2026-05-15 |
| 16 | Portal cliente público QR Uber-style | 2026-05-15 |
| 17 | Estados granulares (Aguarda Peça + Em Reparação) | 2026-05-16 |
| 18 | IMEI Fase A (Luhn, histórico) | 2026-05-16 |
| 19 | Kanban view com drag-drop | 2026-05-16 |
| 20-22 | Import/Export CSV clientes, reparações, stock | 2026-05-16 |
| 23 | Diagnóstico Guiado + Health Score | 2026-05-17 |
| 24 | Garantia digital QR + Avaliações 1-5★ | 2026-05-17 |
| 25 | UI Definições Pós-venda | 2026-05-17 |
| 26 | Tabela de preços partilhada | 2026-05-17 |
| 29 | Fotos antes/depois (IPhotoStorage abstraction) | 2026-05-17 |
| 30 | Quick wins UX (lucide-react, Button, StatusBadge, sonner) | 2026-05-17 |
| 31 | Stock de peças (Part + PartMovimento + decremento auto) | 2026-05-17 |
| 32 | Onboarding wizard 5 passos | 2026-05-17 |
| 33 | UI primitives (PageHeader, EmptyState, Skeleton) | 2026-05-17 |
| 34 | CI/CD GitHub Actions | 2026-05-17 |
| 35 | Cloudflare R2 storage adapter | 2026-05-17 |

### Próximos sprints (Codex em fila)
- 36 — Backup automático SQL Server + R2 (#C6)
- 37 — Audit log + RGPD UI (#C7)
- 38 — Serilog + correlation IDs + health checks (#C8)
- 39 — Integração Moloni / InvoiceXpress
