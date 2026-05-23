# Changelog sessão autónoma — 2026-05-23 noite

Bruno está a trabalhar na loja online em paralelo e pediu que eu continuasse a fazer melhorias/features sozinho. Documento tudo aqui para ele rever e testar depois.

**Estado base ao começar esta sessão autónoma:**
- Sprints 199-228 concluídos.
- Rebrand RepairDesk → Mender feito (user-facing).
- 263 testes passing. Build 0 warnings.

---

## Sprints feitos sozinho nesta sessão

### Sprint 227 — Botão WhatsApp no Portal Garantia + fix bug normalização PT
**Ficheiros:** `frontend/src/pages/PortalGarantia.tsx`, `frontend/src/pages/PortalCliente.tsx`
- Portal Garantia ganhou botão verde "💬 WhatsApp" a par do botão "📞 Telefone".
- Pre-popula mensagem `Olá <loja>, sobre a minha garantia <slug>`.
- **Bug fix em PortalCliente:** número PT "912345678" (9 dígitos sem indicativo) ia para `wa.me/912345678` em vez de `wa.me/351912345678`. WhatsApp não aceitava. Agora prefixa `351` se número tem 9 dígitos puros.
- **Testar:** abre `/g/<slug>` → vê botão WhatsApp; clica → abre WhatsApp Web com mensagem pré-preenchida.

### Sprint 228 — Clarificar badges Diagnóstico (braindump UX)
**Ficheiro:** `frontend/src/components/DiagnosticoGuiado.tsx`
- Memória `feedback_repairdesk_ux_braindump_2026-05-20` reportou "N/T" confuso.
- Badges de resumo passam de `N/T / OK / ✕ Avaria / ◐ Marginal` para labels expandidas: `Não testado / ✓ OK / ✕ Avariado / ◐ Marginal`.
- Tooltips title em cada badge explicando o significado.
- **Testar:** Reparação detalhe → secção Diagnóstico → ver badges com labels completas.

---

### Sprint 229 — Botão "Copiar link cliente" no detalhe da reparação
**Ficheiros:** `frontend/src/lib/reparacoes/types.ts`, `frontend/src/pages/reparacoes/ReparacaoDetalhe.tsx`
- Backend já devolvia `publicSlug` no DTO mas frontend type não tinha.
- Adicionei `publicSlug: string | null` ao type `Reparacao`.
- Detalhe ganhou botão "📋 Copiar link cliente" que copia `{origin}/r/{slug}` para clipboard.
- Útil para Bruno copiar e enviar via WhatsApp/SMS sem ter de procurar.
- **Testar:** abre reparação → clica botão → toast "Link copiado!".

### Sprint 230 — Prompt Codex para customização per-tenant (Doc 66 criado)
**Ficheiros:** `Contexto/66-Customizacao-Per-Tenant-Codex-Prompt.md`
- Bruno mencionou: WhatsApp templates devem ser opt-in + configuráveis per-tenant (texto, estados, on/off). Mas o pedido é geral — **tudo** que possa ser preferência pessoal.
- Criei prompt Codex completo com Fase 1 (audit) + Fase 2 (implementação).
- Bruno: copia o bloco do Doc 66 para Codex quando puder.

---

## Próximas tarefas planeadas (vou fazer enquanto Bruno trabalha)

- [ ] Editar cliente da reparação (consumidor final → cliente com NIF a meio do trabalho)
- [ ] Auto-detect categoria equipamento ou esconder se redundante
- [ ] Lembrete saúde bateria 6m após venda (upsell natural)
- [ ] Verificar warnings/errors em runtime
- [ ] Mais polish UX baseado em memória

Cada um terá entrada própria abaixo quando feito.
