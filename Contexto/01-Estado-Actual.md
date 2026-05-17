# Estado Actual do RepairDesk — 2026-05-16

**Para o Bruno se orientar.** Este ficheiro é o único sítio onde se vê tudo o que está implementado vs o que falta. Atualizar a cada 3-5 sprints (não em cada commit).

---

## 1. Produto em funcionamento (localhost)

O RepairDesk corre em Docker Compose com 4 containers: `api` (.NET 10), `web` (React + nginx), `db` (SQL Server 2022), `cache` (Redis 7).

### O que funciona agora — features ao utilizador

**Gestão base:**
- ✅ Login / logout / refresh token JWT
- ✅ Clientes (CRUD, search por nome/telefone/email/NIF)
- ✅ Reparações (CRUD, search, paginação)
- ✅ Trabalhos (CRUD, com cliente **obrigatório** e categorias incl. Hardware, Serviços)
- ✅ Despesas (CRUD, com link opcional a reparação/trabalho)

**Workflow:**
- ✅ Estados granulares: Orçamento → Recebido → Diagnóstico → Aguarda Peça → Em Reparação → Reparado → Entregue (+ Cancelado)
- ✅ Cores distintas por estado, transições validadas backend+frontend
- ✅ Timeline interna por reparação com logs de transição
- ✅ Reabrir reparação Entregue (botão dedicado)
- ✅ 3-tier lock: Aberto / Frozen (Concluído NãoPago) / Locked (Concluído Pago)

**Dashboard financeiro honesto:**
- ✅ Lucro Realizado vs Receita Pendente vs Investimento Stock (separados, sem confusão)
- ✅ Δ% vs período anterior (verde/vermelho)
- ✅ Gráfico de evolução 6 meses (SVG nativo: receita + custo bar, lucro line)
- ✅ Top 5 reparações lucrativas do período
- ✅ Lucro por categoria com margem %
- ✅ Card "Em curso" agrupado por urgência (Recebido / Reparado / Em Reparação / Aguarda Peça / Diagnóstico)
- ✅ Alertas inline: "X itens por cobrar", "Y despesas órfãs" — clicáveis com drill-down

**Vistas de reparações:**
- ✅ Lista clássica com paginação real (sem scroll infinito)
- ✅ Vista **Kanban** com drag-drop entre 6 colunas (toggle persistido)
- ✅ Filtros por estado, search por equipamento/IMEI/cliente

**Portal cliente público (Uber-style):**
- ✅ Rota `/r/{slug}` sem auth, rate-limited 30/min/IP
- ✅ Mobile-first com timeline visual, linguagem "Em análise" (não jargão técnico)
- ✅ Aprovar/recusar orçamento sem login
- ✅ Botões WhatsApp/telefone para a loja
- ✅ DTO público reduzido (sem custos internos, sem outras reparações do cliente)
- ✅ Compliance: 404 para reparações > 2 anos

**PDF orçamento profissional:**
- ✅ Cabeçalho com logo + NIF + CAE + morada completa + website
- ✅ Tabela de linhas (peças vs mão-de-obra)
- ✅ Secção IBAN formatado para pagamento
- ✅ Termos e condições da tenant
- ✅ Brand color custom
- ✅ **QR code** apontando para portal cliente

**IMEI / Histórico de equipamento (Fase A):**
- ✅ Validação Luhn (15 dígitos)
- ✅ Normalização automática (espaços/hífens removidos)
- ✅ Detecção de re-entrada no form: "Este IMEI já cá entrou X vezes"
- ✅ Modal de histórico no detalhe da reparação

**Tenant settings (Definições):**
- ✅ 4 tabs: Empresa / Fiscal / Pagamentos / Aparência
- ✅ Logo URL, NIF, CAE principal+secundários, IBAN, regime fiscal, T&Cs
- ✅ Auto-save 1.2s com indicador "A guardar / Guardado"

**UX guard rails:**
- ✅ Modal "Foi pago?" ao Concluir/Entregar trabalho/reparação
- ✅ Selector "Associar a reparação/trabalho?" ao criar despesa
- ✅ Alertas no dashboard para evitar dados órfãos

