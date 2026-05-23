# Prompt Codex — Bugs, Segurança, Melhorias, Funcionalidades

**Data:** 2026-05-23 noite
**Pedido Bruno:** "Manda trabalho ao Codex — melhorias, questões de bugs, correções de erros, segurança, melhorar o que já há, melhorar funcionalidades, aumentar funcionalidades etc"

Codex deve atacar este prompt em **paralelo** com a Fase 2 do Doc 66 (customização per-tenant). Cada task aqui é independente.

---

## 🔵 Codex Task G — Auditoria + Fixes ampla

**Branch:** `codex/sprint-233-improvements`

Lê o codebase e ataca **3 frentes** em ordem:

### Frente 1 — BUGS / SEGURANÇA (~3-5h)

#### G1.1 — Admin user com password seed por defeito (CRÍTICO)
- Logs mostram: `WRN Admin user already exists with DEFAULT seed password — change it.`
- File: `backend/src/RepairDesk.DAL/Persistence/AppDbContext.cs` (provavelmente em `SeedAsync`).
- Fix: ao detectar password = "ChangeMe!2026", forçar `RequireChangePasswordOnNextLogin = true` no AppUser (se campo não existe, criar).
- UI: ao login, se flag true → redirect `/auth/change-password` antes de qualquer rota.
- Não deixar dashboards inacessíveis (loop) — endpoint de change-password deve ser sempre acessível com JWT válido.

#### G1.2 — EF Core SQL spam em logs Development
- File: `backend/src/RepairDesk.API/appsettings.Development.json`
- Hoje: `Microsoft.EntityFrameworkCore: "Information"` → SQL queries em logs.
- Mudar para:
  ```json
  "Microsoft.EntityFrameworkCore": "Warning",
  "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
  "Microsoft.EntityFrameworkCore.Infrastructure": "Warning"
  ```
- Manter `Microsoft.EntityFrameworkCore.Database.Connection: "Warning"` (não Information).
- Logs ficam só com Information/Warning/Error funcionais, sem SELECTs.

#### G1.3 — Webhook polling muito frequente (5s?)
- Logs mostram polls SELECT WebhookDeliveries a cada 5s.
- File: `backend/src/RepairDesk.API/Webhooks/WebhookDeliveryHostedService.cs`
- Verificar interval. Se 5s, mudar para 15s ou 30s. Mais que 100 polls/min é overkill para 0 deliveries pendentes.
- Bonus: skip query se `await _db.WebhookDeliveries.Where(...).AnyAsync(ct)` é false primeiro (cheap query).

#### G1.4 — Rate limit no endpoint /api/auth/login (anti brute-force)
- File: `backend/src/RepairDesk.API/Controllers/AuthController.cs`
- Verificar se existe `[EnableRateLimiting]` no endpoint.
- Se não, adicionar policy `auth-strict` em `Program.cs` com 5 tentativas / 15min por IP. Identity já tem lockout do user mas IP-level também é importante (atacante pode brute-force vários users).

#### G1.5 — CORS aberto em Development
- Verificar `Program.cs` para política CORS. Se Development usa `AllowAnyOrigin/Header/Method` sem credentials, está OK.
- Em Production deve ser restrito a `Frontend:BaseUrl`.
- Confirmar que appsettings.json + Production têm a allowlist correcta.

#### G1.6 — Verify storage backup R2 fallback
- File: `backend/src/RepairDesk.API/Backups/R2BackupStorage.cs`
- Se R2 credentials vazias, backup local ainda corre? Confirmar fallback graceful (não exception).

### Frente 2 — MELHORAR O QUE EXISTE (~4-6h)

#### G2.1 — Dashboard "Reparações paradas há > 7 dias"
- File: `backend/src/RepairDesk.Core/Abstractions/IDashboardRepository.cs` (`AlertasSnapshot` record)
- Adicionar campo `ReparacoesParadas: IReadOnlyList<ReparacaoParadaRow>` onde Row = (id, numero, cliente, equipamento, estado, diasParado).
- Filtra reparações com `estadoSince < now - 7 days` AND estado in {OrcamentoAprovado, EmReparacao, AguardaPecas}.
- Frontend: card no Dashboard zone "Alertas" mostrando count + lista clicável.

#### G2.2 — Filtro "Em garantia activa" em /clientes
- File: `backend/src/RepairDesk.API/Controllers/ClientesController.cs`
- Query param `?garantiaActiva=true` filtra clientes com pelo menos 1 garantia activa (data fim > now AND !anulada).
- UI: checkbox no header da página /clientes.

#### G2.3 — Histórico de envio WhatsApp (log per-reparação)
- Hoje WhatsApp abre wa.me e perde-se. Bruno não sabe se enviou X mensagem.
- Criar `WhatsAppNotificationLog` entity (id, tenantId, reparacaoId, vendaId, estadoEnviado, texto, enviadoAt).
- POST `/api/whatsapp/log` (frontend chama quando user clica botão wa.me).
- Mostrar na reparação detalhe: "Última mensagem WhatsApp: 'Olá Sergio...' há 3 dias".
- Migration EF Core com TenantId filter.

