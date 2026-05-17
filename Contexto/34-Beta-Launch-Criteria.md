# Beta Launch Criteria — Quando é que RepairDesk está pronto?

Última actualização: 2026-05-17

Este documento define **o que significa "pronto para beta"** com critérios objectivos. O objectivo é evitar dois erros opostos:

1. **Lançar cedo demais** — primeiros clientes sofrem com bugs, formam-se opiniões negativas, churn alto.
2. **Polir infinitamente** — Bruno gasta meses sem feedback real, perde momentum e perde dinheiro.

Bruno disse explicitamente (2026-05-17): *"isto ainda está muito fraco para produção, isto nao tem nada de diferenciado"*. Este doc serve para responder em que ponto é que isso deixa de ser verdade.

---

## Definição operacional

**Beta = primeira oficina externa a usar RepairDesk em produção real**, não LopesTech. O cliente paga (mesmo que preço promocional), o cliente assume risco, o cliente substitui o sistema actual dele pelo RepairDesk.

Não é "beta fechado com 1 amigo a clicar". É **uma oficina real, num dia normal, a depender do RepairDesk**.

---

## Critérios MUST-HAVE (bloqueiam beta)

Falha qualquer um → não lançar.

### 1. Funcionalidade core
- [x] CRUD Clientes/Reparações/Trabalhos/Despesas funcional
- [x] Estados de reparação coerentes (Recebido → Diagnóstico → Aguarda Peça → Em Reparação → Reparado → Entregue → Cancelado)
- [x] PDF orçamento profissional (logo, NIF, IBAN, T&Cs)
- [x] PDF garantia (com QR code para verificação pública)
- [x] Portal cliente público (`/r/{slug}`) — vê estado sem login
- [x] Stock de peças com decrementação automática ao consumir (sprint #C1 Codex)
- [ ] **Onboarding wizard** — novo tenant não fica perdido (sprint #C2 Codex em curso)
- [x] Avaliações + NPS no dashboard
- [ ] **Toasts de feedback em todas as acções** (criar/editar/apagar) — quase feito, falta auditar

**Status:** 8/10 ✅

### 2. Multi-tenant + Segurança
- [x] Global query filters EF Core garantem isolamento de tenant
- [x] JWT Bearer + ASP.NET Identity
- [x] Refresh tokens com revogação
- [x] Soft delete em tudo (recuperação possível)
- [x] Rate limiting em endpoints públicos
- [x] Tests de isolation (`TenantIsolationTests`)
- [ ] **Audit log** — quem fez o quê e quando (next sprint)
- [ ] **Backup automático diário** — perda de dados é game-over (next sprint)

**Status:** 6/8 ⚠️

### 3. Fiscalidade PT (DL 28/2019)
- [ ] **Decisão tomada e documentada** — emitir faturas via provider PT certificado (Moloni / InvoiceXpress / Vendus) OU certificar RepairDesk
- [ ] **Provider integrado** ou **claim explícito** "para faturas, integramos com Moloni/InvoiceXpress — não emitimos directamente"
- [x] SAFT-PT preparado (estrutura ATCUD já no PDF orçamento, não é fatura)
- [x] NIF + CAE no settings do tenant
- [x] IVA correcto (23% base, ver `10-Compliance-PT.md`)

**Status:** 3/5 🔴 — bloqueador legal

> **CRÍTICO:** Sem isto, qualquer oficina que use RepairDesk para faturar comete infracção. Memory `feedback_certificacao_fiscal_pt.md` confirma. Bruno tem que decidir esta semana se integra Moloni ou só emite orçamentos (cliente fatura noutro lado).

### 4. RGPD
- [x] Privacy-by-design audit feito (`29-Privacy-By-Design-Audit.md`)
- [x] Soft delete + retention
- [ ] **Página de privacidade** (Política RGPD pública)
- [ ] **Direito ao esquecimento** UI — botão "Apagar definitivamente cliente + reparações"
- [ ] **Export de dados** por cliente (Art. 20.º — portabilidade)

**Status:** 2/5 🔴

### 5. Operacional
- [ ] **Health checks** (`/api/health/db`, `/api/health/ready`)
- [ ] **Logs estruturados** com correlation ID
- [ ] **Monitoring básico** (uptime, error rate)
- [ ] **Plano de DR** (Disaster Recovery) — backup + restore testado
- [ ] **CI/CD pipeline** (sprint #C4 Codex em curso)
- [ ] **Documentação para o cliente** (FAQ, vídeos curtos de "como criar primeira reparação", canal de suporte)

**Status:** 0/6 🔴

### 6. Demo readiness
- [x] Tenant demo populado com dados credíveis
- [ ] **Screenshots actualizados** para landing page
- [ ] **Vídeo 90 segundos** "RepairDesk em 1 minuto"
- [x] Domínio configurado (`lopestech.pt` aparenta estar setup)
- [ ] **HTTPS produção** com certificado válido
- [ ] **Email transaccional** (forgot password, convite funcionário) — depende do scaffold de notificações

**Status:** 2/6 🟡

---

## Critérios SHOULD-HAVE (embaraçoso se falhar, não bloqueador)

- [ ] WhatsApp template para "orçamento pronto" + "reparação pronta para levantar"
- [ ] Importação CSV de clientes (existe — confirmado em `Clientes.tsx`)
- [ ] Importação CSV de stock (sprint #C1 Codex)
- [ ] Print de etiquetas (térmica 80mm com QR)
- [ ] Auto-save em formulários longos
- [ ] Atalhos de teclado em listas (Cmd+K command palette)
- [ ] Dark mode coerente em todas as páginas

---

## Critérios NICE-TO-HAVE (diferir até pós-beta)

- IMEI ↔ autoridades (Fase B GSMA — requer subscrição + parcerias)
- Mobile app nativa (PWA chega para beta)
- Multi-loja por tenant
- Marketplace de peças
- IA previsão de avarias
- i18n EN

---

## Diferenciação — o que torna RepairDesk **especial**

Bruno disse: *"isto nao tem nada de diferenciado"*. Critério crítico de beta — pelo menos 2 destes têm de estar funcionais:

| Diferenciador | Estado | Notas |
|---|---|---|
| **Portal cliente público com timeline visual** | ✅ Done | `PortalCliente.tsx` — Apple-style tracking |
| **Garantia digital com QR + página pública** | ✅ Done | `PortalGarantia.tsx` — único no mercado PT |
| **Dashboard financeiro honesto** (Lucro Realizado vs Pendente vs Stock) | ✅ Done | Sprint 14 — diferencia de Excel pessoal |
| **Avaliações + NPS integrado** | ✅ Done | Sprint 24 |
| **Diagnóstico guiado** (templates por dispositivo) | ✅ Done | Sprint 23 — `DiagnosticoGuiado.tsx` |
| **Fotos Antes/Durante/Depois** com visibilidade configurável | ✅ Done | Sprint 29 |
| **Health Score** do equipamento no portal cliente | ✅ Done | `HealthScoreCard` em `PortalCliente.tsx` |
| **IMEI tracking interno** (Fase A) | ✅ Done | Reparacao.Imei + validação Luhn |
| **IMEI cross-tenant alert** (Fase A.5) | ❌ Falta | "Este IMEI já entrou cá antes" |
| **Tabela de preços partilhada com cliente** (`/precos/{slug}`?) | ❌ Falta | Hoje só interno |
| **Integração Moloni/InvoiceXpress** | ❌ Falta | Bloqueador legal |

**Veredicto:** RepairDesk tem **7 diferenciadores reais já funcionais**. Bruno está a ser duro consigo. O problema não é falta de diferenciação — é falta de **comunicação da diferenciação** (landing page, vídeo, screenshots).

---

## Gap analysis — o caminho mais curto até beta

Por ordem de bloqueio:

### Sprint 36-37 (Maio-Junho 2026) — bloqueadores legais e operacionais
1. **Decisão fiscal** + integração Moloni (ou claim explícito "só orçamentos")
2. **Backup automático** + DR runbook
3. **Página privacidade + RGPD UI** (export, eliminar)
4. **Health checks + monitoring básico** (Uptime Kuma ou Better Uptime free tier)
5. **CI/CD verde** (depende #C4 Codex)

### Sprint 38 (Junho 2026) — produto pronto
6. **Onboarding wizard** terminado (depende #C2 Codex)
7. **Audit log** simples
8. **Toasts auditados** em todas as acções
9. **Vídeo demo 90s**
10. **Landing actualizada** com screenshots

### Sprint 39 (Junho/Julho 2026) — primeiros utilizadores
11. **Convidar 1 oficina amiga** (não LopesTech) — beta fechado
12. **Acompanhar 2 semanas** com canal directo (WhatsApp Bruno)
13. **Iterar feedback** crítico
14. **Decidir GA** — abrir para mais oficinas ou voltar a polir

**Estimativa realista:** beta com 1ª oficina externa em **6-8 semanas** (Junho-Julho 2026), dado o ritmo actual + Codex paralelo.

---

## Risco principal: feature creep antes de feedback

Bruno tem tendência (legítima — quer um produto bom) para continuar a polir. Mas:

> **"Feedback real de 1 oficina > 100 iterações sozinho em 5 ficheiros de contexto."**

Sugestão crítica: assim que os MUST-HAVEs estiverem ✅, **lançar beta com a oficina amiga mesmo que SHOULD-HAVEs fiquem por fazer**. Cada semana sem feedback real é uma semana a desenvolver baseado em suposições.

---

## Reflexão crítica

Pontos onde discordo do roadmap actual:

1. **Fiscalidade é mais urgente que UX polishing.** Substituir 3 emojis por icons é satisfatório mas se Bruno lançar beta sem decidir fiscalidade está a montar uma bomba-relógio jurídica. **Priorizar #C4 e decisão fiscal antes de mais UX.**

2. **Backup é boring mas é a coisa #1 que pode matar o produto.** Uma oficina perde 6 meses de fichas → fim do negócio. Implementar mesmo que feio.

3. **A diferenciação está lá — falta é mostrá-la.** Em vez de inventar mais features, **gravar um vídeo 90s** a percorrer o que JÁ existe (timeline portal + garantia QR + health score + fotos antes/depois). Isto não precisa de Codex.

4. **Definir target market mais apertado.** "Oficinas de reparação" é largo. **"Oficinas de reparação de telemóvel em Viseu + concelhos limítrofes, 1-3 funcionários, sem software actual ou só Excel"** — 100% mais accionável. (Ver `09-Customer-Acquisition.md`).

---

## Próxima revisão

Reavaliar este doc após:
- Cada PR Codex aterrar (#C1-#C5)
- Decisão fiscal tomada
- Backup automático em produção

Quando todos os MUST-HAVEs ficarem ✅, marcar **"BETA READY"** no `01-Estado-Actual.md`.
