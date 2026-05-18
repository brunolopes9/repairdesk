# Rebrand mapping — onde aparece "RepairDesk"

Data: 2026-05-18

Per `Contexto/37-Insights-Mercado-Reddit.md`, "RepairDesk" colide com software comercial US. Este doc lista **onde o nome aparece** para facilitar o rebrand quando o nome novo for decidido.

Estado: **decisão de nome ainda não tomada**. Este mapeamento é prep work.

---

## Categorias de impacto

### 🟢 Camada 1 — visível ao utilizador final (substituir = rebrand efectivo)

Mudar estes 7 sítios torna o produto rebrand-ado para o cliente.

| Ficheiro | Linha | Contexto |
|---|---|---|
| `frontend/src/pages/Login.tsx` | 50 | Logo card no login (texto principal) |
| `frontend/src/pages/Login.tsx` | 123 | Copyright footer |
| `frontend/src/components/Layout.tsx` | 101 | Header sticky do app |
| `frontend/src/components/Layout.tsx` | 180 | Sidebar logo |
| `frontend/src/pages/OnboardingWizard.tsx` | 398 | Header do wizard |
| `frontend/src/pages/legal/LegalLayout.tsx` | 20 | Header das páginas legais |
| `frontend/src/pages/PortalCliente.tsx` | 139 | Footer "Gerado pelo RepairDesk" — **visível ao cliente final** |
| `frontend/src/pages/PortalGarantia.tsx` | 127 | Footer "Gerado pelo RepairDesk" — **visível ao cliente final** |

**Total: 8 alterações** numa search-and-replace bem dirigida.

### 🟡 Camada 2 — copy legal substantivo (substituir + revisão)

Conteúdo legal usa o nome do produto. Substituir mas verificar coerência.

| Ficheiro | Ocorrências | Notas |
|---|---|---|
| `frontend/src/pages/legal/Termos.tsx` | 11 | Contrato comercial — copy importante |
| `frontend/src/pages/legal/PoliticaPrivacidade.tsx` | 8 | Política RGPD pública |
| `frontend/src/pages/legal/Cookies.tsx` | 1 | Texto introdutório |
| `frontend/src/components/CookieBanner.tsx` | 1 | Comentário interno (não afecta UI) |

**Total: ~21 ocorrências** em conteúdo legal. Pode ser sed simples mas verificar caso a caso.

### 🟠 Camada 3 — branding técnico (decidir caso a caso)

| Item | Onde | Decisão |
|---|---|---|
| **GitHub repo** | `github.com/brunolopes9/repairdesk` | Renomear repo → URL muda. GitHub mantém redirect 1 ano. |
| **Domínio público** | `repairdesk.lopestech.pt`? (planeado) | Usar o nome novo desde o início |
| **README.md** | Topo, badges, secções | Substituir |
| **Docker image names** | `repairdesk-api`, `repairdesk-web` | Manter ou renomear. **Pouco visível** se for produção interna. |
| **Container names** | `repairdesk-api`, `repairdesk-db`, etc | Manter. Só visível em logs/admin. |
| **Backend namespace** | `RepairDesk.Core`, `RepairDesk.API`, etc | **NÃO mudar.** Refactor de namespace é XL effort sem valor. Fica como código interno. |
| **DB name** | `RepairDesk` (em ConnectionString) | Manter — só visível para DBA. |
| **`package.json`** | `"name": "repairdesk-frontend"` | Substituir |

### 🔵 Camada 4 — `Contexto/` (docs internos)

39 docs em `Contexto/` mencionam RepairDesk. **Não há urgência** — são docs internos. Quando o nome novo estiver decidido, fazer 1 passagem global com sed e marcar como migrados.

Excepções (pseudo-públicos se publicados):
- `34-Beta-Launch-Criteria.md` — para investidores/sócios
- `35-Faturacao-Decisao-Final.md` — para advogado/contabilista
- `36-Video-Demo-Script.md` — para a equipa de gravação

---

## Sugestão de processo de rebrand

Quando o nome novo (`{NOVO}`) for decidido:

### Fase 1 — Decisão (1 dia)
1. Shortlistar 3 nomes em meeting com Bruno
2. **Verificar disponibilidade:**
   - `.pt` (registo.pt)
   - `.app`, `.com`, `.io` (whois / Namecheap)
   - EUIPO trademark search (free, ~2 min)
   - Reddit / Google search por colisão
3. Escolher 1 + 1 backup

### Fase 2 — Implementação (2-3h)
1. Branch nova: `git checkout -b rebrand/{NOVO}`
2. **Camada 1 (8 alterações):** search-and-replace `RepairDesk → {NOVO}` nos 8 sítios listados acima
3. **Camada 2 (21 ocorrências):** search-and-replace no conteúdo legal, depois ler para garantir que faz sentido (ex: "o {NOVO} é prestado pela LopesTech" deve fluir)
4. **Camada 3:**
   - `README.md` — actualizar título, badges, copy
   - `package.json` frontend — `name` field
   - Manter image/container/namespace nomes (avoid rabbit hole)
5. **Camada 4:** sed em Contexto/ — global passagem mas confirmar 35-Faturacao e 34-Beta antes de publicar
6. Build + lint verde
7. Visual smoke test: Login, Dashboard, Portal, Definições, Páginas legais

### Fase 3 — Comunicação (1 dia)
1. Renomear repo GitHub (mantém redirect)
2. **Não anunciar** se ainda não houver beta launch — silently
3. Quando beta launch, comunicar nome final + mensagem honesta sobre rebrand

### Fase 4 — DNS + branding visual (depois do beta)
1. Apontar `{novo}.lopestech.pt` (ou `{novo}.pt` standalone)
2. Logo / favicon novo (Codex Brand Design System `26-Brand-Design-System.md` pode ajudar)
3. Atualizar landing page

---

## Notas estratégicas

- **Domain availability driver:** se `{NOVO}.pt` estiver tomado, considerar 2ª opção. Preferir nome ".pt-clean" sobre .com / .app — target market é PT.
- **Não escolher nomes que pareçam "open source"** ou "scrappy" — produto cobra €30+/mês. Soa profissional.
- **Não escolher nomes com numbers** (`Repair2`, `Oficina2025`) — datado.
- **Test pronunciation** em chamada — Bruno vai dizer o nome a clientes ao telefone.

### Shortlist inicial para discussão

| Nome | Domain .pt | Conotação | Trademark check |
|---|---|---|---|
| `Bancada` | a verificar | "bancada de trabalho" — perfeito vertical | a verificar |
| `Ficha` | provavelmente livre | "ficha de reparação" — termo PT do balcão | a verificar |
| `Repara` | provavelmente livre | verbo PT directo | a verificar |
| `Oficina.app` | livre | descritivo | a verificar |
| `FixDesk` | a verificar | mantém "Desk" sufixo | a verificar |

Nenhum é decisão. Bruno + um amigo de marketing decidem.
