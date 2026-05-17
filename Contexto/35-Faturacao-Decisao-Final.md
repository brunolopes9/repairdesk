# Decisão Final — Faturação no RepairDesk

Data: 2026-05-17
Estado: **DECIDIDO — Path A (integrar provider externo certificado). Implementação NÃO urgente, mas direcção fechada.**

Este doc fecha de vez a confusão entre **certificação de software** e **comunicação com AT**. Substitui qualquer ambiguidade nos docs `10-Compliance-PT.md` e `21-Certificacao-AT.md`, que ficam como referência técnica.

---

## A confusão raiz (e a clareza)

A AT mistura no mesmo portal dois processos que são **completamente independentes**:

### Camada 1 — Software Certificado (DL 28/2019, art. 4.º n.º 1 b)
*Resposta a: "posso emitir faturas?"*

| Item | Detalhe |
|---|---|
| O que é | Software que **EMITE** documentos fiscais tem de ser certificado pela AT |
| Requisitos técnicos | Cadeia de hashes, assinatura RSA SHA-256 por fatura, ATCUD, SAF-T PT export, anti-tampering, comunicação prévia de séries |
| Processo | Submeter ao AT → auditoria → atribuição de número (ex: `N0235/AT` — Wisedat) |
| Custo aproximado | €5.000-15.000 inicial + manutenção anual + auditorias |
| Tempo | 6-12 meses |
| Onde aparece | Rodapé da fatura: *"Processado por programa certificado N0xxx/AT"* |

### Camada 2 — Webservices AT / e-Fatura
*Resposta a: "como reporto faturas que já emiti?"*

| Item | Detalhe |
|---|---|
| O que é | Webservices SOAP para **COMUNICAR** dados à AT |
| Requisitos | Certificado TLS de Produtor de Software (o que Bruno já tem: `ChaveCifraPublicaAT2027.cer`) |
| Para quê | Submeter faturas, documentos de transporte, inventário, autofaturação |
| Modos | SAF-T mensal · webservice real-time · webservice 4h |
| Quem usa | Software já-emitido. Não emite — só reporta |

**Analogia:** Camada 1 é a carta de condução (precisas para conduzir). Camada 2 é o cartão Via Verde (precisas para passar portagens). Tens Via Verde mas não tens carta → não te deixam passar.

### O caso Wisedat / café dos pais

Quando Bruno clicou "Imprimir fatura" no café:

1. **Wisedat gerou a fatura localmente** (FS AA/18962):
   - Sequência: a série "AA" tinha sido pré-comunicada à AT, recebeu validation code `JJ44H2MV`
   - ATCUD = `JJ44H2MV.18962`
   - Hash da fatura anterior + dados desta + assinatura digital com chave privada Wisedat
   - QR code AT-spec
   - Footer: *"Processado por programa certificado N0235/AT"*
2. **Impressão imediata.** A fatura existe legalmente.
3. **A AT ainda não sabe.** A comunicação acontece depois:
   - SAF-T mensal (até dia 5 do mês seguinte) OU
   - Webservice em tempo real (configurável) OU
   - Webservice 4h (faturas-recibo, regime simplificado)

Por isso quando Bruno vai ao Portal das Finanças e vê a fatura → foi o Wisedat que carregou.

---

## Pergunta-resposta directa

| Pergunta | Resposta |
|---|---|
| Posso integrar APIs AT para emitir faturas? | **Não.** AT não emite faturas. As APIs só recebem o que tu já emitiste. |
| Posso emitir no RepairDesk e depois comunicar à AT? | **Não legalmente.** Fatura emitida por software não-certificado é inválida, mesmo que comunicada. |
| Mas a AT publica APIs! Porquê é que preciso de Moloni? | Porque as APIs AT são **para REPORT**. Moloni **emite** (é certificado) **e** reporta. |
| Posso usar o certificado `ChaveCifraPublicaAT2027.cer` para comunicar? | **Sim** — para webservices AT (consulta NIF, comunicação SAF-T, etc.). Mas não certifica o RepairDesk a emitir. |
| É cedo para criar o meu próprio software de faturação? | **Sim, demasiado cedo.** Mercado saturado. Foco RepairDesk. Reavaliar pós-tracção (sprint 50+, 100 clientes pagantes). |

---

## Decisão tomada: Path A — Integrar provider externo certificado

**Bruno aprovou (2026-05-17):** *"Sim, concordo e quero o path A".*

### Como funciona na prática

