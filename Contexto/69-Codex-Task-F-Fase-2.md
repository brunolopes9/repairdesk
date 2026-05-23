# Prompt Codex Task F — Fase 2 (Implementação Customização Per-Tenant)

**Data:** 2026-05-23 noite
**Base:** Codex Fase 1 entregou `Contexto/67-Customizacao-Audit.md` com 38 áreas. Bruno revê e aprova Fase 2 com shortlist Codex (blocos 1-4 = 35-55h).

**Importante:** este task **deve correr em paralelo** com a Task G (Doc 68 — bugs/segurança/melhorias). Branches separadas evitam conflito.

---

## 🔵 Codex Task F Fase 2 — Implementar shortlist customização

**Branch:** `codex/sprint-234-tenant-preferences`

```
Doc 67 (`Contexto/67-Customizacao-Audit.md`) tem audit completo + shortlist
recomendada. Implementa os blocos 1-4 da shortlist com a arquitectura
proposta pelo Codex na secção "Modelo de implementacao sugerido".

ARQUITECTURA OBRIGATÓRIA:

1. Criar entity `TenantPreferences` (1:1 com Tenant) com:
   - `Guid Id` (PK)
   - `Guid TenantId` (FK, unique index)
   - `int Version` (para migrations de JSON schema)
   - `string PreferencesJson` (string NOT NULL com defaults — ver abaixo)
   - timestamps auditoria padrão

2. Migration EF Core: criar tabela vazia + seed default em todos os
   tenants existentes (UPDATE com defaults).

3. Criar `ITenantPreferencesService`:
   - `Task<TenantPreferences> GetAsync(CancellationToken ct)`
     - Cache em memória per-tenant (invalidate on Save)
     - Lê JSON e materializa para tipo C# strongly-typed
     - Se inexistente para tenant, cria com defaults
   - `Task UpdateAsync(TenantPreferences prefs, CancellationToken ct)`
     - Valida defaults
     - Persiste JSON + bump Version se schema mudar
     - Invalida cache
     - Audit log da mudança

4. Tipo strongly-typed (records) por grupo:

   ```csharp
   public record TenantPreferencesRoot(
       CommunicationPrefs Communication,
       PortalPrefs Portal,
       RepairsPrefs Repairs,
       SalesPrefs Sales);

   public record CommunicationPrefs(
       bool WhatsAppEnabled,           // default true (Bruno usa hoje)
       Dictionary<string, WhatsAppStateTemplate> TemplatesByState,
       WhatsAppRepeatMode RepeatMode,  // default Sempre (comportamento actual)
       int StaleDaysThreshold,         // default 7
       PushPrefs Push);

   public record WhatsAppStateTemplate(
       bool Enabled,
       string Texto,           // suporta variáveis {{cliente}}, {{equipamento}}
       int Order);

   public enum WhatsAppRepeatMode { Sempre, UmaVez, MarcarManualmente }

   public record PushPrefs(
       bool Enabled,
       string[] EstadosPermitidos);  // default todos os estados não-internos

   public record PortalPrefs(
       bool MostrarFotos,        // default true
       bool MostrarDiagnostico,  // default true
       bool MostrarOrcamento,    // default true
       bool MostrarGarantia,     // default true
       bool MostrarTimeline,     // default true
       bool MostrarAvaliacao,    // default true
       bool PermitirAprovarOrcamento,  // default true
       int GoogleReviewMinScore,  // default 4
       string? GoogleReviewUrl);

   public record RepairsPrefs(
       EntregarMarcaPagoMode EntregarMarcaPago,  // default Sim (comportamento actual)
       GarantiaAutoMode GarantiaAutomatica);     // default Sim

   public enum EntregarMarcaPagoMode { Sim, Perguntar, Nao }
   public enum GarantiaAutoMode { Sim, Perguntar, Nao }

   public record SalesPrefs(
       string DefaultMetodoPagamento,    // default "MBWay"
       int DefaultCondicaoArtigo,        // default 0 (Novo)
       EmitirFaturaMode EmitirFatura,    // default Perguntar
       GarantiaAutoMode VendaGarantia);  // default Sim

   public enum EmitirFaturaMode { Nunca, Perguntar, Automatico }
   ```

   Os defaults devem **manter LopesTech a funcionar exactamente como hoje**.

5. UI em `/definicoes/preferencias` (nova página):
   - Tabs por grupo: Comunicação, Portal Cliente, Reparações, Vendas
   - Cada tab tem auto-save (Sprint 117 pattern) + spinner + ✓ verde 2s
   - Botão "Restaurar defaults Mender" por grupo (com confirmação)
   - Mensagem ao guardar: "Aplicado a partir de agora" (não retroactivo)

6. Refactor onde aplicável para USAR as preferências:

   - **WhatsApp templates (Block 1.1-1.4):**
     - `frontend/src/lib/whatsapp/templates.ts` lê `prefs.Communication.TemplatesByState`
     - `frontend/src/components/WhatsAppMenu.tsx` filtra por `EstadoActivo`
     - Adicionar `WhatsAppNotificationLog` entity para modo "UmaVez"
       (POST quando user clica wa.me; bloqueia se repeat=UmaVez já enviado)
     - `staleDaysThreshold` substituí hardcoded `7`

   - **Push notifications (Block 2.1):**
     - `ReparacaoService.NotifyEstadoChangedAsync` verifica `prefs.Communication.Push.Enabled`
     - Filtro `EstadosPermitidos`
     - Texto custom por estado se Push.TitleByState etc

   - **Portal cliente visibility (Block 2.1):**
     - `PublicPortalService.GetByPublicSlugAsync` aplica filtros antes de retornar
     - Se `!MostrarFotos`, devolve `[]`
     - Idem outros toggles

   - **Aprovação online (Block 2.1):**
     - Endpoint `/portal/orcamento/aprovar` retorna 403 se `!PermitirAprovarOrcamento`

   - **Reparações automatismos (Block 3.1):**
     - `ReparacaoService.MarcarEntregueAsync` lê `prefs.Repairs.EntregarMarcaPago`
       - `Sim`: comportamento actual
       - `Perguntar`: response inclui flag `precisaConfirmacaoPagamento=true`
       - `Não`: skip alteração pagamento
     - `prefs.Repairs.GarantiaAutomatica` idem

   - **POS/Vendas (Block 4.1):**
     - `Vendas.tsx` POS usa `prefs.Sales.DefaultMetodoPagamento` para inicializar
     - `prefs.Sales.DefaultCondicaoArtigo` idem
     - `prefs.Sales.EmitirFatura` decide UX (modal vs auto vs nunca)
     - `prefs.Sales.VendaGarantia` idem

7. Testes (mínimo 12 tests):
   - 4× Service: Get cria defaults, Update persiste, cache invalida, JSON inválido reseta
   - 2× WhatsApp: template editado renderiza correctamente, UmaVez bloqueia 2ª vez
   - 2× Portal: filter Mostrar* esconde dados
   - 2× Repairs: EntregarMarcaPago Sim vs Não muda comportamento
   - 2× Sales: DefaultMetodoPagamento aplica + EmitirFatura modes

PITFALLS:
- Defaults têm de manter LopesTech a funcionar IGUAL (regressão silenciosa = bug crítico)
- TenantPreferences entity precisa de TenantId index unique
- NÃO criar coluna NOT NULL sem default
- Designer.cs autogerado em migration — não editar
- Auto-save UI tem de invalidar cache do backend (pode fazer GET após PATCH para garantir)
- Migration EF Core: seed defaults em UPDATE para tenants existentes
- WhatsAppNotificationLog: TenantId index, soft-delete pattern (IsDeleted), filtro multi-tenant
- Multi-tenant: ITenantContext em todas queries
- Money em cents, datas em UTC

NÃO IMPLEMENTAR (deixar para Fase 3):
- PDFs customizáveis (Bloco 5 — só footer custom)
- Stock janelas/defaults (Bloco 6)
- Loja online toggles
- Shop AI persona

ENTREGÁVEIS:
- Branch codex/sprint-234-tenant-preferences com commits separados:
  - Commit 1: TenantPreferences entity + migration + service base
  - Commit 2: WhatsApp customization (Comunicação tab + WhatsAppNotificationLog)
  - Commit 3: Portal cliente toggles
  - Commit 4: Reparações automatismos (Entregar marca pago + garantia)
  - Commit 5: Vendas/POS defaults
  - Commit 6: UI /definicoes/preferencias completa (4 tabs)
  - Commit 7: Tests

- Após cada commit, dotnet build + dotnet test verde antes do próximo.

- Não fazer push para main directo. Bruno revê PR/branch antes de merge.

Estimativa Codex Fase 1: 35-55h. Plano para 1 sessão grande ou 2-3
sessões.

Sprints relacionados (para contexto):
- 117 (auto-save Faturação) — replicar UX pattern
- 165 (UI Automações) — referência de layout
- 175 (retention configurável per-tenant) — exemplo de prefs simples
- 184 (regra fornecedor) — exemplo de override per-tenant
- 121-128 (garantia parametrizada) — já existe campo equivalente
```

---

## Como mandar ao Codex

1. Copia o bloco entre triple-backticks acima
2. Cola no Codex Cloud
3. Diz "executa em paralelo com Task G — branches separadas"

Codex deve entregar 7 commits incrementais. Bruno revê e merges branch quando aprovar.
