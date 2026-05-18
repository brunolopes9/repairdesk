# Ideias spin-off — Automação como negócio

Data captura: 2026-05-18 · análise + recomendações
Estado: **adiado**. Não tocar até RepairDesk beta validado (Q3 2026).

Este doc preserva uma conversa estratégica com o Bruno sobre se faz sentido lançar um produto B em paralelo com o RepairDesk, focado em automação ("email → extrair fatura → meter em sistema", "chamada → transcrever → CRM", etc.).

---

## A ideia do Bruno (resumida)

1. **Dentro do RepairDesk:** ao receber email com fatura de fornecedor (Tudo4Mobile, BateriasOnline, etc.), o sistema extrai automaticamente os dados e cria a despesa + actualiza stock. Resolve dor real: hoje Bruno tem de copiar manualmente cada compra.

2. **Como spin-off SaaS:** automações vendáveis por €20/mês a vários nichos. Exemplo do Reddit/conhecido: imobiliária recebe chamadas dos consultores, transcrição automática meter no CRM. Bruno vê "potencial de receita recorrente com facilidade de venda".

---

## Análise honesta (a minha)

### Sobre a feature dentro do RepairDesk
✅ Vale a pena. Killer feature, diferenciador, IA torna isto barato em 2026.
❌ **Não agora.** Sprint 50+, após 5-10 oficinas em beta. Razão: killer features retêm clientes que ainda não temos.

**Riscos concretos para o produto:**
- Custo recorrente LLM (~$50/mês com 100 clientes a 50 emails/mês) — afecta pricing
- Setup IMAP/Gmail forward é confuso para oficina não-técnica → horas de suporte
- Variabilidade dos PDFs PT por fornecedor → tuning fino
- False positives (extrai mal) são piores que não fazer nada — quebra confiança

### Sobre o spin-off de automações

**Discordo de o fazer agora.** Razões:

#### 1. Mercado saturado
n8n (€17M funding), Make.com, Zapier ($5B valuation), Pipedream, Bardeen, Activepieces. Todos com IA built-in em 2026. Concorrer head-on sendo solo é suicídio comercial.

#### 2. O que o Bruno descreve é AGÊNCIA, não produto
> *"podia vender a 20€/mês com muitos clientes"*

Matemática só funciona se for **SaaS auto-serve**. Automação cliente-a-cliente customizada = consultoria. 100 clientes × €20/mês = €2k/mês — mas se cada um come 3-4h/mês manutenção = 300-400h. Receita/hora cai para €5-7. **Não escala.**

#### 3. O exemplo da imobiliária é exactamente uma agência
O "gajo" estava a construir **1 automação para 1 cliente**. É um modelo válido se gostas de trabalho cliente-a-cliente, mas é o oposto de SaaS.

#### 4. Foco mata possibilidade
RepairDesk não tem 1 cliente externo ainda. Lançar produto B antes de provar produto A é o erro #1 do founder solo. Cada hora gasta no spin-off é hora não-gasta a fechar o A.

---

## Padrões que ESCALAM (se mesmo assim quiseres entrar em automação)

Casos reais de produtos verticais apertados que funcionam:

| Produto | Vertical | Modelo | Pricing |
|---|---|---|---|
| **Dext / Receipt Bank** | Email → contabilidade | SaaS | €15-30/mês |
| **Auto-Entry (Sage)** | Mesmo nicho | SaaS | Vendido a contabilistas |
| **Tava (PT)** | Gestão de saúde | Vertical SaaS | €50+/mês |
| **Bardeen** | Workflow no browser | Freemium → €30+ |

**Padrão comum:** um vertical, uma dor profunda, um workflow, vendido com obsessão. Não "automação para todos".

---

## Ideias melhores (extracções do RepairDesk)

As melhores ideias B não são desconexas da A — são extracções. Para o Bruno, em ordem decrescente de viabilidade:

### Ideia 1: Whitelabel "Portal cliente" como SDK
- Vende o portal estilo Uber que já tens no RepairDesk como SDK/API
- Verticais possíveis: oficinas auto, cabeleireiros, dentistas, fisioterapeutas
- **Não construas tudo de novo — extrai o que já tens**
- Cobra €30-100/mês por loja consoante volume
- Vantagem: aproveitas 6 meses de trabalho que já está feito

### Ideia 2: Plataforma de garantias digitais standalone
- Tens a feature já feita (Sprint 24)
- Outras categorias: roupa premium, sapatos, electrónica, móveis
- Domain ideia: `garantias.pt` ou similar
- Mercado real, baixa concorrência PT
- Cliente compra produto → scan QR → vê garantia digital permanente
- B2B: cobras à loja €X/mês por X garantias emitidas

### Ideia 3: Vertical "tomada de notas durante chamada"
- Twilio (chamada) + Whisper (transcrição) + LLM (extrair acções)
- Vertical concreto: imobiliárias 5-15 consultores
- Mete no CRM imobiliário (Vincicom, eGO Real Estate)
- Cobra €50-100/mês por consultor
- ICP claro, dor recorrente, valor mensurável
- **Mas é projecto B, não A** — mantém prioridade

---

## Recomendação concreta

### Fazer
1. **Não comeces produto B agora.** Termina RepairDesk beta. 6-8 semanas mais.
2. **Implementa email→fatura DENTRO do RepairDesk** quando Codex voltar — sprint #C9. Testa a tese sem começar produto novo. Vês se as 2-3 oficinas beta usam.
3. **Mantém este doc** como referência. Em Q4 2026, com beta validado, decides com dados:
   - Se "email→fatura" for o que mais usam → confirma automação como núcleo → considera spin-off baseado nos próprios utilizadores RepairDesk
   - Se for "nice but optional" → não vale spin-off, foca RepairDesk

### Não fazer
- ❌ Spawnar produto B com 0 clientes do produto A
- ❌ "Automação genérica adaptável a nichos" — slogan do Zapier
- ❌ Vender €20/mês com setup customizado — matemática não fecha

### Excepção
Se mesmo assim quiseres testar AGORA, faz consultoria leve à margem (sem construir produto):
- 1 cliente real do network (não 10 imaginários)
- €500-1500 one-time + €30-50/mês manutenção
- n8n self-hosted (não construas plataforma)
- 5-10h por cliente
- **Descobres se gostas do modelo** sem investir tempo de produto

---

## Métrica de sucesso para revisitar este doc

**Quando vir Q4 2026 (Outubro), reavaliar com:**

| Pergunta | Resposta = Spin-off viável? |
|---|---|
| RepairDesk tem ≥3 oficinas beta activas? | Sim → considerar |
| Email→fatura é a feature mais elogiada? | Sim → forte sinal |
| Oficinas beta pedem "isto noutro contexto"? | Sim → mercado adjacente real |
| Bruno tem >5h/semana livres do RepairDesk? | Sim → bandwidth para B |

Se 3/4 são "sim" em Outubro 2026, abrir conversa de spin-off.
Se 0-2/4 são "sim", manter foco RepairDesk até 2027.

---

## Frase para fixar

> **"Killer features são construídas para reter clientes, não para os atrair."**

— A minha resposta ao Bruno, 2026-05-18.

A diferenciação que o RepairDesk já tem (portal cliente, garantia QR, dashboard honesto) **não foi ainda comunicada**. Inventar mais features antes de comunicar as actuais é diluir esforço. Vídeo demo 90s + landing decente vai mais longe que feature nova.