**Import / Export (princípio "dados são do utilizador"):**
- ✅ Importar CSV de clientes (drag-drop, preview, dedupe por NIF)
- ✅ Importar CSV de reparações (cria/reaproveita clientes, parser tolerante PT)
- ✅ Exportar CSV de clientes e reparações (UTF-8 BOM, Excel-friendly)

**Diagnóstico Guiado + Health Score:**
- ✅ Templates configuráveis por tenant (Smartphone, Tablet, Laptop, Desktop, Smartwatch — default seeded com 9-20 items cada)
- ✅ Checklist visual no detalhe da reparação (OK / Marginal / Avaria / N/T)
- ✅ Score 0-100 ponderado em tempo real
- ✅ Score visível no portal cliente público (cor verde >80, âmbar 50-80, vermelho <50)
- ✅ "Pontos a destacar" expostos ao cliente final (só labels, sem detalhes técnicos)

**Garantia digital + Avaliações:**
- ✅ Garantia auto-emitida ao Entregar (dias configuráveis por tenant)
- ✅ Página pública `/g/{slug}` para verificação de garantia (rate-limited)
- ✅ Cobertura / Exclusões / dias configuráveis em Definições → Pós-venda
- ✅ Card "Como correu?" no portal cliente com 5 estrelas + comentário
- ✅ Funil Google Reviews honesto: 4-5★ → Google Reviews da loja, 1-3★ ficam internas

**Outras:**
- ✅ Sidebar com hover/pin colapsável (localStorage)
- ✅ Dark mode 3-states (light/dark/system)
- ✅ Multi-tenant com isolation via global query filter
- ✅ Soft-delete em todas as entidades

**Qualidade:**
- ✅ 50/50 testes backend a passar
- ✅ Frontend build sem warnings

---

## 2. Documentação estratégica entregue (`Contexto/`)

### Estratégia
- ✅ `02-Concorrentes.md`
- ✅ `03-Dores-Reais.md` — citações Reddit/Capterra
- ✅ `04-Roadmap-Detalhado.md`
- ✅ `05-Reflexao-Critica.md`
- ✅ `06-Prompts-Codex.md` — templates de delegação
- ✅ `09-Customer-Acquisition.md`

### Decisões tecnológicas + legais (Codex)
- ✅ `07-Pricing-Proposta.md` — tiers €19/39/89 por loja
- ✅ `10-Compliance-PT.md` — SAF-T, ATCUD, certificação AT
- ✅ `11-WhatsApp-Templates.md`
- ✅ `12-Onboarding-Wizard.md`
- ✅ `13-IMEI-Autoridades.md`
- ✅ `14-Storage-Fotos.md`
- ✅ `15-WhatsApp-Provider.md`
- ✅ `16-Compliance-RGPD.md`
- ✅ `17-Hosting-Deployment.md`
- ✅ `18-Backup-DR.md`
- ✅ `19-Monitoring.md`
- ✅ `20-Suporte-Cliente.md`

### Pendente para o Codex (1)
- ⏳ `08-Pagamentos-Comparacao.md` — Stripe/Mollie/Easypay/SIBS — **adiar até começarmos a cobrar SaaS (~6 meses)**

---

## 3. O que falta para Beta com 2-3 lojas amigas

Em ordem de bloqueio:

### 🔴 Crítico — bloqueia ir para produção
1. **Hosting + Deploy** — comprar VPS Hetzner (ou Codex sugeriu outro), apontar domínio, SSL, deploy CI/CD. Especificado em `17-Hosting-Deployment.md`. **Esforço: 1-2 dias.**
2. **Backups automáticos** — implementar estratégia do `18-Backup-DR.md` (snapshots + off-site). **Esforço: 1 dia.**
3. **Compliance público RGPD** — publicar privacy policy, ToS, cookies banner conforme `16-Compliance-RGPD.md`. **Esforço: meio dia (textos prontos).**

