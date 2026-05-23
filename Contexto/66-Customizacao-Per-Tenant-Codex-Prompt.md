# Prompt Codex — Auditoria + Implementação de Customização Per-Tenant

**Data:** 2026-05-23
**Pedido Bruno:** "Permitir bastante customização nas definições do Mender." WhatsApp templates é exemplo: nem todos querem enviar mensagens automáticas. Tem de haver definição:
- liga/desliga totalmente
- escolhe templates próprios (tenant edita texto)
- escolhe em que estados envia (ex: só quando muda para "Pronto")
- "sempre" vs "uma vez"
- etc.

Bruno quer este tipo de customização **para tudo o que seja de preferência pessoal**.

---

## 🔵 Codex Task F — Audit + Implementação Customização Per-Tenant

**Branch:** `codex/sprint-230-tenant-customization`

```
Bruno pediu (2026-05-23): tornar Mender altamente customizável per-tenant.
WhatsApp templates é o exemplo concreto, mas o pedido é mais amplo —
tudo o que possa ser preferência pessoal deve ser configurável em
/definicoes.

FASE 1 (AUDIT — 30min de leitura, sem código)

Lê o codebase e identifica TODAS as features que hoje têm comportamento
hardcoded mas que diferentes tenants podem preferir diferente. Para cada,
documenta em Contexto/67-Customizacao-Audit.md:

1. Nome da feature
2. Localização do código (ficheiro:linha)
3. Como funciona hoje (default hardcoded ou config global?)
4. Que customização Bruno provavelmente quer (templates, on/off, regras)
5. Esforço estimado em horas
6. Prioridade (alta/média/baixa)

Sugestões iniciais de áreas a auditar (não limitativo):

- WhatsApp templates (já existem em TenantSettings? — verificar)
  - Estados em que envia
  - Texto custom por estado
  - On/off global vs por cliente
  - "Enviar uma vez" vs sempre

- PDF Orçamento/Garantia/Recibo Venda
  - Logo, cor primária, footer custom — TenantSettings.Brand fields?
  - Termos e condições editáveis
  - IBAN/contactos no footer (provavelmente já existe — confirmar)
  - Linguagem (pt-PT vs pt-BR, raro mas técnico)

- Garantia
  - Período por condição artigo (já parametrizado Sprint 127-128 — confirmar)
  - Cobertura/exclusões texto (mostrar default vs editar)

- Faturação
  - Auto-emit fatura no momento de pagamento (sim/não)
  - Bulk-emit threshold (qty de vendas para sugerir)
  - Default provider (Moloni vs InvoiceXpress vs PDF próprio)

- Stock
  - QtdMinima default por categoria
  - Reabastecer days window default (hoje 30d hardcoded?)
  - Alertas push quando stock baixo (on/off)

- Diagnóstico
  - Templates default por DeviceCategory
  - Skip diagnóstico em reparações triviais (toggle)

- Reparações
  - Auto-criar Garantia ao Entregar+Pago (sim/não)
  - Dias até considerar "parado" (hoje hardcoded)
  - Notificar cliente quando muda estado (sim/não por estado)

- Vendas
  - Default condição artigo (Novo vs Usado vs Recondicionado)
  - Garantia automática vs perguntar a cada venda
  - Apertura POS auto: cliente Consumidor Final default

- Portal Cliente
  - Mostrar/esconder fotos antes/depois
  - Mostrar/esconder orçamento
  - Permitir aprovar orçamento online (sim/não)

- Notificações
  - Push notifications: quais estados disparam
  - Email transaccional: ainda não existe? (futuro)

- Loja online
  - Webhooks: quais eventos enviar (hoje já editável Sprint 161)
  - Default mostrarLojaOnline em novos produtos

- Brand/UI
  - Tema (light/dark/sistema) — já está per-user (Sprint 102), mas per-tenant?
  - Cor primária custom — já em TenantSettings?
  - Logo upload — provavelmente já

FASE 2 (IMPLEMENTAÇÃO — depois da auditoria, com aprovação Bruno)

Para cada feature de prioridade alta:

1. Adicionar campo em TenantSettings entity (se ainda não existe)
2. Migration EF Core com defaults backward-compatible
3. Service helper para ler com fallback
4. UI em /definicoes section apropriada
5. Test backend que valida override toma efeito
6. Doc Contexto/ com explicação do toggle

PITFALLS (memória RepairDesk):
- NÃO criar coluna NOT NULL sem default — migrations Codex já partiram
  containers antes (memória feedback_codex_bugs_recorrentes)
- Migration Designer.cs files são autogerados — não editar
- Multi-tenant: usar ITenantContext em todas as queries
- LopesTech é tenant #1 (Bruno) — defaults devem mantê-lo a funcionar
  exactamente como hoje
- Auto-save preferido (Sprint 117 polish) em vez de botão "Guardar"

ENTREGÁVEIS FASE 1:
- Contexto/67-Customizacao-Audit.md com tabela completa
- Estimativa total de horas
- Recomendação Codex sobre ordem de implementação

NÃO IMPLEMENTAR código em Fase 1 — só ler + documentar. Bruno aprova
shortlist antes de Fase 2.

Sprints relacionados:
- 102 (theme per-user)
- 117 (auto-save Faturação)
- 121-128 (garantia parametrizada)
- 161 (webhook subscriptions UI editável)
- 165 (UI automações)
- 175 (retention policy configurável per-tenant)
- 184 (regra fornecedor stock/despesa)
```

---

## Como mandar ao Codex

1. Copia o bloco entre triple-backticks acima
2. Cola no Codex Cloud
3. Diz "executa Fase 1 primeiro, espera aprovação para Fase 2"

Quando Codex devolver `Contexto/67-Customizacao-Audit.md`, Bruno revê e aprova shortlist.