#### G2.4 — Validação NIF inline no formulário cliente
- Já existe `frontend/src/lib/nif/validator.ts` mas só usado em ClienteForm. Audit outros sítios:
  - Vendas POS modal cliente novo
  - OnboardingWizard
  - Definições/Empresa (NIF do tenant)
- Adicionar feedback inline "NIF inválido" em todos.

#### G2.5 — Comando "Marcar todas as faturas pagas como cobradas" (bulk action /vendas)
- Bruno tem várias vendas com status "Entregue" mas "NaoPaga" porque facturou Moloni mas esqueceu marcar pago.
- Adicionar checkbox "Marcar como pago" em bulk em /vendas filtrado por "facturadas mas não pagas".

#### G2.6 — Polish: indicador visual de "guardar a alterar..." em /definicoes
- Auto-save (Sprint 117) é silencioso. Bruno não tem feedback.
- Mostrar spinner pequeno ao lado do campo enquanto está a guardar, depois ✓ verde 2s.

### Frente 3 — NOVAS FUNCIONALIDADES (~6-10h)

#### G3.1 — Lembrete saúde bateria 6 meses pós-venda
- Doc `Contexto/RepairDesk_Novas_Ideias.md` mencionou: upsell natural.
- Backend HostedService que corre diário:
  - Query Vendas com IMEI Phone vendidas há ~180d
  - Cria notificação push (Sprint #C15 já tem infra) OU email se tenant tem provider
- Configurável per-tenant: on/off, dias (180/365/etc).

#### G3.2 — Agendamento online básico (/agendar)
- Página pública `/agendar/{tenant-slug}` onde cliente escolhe data+hora para entregar equipamento.
- Tenant configura horário (8h-18h, fechado domingos) em Definições.
- Slots de 30min. Bloqueia se já tem appointment.
- Entity `Agendamento` (id, tenantId, clienteId|null, nome, telefone, data, hora, equipamento, notas, status).
- Bruno aprova/rejeita no /reparacoes nova-tab "Agendamentos".

#### G3.3 — Auto-import preços fornecedor
- Doc braindump UX mencionou: Utopya, LCPhones, Tudo4Mobile, alrossio.
- Sem fazer web scraping (CAPTCHA + ToS):
  - Endpoint `POST /api/suppliers/{fornecedorId}/import-prices` aceita CSV com colunas: brand, model, descricao, preco
  - UI em `/definicoes/fornecedores/{id}` permite upload CSV
  - Cria entries em `SupplierPriceEntry` entity
  - Em Reparação, ao criar peça, mostra "Preço médio fornecedor X: €Y" como hint

#### G3.4 — Filtrar logs de auditoria por entidade + intervalo de tempo
- /auditoria já tem filtros mas o de intervalo é limitado.
- Adicionar query params `?fromDate&toDate&entityType` no endpoint.
- UI: date range picker + dropdown entityType.

---

## REGRAS DE EXECUÇÃO

1. **Cada Frente em commit separado** para Bruno poder reverter facilmente.
2. **Tests obrigatórios:** mínimo 1 test backend por entity nova / endpoint novo.
3. **Migrations:** seguir padrão (Sprint X — DescricaoCurta), Designer.cs autogerados.
4. **Multi-tenant:** TODAS as queries com ITenantContext filter.
5. **Money em cents.** Datas em UTC.
6. **Não tocar em:**
   - Namespaces .NET (`RepairDesk.*`)
   - Webhook headers `X-RepairDesk-*` (back-compat)
   - `DatabaseName="RepairDesk"` (DB connection)
   - `DataProtection "RepairDesk.Billing.Secrets.v1"` (cripto namespace)
   - Migrations existentes (só adicionar novas)
7. **Memória RepairDesk pitfalls:**
   - Designer.cs nas migrations são autogerados — não editar
   - Duplicate `[Migration]` attribute — não acrescentar
   - Volumes Docker sem chown — Permission denied (Sprint anteriores tiveram)
   - NÃO usar `docker compose down -v` (Bruno tem dados reais)
8. **Não usar `--no-verify` em commits.**

---

## ORDEM SUGERIDA

1. Frente 1 (bugs/segurança) — **first**, são quick wins de quality.
2. Frente 2 (melhorias) — quando Bruno aprovar Frente 1.
3. Frente 3 (novas features) — last, pode esperar.

Codex devolve commits + sumário por frente. Bruno revê e mergeia conforme apropriado.

---

## NOTAS

- Doc 65 (`Contexto/65-Changelog-Autonomous-Session.md`) tem registo do que Claude fez sozinho. Codex pode referir.
- Doc 67 (`Contexto/67-Customizacao-Audit.md`) tem o audit que Codex fez na Fase 1. Cuidado para não duplicar.
- Branch atual main já tem Sprints 199-232. Codex sai da main.