### 🟡 Importante antes da Beta
4. **Monitoring** — Sentry + uptime + alertas conforme `19-Monitoring.md`. **Esforço: meio dia.**
5. **Onboarding wizard** — conforme `12-Onboarding-Wizard.md`. **Esforço: 2-3 dias.**
6. **Upload de fotos antes/depois** — conforme `14-Storage-Fotos.md` (Cloudflare R2 escolhido). **Esforço: 2 dias.**
7. **WhatsApp Business automático** — conforme `15-WhatsApp-Provider.md` + templates já em `11-WhatsApp-Templates.md`. **Esforço: 2-3 dias.**

### 🟢 Nice-to-have antes de Beta
8. **Suporte cliente** — abrir email + KB com 5 artigos essenciais conforme `20-Suporte-Cliente.md`. **Esforço: meio dia.**
9. **Garantia QR** — feature de "trust" complementar ao portal cliente. **Esforço: 1 dia.**

### ⚫ Pode esperar pós-Beta
10. Integração com provider PT de faturação certificada (Moloni/InvoiceXpress) — só quando uma loja pedir
11. MBWay no portal cliente (requer KYC SIBS)
12. App mobile nativa
13. Reviews Google funil automático
14. IMEI Fase B (GSMA CheckMEND)

---

## 4. Decisões importantes em aberto

| Decisão | Quem decide | Quando | Notas |
|---|---|---|---|
| Provider de hosting (Hetzner vs OVH vs ...) | Bruno | esta semana | Cf. `17-Hosting-Deployment.md` |
| Domínio público (.pt / .app / outro) | Bruno | esta semana | Bruno já tem `lopestech.pt` — usar `repairdesk.lopestech.pt`? |
| Rebrand do nome RepairDesk | Bruno | depois de 5-10 clientes | Discutido em `05-Reflexao-Critica.md` |
| Storage de fotos (Cloudflare R2 confirmado) | Bruno + Codex | feito | Validar preços actuais antes de contratar |
| Provider WhatsApp Business | Bruno | quando avançar para automação | Cf. `15-WhatsApp-Provider.md` |
| Plano legal de empresa (sociedade Lda vs nome individual) | Bruno + contabilista | quando ultrapassar €15k/ano | Hoje em Isenção Art. 53 |

---

## 5. Para o Bruno — menu de próximos passos

Olha para a lista de bloqueio acima. **Não é preciso fazer tudo de uma vez.** O caminho realista é:

**Esta semana (dias 1-3):**
- Comprar VPS + domínio (1 dia)
- Deploy produção (1 dia)
- Setup backups automáticos (meio dia)
- Publicar privacy/ToS/cookies (meio dia)

**Próxima semana (dias 4-7):**
- Implementar monitoring (meio dia)
- Implementar upload de fotos (2 dias)
- Convidar 1.ª loja amiga para teste (Patrícia? António? quem te der confiança)

**Semana 3:**
- Onboarding wizard (2 dias)
- WhatsApp automático (2 dias)
- Iterar com feedback da 1.ª loja

**Semana 4-6:**
- Convidar 2.ª e 3.ª lojas
- Resolver bugs que aparecem
- Documentar processos

---

## 6. Sprints concluídos (apenas referência histórica)

| Sprint | Conteúdo | Data |
|---|---|---|
| 14 | Tenant settings + Dashboard financeiro + PDF profissional | 2026-05-15 |
| 15 | Dashboard tendência + Δ% + drill-down + top reparações | 2026-05-15 |
| 16 | Portal cliente público QR Uber-style | 2026-05-15 |
| 17 | Estados granulares (Aguarda Peça + Em Reparação) | 2026-05-16 |
| 18 | IMEI Fase A (Luhn, histórico, validação) | 2026-05-16 |
| 19 | Kanban view com drag-drop | 2026-05-16 |
| 20 | Import CSV clientes | 2026-05-16 |
| 21 | Import CSV reparações | 2026-05-16 |
| 22 | Export CSV clientes + reparações | 2026-05-16 |
| 23 | Diagnóstico Guiado + Health Score | 2026-05-17 |
| 24 | Garantia digital QR + Avaliações 1-5 estrelas | 2026-05-17 |
| 25 | UI Definições Pós-venda (Garantia + Google Reviews config) | 2026-05-17 |

Próximos sprints: definir com base nos research que o Codex devolver (PWA offline, Distribuidores PT, Brand, testes, performance, privacy).