```
┌─────────────────┐
│  Cliente final  │
└────────┬────────┘
         │ paga reparação
         ▼
┌─────────────────────────┐
│   RepairDesk (oficina)  │  ← oficina vê tudo aqui
│  - Reparação concluída  │
│  - Marca como Paga      │
│  - Click "Emitir fatura"│
└────────┬────────────────┘
         │ API call (HTTPS + API key)
         ▼
┌─────────────────────────┐
│ Moloni / InvoiceXpress  │  ← certificado AT (N0xxx/AT)
│ (provider escolhido)    │
│  - Gera fatura          │
│  - ATCUD + QR + hash    │
│  - Reporta à AT         │
└────────┬────────────────┘
         │ devolve PDF assinado
         ▼
┌─────────────────────────┐
│ RepairDesk armazena PDF │  ← cliente vê tudo dentro do RD
│ e mostra no portal      │
└─────────────────────────┘
```

**Para o cliente final da oficina:** parece que vem tudo do RepairDesk. O PDF mostra "Processado por Moloni N0xxx/AT" no rodapé (obrigatório legal), mas a maior parte do PDF tem branding da oficina (logo, cores, T&Cs).

**Para a oficina:** cria conta gratuita no Moloni (ou plano €10-30/mês), mete a API key na settings do RepairDesk, e nunca mais entra no Moloni — tudo via RepairDesk.

### Vantagens

| Aspecto | Path A | Path B (certificar próprio) |
|---|---|---|
| Time-to-market | 1-2 semanas | 6-12 meses |
| Custo inicial | €0-30/mês por tenant | €5-15k + dev |
| Manutenção | Provider trata | Bruno trata + AT auditorias |
| Risco legal | Provider absorve | Bruno absorve |
| Diferenciação | Igual a outros SaaS | Forte (vertical certificado) |
| Quando faz sentido | Agora, beta, primeiros 100 clientes | Pós-tracção, com receita justificável |

---

## Que provider escolher

Três sérios candidatos PT certificados:

### **Moloni** (recomendação principal)

| Critério | Score |
|---|---|
| Qualidade API | 🟢 REST, OAuth2, docs OK, sandbox real |
| Cobertura | 🟢 Faturas, V/D, recibos, doc.transporte, SAFT export |
| Pricing | 🟡 Plano Free (50 docs/mês) → Premium 19€/mês → API agressiva |
| White-label | 🟡 Possível em planos pagos (esconder "powered by") |
| Estabilidade | 🟢 7+ anos, milhares de clientes PT |
| Suporte dev | 🟢 Forum activo, exemplos código |

**Endpoints chave:**
- `POST /api/{version}/invoices/insert` — criar fatura
- `POST /api/{version}/invoices/getPDF` — PDF assinado
- `POST /api/{version}/documents/sendByMail` — enviar por email

### **InvoiceXpress** (alternativa)

| Critério | Score |
|---|---|
| Qualidade API | 🟢 REST, API key simples |
| Cobertura | 🟢 Igual ao Moloni |
| Pricing | 🟢 Mais barato que Moloni nos planos baixos (~€10/mês) |
| White-label | 🟢 Mais agressivo (rebranding total em planos pagos) |
| Estabilidade | 🟢 Acquired by Vendus 2021, integração robusta |

### **Vendus** (alternativa)

| Critério | Score |
|---|---|
| Qualidade API | 🟡 REST, mas mais limitada |
| Cobertura | 🟢 |
| Pricing | 🟢 Free tier generoso |
| White-label | 🟡 |

### Decisão técnica

**Recomendado: Moloni**, por:
- API mais madura
- Comunidade dev maior (Stack Overflow + Github examples)
- Sandbox testável sem cartão
- Suporta multi-tenant via API keys distintas

Reavaliar para **InvoiceXpress** se Moloni for caro para o nosso target (oficinas pequenas, 50-100 docs/mês). InvoiceXpress tem free tier mais largo.

---

## Quando implementar

**NÃO é urgente.** Bruno disse: *"é decidido mas não tem que ser já"*.

Critério de quando entra:

- **Não pode entrar antes de #C6 (backup) e #C7 (audit log)** — porque emitir fatura sem backup ou audit é montar bomba-relógio
- **Não precisa entrar para a primeira oficina amiga em beta** se for um arranjo informal (oficina amiga emite no Moloni dela, RepairDesk só faz orçamento)
- **Tem de entrar antes da 2ª/3ª oficina paga** — porque aí o "use o Moloni separadamente" deixa de ser aceitável

