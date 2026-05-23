# Audit de Rebrand "RepairDesk" — preparação técnica

**Data:** 2026-05-23
**Driver:** Doc 37 (Reddit Insights) identificou colisão de nome com produto US estabelecido. Rebrand é única acção bloqueante para beta pago.

---

## Sumário do audit

Pesquisa `grep -rn "RepairDesk"` no codebase (excluindo `.git`, `node_modules`, `bin`, `obj`):

| Camada | Ficheiros | Ocorrências | Natureza |
|---|---|---|---|
| Frontend (`.tsx`, `.ts`, `.html`) | 23 | ~73 | User-facing strings |
| Backend código | 795 | **~19,540** | Quase tudo namespaces |
| Configs (docker, Caddy, deploy) | ~5 | ~15 | URLs + service names |
| Docs `Contexto/` | ~20 | ~150 | Documentação interna |

**Insight crítico:** 99% das ocorrências backend são **namespaces .NET** (`namespace RepairDesk.Core`, `using RepairDesk.Services.X`) — invisíveis para utilizador. **Não bloqueiam rebrand público.**

---

## Estratégia faseada

### Fase A — User-facing (URGENTE, antes de beta pago)

Tudo o que o cliente VÊ. Substituir "RepairDesk" pelo nome novo nos seguintes locais:

#### A.1 Strings em frontend (`.tsx`)
- `frontend/index.html` (title, meta description) — 3 ocorrências
- `frontend/src/pages/Login.tsx` — 2 (logo + heading)
- `frontend/src/components/Layout.tsx` — 2 (sidebar title)
- `frontend/src/components/CookieBanner.tsx` — 1
- `frontend/src/pages/OnboardingWizard.tsx` — 1
- `frontend/src/pages/PortalCliente.tsx` — 1
- `frontend/src/pages/PortalGarantia.tsx` — 1
- `frontend/src/pages/produtos/Produtos.tsx` — 1
- `frontend/src/pages/relatorios/Iva.tsx` — 1
- `frontend/src/pages/reparacoes/ReparacaoDetalhe.tsx` — 1
- `frontend/src/pages/vendas/Vendas.tsx` — 4
- `frontend/src/pages/definicoes/*.tsx` — ~19
- `frontend/src/lib/webhooks/api.ts` — 1

**Total: ~37 ocorrências user-facing.** Trabalho: ~1h (find-and-replace + revisão).

#### A.2 Documentos legais (alto impacto RGPD)
- `frontend/src/pages/legal/PoliticaPrivacidade.tsx` — 11
- `frontend/src/pages/legal/Termos.tsx` — 14
- `frontend/src/pages/legal/Dpa.tsx` — 5
- `frontend/src/pages/legal/SubProcessors.tsx` — 1
- `frontend/src/pages/legal/Cookies.tsx` — 1
- `frontend/src/pages/legal/LegalLayout.tsx` — 1

**Total: ~33 ocorrências.** Trabalho: ~30min. **Atenção:** rever que LopesTech (entidade legal) fica intacta; muda só o nome do **produto**.

#### A.3 PDFs gerados (Garantia, Recibo, Orçamento)
- `backend/src/RepairDesk.Services/Documents/GarantiaPdfRenderer.cs`
- `backend/src/RepairDesk.Services/Documents/OrcamentoPdfService.cs`
- Possíveis footers "Gerado por RepairDesk" / logos
**Trabalho:** ~20min. Buscar strings literal e mudar.

#### A.4 Emails (se houver)
Buscar templates de email enviados a clientes. **Trabalho:** ~15min.

**Fase A total estimado: 2-3h.**

---

### Fase B — Infraestrutura pública

#### B.1 Domínio
- `repairdesk.lopestech.pt` (Caddyfile, deploy/hetzner/Caddyfile.app.lopestech.pt)
- Decisão: continuar `repairdesk.lopestech.pt` (subdomain) ou novo `{rebrand}.lopestech.pt`?
- Se rebrand for nome standalone: registar `{rebrand}.pt` + apontar DNS.

#### B.2 Service names docker
- `docker-compose.yml`: `repairdesk-api`, `repairdesk-web`, `repairdesk-db` — só naming, sem efeito user.
- Decisão pragmática: **manter "repairdesk-" como prefix interno** (não justifica trabalho de rename).

#### B.3 .env.example
- 1 ocorrência. ~1min.

**Fase B total: ~30min se manteres subdomain; ~2h se mudares domain (DNS + Caddy + certificates).**

---

### Fase C — Interno (não bloqueante)

#### C.1 Namespaces .NET (~19,540 ocorrências em 795 ficheiros)
- `namespace RepairDesk.Core` em todas as entidades
- `using RepairDesk.X` em todos os controllers/services
- Solution file `RepairDesk.slnx`
- Project files `RepairDesk.*.csproj`

