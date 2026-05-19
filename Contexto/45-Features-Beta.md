# Features beta — Vendas/POS, Push notifications, Relatório IVA

Documentação operacional das 3 features mais recentes (Codex #C12, #C15, #C16).

Audiência: tu (Bruno, para teste end-to-end) + futuros beta users.

---

## 1. Vendas / POS (Sprint 43 — #C12)

### Quando usar

Quando vendes algo **avulso, sem ser reparação**:
- Acessório (capa, película, cabo, carregador)
- Peça solta (bateria, ecrã)
- Telemóvel novo ou usado revendido
- Qualquer artigo do stock

Para reparações continuas a usar a página **Reparações**.

### Como usar

1. Vai a **Vendas** no menu lateral
2. Clica **Nova venda**
3. Selecciona cliente (ou deixa em branco para B2C sem NIF)
4. Adiciona itens:
   - Procura peça no stock por nome ou SKU
   - Quantidade
   - Preço (auto-puxa do stock, podes editar)
5. Total calcula automático com IVA
6. **Marca como Paga** quando recebes
7. **Emite fatura via Moloni** → fatura aparece no e-Fatura em 24h

### Comportamento

| Acção | Efeito |
|---|---|
| Adicionar item à venda | Reserva mentalmente, não decrementa stock |
| Marcar como Paga | Cria movimento de stock `VendaCliente` (decrementa stock) |
| Emitir fatura | Chama Moloni `simplifiedInvoices/insert` ou `invoices/insert` |
| Cancelar venda | Devolve ao stock (cria movimento `Devolucao`) |

### Estados

```
Rascunho → Paga → (fatura emitida) → Concluída
                ↓
              Cancelada
```

### Diferença vs Trabalhos

| | Reparação | Trabalho | Venda |
|---|---|---|---|
| Trabalho manual | ✅ (técnico) | ✅ (vário) | ❌ |
| Peças do stock | ✅ | ✅ | ✅ |
| Equipamento próprio do cliente | ✅ (IMEI) | ⚠️ opcional | ❌ |
| Garantia automática | ✅ | ❌ | Só de fábrica |
| Tempo médio | dias | semanas | minutos |

---

## 2. Push Notifications PWA (Sprint 46 — #C15)

### Quando usar

Cliente final quer ser notificado quando o estado da reparação muda — sem ter de fazer F5 no portal nem ligar à oficina.

### Como o cliente activa

1. Cliente abre o portal: `https://app.repairdesk.pt/portal/{slug}` (ou local: `localhost/portal/{slug}`)
2. No topo aparece banner **"Receber notificações no telemóvel"**
3. Cliente clica
4. Browser pede permissão (notificação nativa)
5. Cliente aceita → fica subscrito

A partir daí, sempre que o operador muda o estado da reparação:
- Cliente recebe notificação push no browser/telemóvel
- Notificação tem o título "Reparação iPhone 12 — Pronta para levantar"
- Click na notificação abre o portal

### Stack técnico (Web Push standard)

- VAPID keys configuradas em `appsettings.json` ou env vars
- `Push__VapidPublicKey` (público, exposto no frontend para subscribe)
- `Push__VapidPrivateKey` (privado, fica no server)
- `Push__Subject` (email de contacto, exigido pelo standard)
- TTL e retention configuráveis

### Geração de VAPID keys

```bash
# Gerar par de chaves (uma única vez, antes de produção)
docker run --rm gcr.io/elektron-it/web-push-codelab vapid-key
```

Output: `publicKey` + `privateKey`. Cola em `.env` (ou `.env.production`):
```
Push__VapidPublicKey=BLkqx_xxxxxxxxxxxxxx
Push__VapidPrivateKey=PpqR4_yyyyyyyyyyyyyy
Push__Subject=mailto:suporte@repairdesk.pt
```

### Limitações

- **Safari iOS:** suporte a Web Push só desde iOS 16.4 e exige PWA instalada como app (não basta abrir no Safari)
- **Browsers desktop:** Chrome, Edge, Firefox suportam directamente
- **Sem permissão:** se cliente rejeita, podes pedir de novo só após uns dias (browser limita)

### Retention RGPD

- Subscriptions de reparações **Entregues há > 30 dias** são auto-purgadas
- Cliente pode revogar a qualquer momento (botão "Cancelar notificações" no portal)
- Apenas o `endpoint` URL e `keys` ficam guardados (sem dados pessoais)

---

## 3. Relatório IVA trimestral (Sprint 47 — #C16)

### Quando usar

Antes de submeteres a tua declaração IVA trimestral à AT, queres **conferir** os valores que o Moloni vai exportar via SAF-T.

Como? Vês o cálculo do RepairDesk (DB própria) e comparas com o relatório oficial Moloni. Se há divergência, investigas onde.

### Como aceder

1. Menu lateral → **Relatórios**
2. Subpágina **IVA**
3. Selecciona ano (default: actual) + trimestre (T1/T2/T3/T4)
4. Vês KPIs:
   - **Receita s/IVA** (base tributável)
   - **IVA liquidado** (a entregar à AT)
   - **IVA suportado** em compras intra-UE (auto-liquidação reverse charge — preenches manualmente)
   - **IVA a pagar** (= liquidado − suportado)
5. Tabela detalhada por documento (cada Reparação/Trabalho/Venda com fatura emitida)
6. Botões **Exportar CSV** / **Exportar PDF**

### Cálculo

O RepairDesk filtra:
- `InvoiceEmittedAt` dentro do trimestre
- `TenantId` = teu
- Apenas documentos NÃO cancelados

Para cada documento:
- **Base** = `PrecoFinalCents / (1 + IVA%/100)`
- **IVA** = `PrecoFinalCents − Base`

Se regime fiscal = **Isenção art. 53**: IVA = 0 em todos os documentos. Útil para confirmar que estás abaixo do limite €15.000/ano.

### Reverse charge intra-UE

Aplica-se quando compras a fornecedor europeu com VAT válido (ex: Molano da Polónia). Pagas IVA na declaração e deduzes ao mesmo tempo (resultado líquido zero, mas tens de declarar).

No relatório, o campo **IVA suportado** é manual: tu somas o IVA das compras intra-UE do trimestre e metes aí. O RepairDesk não importa automaticamente facturas de fornecedores (Sprint futuro).

### Não substitui o Moloni

Importante: este relatório é **operacional**, não é a fonte fiscal autoritativa. A declaração à AT usa os dados do Moloni (SAF-T mensal automático).

Para que serve então:
- Sanity check antes de fechar mês/trimestre
- Detectar discrepâncias entre o que emitimos e o que está no Moloni
- Ter base para a coluna "IVA a pagar" do contabilista

---

## Próximos passos para os beta users

| Quando | Acção |
|---|---|
| **Setup inicial** | Liga Moloni + auto-configura IDs (43-Moloni-Setup-Beta-Users.md) |
| **Primeiro dia** | Cria uma reparação, marca como Paga, emite fatura via Moloni |
| **Primeira semana** | Configura push notifications no portal cliente (gera VAPID keys) |
| **Final do mês 1** | Abre Relatório IVA, compara com o que o Moloni mostra |
| **Final do trimestre** | Exporta CSV, envia ao contabilista, submete declaração à AT |

---

## FAQ

**Q: Posso ter Vendas e Reparações para o mesmo cliente?**
A: Sim. Tudo aparece no histórico do cliente.

**Q: O cliente final tem acesso à página de Vendas?**
A: Não. Vendas é só admin. Cliente só vê reparações no portal.

**Q: As notificações push funcionam offline?**
A: A notificação chega ao dispositivo via Web Push (assíncrono, mesmo se o browser está fechado). Mas clicar nela só funciona com internet (abre o portal).

**Q: Quanto custa enviar notificações push?**
A: Zero. Web Push standard é gratuito (mensagens passam pelo browser vendor, não pelo nosso server).

**Q: O relatório IVA pode estar errado?**
A: Só se houver fatura no Moloni que não foi registada no RepairDesk (ex: emitida manualmente fora do RepairDesk). Aí o cálculo do RepairDesk sub-conta. Sempre confere contra o relatório oficial Moloni.