**Encaixa em:** Sprint 39-40 (Junho/Julho 2026), depois do bloco operacional (#C6, #C7, #C8) e antes do bloco demo (vídeo + landing).

---

## Prompt Codex preparado (para quando o momento chegar)

```
[CONTEXTO]
RepairDesk SaaS PT. Decisão fiscal tomada: integrar Moloni
(Path A do `Contexto/35-Faturacao-Decisao-Final.md`). Cada tenant
configura a sua própria conta Moloni (API key) e o RepairDesk chama
a API em seu nome para emitir faturas.

[OBJECTIVO]
Quando uma Reparacao ou Trabalho é marcado como pago, ter botão
"Emitir fatura" que chama API Moloni e devolve PDF assinado.

[TAREFAS]
1. Entidade TenantBillingSettings (Provider enum Moloni/InvoiceXpress,
   ApiKey, CompanyId, DefaultDocumentType, DefaultSerie, NumberingSerie)
2. Settings UI em /definicoes — tab "Faturação" com:
   - Selector provider
   - Campo API key (encriptado at-rest com DataProtection)
   - Botão "Testar conexão"
   - Botão "Sincronizar séries" (lista séries disponíveis no Moloni)
3. Service IBillingProvider + MoloniBillingProvider:
   - CreateInvoice(reparacaoId | trabalhoId, paymentMethod, vatRate)
   - GetInvoicePdf(invoiceId)
   - SendByEmail(invoiceId, email)
4. Endpoint POST /api/reparacoes/{id}/emitir-fatura:
   - Valida reparação está paga
   - Chama IBillingProvider.CreateInvoice
   - Armazena InvoiceId + PDF localmente (R2/local)
   - Devolve URL para download
5. UI no detalhe da reparação: botão "Emitir fatura" (só visível se
   TenantBillingSettings está configurado E reparação está paga E
   ainda não tem InvoiceId)
6. PDF preview embeded no UI

[CONSTRAINTS]
- API keys NUNCA logadas
- Encriptação at-rest com IDataProtector (não plain text na DB)
- Erro do Moloni → mostrar ao operador (não engolir)
- NÃO emitir fatura duas vezes (idempotência por reparacaoId)
- Test mode: env var BILLING_SANDBOX=true usa endpoint de teste

[OUTPUT]
- Branch codex/sprint-39-billing-moloni
- 1 migration (TenantBillingSettings)
- 1 controller (BillingController)
- 1 service (MoloniBillingProvider) + tests com mock
- 1 settings tab (frontend)
- 1 button + flow no detalhe Reparacao + Trabalho

[VERIFICAÇÃO]
- Bruno cria conta Moloni sandbox, mete API key
- Marca reparação como paga
- Click "Emitir fatura"
- Aparece PDF assinado com ATCUD + QR
- Verifica em Moloni sandbox que fatura existe lá
```

---

## Comunicação ao cliente final (UI/UX)

Quando a oficina ainda **não tem** Moloni configurado, na settings:

> **Faturação**
>
> *Para emitir faturas legais a partir do RepairDesk, integramos com Moloni — software certificado pela AT (N0xxx/AT).*
>
> *Cria conta gratuita em [moloni.com](https://www.moloni.com) (plano Free permite 50 documentos/mês), copia o API key e cola aqui.*
>
> *Não queres usar Moloni? Podes continuar a usar o teu software actual — o RepairDesk gera orçamentos não-fiscais e tu emites a fatura no software certificado da tua loja.*

Quando **tem** Moloni configurado:

> **Faturação activa via Moloni** ✓
>
> *Próxima fatura emitida na série {AA} — número {18962}. Comunicação à AT automática.*

---

## O que NÃO fazemos

- ❌ **Não emitimos faturas pelo RepairDesk** sem provider certificado. Mesmo "informalmente".
- ❌ **Não chamamos webservices AT para emitir.** Eles não emitem.
- ❌ **Não certificamos o RepairDesk** já. Reavaliar com 100 clientes pagantes.
- ❌ **Não absorvemos o custo do Moloni** para os tenants em beta — eles criam a conta deles (Free tier chega para arrancar).

---

## Revisitar este doc quando

- Bruno tiver 100 clientes pagantes (decidir Path B?)
- Moloni mudar política de API ou pricing
- AT publicar nova regulação relevante
- Aparecer concorrente PT certificado a fazer software para oficinas (verificação competitiva)