**Decisão pragmática:** **deixar namespaces como estão.** Razões:
1. Não tem impacto user (invisível).
2. Rename via IDE (Rider/Visual Studio) afecta 795 ficheiros — risco de regressão.
3. Suite de 263 testes valida comportamento, não nomes.
4. Database schema migrations referem `RepairDesk` apenas em comentários `using RepairDesk.DAL.Migrations`.

**Quando mudar:** apenas se rebrand muito forte (vendas para terceiros / open-source) justificar consistência. Em fase 1 (Bruno + 5-10 lojas PT), não há razão.

#### C.2 Docs `Contexto/`
- 20 ficheiros mencionam "RepairDesk" como nome do produto.
- Trabalho: ~1h de find-and-replace + revisão.
- **Decisão:** actualizar a par com Fase A (não bloqueia mas convém para consistência interna).

---

## Plano de execução recomendado

### Pré-rebrand (decisão de Bruno)
1. Shortlistar 3 nomes candidatos.
2. Verificar `.pt` disponíveis (NIC.pt search).
3. EUIPO trademark search (eSearch plus) para confirmar zero colisão na UE.
4. Logo provisório (pode ser texto simples até design system Sprint).

### Sprint Rebrand (2-3h trabalho meu, 1 sessão)
1. Bruno fornece o nome final.
2. Eu faço:
   - Find-and-replace user-facing strings (Fase A.1 + A.2 + A.3 + A.4)
   - Smoke test páginas legais + PDFs
   - Atualizar docs `Contexto/` (Fase C.2 — só nome do produto, manter LopesTech intacto)
   - Commit + rebuild + deploy
3. Test manual:
   - Login → ver logo/title novos
   - Página Termos / Privacidade / DPA → texto novo
   - Gerar PDF garantia → footer novo
   - Portal cliente → branding novo

### Post-rebrand
4. Decidir se subdomain muda (`{rebrand}.lopestech.pt`) ou mantém `app.lopestech.pt`.
5. Domain registration `{rebrand}.pt` se quiseres standalone.
6. Migrate-CNAME se mudares domínio.

---

## Sugestões de nomes (já no Doc 37)

Doc 37 propôs:
- *Oficina.io / oficina.pt*
- *Bancada* (.pt)
- *Reparo*
- *FixDesk*
- *Banc.pt*
- *FichaPro / Ficha*

**Recomendação minha:** evita "Desk" no nome (FixDesk, RepairDesk) — confusão com competidor. Preferir nome PT-friendly:
- **Bancada** — directo, técnico, brand-able
- **Ficha** — "ficha de reparação" é jargão real PT
- **Reparo** — verbo, simples
- Compostos: **Oficina.cloud**, **Bancada.pt**, **Ficha.io**

Confirma disponibilidade antes de comprometer.

---

## Riscos identificados

1. **Search/replace cego** pode partir testes que validam namespaces ou audit logs com a string "RepairDesk". Mitigação: replace **só em strings literais user-facing**, não em código.
2. **Custom fields em equipamento** (Sprint 41) tem labels customizáveis por tenant. Se Bruno já usou "RepairDesk" como label, não é tocado pelo rebrand do código — fica como dado.
3. **Audit log histórico** tem entries com "RepairDesk" no `EntityType` ou `Notes`. **Não tocar** (audit é imutável por design).
4. **Cookie consent** — se mudar nome de produto mas LopesTech for o controller RGPD, o nome do controller mantém-se. Verificar texto consent.

---

## Próximo passo

Bruno: decide nome → eu executo Fase A num único sprint (~2h, suite verde mantida).

---

## Execução do rebrand (2026-05-23 noite)

### Tentativa 1: "Reparo" (Sprint 222) → falhou
- Find-and-replace executado em frontend + backend user-facing strings.
- Bruno descobriu **`reparo.pt` já registado** por terceiro.

### Decisão final: **Mender** (Sprint 223)
- sed `Reparo → Mender` em todos os files do Sprint 222.
- Domain a verificar: `mender.pt` / `mender.io` / `mendersuite.com`.

### Resultado
- Frontend: 23 ficheiros `.tsx`/`.ts`/`.html` actualizados.
- Backend user-facing: 5 ficheiros (Program.cs swagger, VendaPdfService, GarantiaPdfRenderer, OrcamentoPdfRenderer, VendaService Moloni reasons).
- Suite: 263 testes passing. Build: 0 warnings, 0 errors.
- Mantidos intencionalmente (Sprint 64 estratégia):
  - Namespaces `.NET` `RepairDesk.*` (~19k ocorrências) — invisíveis user.
  - Webhook headers `X-RepairDesk-*` — breaking change para integradores.
  - `DatabaseName`, `DataProtection ApplicationName` — partiam DB/tokens.
  - `LopesTech` (entidade legal) intacto.

### Pendentes
1. Bruno: registar `mender.pt` ou alternativa + EUIPO trademark search.
2. Quando domain pronto: actualizar Caddyfile + DNS.
3. Sprint 224 actualizar docs `Contexto/` restantes.
