# Prompts para delegar ao Codex (e outros agentes)

O Bruno disse:
> *"caso queiras delegar algo ao codex tem que se dar um prompt bastante forme para ele perceber"*

Este ficheiro tem **templates prontos** para copiar/colar quando queremos delegar tarefas a Codex (ou outro IA agent). Cada prompt segue uma estrutura que comprovadamente reduz alucinação e aumenta qualidade:

1. **Contexto** — quem somos, onde estamos, qual é o estado
2. **Objectivo** — o que queremos no fim
3. **Constraints** — o que NÃO pode fazer
4. **Outputs esperados** — formato concreto
5. **Verificação** — como vamos saber que está bem

---

## Template Universal — preencher antes de delegar

```
[CONTEXTO]
- Projeto: RepairDesk SaaS — backoffice para oficinas de reparação (PT)
- Stack: .NET 10 + EF Core 10 + SQL Server (backend), React 19 + Vite + Tailwind v4 + React Query 5 + React Router 7 (frontend), Docker Compose para orquestrar
- Multi-tenant Clean Architecture (Domain, Services, Persistence, API)
- Repo: github.com/brunolopes9/lopestech (este projecto está em RepairDesk/)
- Fundador: Bruno Lopes (LopesTech, Viseu, NIF 263758141) — dogfooding na própria loja
- Regime fiscal: Isenção Art. 53 do CIVA
- Concorrentes principais: RepairDesk (Lahore), RO App (UK/PL), BytePhase

[ESTADO ACTUAL]
- {{descrever o que já está implementado relevante para a tarefa}}

[OBJECTIVO]
{{frase curta clara do que queremos}}

[REQUISITOS FUNCIONAIS]
1. {{...}}
2. {{...}}

[CONSTRAINTS]
- NÃO criar novos endpoints sem documentar OpenAPI
- NÃO mudar interfaces públicas de services sem confirmar
- NÃO commitar credenciais ou .env
- NÃO usar bibliotecas pagas sem aprovação
- Code style: existente no projeto (segue convenções actuais)
- Português nativo nos labels UI (não tradução automática)
- Manter compatibilidade com multi-tenancy (TenantId em todas as queries)

[OUTPUT ESPERADO]
- Lista de ficheiros alterados/criados (paths absolutos)
- Diff resumido por ficheiro
- Comando de teste para validar
- Migration EF Core (se mexer em entidades)

[VERIFICAÇÃO]
- `docker compose up -d` arranca sem erros
- `dotnet test` passa
- `npm run build` na frontend passa
- Funcionalidade descrita no objectivo é alcançada
```

---

## Prompt #1 — Análise de pricing dos concorrentes (task #56)

```
[CONTEXTO]
Estamos a construir um SaaS de gestão para oficinas de reparação focado em
Portugal. Concorrência principal:
- RepairDesk (Lahore, $99/user/mês + add-ons pagos)
- RO App (UK/Polónia, €15-69/mês)
- BytePhase ($499/mês — premium nicho)
- Repairshopr, AutoLeap, Tekmetric (auto repair, US)
- Reparo (Kossano, gratuito offline) — referência de UX simples

Mais detalhes em Contexto/02-Concorrentes.md (lê primeiro este ficheiro).

[OBJECTIVO]
Investigar e propor estrutura de pricing para o nosso SaaS pensando no
mercado português, considerando:
- Salário mínimo PT 2026 = €870
- Lojas pequenas têm 1-3 funcionários
- IVA 23% aplicável (vamos no Isenção Art. 53 até €15k anuais)
- Sensibilidade a "preço por user" vs "preço por loja"

[REQUISITOS]
1. Propor 3 tiers (Starter / Pro / Enterprise) com:
   - Preço €/mês
   - Limites (tickets, utilizadores, lojas, storage)
   - Features incluídas vs add-ons
2. Comparar com RO App e RepairDesk (tabela markdown)
3. Estimar payback (CAC vs LTV) para cada tier
4. Identificar 2-3 "killer features" que justificam upgrade entre tiers
5. Sugerir política de descontos (anual vs mensal, ONG, multi-loja)
6. Sugerir trial / freemium policy

[CONSTRAINTS]
- NÃO inventar dados — se não souberes preço actual de concorrente, marca {{TODO confirmar}}
- NÃO copiar pricing de concorrente, propor pensado para PT
- Tier mais barato deve ser viável para loja com 30 reparações/mês
- Não usar dark patterns (sem trial que cobra automaticamente)

[OUTPUT]
- Ficheiro markdown em Contexto/07-Pricing-Proposta.md
- Secção "Riscos" no fim com 3 cenários (preço baixo demais / alto demais / errado)

[VERIFICAÇÃO]
- Bruno revê e decide; se pricing é defensável vs RO App, está bom
```

---

## Prompt #2 — Refactor estados da reparação (mais granular)

```
[CONTEXTO]
Backend: RepairDesk/backend/src/RepairDesk.Core/Enums/RepairStatus.cs
Estados actuais (enum int):
- Orcamento = 0
- Recebido = 1
- Diagnostico = 2 (Diagnóstico)
- Reparacao = 3 (Em Reparação)
- Pronto = 4 (Reparado)
- Entregue = 5
- Cancelado = 6

State machine actual em ReparacaoService.cs (método mudarEstado).
Tem timeline log (ReparacaoEstadoLog) que regista de→para+timestamp.
Tem 3-tier lock: Aberto / Frozen (Concluído NãoPago) / Locked (Concluído Pago).

[ESTADO ACTUAL]
Falta granularidade. Lojas precisam de distinguir:
- "Estou à espera de peça encomendada" vs "Estou a diagnosticar"
- "Cliente aprovou orçamento" vs "Aguardo aprovação"
- "Cancelado pelo cliente" vs "Cancelado por incompatibilidade técnica"

[OBJECTIVO]
Refactorar para state machine mais expressiva sem partir dados existentes
nem complicar UX demais.

[REQUISITOS]
1. Propor novo enum com:
   - Orcamento_Pendente (à espera de aprovação do cliente)
   - Orcamento_Aprovado
   - Orcamento_Recusado
   - Recebido
   - Diagnostico
   - Aguarda_Peca  ← NOVO
   - Em_Reparacao
   - Reparado
   - Pronto_Para_Entrega ← NOVO (cliente avisado)
   - Entregue
   - Cancelado (com sub-razão: cliente, técnico, fornecedor)
2. Definir transições válidas (matriz)
3. Migration EF Core que mapeia valores antigos para novos sem perder dados
4. Atualizar ReparacaoService.MudarEstadoAsync com validação
5. Atualizar frontend (badges, filtros, dashboard widget "Em Curso")
6. Atualizar testes unitários e integração

[CONSTRAINTS]
- NÃO partir migrations existentes (forward-only)
- NÃO assumir que enum int values mudam (manter retro-compat se possível)
- Estados visíveis ao cliente no portal público são subset (não mostrar
  "Diagnostico" mas sim "Em análise")
- Cancelado mantém row, NÃO apaga (soft delete via BaseEntity)
- Manter timeline log compatível (EstadoFrom / EstadoTo são int)

[OUTPUT]
- Diff de RepairStatus.cs
- Migration nova (Add-Migration RefactorRepairStatusGranular)
- Diff de ReparacaoService.cs
- Diff de frontend (badges, mapping, filtros)
- Testes novos/alterados

[VERIFICAÇÃO]
- dotnet test passa
- docker compose down && up arranca DB
- Reparação existente #3 (Xiaomi Redmi Note 11 Pro) mantém estado correcto
- Frontend mostra badges com cores distintas
```

---

## Prompt #3 — Portal cliente público (QR code, Uber-style)

```
[CONTEXTO]
Backend tem ReparacaoService com método GetAsync(id, ct). Hoje só
authenticated user da tenant correcta pode ver.

Queremos portal público:
- Cliente recebe QR code (impresso no ticket de entrada) ou link curto
  (lopestech.pt/r/AB12CD por exemplo)
- Abre e vê estado da reparação em tempo real, estilo Uber Eats
- Não precisa de login (segurança via slug curto não-adivinhável)
- Pode aprovar/recusar orçamento se houver
- Pode fazer perguntas (chat simples? talvez fase 2)

[OBJECTIVO]
Implementar portal cliente público funcional.

[REQUISITOS]
1. Backend:
   - Adicionar campo `PublicSlug` (string 8 chars alfanuméricos) em Reparacao
   - Gerar slug no Create (validar unicidade)
   - Endpoint público GET /api/public/repair/{slug} → DTO reduzido (sem
     custos internos, sem dados de outras reparações do cliente)
   - Endpoint público POST /api/public/repair/{slug}/orcamento → accept/reject
   - Rate limit (10 req/min por IP)
   - Sem JWT — anonymous endpoint
2. Frontend:
   - Nova rota /r/{slug} no React Router (componente PortalCliente.tsx)
   - Design Apple-like: device foto, timeline visual com estados, ETA
   - Linguagem clientFriendly ("Estamos a diagnosticar" não "Diagnostico")
   - Botão WhatsApp para "Falar com a loja"
   - Mobile-first (90% utilizadores vão abrir no telemóvel)
3. QR code:
   - Gerar QR no PDF do orçamento (QuestPDF + QRCoder ou similar)
   - URL: {baseUrl}/r/{slug}

[CONSTRAINTS]
- Slug NÃO sequencial (não revela total de reparações da tenant)
- DTO público NÃO retorna: custos internos, peças usadas, notas técnicas,
  outras reparações do cliente, IBAN/NIF
- Rate limiting OBRIGATÓRIO (sem isso é vector de scraping)
- Endpoint público NÃO retorna estado se reparação > 2 anos (compliance)
- Logs de acesso ao portal (auditoria)

[OUTPUT]
- Backend: novo controller PublicRepairController, novo DTO, migration
- Frontend: nova rota, página PortalCliente.tsx
- QR code integrado no PDF de orçamento
- Testes integração para endpoint público
- README atualizado com rota /r/{slug}

[VERIFICAÇÃO]
- Bruno cria reparação na sua loja, vê QR no PDF
- Abre URL no telemóvel sem login → vê estado
- Endpoint privado continua a funcionar
- Rate limit testado (curl 11x dispara 429)
```

---

## Prompt #4 — Dashboard financeiro reestruturado (task #54)

```
[CONTEXTO]
Dashboard actual em frontend/src/pages/Dashboard.tsx mostra um número
"Lucro Mensal" que mistura:
- Receita de reparações pagas
- Investimento em peças (mesmo as ainda em stock)

Resultado: aparece -312€ enganador quando o Bruno compra peças para stock.

Bruno é CAE 62100 + secundários 47401, 58290, 95101, 95102.
Tem várias categorias:
- Reparações (Reparações)
- Websites (Trabalhos categoria=Web)
- Software (Trabalhos categoria=Software)
- Junta (parcerias)
- Hardware (revenda)
- Serviços (consultoria)

[OBJECTIVO]
Reestruturar dashboard financeiro para mostrar verdade contabilística:
separar receita realizada de investimento em stock e mostrar breakdown por
categoria.

[REQUISITOS]
1. Backend — novo DTO DashboardFinanceiroDto:
   - LucroRealizado (receita - custos imputados a reparações já pagas)
   - InvestimentoStock (peças compradas ainda não usadas em reparações pagas)
   - ReceitaPendente (reparações concluídas não pagas)
   - PorCategoria: { Reparacoes, Websites, Software, Junta, Hardware, Servicos }
     - Cada categoria: { Receita, Custo, Lucro }
   - Período: mês actual + comparação com mês anterior
2. Lógica de imputação:
   - Custo de peça = só quando reparação é Paga
   - Custo geral (rent, luz, internet) = imputar pro-rata mês (fase 2)
3. Frontend:
   - 3 cards principais lado-a-lado: Lucro Realizado / Investimento Stock / Pendente
   - Gráfico bar horizontal por categoria
   - Toggle mensal/anual
   - Sem scroll infinito — tudo above-the-fold em 1080p
4. Não partir páginas existentes (lista de reparações, etc.)

[CONSTRAINTS]
- NÃO mostrar números enganadores (preferir "nada para mostrar" se sem dados)
- NÃO usar moeda diferente de EUR
- Multi-tenant: cada tenant vê só os seus números
- Performance: query agregada com 1 round-trip à DB (sem N+1)

[OUTPUT]
- Backend: DashboardService, DTO, endpoint, testes
- Frontend: refactor Dashboard.tsx + novo componente FinanceiroCards.tsx
- Screenshot do antes/depois (anexar PR)

[VERIFICAÇÃO]
- Reparação #3 (Xiaomi) está paga → conta no Lucro Realizado
- Peça em stock não usada → conta em Investimento, não em Lucro
- Bruno valida com a sua contabilidade interna
```

---

## Prompt #5 — Análise crítica de feature antes de implementar

Template para pedir ao Codex segunda opinião antes de meter mão à massa:

```
[CONTEXTO]
RepairDesk SaaS para oficinas. Estado actual descrito em
Contexto/01-Estado-Actual.md (lê primeiro).

[O QUE QUERO IMPLEMENTAR]
Feature: {{descrever em 1-2 frases}}

[O MEU PLANO]
1. {{passo 1}}
2. {{passo 2}}
...

[O QUE TE PEÇO]
Antes de implementar, dá-me análise crítica:
1. Esta feature resolve dor real? (consulta Contexto/03-Dores-Reais.md)
2. Há concorrente que já faz isto bem? Como? Como podemos ser melhores?
3. Que riscos vês na minha abordagem? (técnicos, UX, negócio)
4. Há alternativa mais simples que tenha 80% do valor com 20% do esforço?
5. Há feature pré-requisito que devíamos fazer antes?
6. Como medimos sucesso? Que métrica vamos olhar daqui a 3 meses?

[CONSTRAINTS NA TUA ANÁLISE]
- NÃO concordas só para concordares — discorda se vires problema
- Cita evidências de dores reais quando justificares
- Propõe alternativas concretas, não vagas
- Considera viés pessoal do Bruno (ele acabou de sair do emprego, anda
  excitado, pode querer construir tudo de uma vez)

[OUTPUT]
- Recomendação clara: AVANÇAR / PIVOT / ADIAR / REJEITAR
- Justificação em 5-10 bullets
- Se PIVOT/ADIAR, proposta alternativa
```

---

## Prompt #6 — Code review pré-commit

```
[CONTEXTO]
Vou commitar estas mudanças no branch {{nome}}:
{{git diff --stat}}

[REVIEW REQUEST]
Analisa critically:
1. Há bugs óbvios?
2. Há security issues? (SQL injection, XSS, auth bypass, secrets em logs)
3. Há quebra de multi-tenancy? (TenantId ausente em query)
4. Há quebra de soft-delete? (.IgnoreQueryFilters() usado sem necessidade)
5. Há performance issues? (N+1, query sem index, full table scan)
6. Há regressão potencial? (mudança quebra algo noutro lado)
7. Há testes a faltar? Quais?
8. Há comentário/log que revele dados sensíveis?

[CONSTRAINTS]
- NÃO sugiras refactor cosmético se não for crítico
- NÃO peças mais testes só por pedir — diz que cenário falta
- Cita ficheiro:linha quando fizeres comentário

[OUTPUT]
- BLOCKER (não commitar): {{lista}}
- IMPORTANT (commitar mas corrigir já): {{lista}}
- NICE-TO-HAVE (próximo PR): {{lista}}
- LGTM se nada relevante
```

---

## Ordem recomendada de delegação (atualizada 2026-05-16, tarde)

Estado actual (2026-05-16, fim do dia):
- ✅ **#7 SAF-T + ATCUD + DL 28/2019** — `10-Compliance-PT.md`
- ✅ **#9 Customer Acquisition B2B PT** — `09-Customer-Acquisition.md`
- ✅ **#10 Templates WhatsApp** — `11-WhatsApp-Templates.md`
- ✅ **#11 Onboarding wizard UX** — `12-Onboarding-Wizard.md`
- ✅ **#12 IMEI ↔ Autoridades** — `13-IMEI-Autoridades.md`
- ✅ **#13 Storage de fotos** — `14-Storage-Fotos.md`
- ✅ **#14 WhatsApp Business API provider** — `15-WhatsApp-Provider.md`
- ✅ **#15 Compliance RGPD** — `16-Compliance-RGPD.md`
- ✅ **#16 Hosting + Deployment** — `17-Hosting-Deployment.md`
- ✅ **#17 Backup + DR runbook** — `18-Backup-DR.md`
- ✅ **#18 Monitoring + Alertas** — `19-Monitoring.md`
- ✅ **#19 Modelo de Suporte** — `20-Suporte-Cliente.md`
- ✅ **#20 Certificação AT operacional** — `21-Certificacao-AT.md`
- ✅ **#21 Tabela de preços PT reparações** — `22-Tabela-Precos-PT.md`
- ✅ **#22 Estratégia jurídico-fiscal pessoal** — `23-Plano-Fiscal-Pessoal.md`
- ✅ **#23 PWA + Offline strategy** — `24-PWA-Offline.md`
- ✅ **#24 Distribuidores de peças PT/EU** — `25-Distribuidores-Pecas-PT.md`
- ✅ **#25 Brand + Design System + Naming** — `26-Brand-Design-System.md`
- ✅ **#26 Plano de testes automatizados** — `27-Plano-Testes.md`
- ✅ **#27 Performance & Caching strategy** — `28-Performance-Caching.md`
- ✅ **#28 Privacy by Design audit técnico** — `29-Privacy-By-Design-Audit.md`
- ✅ **#29 Release + Versioning + Changelog público** — `30-Release-Strategy.md`

**Prompts ainda não delegados/adiados:**

| Ordem | Prompt | Output | Porquê |
|---|---|---|---|
| ⚫ adiar | **#8 Pagamentos SaaS** | `08-Pagamentos-Comparacao.md` | Só quando começarmos a cobrar (6+ meses). |

Razão: infra, DR, monitoring, suporte, certificação AT, preços de reparação, plano fiscal pessoal, PWA/offline, distribuidores, marca, testes, performance/cache, privacy by design e release management já ficaram documentados. Pagamentos SaaS só faz sentido quando houver cobrança real.

Próxima delegação útil:

| Ordem | Prompt | Porquê fazer já |
|---|---|---|
| 🟢 **1.º** | **#8 Pagamentos Stripe/Mollie/Easypay/SIBS** | Só faz sentido quando começarmos a cobrar SaaS. Pode esperar até haver beta estável e intenção clara de pagamento. |

Razão: antes de pagamentos, o risco maior ainda é operação estável, backups testados, performance medida, releases controladas e clientes beta a usar o produto.

---

## Prompt #7 — Research SAFT-PT + ATCUD + Decreto-Lei 28/2019 (compliance fiscal PT)

```
[CONTEXTO]
RepairDesk SaaS para oficinas de reparação em Portugal. Fundador (Bruno Lopes,
LopesTech) está em regime Isenção Art. 53 do CIVA. Queremos vender SaaS a
outras oficinas que podem estar em diferentes regimes (Isenção Art. 53, Regime
Normal IVA, Regime Simplificado).

Já tem ChaveCifraPublicaAT2027.cer válida até 2028-04-26 para webservices AT.
Documentação interna em finanças/ (não partilhar links públicos).

Concorrentes (RepairDesk Lahore, RO App) NÃO têm compliance fiscal PT —
é o nosso moat.

[ESTADO ACTUAL]
- Software gera orçamentos em PDF (não fiscais)
- Não emite faturas, recibos, faturas-recibo
- Não comunica documentos à AT
- Não exporta SAFT-PT

[OBJECTIVO]
Mapa claro e prático do que é preciso para emitir documentos fiscais legais em
Portugal, em cada regime, e qual o caminho para certificação AT.

[REQUISITOS DA RESPOSTA]
1. Resumir Decreto-Lei 28/2019:
   - Quando é obrigatório software certificado
   - Quando não é (Isenção Art. 53, volume <€200k, etc.) — citar artigos
   - Excepções e regimes transitórios
2. ATCUD (Código Único de Documento):
   - Como funciona (estrutura, série, sequência)
   - Como pedir séries à AT (webservice ou manual)
   - Frequência de comunicação
3. Comunicação de faturas à AT (e-Fatura):
   - Webservice vs portal manual
   - Quem é obrigado, em que prazos
   - WSDLs disponíveis
4. SAFT-PT (Standard Audit File for Tax):
   - Estrutura XML
   - Quando exportar (mensal? anual?)
   - Como submeter (webservice ou upload)
5. Certificação de software de faturação:
   - Processo formal (despacho, OCC, custos)
   - Quanto tempo demora
   - Alternativas (certificação por terceiros, white-label)
6. Estratégia recomendada para RepairDesk:
   - Fase 1 (Isenção Art. 53): podemos emitir documentos próprios sem certificação?
   - Fase 2 (Regime Normal abaixo de €200k): podemos?
   - Fase 3 (volumes acima de €200k): certificação obrigatória?

[CONSTRAINTS]
- Citar SEMPRE artigos de lei (Decreto-Lei 28/2019 art. X, CIVA art. Y, etc.)
- Distinguir claramente entre "exigência legal" e "boa prática"
- Não inventar números — se não souberes, marca {{confirmar}}
- Considerar que o RepairDesk pode vender para qualquer regime; estratégia
  deve servir todos

[OUTPUT]
- Ficheiro markdown em Contexto/10-Compliance-PT.md
- Tabela resumo: regime → o que precisa → o que NÃO precisa
- Fluxograma de decisão: "vou emitir factura? que regime? que software preciso?"
- Roadmap de implementação no RepairDesk em fases
- Riscos legais identificados

[VERIFICAÇÃO]
- Bruno revê com a sua contabilista
- Documento é claro o suficiente para apresentar a outras lojas como argumento
```

---

## Prompt #8 — Comparação Stripe vs Mollie vs Easypay vs SIBS (subscrições SaaS + MBWay)

```
[CONTEXTO]
RepairDesk SaaS PT. Vamos cobrar subscrições mensais €19/€39/€89 (cf.
Contexto/07-Pricing-Proposta.md). Stack .NET 10.

Dois casos de uso distintos:
A) Cobrança recorrente de subscrições SaaS aos donos de lojas
B) MBWay no portal cliente público (cliente final paga reparação via QR)

[OBJECTIVO]
Decisão fundamentada de qual processador usar para cada caso, com prós/contras,
custos reais e plano de integração.

[REQUISITOS DA RESPOSTA]
Para cada um (Stripe, Mollie, Easypay, SIBS Gateway, SumUp):
1. Disponibilidade em Portugal (KYC, NIF PT, conta bancária PT)
2. Taxas reais (% + €) por método: cartão, MBWay, SEPA débito directo
3. Suporte para subscrições recorrentes nativo (não só one-off)
4. SDK / API .NET disponível e qualidade
5. Webhooks e idempotency
6. Tempo de setup (KYC, integração)
7. Custo fixo mensal mínimo
8. Reembolsos e disputes (UX e taxas)
9. Tax handling (IVA automático se aplicável)
10. Limitações PT específicas

Comparar:
- Best fit para subscrições SaaS (caso A) → 1 recomendação principal
- Best fit para MBWay portal cliente (caso B) → 1 recomendação principal
- Podem ser o mesmo processador? Trade-offs?

[CONSTRAINTS]
- Foco PT — não US/UK
- Considerar regime Isenção Art. 53 do CIVA do Bruno (pode mudar)
- Avaliar SIBS Gateway com cuidado (caro mas é "padrão PT")
- Não escolher por preço; LTV e UX contam

[OUTPUT]
- Ficheiro em Contexto/08-Pagamentos-Comparacao.md
- Tabela comparativa
- Decisão recomendada com justificação
- Plano de integração técnico de alto nível (não código)
- Riscos identificados

[VERIFICAÇÃO]
- Bruno revê; abertura de conta KYC é o teste real
```

---

## Prompt #9 — Estratégia de aquisição B2B PT para SaaS de oficinas

```
[CONTEXTO]
RepairDesk SaaS PT. Mercado-alvo: oficinas de reparação telemóveis/eletrónica
em Portugal. Pricing €19-89/mês (cf. Contexto/07-Pricing-Proposta.md).

Desafio: distribuição B2B em PT é difícil (cf. Contexto/05-Reflexao-Critica.md
secção F1). Não temos budget de paid ads agressivo.

[ESTADO ACTUAL]
- 0 clientes externos (só dogfooding na LopesTech)
- Sem website público ainda
- Fundador faz tudo (não há equipa de vendas)
- Localização Viseu, mas mercado-alvo é nacional

[OBJECTIVO]
Plano realista para conquistar primeiros 50 clientes pagantes em PT, com baixo
CAC (<€100/cliente), em 12-18 meses.

[REQUISITOS DA RESPOSTA]
1. Mapear universo de oficinas em PT (NIF/CAE) — tamanho do mercado
   - Quantas oficinas existem em PT (CAE 95110, 95120, etc.)
   - Distribuição geográfica
   - Concentração (cidades vs interior)
2. Para cada canal, avaliar custo, tempo, escalabilidade:
   - SEO + blog (manuais, guias, comparações)
   - YouTube técnico (vídeos de reparações + branding subtil)
   - Comunidades técnicas PT (Discord, Facebook Groups, Reddit r/portugal)
   - Distribuidores de peças (Mobiltrust, Tudo4Mobile, EurAsia, FixGSM) —
     parceria de referenciação
   - Eventos / feiras (Mobile World Congress, feiras locais)
   - Founder-led sales (Bruno visita lojas pessoalmente)
   - Indicações de clientes (referral program)
   - LinkedIn outbound
   - Cold email (legal em B2B em PT? RGPD?)
3. Estratégia de conteúdo:
   - 5 títulos de blog específicos que ranking para search PT
   - 5 títulos de vídeos YouTube
4. Estimar CAC e tempo de payback por canal
5. 90 dias roadmap concreto: o que fazer semana a semana

[CONSTRAINTS]
- Foco PT (não ES nem BR ainda)
- Considerar compliance RGPD (cold email tem regras)
- Avoid clichés ("growth hacking", "viral", etc.) — propor coisas concretas
- Considerar limite de tempo do Bruno (sai do emprego mas tem stress próprio)
- Honesto sobre dificuldade: dizer claramente o que NÃO funciona em PT

[OUTPUT]
- Ficheiro em Contexto/09-Customer-Acquisition.md
- Tabela canal → CAC esperado → tempo investimento → escalabilidade
- Recomendação top 3 canais para 90 primeiros dias
- Calendário concreto semana 1, 2, 3...
- Métricas a seguir (visitas, leads, demos, conversões)
- Risco: o que pode correr mal e como mitigar

[VERIFICAÇÃO]
- Plano é actionable (Bruno consegue executar sem perguntar mais)
- Bruno faz pelo menos 1 canal completo em 30 dias para validar
```

---

## Prompt #10 — Templates WhatsApp Business contextuais por estado de reparação

```
[CONTEXTO]
RepairDesk tem botões WhatsApp em cada reparação para o técnico falar com o
cliente. Hoje usa mensagem genérica. Queremos templates contextuais por estado
da reparação para reduzir trabalho manual.

Estados actuais (cf. backend/src/RepairDesk.Core/Enums/RepairStatus.cs):
- Orcamento (a aguardar aprovação do cliente)
- Recebido (acabou de entrar)
- Diagnostico (em análise)
- AguardaPeca (peça encomendada)
- EmReparacao (a ser reparado)
- Pronto (reparado, à espera de levantamento)
- Entregue
- Cancelado

[OBJECTIVO]
Conjunto de templates WhatsApp em PT-PT, naturais e humanos, por estado +
variações por categoria (telemóvel, computador, tablet).

[REQUISITOS DA RESPOSTA]
1. Para cada estado, criar 3 templates:
   - Template padrão (formal mas próximo)
   - Template informal (cliente jovem / habitual)
   - Template profissional (cliente empresa / Junta de Freguesia)
2. Cada template deve:
   - Ser em PT-PT (não PT-BR)
   - Ser natural, soar humano (não robotizado)
   - Ter campos de substituição claros: {{cliente_nome}}, {{equipamento}},
     {{valor}}, {{loja_nome}}, etc.
   - Caber em 1-3 frases (WhatsApp não gosta de paredes de texto)
   - Ter call-to-action quando faz sentido (responder, vir levantar, aprovar)
3. Casos especiais:
   - Cliente cujo equipamento está há +14 dias
   - Cliente que recusou orçamento (follow-up educado)
   - Cliente após entrega (pedido de Google Review aos 5 dias)
   - Lembrete de levantamento (passados 7 dias após Pronto)
4. Mensagens de erro / disculpas:
   - "Atrasou-se a peça" (template humano, não corporativo)
   - "Não conseguimos reparar" (devolução do equipamento)

[CONSTRAINTS]
- Estilo: simples, directo, simpático — não corporativo, não bombástico
- Sem emojis excessivos (1-2 max por mensagem)
- Português europeu autêntico — verbos no "tu" como default, "você" como
  variante mais formal
- Considerar contexto cultural PT: clientes apreciam confiança e clareza
- NÃO usar superlativos vazios ("ótimas notícias!", "fantástico!")
- Sem dark patterns (não criar urgência falsa)

[OUTPUT]
- Ficheiro em Contexto/11-WhatsApp-Templates.md
- Tabela: estado → template padrão → template informal → template profissional
- Secção "casos especiais"
- Recomendação de quais usar como default no produto
- Notas sobre RGPD e opt-in para envio automático

[VERIFICAÇÃO]
- Bruno testa 3 templates com clientes reais
- Mensagens soam naturais (não "feitas por IA")
```

---

## Prompt #11 — Onboarding wizard UX para novas lojas (research)

```
[CONTEXTO]
RepairDesk SaaS PT. Vamos abrir beta a 2-3 lojas amigas em 2-3 meses (cf.
Contexto/04-Roadmap-Detalhado.md Sprint 20). Sem onboarding bom, lojas
desistem em 24h.

[OBJECTIVO]
Especificação detalhada de um onboarding wizard que leve uma loja de "criou
conta" a "primeira reparação registada" em menos de 30 minutos, sem precisar
de chamada com o Bruno.

[REQUISITOS DA RESPOSTA]
1. Mapear referências de onboarding excelentes:
   - RO App (concorrente directo) — como fazem?
   - Shopify (gold standard SaaS) — quais princípios aplicar?
   - Intercom (B2B SaaS) — checklist visível?
   - Notion (zero-friction start) — empty state?
2. Especificar passo-a-passo wizard ideal para RepairDesk:
   - Passo 1: dados da empresa (logo, NIF, IBAN)
   - Passo 2: primeiro cliente fictício ou real?
   - Passo 3: primeira reparação demo
   - Passo 4: explorar dashboard
   - Passo 5: convidar funcionário
3. Para cada passo:
   - O que mostrar
   - O que pedir (input mínimo)
   - O que skipável vs obrigatório
   - Como dar "feedback de progresso" (X de 5 completo)
4. Onboarding pós-wizard:
   - Checklist persistente no dashboard até completar
   - Tour interactivo (Intro.js style?) ou tooltips contextuais?
   - Empty states que ensinam (não "no data")
5. Métricas a medir:
   - Tempo médio do início até primeira reparação real
   - Taxa de abandono por passo
   - Activation rate (X% completam todos os passos)
6. Estratégia de "rescue":
   - O que fazer se loja não voltar nos primeiros 7 dias
   - Email/WhatsApp automático com tutorial vídeo?

[CONSTRAINTS]
- Filosofia: "show, don't tell" — exemplos > documentação longa
- Sem vídeos de >2 minutos (ninguém vê)
- Sem onboarding que pede 20 campos antes de começar a usar
- Considerar lojas pequenas: dono está sozinho no balcão, sem tempo
- PT-PT em toda a copy

[OUTPUT]
- Ficheiro em Contexto/12-Onboarding-Wizard.md
- Mockups em ASCII de cada ecrã do wizard
- Tabela: passo → UI → input → validação → próximo
- Tour interactivo: lista de tooltips contextuais com texto exacto
- Métricas e instrumentação necessária
- Plan B se loja ficar inactiva 7 dias

[VERIFICAÇÃO]
- Bruno consegue imaginar e construir a partir do documento
- Documento é apresentável às lojas beta como "como vai correr"
```

---

## Prompt #12 — IMEI ↔ Autoridades / GSMA CheckMEND (viabilidade técnica + legal)

```
[CONTEXTO]
RepairDesk SaaS PT para oficinas de reparação. O fundador (Bruno Lopes,
LopesTech) propôs uma feature potencialmente diferenciadora: ao registar um
telemóvel, cruzar IMEI/serial com bases de dados de equipamentos reportados
como roubados, alertando o técnico antes de aceitar o trabalho.

Estado actual: registo de IMEI já existe como campo opcional. Sem
verificação. Nenhum concorrente PT (nem global, do que sabemos) faz isto bem.

Análise inicial em Contexto/04-Roadmap-Detalhado.md secção "Feature destaque:
IMEI ↔ Autoridades". Roadmap em 3 fases: A registo interno robusto, B
integração GSMA CheckMEND, C parceria institucional PSP/MAI.

[OBJECTIVO]
Mapa claro e prático para decidir SE e COMO implementar esta feature, com
viabilidade técnica, legal, custo real e plano por fases.

[REQUISITOS DA RESPOSTA]
1. Validação técnica:
   - Algoritmo Luhn para validar IMEI: confirma formato (15 dígitos + check)
   - Diferença entre IMEI, serial, IMEI2 (dual-SIM), eSIM
   - Como capturar (manual, foto via scan OCR, *#06# em Android)
2. Bases de dados disponíveis:
   - **GSMA Device Check / CheckMEND** — pricing real 2026, processo
     subscrição, KYC, API/SDK disponíveis, latência média, SLA
   - **National Mobile Phone Crime Unit (UK)** — relevante para PT?
   - **CEIR** (Central Equipment Identity Register) — existe em PT? Quem
     gere? Acesso?
   - **Apple Activation Lock** — possível verificar via API? Limitações
   - **Bases USA/EU** (Stolen911, immobilise.com, gsmaspamap.org, etc.) —
     qualidade real, viabilidade
3. Caminho institucional PT:
   - PSP — existe protocolo para lojas consultarem? Quem contactar (DCIAP,
     Gabinete Inspetor, Gestão Operacional)
   - GNR — equivalente?
   - MAI / Direção Nacional de Segurança Pública
   - Polícia Judiciária
   - Existe portal de "lojas confiáveis" / "buy-back trusted" em PT? Olho a
     iniciativas como TruStore, comércio justo, etc.
4. Compliance legal PT/EU:
   - RGPD: base legal aplicável (art. 6.º 1 (e) interesse público vs (f)
     interesse legítimo)
   - Comunicação obrigatória ao cliente (no recibo de entrada): texto exacto
     que cobre a verificação IMEI
   - Cooperação com autoridades: obrigação de denúncia se match (art. 244.º
     CP — denúncia obrigatória de funcionário público? Para privados?)
   - Risco de falsos positivos (IMEI clonado, BD desactualizada): que
     responsabilidade civil/criminal da loja?
   - Comparação: UK Mobile Phone Re-Programming Act 2002 — modelo a seguir?
5. Tradeoffs UX:
   - Alerta antes ou depois de aceitar trabalho?
   - Cliente tem direito a ver resultado da consulta?
   - Como gerir match positivo (cancelar trabalho? Reter equipamento?
     Notificar PSP?)
6. Plano por fases:
   - Fase A — só registo interno robusto (sprint 16-17): IMEI obrigatório
     em telemóveis, validação Luhn, histórico "este IMEI já cá entrou"
   - Fase B — integração com 1 BD externa (CheckMEND mais provável): que
     volume necessário para subscrição valer
   - Fase C — parceria institucional PT (horizon 4): que passos formais

[CONSTRAINTS]
- Não inventar custos — se não souberes preço GSMA, marca {{confirmar}}
- Citar fontes oficiais (sites GSMA, PSP, RGPD, DRE, etc.)
- Considerar que somos uma loja PT com ambição internacional — não
  comprometer scope geográfico
- Considerar que falsos positivos podem destruir reputação — risco real
- Pesar custo (subscrições, manutenção) vs valor diferenciador (PR,
  conversão de lojas)

[OUTPUT]
- Ficheiro em Contexto/13-IMEI-Autoridades.md
- Resumo executivo: AVANÇAR / ADIAR / REJEITAR com fundamentação
- Comparação de bases de dados (tabela)
- Mapa institucional PT (organismos a contactar, prioridade)
- Templates de cláusulas RGPD para recibo de entrada
- Estimativa de custo Fase B (subscrição CheckMEND × consultas/mês)
- Riscos identificados (técnicos, legais, reputacionais)
- Próximo passo concreto se decisão for AVANÇAR

[VERIFICAÇÃO]
- Bruno consegue tomar decisão informada com base no documento
- Se AVANÇAR, sabe exactamente o que pedir a quem
- Se ADIAR/REJEITAR, sabe porquê e em que condições reavaliar
```

---

## Prompt #13 — Storage de fotos antes/depois para reparações

```
[CONTEXTO]
RepairDesk SaaS PT. Vamos adicionar foto antes/depois em cada reparação —
é killer-feature para confiança visual do cliente (cf. Contexto/05-Reflexao-
Critica.md A6) e prova de estado de entrada (proteção legal da loja).

Estado actual:
- Nenhum upload de fotos. Apenas LogoUrl da Tenant como URL externo.
- Backend .NET 10 + EF Core 10 + SQL Server. Stack Docker.
- Não queremos guardar binários grandes no SQL Server (custo e performance).
- Tenants são lojas independentes — isolamento é crítico.

Volume estimado:
- Loja pequena: 30 reparações/mês × 4 fotos × 1MB = ~120 MB/mês
- 100 lojas: ~12 GB/mês = ~140 GB/ano
- Manter 2 anos: ~280 GB
- Crescimento orgânico para 1 TB em 18-24 meses

[OBJECTIVO]
Decisão fundamentada de qual serviço de storage usar para fotos de
reparações, com prós/contras, custo real e plano de integração .NET.

[REQUISITOS DA RESPOSTA]
1. Comparar opções (custo real € por mês para 100 GB / 1 TB com egress típico):
   - **Cloudflare R2** (S3-compat, zero egress fees — promissor para PT)
   - **AWS S3** (standard) — referência mas com egress caro
   - **Backblaze B2** (barato, S3-compat)
   - **Azure Blob Storage** — bom se já estivermos em Azure
   - **MinIO self-hosted** (Hetzner / OVH / VPS PT) — controle máximo
   - **Bunny.net Storage + CDN** (PT-friendly latência baixa)
2. Para cada opção:
   - Preço por GB/mês storage
   - Preço por GB de egress
   - Operações ($/1000 PUT, GET)
   - SDK / lib .NET disponível e qualidade (Amazon.S3, etc.)
   - Compatibilidade S3 (importante para portabilidade)
   - SLA, durabilidade (9s)
   - Latência típica PT
3. Arquitectura recomendada:
   - Upload directo do browser via signed URLs? Ou via API .NET?
   - Resizing/thumbnails (servidor vs CDN-side com ImageKit/Cloudinary)
   - Validação tamanho/tipo no client + server
   - Vírus scanning? (ClamAV, ou ignorar)
   - Path structure: tenants/{tenantId}/reparacoes/{id}/{nome}.jpg
   - Soft-delete vs hard-delete (alinhado com BaseEntity)
   - Retention: apagar fotos de reparações > 2 anos?
4. Compliance RGPD:
   - Onde está o servidor (EU obrigatório)
   - Encryption at rest
   - Como dar export ao cliente (foto incluída)
   - Como apagar tudo se cliente exercer "direito ao esquecimento"
5. Segurança:
   - Sem URLs públicas adivinháveis
   - Signed URLs com TTL curto (5-15 min)
   - Cross-tenant isolation reforçado

[CONSTRAINTS]
- EU-only para compliance
- Não inventar preços — se não souberes 100%, marca {{confirmar 2026}}
- Pensar em custo a 100 lojas + 1000 lojas (escalabilidade)
- Considerar lock-in vs portabilidade
- Avoid hype — propor a opção mais aborrecida que funciona

[OUTPUT]
- Ficheiro em Contexto/14-Storage-Fotos.md
- Tabela comparativa com custo/mês para 100 GB e 1 TB
- Recomendação principal + alternativa de fallback
- Plano de integração técnica (não código, alto nível)
- Riscos e plano de migração se mudarmos provider

[VERIFICAÇÃO]
- Bruno consegue decidir e abrir conta no provider
- Sabe custo esperado/mês a partir de N lojas
- Sabe como começar (steps 1, 2, 3)
```

---

## Prompt #14 — WhatsApp Business API: provider para automação

```
[CONTEXTO]
RepairDesk SaaS PT. Já temos templates WhatsApp por estado em
Contexto/11-WhatsApp-Templates.md. Atualmente são links wa.me que abrem o
WhatsApp manual. Queremos automatizar envio quando estado muda.

Restrições conhecidas:
- WhatsApp Business **Cloud API** da Meta (directa) — gratuita até X
  conversas/mês mas requer KYC empresa e número dedicado
- Verticais: Twilio, MessageBird, Vonage (Nexmo), 360dialog, Wati, etc.
- WhatsApp cobra por "conversation" (24h window). Há categorias:
  marketing, utility, authentication, service — preços diferentes
- Notificações iniciadas pela loja precisam de **templates aprovados**
  pela Meta (não free-form)

[ESTADO ACTUAL]
- Frontend tem botão wa.me com texto pré-preenchido (manual)
- Sem integração API
- Bruno tem número de telemóvel pessoal e LopesTech (mesmo número)

[OBJECTIVO]
Decisão fundamentada de qual provider WhatsApp usar para automação, com
preço real, complexidade de setup, e plano de integração .NET.

[REQUISITOS DA RESPOSTA]
1. Comparar opções para mercado PT:
   - **Meta Cloud API directa** (sem intermediário)
   - **Twilio WhatsApp** (referência global)
   - **MessageBird / Bird**
   - **360dialog** (PT-relevante? EU-based?)
   - **Wati** (low-code, foco PME)
   - **Vonage WhatsApp Business**
2. Para cada provider:
   - Preço €/conversa em PT (utility vs service vs marketing)
   - Setup time (dias entre KYC e primeira mensagem)
   - Verificação business no Meta Business Manager (passos)
   - Templates: como criar, prazo de aprovação Meta
   - Webhooks de status (delivered, read, failed)
   - SDK .NET / facilidade de integração
   - Suporte PT-PT
3. Decisão sobre número dedicado:
   - Bruno pode usar o número pessoal? (NÃO, depois fica preso)
   - Onde comprar número dedicado em PT? Custo?
4. Estratégia de templates:
   - Quais dos templates de `11-WhatsApp-Templates.md` submeter como
     utility (free) vs service (cobrado)
   - Variáveis dinâmicas vs texto fixo (regras Meta)
5. Compliance:
   - Opt-in explícito do cliente (já mencionado no #10)
   - Como gerir "STOP" e descadastramento
   - RGPD: dados ficam onde
6. Integração técnica RepairDesk:
   - Quando enviar (event hook em ChangeEstadoAsync)
   - Retries em falha
   - Logs de envio (auditoria)
   - Fallback se WhatsApp falhar (SMS? email?)

[CONSTRAINTS]
- EU-based provider preferido (latência + RGPD)
- Custo razoável para SaaS pequeno (não enterprise)
- Não inventar preços — marcar {{confirmar}}
- Considerar risco vendor lock-in
- Templates Meta podem ser rejeitados — plano B

[OUTPUT]
- Ficheiro em Contexto/15-WhatsApp-Provider.md
- Tabela comparativa
- Recomendação para Fase 1 (validar com Bruno) e Fase 2 (100+ lojas)
- Estimativa de custo/mês a 100 lojas com X conversas/dia
- Plano de setup KYC + primeiro template aprovado

[VERIFICAÇÃO]
- Bruno consegue iniciar processo Meta Business em 1 dia
- Sabe quanto vai custar/mês quando tiver 100 lojas
```

---

## Prompt #15 — Compliance RGPD para RepairDesk SaaS

```
[CONTEXTO]
RepairDesk SaaS PT para oficinas. Como SaaS, vamos ser **subcontratante**
de tratamento (data processor) para as lojas (data controllers) que nos
contratam. Os dados que tratamos incluem: identificação de clientes finais
(nome, telefone, email, NIF), dispositivos (IMEI, equipamento), histórico
de reparações, fotos (futuro), comunicações WhatsApp (futuro), IBANs.

Bruno é fundador e DPO interino. Sem departamento legal próprio.

Estado actual:
- Política de privacidade não existe
- Termos de serviço não existem
- Cookie banner não existe
- Não há contrato de processamento de dados modelo
- Há logs em ficheiros (Serilog) sem retenção definida
- Backups não definidos

[OBJECTIVO]
Pacote prático de compliance RGPD para começar a operar legalmente em PT
quando vendermos a outras lojas (beta + público).

[REQUISITOS DA RESPOSTA]
1. **Política de Privacidade pública** (texto pronto a publicar):
   - Quem somos (LopesTech)
   - Que dados recolhemos (utilizadores SaaS, NÃO clientes finais)
   - Base legal de cada tratamento
   - Partilha com terceiros (provider faturação, storage, WhatsApp)
   - Retenção (quanto tempo guardamos)
   - Direitos do titular (acesso, retificação, esquecimento, portabilidade)
   - DPO contacto
   - Linguagem natural PT-PT (não juridiquês inacessível)
2. **Termos de Serviço** (texto pronto a publicar):
   - Quem usa o serviço, em que regime
   - SLA realista (sem promessas vazias)
   - Propriedade dos dados — **sempre do cliente**
   - Limitação de responsabilidade
   - Foro PT
   - Cancelamento e export
3. **Contrato de Processamento de Dados (DPA)** modelo entre RepairDesk
   (subcontratante) e Loja (responsável pelo tratamento):
   - Cláusulas obrigatórias art. 28.º RGPD
   - Lista de subcontratantes (Moloni? Cloudflare R2? Meta?)
   - Procedimento de notificação de breach
   - Audit rights da loja
   - Pronto a assinar (PDF + DocuSign futuro)
4. **Política de Cookies + Banner**:
   - Que cookies usamos (essenciais vs analíticas vs marketing)
   - Texto do banner (PT-PT)
   - Implementação técnica simples (sem CMP cara)
5. **Procedimentos internos**:
   - Como gerir pedido de acesso/esquecimento
   - Como gerir breach (passos primeiras 72h)
   - Retenção de logs (quanto tempo)
   - Backup encryption requirements
6. **Riscos identificados**:
   - Sub-processadores fora UE (se houver)
   - Transferência internacional (Cloudflare R2 EU region? Meta nos EUA?)
   - Necessidade de DPIA (Data Protection Impact Assessment)
7. Roadmap de implementação prioritária (o que fazer primeiro):
   - O que tem de estar pronto **antes** de beta com 2-3 lojas amigas
   - O que pode esperar para depois

[CONSTRAINTS]
- Foco RGPD (UE 2016/679) + Lei 58/2019 (PT)
- Linguagem PT-PT clara, não juridiquês defensivo
- Sem inventar artigos — citar correctamente
- Considerar Bruno é solo (sem advogado dedicado)
- Avoid sobre-engenheirar — proporcionar o mínimo legal
- Identificar pontos onde Bruno PRECISA de consultar advogado

[OUTPUT]
- Ficheiro em Contexto/16-Compliance-RGPD.md
- Anexos: textos prontos para publicar (privacy, ToS, cookies, DPA)
- Checklist de implementação técnica (campos a guardar, retenções)
- Avisos sobre riscos que precisam advogado
- Roadmap MVP legal → full compliance

[VERIFICAÇÃO]
- Bruno publica privacy + cookies + ToS no site em 1 dia
- DPA está pronto para enviar à primeira loja beta
- Bruno sabe exactamente o que falta resolver com advogado
```

---

## Prompt #16 — Deployment + Hosting strategy (produção)

```
[CONTEXTO]
RepairDesk SaaS PT, stack Docker Compose com 4 serviços: api (.NET 10),
web (React + nginx), db (SQL Server 2022), cache (Redis 7). Actualmente
corre só em localhost no portátil do Bruno.

Para abrir beta a 2-3 lojas amigas (próximos 3 meses) precisa de estar
sempre online, com domínio próprio, SSL, backups e DNS.

Restrições:
- EU-only (RGPD)
- Custo razoável para SaaS pré-receitas (€0-50/mês ideal)
- Bruno é solo — não tem tempo para gerir Kubernetes complexo
- Deve permitir escala futura sem refactor (1 loja → 100 lojas)

[OBJECTIVO]
Decisão fundamentada de onde alojar produção, com custo real, plano de
setup (DNS, SSL, deploy) e estratégia de escala.

[REQUISITOS DA RESPOSTA]
1. Comparar opções EU para Docker Compose:
   - **Hetzner Cloud** (CX22 €4.5/mês, alemão, RGPD-friendly)
   - **OVH (PT)** (B2-7, etc) — provedor PT-friendly
   - **DigitalOcean** (Droplet €6+)
   - **Azure Container Apps** ou App Service
   - **Railway / Render / Fly.io** (PaaS — útil para early stage)
   - **AWS Lightsail** ou **AWS Fargate**
   - **Self-host on-prem PT** (Bruno tem ligação fibra?)
2. Para cada opção:
   - Custo €/mês para stack actual (db + api + web + cache)
   - SSL automático (Let's Encrypt managed vs manual)
   - Backup automático (snapshots)
   - Monitoring básico incluído
   - Tempo de setup
3. SQL Server licensing:
   - SQL Server Developer Edition (gratuito mas só dev/test)
   - SQL Server Express (gratuito mas <10GB DB, <1 socket, <1GB RAM)
   - SQL Server Standard (caro)
   - **Plano B: migrar para PostgreSQL?** Quando vale a pena
4. DNS + domínio:
   - Onde comprar (Cloudflare Registrar, dn.pt, GoDaddy)
   - Cloudflare proxy para SSL + DDoS + CDN
   - Subdomínios sugeridos: app.repairdesk.pt, api.repairdesk.pt, status.repairdesk.pt
5. Estratégia de deploy:
   - GitHub Actions → docker push → SSH redeploy
   - Zero-downtime deploy? (blue-green simples)
   - Rollback rápido (1 comando)
6. Compliance RGPD:
   - Servidor EU obrigatório
   - DPA com provedor (ver `16-Compliance-RGPD.md`)
   - Logs com retenção definida

[CONSTRAINTS]
- Sem ferramentas hyper-complexas (Kubernetes, Pulumi, Terraform) — Bruno é solo
- Custo total inclui DB + API + web + cache + monitoring + backups
- Avoid hype — preferir o aborrecido que funciona
- Não inventar preços — marca {{confirmar 2026}}

[OUTPUT]
- Ficheiro em Contexto/17-Hosting-Deployment.md
- Tabela comparativa preço/mês + features
- Recomendação principal + alternativa
- Plano de setup passo-a-passo (semana 1, 2, 3)
- Checklist pré-launch (SSL, backups, DNS, monitoring)
- Custo total mensal estimado (com escala 1 / 10 / 100 lojas)

[VERIFICAÇÃO]
- Bruno consegue contratar provider em 1 dia
- Sabe custo mensal exacto que vai pagar
- Tem plano de migração se mudar provider
```

---

## Prompt #17 — Backup + Disaster Recovery runbook

```
[CONTEXTO]
RepairDesk SaaS PT em produção contém dados críticos das lojas: clientes,
reparações, fotos, faturas, IMEIs, despesas. Perder estes dados = perder
clientes e enfrentar problemas legais (RGPD não tem cobertura para
negligência grave).

Sem advogado dedicado, Bruno tem de cumprir art. 32.º RGPD: "medidas
técnicas e organizativas para garantir segurança apropriada".

Estado actual:
- SQL Server em Docker volume local — sem backup automatizado
- Sem snapshot de DB
- Sem off-site backup
- Sem teste de restore

[OBJECTIVO]
Runbook prático de backup + disaster recovery para SaaS de 1-100 lojas,
com plano de teste, custos, e checklist de incidente.

[REQUISITOS DA RESPOSTA]
1. Estratégia de backup:
   - **DB SQL Server**: full diário + log incremental hourly? Backup hot vs cold?
   - **Photos storage (Cloudflare R2)**: versioning + lifecycle policy
   - **Logs**: retenção quanto tempo (RGPD = mínimo necessário, geralmente 90 dias)
   - **Config + secrets**: como guardar (1Password, Bitwarden Team, gitignored .env encrypted)
2. Off-site backup:
   - Local 1 (provedor primário): snapshots automáticos
   - Local 2 (off-site EU): Backblaze B2 / Hetzner Storage Box / iDrive
   - **Regra 3-2-1**: 3 cópias, 2 media diferentes, 1 off-site
3. Encryption:
   - At rest: AES-256 server-side
   - In transit: HTTPS + TLS 1.3
   - Backup encryption with key separation (cliente da DB ≠ cliente da chave)
4. Teste de restore:
   - Frequência (mensal? trimestral?)
   - Procedimento exacto
   - Métricas: RTO (Recovery Time Objective) e RPO (Recovery Point Objective)
5. Cenários de disaster:
   - DB corrompida (SQL Server crash)
   - Servidor inteiro perdido (provedor crash, conta suspensa)
   - Foto storage hijacked / encryption ransomware
   - Bruno doente / inacessível por X dias — quem actua?
   - Conta GitHub comprometida (perdemos código fonte)
6. Runbook por cenário:
   - Passos exactos para restaurar
   - Quem contactar (provedor, hosting, ANSP)
   - Tempo estimado
   - Como notificar clientes (template email)
7. Compliance:
   - Breach notification: <72h (art. 33.º RGPD) — passos
   - Manter prova de backups para auditoria
   - DPA com provedores
8. Custos estimados:
   - Storage backup off-site €/mês
   - Tempo gasto em testes mensais

[CONSTRAINTS]
- Foco em soluções managed (não construir scripts cron complexos sem necessidade)
- Plano deve ser executável por Bruno solo em 2-4h/mês
- Avoid hype enterprise (não precisamos de Zerto, etc)
- EU-only

[OUTPUT]
- Ficheiro em Contexto/18-Backup-DR.md
- Estratégia 3-2-1 detalhada
- Runbook para 5 cenários disaster
- Checklist mensal de teste
- Templates de comunicação a clientes em caso de incidente

[VERIFICAÇÃO]
- Bruno implementa backup automático esta semana
- Faz teste de restore na próxima semana
- Sabe exactamente o que fazer se DB crashar
```

---

## Prompt #18 — Monitoring + Alertas + Observabilidade

```
[CONTEXTO]
RepairDesk SaaS em produção, fundador solo (Bruno). Sem departamento de
ops. Precisa de descobrir bugs e quedas **antes** que o cliente reporte.

Estado actual:
- Serilog escreve para console + ficheiro
- Sem APM (Application Performance Monitoring)
- Sem alertas (uptime, errors, slow queries)
- Sem dashboard de saúde

[OBJECTIVO]
Stack de monitorização mínima viável para SaaS B2B, com alertas
accionáveis, baixo custo e baixa manutenção.

[REQUISITOS DA RESPOSTA]
1. O que monitorizar (com critério: "se isto não está bem, algo está partido"):
   - **Uptime**: API + Web respondem 200
   - **Error rate**: % de requests com 5xx por minuto
   - **Response time p95/p99**: API e queries SQL
   - **Database**: conexões abertas, DB size, deadlocks, slow queries (>500ms)
   - **Containers**: CPU, RAM, disco — alertas em >80%
   - **Backup status**: último backup OK há <24h
   - **SSL certificate**: avisa 14 dias antes de expirar
   - **Critical business events**: novo signup, primeira reparação, churn
2. Comparar ferramentas (free/cheap tiers):
   - **Sentry** (errors, free 5k events/mês)
   - **Better Stack / Logtail** (logs + uptime)
   - **UptimeRobot / Pingdom** (uptime monitoring)
   - **OpenTelemetry → Grafana Cloud / Honeycomb / SigNoz**
   - **Application Insights** (Azure)
   - **Self-host Plausible/Umami** para analytics frontend
3. Alertas:
   - Onde recebe (email? Telegram? Slack próprio? Discord?)
   - Severidade (P1 acorda Bruno às 3h vs P3 lê amanhã)
   - On-call para solo founder: regras de não-disturb (ex: alerta crítico via SMS, resto via email digest)
   - Anti-spam: agrupar alertas, snooze, deduplicate
4. Integração .NET:
   - Serilog sink para Sentry / Better Stack
   - OpenTelemetry com SQL Server instrumentation
   - Frontend: Sentry Browser SDK
5. Dashboard de saúde pública:
   - status.repairdesk.pt (Better Stack tem incluído?)
   - Build vs comprar
6. Métricas de negócio (não só técnico):
   - Reparações criadas/dia
   - Lojas activas (login últimos 7 dias)
   - Tempo médio de Recebido → Entregue
   - Activation rate (cliente cria primeira reparação <24h)
7. Custos estimados:
   - Free tier até X lojas
   - Quando paga e quanto

[CONSTRAINTS]
- Não usar Datadog/NewRelic (caros)
- Free tier suficiente para 0-10 lojas; pay-as-you-grow
- Avoid yet-another-tool — preferir 1 que faz 3 coisas em vez de 3 ferramentas
- Stack .NET 10 nativo (Serilog já está)

[OUTPUT]
- Ficheiro em Contexto/19-Monitoring.md
- Stack recomendada (1 escolha principal + alternativas)
- Configuração concreta (appsettings.json snippet, packages NuGet)
- Lista de alertas com severidade
- Custo mensal estimado
- Checklist semanal/mensal de revisão métricas

[VERIFICAÇÃO]
- Bruno tem Sentry/equivalente integrado em 1 dia
- Recebe primeiro alerta de teste em 1h
- Sabe distinguir P1/P2/P3 sem hesitar
```

---

## Prompt #19 — Modelo de suporte ao cliente (fundador solo)

```
[CONTEXTO]
RepairDesk SaaS, fundador solo (Bruno). Quando tiver 10-50 lojas, o
suporte vai consumir tempo. Pior: mau suporte = churn imediato em SaaS
pequeno.

Estado actual:
- Sem canal de suporte definido
- Sem base de conhecimento
- Sem horário declarado
- Sem SLA

Bruno tem €0 de budget para contratar suporte.

[OBJECTIVO]
Modelo de suporte que escala de 1 até 100 lojas sem contratar ninguém
(ou contratando part-time freelancer só >50 lojas), com qualidade
percebida alta.

[REQUISITOS DA RESPOSTA]
1. Canais de suporte (escolher 1-2 primários, não 5):
   - **Email** (suporte@repairdesk.pt) — clássico, escalável
   - **WhatsApp Business** suporte (PT-friendly mas perigoso para solo)
   - **In-app chat** (Crisp free / Intercom / Tawk.to)
   - **Discord server público** (low-touch, comunidade entre lojas)
   - **Forum / fórum próprio** (Discourse self-hosted)
2. Comparar para fundador solo:
   - Custo €/mês
   - Carga de trabalho (interrupções vs batched)
   - Qualidade percebida pelo cliente
   - Escalabilidade quando contratar 1 part-time
3. Base de conhecimento:
   - **Tipo**: vídeos curtos / artigos texto / FAQ / tutoriais
   - **Plataforma**: Notion público / Outline / GitBook / blog próprio
   - **SEO**: artigos públicos atraem leads (dupla função)
   - Lista de 20 artigos essenciais para lançamento
4. SLA realista solo:
   - First response time: 4h business, 24h fins-de-semana?
   - Resolution time por severidade
   - **Não promete o que não pode cumprir** — under-promise, over-deliver
5. Self-service primeiro:
   - Onboarding wizard que evita 80% dos tickets
   - In-app tooltips contextuais
   - "Have you tried X?" antes de submeter ticket
6. Ferramentas para reduzir carga:
   - Templates de resposta (snippets)
   - Saved replies (Front, Help Scout, Crisp)
   - Macros para tickets repetidos
7. Quando contratar primeiro humano:
   - Critério (tickets/dia? horas/semana? receita?)
   - Como recrutar (freelancer PT remoto via Workana, Toptal, indica@)
   - Onboarding do novo
8. KPIs:
   - CSAT score (recolher como?)
   - First response time
   - Tickets/loja/mês (early warning de produto mau)

[CONSTRAINTS]
- Fundador solo realista — não comprar Zendesk de €50/seat/mês
- Português nativo nos artigos e respostas
- Não criar 10 canais (escolhe 2 e foca)
- Avoid AI chatbots no MVP (causam pior UX que email humano)

[OUTPUT]
- Ficheiro em Contexto/20-Suporte-Cliente.md
- Stack recomendada (canal + KB + tooling)
- Lista de 20 artigos KB essenciais (títulos + outline)
- 10 templates de resposta para tickets comuns
- SLA público proposto
- Roadmap: 0-10 lojas / 10-50 / 50-100+

[VERIFICAÇÃO]
- Bruno escolhe canal e abre conta em 1 dia
- Publica os 5 artigos KB mais críticos em 1 semana
- Tem SLA público visível em /suporte
```

---

## Prompt #20 — Plano operacional de Certificação AT (passos concretos)

```
[CONTEXTO]
Já temos research teórico em Contexto/10-Compliance-PT.md sobre DL 28/2019,
SAF-T, ATCUD. Conclusão: para emitir faturas próprias no RepairDesk
precisamos de software certificado AT. Estratégia adoptada: Fase 1 sem
emissão, Fase 2 integrar provider certificado externo, Fase 3 certificação
própria.

Este prompt foca **Fase 3** apenas: o processo OPERACIONAL real de
certificar o RepairDesk junto da AT.

[OBJECTIVO]
Plano operacional concreto com passos, prazos, custos reais e contactos
para certificar o RepairDesk como software de faturação junto da AT.

[REQUISITOS DA RESPOSTA]
1. Processo de certificação AT:
   - Quem certifica (AT directamente? OCC? terceiros autorizados?)
   - Forms a preencher, onde submeter
   - Requisitos técnicos detalhados (hash chain, ATCUD, SAF-T)
   - Documentação a entregar (manual de utilizador, código-fonte?)
2. Custos reais 2026: taxas AT, honorários OCC, auditoria, custo total
3. Prazos: tempo médio de processo, validade, renovação
4. Alternativas operacionais:
   - White-label certificação
   - Acordo com Moloni/InvoiceXpress para emitir em nome do RepairDesk
   - Comprar empresa já certificada
5. Quando vale a pena (volume / receita)
6. Riscos (auditoria, mudanças regulatórias)

[CONSTRAINTS]
- Citar artigos / portarias quando aplicável
- Não inventar números — marca {{confirmar com AT 2026}}
- Considerar que Bruno é solo developer sem advogado fiscal dedicado

[OUTPUT]
- Ficheiro em Contexto/21-Certificacao-AT.md
- Roadmap passo-a-passo (com timeline)
- Custo total estimado (min, max, esperado)
- Decisão recomendada: certificar próprio vs delegar a provider externo
- Lista de contactos (OCC PT, AT, consultores)
```

---

## Prompt #21 — Tabela de preços PT para reparações (mercado real)

```
[CONTEXTO]
RepairDesk SaaS PT vai ter feature "tabela de preços configurável por
tenant" (Sprint 4 do RepairDesk_DOCUMENTO_DEFINITIVO.docx). Para acelerar
onboarding das lojas, queremos oferecer uma tabela base pré-populada com
preços realistas de mercado PT 2026, que cada loja ajusta depois.

[OBJECTIVO]
Tabela de preços de referência para reparações comuns no mercado PT 2026,
agrupada por marca/modelo + tipo de serviço, com fonte e variação.

[REQUISITOS DA RESPOSTA]
1. Pesquisar preços médios PT em 2026 para:
   - **iPhones populares** (11-15): ecrã, bateria, vidro traseiro, conector
     carga, câmara
   - **Samsung populares** (A50, A52, A53, S20, S21, S22, S23): ecrã,
     bateria, conector
   - **Xiaomi populares** (Redmi Note 10/11/12, Mi 11/12/13): ecrã, bateria
   - **Huawei populares** (P20/P30/P40, Mate)
   - **MacBooks** (Air/Pro M1/M2): teclado, bateria, ecrã, SSD
   - **PCs Windows**: SSD, RAM, ecrã, teclado, bateria
   - **Acessórios**: película, capa, carregador
2. Para cada serviço: PVP médio PT 2026, custo aproximado da peça,
   margem típica, tempo médio (min)
3. Fontes: oficinas PT públicas, marketplaces, Reddit/comunidades PT
4. Variação geográfica (Lisboa/Porto vs interior)
5. Tabela CSV-friendly para importar directamente no produto

[CONSTRAINTS]
- Foco PT 2026 — não EUR convertidos de UK/US
- Não inventar — marcar {{aprox.}} quando estimativa
- Citar fonte por linha (ou agregada por categoria)
- Reconhecer variação local (Viseu não cobra como Lisboa)

[OUTPUT]
- Ficheiro em Contexto/22-Tabela-Precos-PT.md
- Tabela markdown/CSV: marca, modelo, serviço, peça, custo_peca, pvp_medio, margem, tempo_min
- Notas por categoria
- Fontes utilizadas
```

---

## Prompt #22 — Estratégia jurídico-fiscal pessoal LopesTech (PT)

```
[CONTEXTO]
Bruno Lopes opera **LopesTech** como nome individual:
- NIF 263758141
- Regime IVA: Isenção Art. 53 CIVA (volume <€15k/ano)
- CAE principal 62100 + secundários 47401, 58290, 95101, 95102
- Sai do emprego em Abril 2026 — agora full-time empreendedor

Volume esperado:
- Ano 1: €10-25k (transição)
- Ano 2: €30-60k (RepairDesk começa a gerar)
- Ano 3: €80-150k (escala SaaS)

[OBJECTIVO]
Plano fiscal/jurídico pessoal que minimize custos, simplifique
contabilidade e prepare crescimento, com momentos de decisão claros.

[REQUISITOS DA RESPOSTA]
1. **Quando sair de Isenção Art. 53** (limite €15k):
   - Que acontece quando ultrapassar
   - Comunicação à AT
2. **Regime simplificado vs contabilidade organizada**:
   - Quando é obrigatório
   - Custo médio contabilista PT em cada regime
3. **Quando passar a sociedade (Lda)**:
   - Limite recomendado em volume
   - Custos: constituição (€360 empresa-online), TOC (€100-200/mês)
   - Vantagens: protecção responsabilidade, separar pessoal/empresa
   - IRC vs IRS comparison
4. **IRS pessoal Cat. B**: retenção, pagamentos por conta, deduções
5. **Segurança Social**: contribuições TI, direito a desemprego pós-emprego
6. **Estratégia operacional**: conta separada, software (Moloni/Toconline),
   quando contratar contabilista
7. **Roadmap por ano (Ano 1/2/3)**

[CONSTRAINTS]
- Estritamente PT 2026
- Citar CIRS, CIVA, CSC quando aplicável
- Não inventar números — marcar {{confirmar 2026}}
- Marcar claramente: "isto NÃO substitui parecer profissional"

[OUTPUT]
- Ficheiro em Contexto/23-Plano-Fiscal-Pessoal.md
- Tabela: volume anual → regime recomendado → custo contabilidade
- Decisões com gatilho (quando, não se)
- Checklist de moves operacionais
- Lista de perguntas para o contabilista
```

---

## Prompt #23 — PWA + Offline strategy para RepairDesk

```
[CONTEXTO]
RepairDesk SaaS PT, stack React 19 + Vite + Tailwind v4 + React Query 5.
Lojas amigas de teste (oficinas pequenas em PT) tipicamente têm WiFi de
fibra mas inconstante, tablets/portáteis partilhados e picos sem internet.

Sprint 15 do RepairDesk_DOCUMENTO_DEFINITIVO.docx pede PWA com offline +
sync automático.

[OBJECTIVO]
Plano técnico para tornar o RepairDesk PWA com offline-first para
operações de balcão críticas, com sync automático e conflict resolution
multi-tenant.

[REQUISITOS DA RESPOSTA]
1. Operações que DEVEM funcionar offline (prioridade):
   - Criar reparação, mudar estado, adicionar diagnóstico
   - Ver histórico recente
   - Quais NÃO precisam (PDF export, etc.)
2. Estratégia técnica: service worker (Workbox), cache strategies, IndexedDB
   schema (idb/Dexie/nativo), sync queue com retries
3. Conflict resolution: LWW com audit trail, ETag/RowVersion, reconciliação
4. UI/UX: indicador online/offline, "X mudanças pendentes", toast sync
5. Backend endpoints adicionais: `/api/sync/pull?since=...`, `/api/sync/push`
6. Instalabilidade: manifest.json, Apple touch icons, A2HS prompt
7. Limitações: QuotaExceededError, Safari iOS API support

[CONSTRAINTS]
- Sem libraries pesadas (Workbox OK, avoid PouchDB)
- Funciona em Safari iOS 16+
- Multi-tenant: NUNCA vazamento offline entre tenants

[OUTPUT]
- Ficheiro em Contexto/24-PWA-Offline.md
- Arquitectura técnica + sequence diagrams
- Schema IndexedDB
- Checklist operações offline (must vs nice)
- Roadmap de implementação
- Riscos identificados
```

---

## Prompt #24 — Parcerias com distribuidores PT de peças (moat de distribuição)

```
[CONTEXTO]
RepairDesk SaaS PT. Para conquistar lojas como clientes, precisamos de
**algo que elas não consigam ter sozinhas**. Ideia poderosa: parcerias
com distribuidores PT de peças (Tudo4Mobile, Mobiltrust, FixGSM, EurAsia,
Tech Componentes, Battery Empire, Utopya, Componenti Digitali, etc.).

Cenários: afiliação com comissões, recomendação cruzada, API↔API com
catálogo em tempo real e encomenda 1-clique.

[OBJECTIVO]
Mapa estratégico de distribuidores PT/EU e plano de abordagem para
parcerias.

[REQUISITOS DA RESPOSTA]
1. Listar distribuidores PT/EU relevantes (Tudo4Mobile, Mobiltrust,
   FixGSM, EurAsia, Tech Componentes, Battery Empire, Utopya, Componenti
   Digitali, Alrossio, SpainSellers, etc.). Para cada:
   - Tipo de peças, volume estimado
   - API pública? B2B portal? Catálogo descarregável?
   - Programa de afiliados / comissões
   - Contacto (form, email, comercial)
2. Modelos de parceria:
   - A — Referência simples (desconto cliente + comissão)
   - B — Integração de catálogo (encomenda 1 clique)
   - C — Stock partilhado entre tenants (futuro)
3. Estratégia de abordagem:
   - Sequência (quem contactar primeiro)
   - Email template PT-PT
   - Argumento de valor para o distribuidor
   - O que pedir / o que oferecer
4. Riscos: pricing pressure, dependência, concorrência

[CONSTRAINTS]
- Foco PT/EU
- Realista — nem todos vão aceitar
- Não inventar APIs — marca {{verificar}}

[OUTPUT]
- Ficheiro em Contexto/25-Distribuidores-Pecas-PT.md
- Tabela: nome, contacto, peças, API, programa
- Top 3 a contactar primeiro
- Template email PT-PT
- Modelo de parceria preferido com justificação
```

---

## Prompt #25 — Brand + Design System + Naming para RepairDesk

```
[CONTEXTO]
RepairDesk é nome **provisório** (clash com RepairDesk de Lahore, $99/user).
Quando o produto for público, vamos precisar nome próprio, logo, paleta,
tipografia, tom de voz.

Stack: React + Tailwind v4. Cor actual brand-500 #0EA5E9 sky-blue
(default Tailwind). Sem logo (placeholder ●). Domínio: lopestech.pt.

Princípios fundadores: PT nativo, UX moderna, honesto (sem dark patterns),
verticalização eletrónica, pricing transparente €19-49.

[OBJECTIVO]
Identidade de marca completa: nome, logo, paleta, tipografia, tom de voz
e templates de marketing inicial.

[REQUISITOS DA RESPOSTA]
1. **Naming (top 3-5 propostas)** com análise de:
   - Disponibilidade .pt / .app / .io / .com
   - Registo de marca PT (INPI)
   - Disponibilidade GitHub/Twitter/Instagram
   - Pronúncia clara em PT/ES/EN
   - Exemplos: Reparalo, Conserta, Fixly.pt, Mecano.app, Officina,
     Bench, Workbench, Atelier
2. **Logo concept** (3 propostas descritas)
3. **Paleta de cores** com hex tokens Tailwind + justificação
4. **Tipografia** (display, body, mono) — preferir Google Fonts ou system
5. **Tom de voz**: 5 exemplos de copy (erro 500, signup, success, breach,
   billing failed)
6. **Aplicações**: mockup ASCII landing hero, email signature, favicon,
   Open Graph
7. **Decisão de naming**: top 1 + plano de migração técnica

[CONSTRAINTS]
- PT-PT primeiro (não traduzido)
- Nome <8 chars, pronunciável, memorável
- Disponível .pt OU .app (verificar)
- Não infringir RepairDesk.co (trademark)
- Avoid hype tech ("AI", "Cloud", "X.io")
- Honesto

[OUTPUT]
- Ficheiro em Contexto/26-Brand-Design-System.md
- Top 3 nomes analisados
- Paleta com tokens Tailwind
- Tipografia
- Logo mockup
- 10 templates de copy
- Plano de migração técnica
- Riscos trademark
```

---

## Prompt #26 — Plano de testes automatizados (E2E + load + security)

```
[CONTEXTO]
RepairDesk SaaS PT, stack .NET 10 + React 19 + SQL Server + Docker. Já temos
50 testes unitários/integração backend (xUnit + WebApplicationFactory).
Frontend sem testes automatizados.

Antes de Beta com 2-3 lojas amigas, queremos cobertura de testes que
proteja contra regressões mas seja sustentável para fundador solo (sem
team QA).

[ESTADO ACTUAL]
- Backend: 50 xUnit tests (controllers, services, integração)
- Frontend: zero testes
- Sem testes E2E (browser real)
- Sem testes de carga
- Sem security scanning automatizado

Princípio do produto (cf. Documento Definitivo Sprint 13): OWASP Top 10
audit + RGPD + pentest manual.

[OBJECTIVO]
Plano realista de testes automatizados para fundador solo: cobertura
suficiente para confiar nos deploys, sem virar tempo de testes > tempo de
features.

[REQUISITOS DA RESPOSTA]
1. **Testes E2E** (Playwright vs Cypress):
   - Recomendação para stack actual (React 19 + Vite + .NET)
   - Cenários críticos a cobrir (login, criar reparação, mudar estado,
     portal cliente público, import/export CSV)
   - Quantos? (não 200 — uns 15-25 críticos)
   - CI/CD integration (GitHub Actions)
2. **Testes de carga** (k6 vs Locust vs Gatling):
   - Recomendação
   - Cenários: login burst, dashboard polling, portal público,
     criação reparações em massa
   - Métricas alvo (latência p95, throughput, error rate)
   - Quando correr (CI? semanal? antes de releases?)
3. **Security testing automatizado**:
   - OWASP ZAP em GitHub Actions
   - Dependabot / Snyk para dependências
   - Secret scanning (TruffleHog, gitleaks)
   - SAST (CodeQL? SonarQube?)
4. **Frontend testes unitários**:
   - Vitest + React Testing Library? Justifica
   - Cobertura alvo realista (não 100%)
   - Quais componentes valem testar (Health Score calc, CSV import preview)
5. **Multi-tenant isolation tests**:
   - Como garantir que tenant A não vê dados de tenant B em **todos** os endpoints
   - Framework de testes para isto
6. **Estratégia geral**:
   - Test pyramid (unit > integration > e2e)
   - O que NÃO testar (snapshots de UI, etc.)
   - Quando promover de smoke test → e2e → load test
   - Como gerir flakiness (testes não-determinísticos)
7. **Time budget**:
   - Quanto tempo/semana Bruno deve dedicar
   - Suite tem de correr em <10min em CI
   - Estratégia para 1 hora de trabalho concentrado vs maintenance

[CONSTRAINTS]
- Fundador solo — sem time QA, sem QA externo
- Foco prático — não cobertura por cobertura
- Free/open-source tools preferidos
- Stack actual (não mudar para .NET 9 ou Vitest se não for crítico)
- Não inventar APIs — verificar que Playwright/k6 ainda existem 2026

[OUTPUT]
- Ficheiro em Contexto/27-Plano-Testes.md
- Stack recomendada (E2E + load + security + unit)
- 20 cenários E2E críticos com priorização
- 5 cenários de load test
- Pipeline CI/CD proposto (snippet GitHub Actions)
- Plano de implementação por sprint (2-3 sprints de setup, 1-2/mês manutenção)
- Métricas de sucesso (cobertura mínima, suite duration)
```

---

## Prompt #27 — Performance & Caching strategy

```
[CONTEXTO]
RepairDesk SaaS PT, stack .NET 10 + EF Core 10 + SQL Server 2022 + Redis 7.
Actualmente em dogfooding com 1 tenant (LopesTech), 16 reparações, ~20
clientes.

Quando escalarmos para 10-100 lojas (cada com 50-300 reparações/mês),
performance vai começar a importar. Dashboard com gráficos, queries de
histórico, exports CSV — alguns vão ser lentos.

[ESTADO ACTUAL]
- Redis disponível mas só usado para nada (placeholder?)
- Sem cache HTTP / browser
- EF Core queries sem optimização específica
- Indices: básicos (TenantId, Numero, etc.)
- Sem profiling activo
- Sem CDN para assets

[OBJECTIVO]
Plano técnico de performance + caching que escala 10 → 100 → 1000 lojas
sem reescrever o produto.

[REQUISITOS DA RESPOSTA]
1. **Profiling actual** — como identificar bottlenecks:
   - Application Insights / OpenTelemetry com traces SQL
   - MiniProfiler para EF Core?
   - Quais endpoints monitorizar primeiro
2. **Optimização EF Core 10**:
   - AsNoTracking onde aplicável (já parcial)
   - Split queries vs single query
   - Projections (Select específico vs full entity)
   - Compiled queries (overkill?)
   - N+1 detection
3. **Índices SQL Server**:
   - Auditar queries lentas com sys.dm_db_missing_index_details
   - Covering indexes para dashboard queries
   - Filtered indexes (já usamos para soft-delete)
   - Quando faz sentido criar índice composto
4. **Caching strategy**:
   - **HTTP cache** (ETags, Cache-Control headers) — para qual endpoints
   - **Redis cache** (server-side) — para o quê:
     - Tenant settings (raros mudam)
     - Dashboard aggregates (recalcular a cada 5min?)
     - Public portal data (cliente acede X vezes/dia)
   - **In-memory cache** (.NET IMemoryCache) — quando é melhor que Redis
   - Cache invalidation strategy (TTL + manual)
5. **Database scaling path**:
   - SQL Server Express → Standard quando? (volume, RAM)
   - Read replicas (quando faz sentido)
   - Quando considerar PostgreSQL (custo licença SQL Server)
6. **Frontend performance**:
   - Bundle splitting (já temos warning >500KB)
   - Lazy load de rotas (Definicoes, Dashboard charts)
   - Image optimization (logo, fotos antes/depois)
   - React Query cache strategy
7. **CDN**:
   - Cloudflare como proxy (já está em `17-Hosting-Deployment.md`)
   - Assets estáticos vs API responses
   - Service worker caching (alinhar com `24-PWA-Offline.md`)
8. **Métricas alvo realistas**:
   - Dashboard load <500ms p95
   - List reparações 20 items <300ms
   - Portal público <1s LCP
   - CSV import 1000 linhas <30s

[CONSTRAINTS]
- Não over-engineer — cache adds complexity
- Multi-tenant: NUNCA cachar entre tenants sem chave correcta
- EF Core compiled queries só se ROI claro
- Não inventar APIs — verificar EF Core 10 / SQL Server 2022 features 2026
- Manter Docker Compose simples (não Kafka, não Elasticsearch)

[OUTPUT]
- Ficheiro em Contexto/28-Performance-Caching.md
- Checklist de profiling (como medir antes de optimizar)
- Lista de optimizações ordenadas por ROI
- Caching matrix (o quê + onde + TTL + invalidation)
- Roadmap de implementação (3-4 sprints)
- Métricas alvo + como medir
- Riscos (cache inconsistency, stale data)
```

---

## Prompt #28 — Privacy by Design audit técnico (RGPD aplicado à arquitectura)

```
[CONTEXTO]
Já temos `Contexto/16-Compliance-RGPD.md` com privacy policy, ToS, DPA,
processos breach. Esse documento cobre o "lado legal".

Este prompt foca o "lado técnico": **a arquitectura actual está alinhada
com privacy by design** (art. 25.º RGPD)?

[ESTADO ACTUAL]
- Multi-tenant via global query filter (EF Core)
- Soft-delete em todas as entidades
- Dados pessoais identificados: Cliente (nome, telefone, email, NIF),
  Reparacao (IMEI, equipamento), Avaliacao (comentário), DiagnosticoExecucao
- Storage de fotos planeado (Cloudflare R2)
- Logs Serilog em ficheiro (sem retenção definida)
- Backups planeados mas não implementados
- Comunicação interna sem encryption específica (HTTPS apenas)

[OBJECTIVO]
Audit técnico de privacy by design + plano de remediação para tornar
a arquitectura RGPD-defensável antes de abrir a 2-3 lojas reais.

[REQUISITOS DA RESPOSTA]
1. **Mapa de dados pessoais** na arquitectura:
   - Que entidades contêm que dados pessoais
   - Categoria (identificação, contacto, sensível)
   - Onde estão armazenados (DB, R2, logs, cache)
   - Quanto tempo retidos (retention period)
   - Quem tem acesso (que roles)
2. **Princípios privacy by design** (art. 25.º):
   - Data minimization — recolhemos só o necessário?
   - Purpose limitation — usamos só para o fim declarado?
   - Storage limitation — retenção definida e aplicada?
   - Default privacy — settings default são mais privadas?
   - Transparency — utilizador vê os seus dados?
3. **Encryption**:
   - At rest: SQL Server TDE? Coluna-a-coluna para NIF/IMEI?
   - In transit: HTTPS + TLS 1.3 ✅
   - Backups: encryption keys onde
4. **Access control**:
   - Quem pode aceder Cliente.Nome no backend (multi-tenant OK?)
   - Logs do que é acedido (audit log) — sem isto, RGPD art. 30.º não
     funciona
   - Anonymous endpoints (portal público, garantia) — que dados expõem?
5. **Right to be forgotten** (art. 17.º RGPD):
   - Soft-delete actual chega? Não — dados continuam acessíveis a SQL admin
   - Hard-delete com cascade — como implementar sem partir audit log
   - Anonymization (substituir nome/telefone por "Cliente removido")
6. **Data portability** (art. 20.º RGPD):
   - Export CSV cliente individual (já temos export geral) — basta?
   - Formato (JSON, CSV, XML)
7. **Breach detection & notification** (art. 33.º + 34.º):
   - Como detectamos um breach (logs, anomaly detection)
   - Procedimento de notificação à CNPD em <72h
   - Template de email a clientes afectados
8. **Sub-processadores**:
   - Lista actual + futura (Cloudflare R2, provider WhatsApp, Hetzner)
   - DPA com cada
   - Como gerir mudança de sub-processador
9. **Logs / audit**:
   - Que logs guardamos
   - Retenção (90 dias? 1 ano?)
   - Como redagir PII dos logs (não logar telefones em texto plano)
10. **Gaps identificados** + plano de remediação:
    - Críticos (bloqueia Beta)
    - Importantes (próximo mês)
    - Nice-to-have

[CONSTRAINTS]
- Foco PT/EU (não US, não SCC dropdowns)
- Aplicação prática — não citar RGPD em abstracto
- Considerar Bruno solo sem DPO formal
- Não inventar — verificar features SQL Server 2022 actual

[OUTPUT]
- Ficheiro em Contexto/29-Privacy-By-Design-Audit.md
- Mapa de dados pessoais (tabela)
- Gap analysis: o que falta vs RGPD art. 25 / 32 / 17 / 20
- Plano de remediação por prioridade (críticos antes de Beta)
- Snippets técnicos para implementar (encryption coluna, anonimização)
- Riscos legais identificados
```

---

## Prompt #29 — Strategy de Release + Versioning + Changelog público

```
[CONTEXTO]
RepairDesk SaaS PT vai começar a ter clientes pagantes em 2-6 meses. A
partir desse momento, cada update tem de ser comunicado, rastreável e
não-quebrar funcionalidade existente.

Estado actual:
- Git repo com commits ad-hoc
- Sem semantic versioning
- Sem changelog
- Sem janela de manutenção planeada
- Sem comunicação prévia de breaking changes
- Migrations EF Core forward-only (não rollback automático)

[OBJECTIVO]
Plano operacional de release management para fundador solo, com
ferramentas e processos sustentáveis (não enterprise-bureaucracy).

[REQUISITOS DA RESPOSTA]
1. **Versioning strategy**:
   - Semantic versioning (MAJOR.MINOR.PATCH) ou date-based (CalVer)?
   - Como aplicar a SaaS (vs library)
   - Pre-releases (alpha, beta, rc) — vale a pena para fundador solo?
2. **Release cadence**:
   - Continuous deployment? Weekly? Monthly?
   - Hotfix workflow (security/data bug crítico)
   - Quando agrupar mudanças vs deploy individual
3. **Changelog público**:
   - Onde publicar (subdomain `changelog.repairdesk.pt`? In-app modal?
     Email? GitHub Releases?)
   - Estrutura (Added/Changed/Fixed/Deprecated/Security)
   - Tom: cliente final (não dev jargon)
   - Quem escreve (Bruno solo — quanto tempo/release)
4. **Breaking changes policy**:
   - O que conta como breaking (API, UI, comportamento)
   - Aviso prévio mínimo (30/60/90 dias?)
   - Deprecation period antes de remover
   - Como comunicar (banner in-app, email)
5. **Database migrations**:
   - Forward-only (já temos) — confirmar boa prática
   - Como gerir alterações destrutivas (rename, drop column)
   - Backup pré-migration obrigatório
6. **Deploy pipeline**:
   - GitHub Actions: build → test → security scan → deploy
   - Smoke tests pós-deploy
   - Rollback strategy (revert commit + redeploy)
   - Blue-green vs in-place
7. **Janela de manutenção**:
   - Quando avisar clientes (24h? 48h?)
   - Horário óptimo PT (madrugada de domingo?)
   - Quanto downtime tolerável (5min? zero?)
8. **In-app banner / notificação**:
   - "Vamos atualizar amanhã às 3h" — implementação técnica
   - "Bug fix aplicado — atualiza a página"
   - Versão visível em footer
9. **Métricas a seguir**:
   - Tempo médio entre release e bug report
   - Releases/mês
   - Hotfixes/mês

[CONSTRAINTS]
- Fundador solo — não criar overhead que consume mais tempo que features
- Free/open-source tools preferidos
- Honesto: comunicar bugs publicamente (post-mortem se crítico)
- Não criar 5 ambientes (dev/staging/prod chega)

[OUTPUT]
- Ficheiro em Contexto/30-Release-Strategy.md
- Versioning scheme escolhido com justificação
- Template de changelog (markdown)
- Workflow de release passo-a-passo
- Snippet GitHub Actions para CI/CD
- Templates de comunicação (banner pre-deploy, email post-deploy, post-mortem)
- Roadmap de implementação (1-2 sprints)
```

---

## Prompt #30 — Sales Playbook + Demo Flow para fundador solo

```
[CONTEXTO]
RepairDesk SaaS PT está perto de Beta. Bruno vai começar a fazer demos a
2-3 lojas amigas em Viseu/Porto. Sem experiência prévia em sales B2B,
precisa de processo claro: como apresentar, que perguntas fazer, como
fechar.

Já temos `Contexto/09-Customer-Acquisition.md` (estratégia macro de canais)
e `Contexto/07-Pricing-Proposta.md` (tiers €19/39/89). Falta o "como
falar com a 1ª loja".

[ESTADO ACTUAL]
- Bruno faz reparações há anos — domina o problema do utilizador
- Sem experiência em sales B2B
- Sem CRM (Pipedrive/HubSpot)
- Sem template de demo
- Sem follow-up automatizado

[OBJECTIVO]
Sales playbook prático para fundador solo levar uma loja de "primeiro
contacto" a "cliente beta a pagar" em 7-14 dias, sem comprar Pipedrive
ou contratar SDR.

[REQUISITOS DA RESPOSTA]
1. **Discovery call** (1ª conversa, 20-30 min):
   - Estrutura: abrir, descobrir, qualificar, próximo passo
   - Perguntas-chave (qualificação BANT adaptada a SaaS B2B PT)
   - Sinais de bom prospect vs perda de tempo
   - Como acabar (próximo passo claro)
2. **Demo flow** (30-45 min):
   - Estrutura: contexto cliente, problema, solução, valor, perguntas
   - 5 momentos "wow" do RepairDesk a destacar (portal cliente Uber-style,
     diagnóstico guiado, garantia QR, Health Score, ...)
   - Como adaptar ao tipo de loja (telemóveis vs computadores)
   - O que NÃO mostrar (overwhelm)
   - Mockups vs produto real (já temos produto — usar)
3. **Objections handling** (PT-PT, lojas pequenas):
   - "Já uso Excel há 10 anos" → resposta
   - "É caro para mim" → resposta + comparação ROI
   - "Vou pensar" → resposta + próximo passo
   - "Não confio em mais um SaaS" → resposta
   - "Os meus técnicos não usam computador" → resposta
4. **Pricing conversation**:
   - Quando introduzir preço (após valor demonstrado)
   - Como apresentar tiers (anchoring, comparações)
   - Beta especial (€29/mês vitalício discutido em pricing)
   - Como dar desconto sem desvalorizar
5. **Close**:
   - Soft close: "Que tal experimentares 30 dias?"
   - Hard close: "Posso criar a tua conta agora?"
   - Trial vs primeiro mês a pagar
6. **Follow-up**:
   - 1h depois da demo (email com resumo + link)
   - 24h (check-in)
   - 48h (case study / testemunho de outra loja)
   - 7 dias (last call ou desistir)
   - Quando parar de fazer follow-up
7. **Sistema simples**:
   - CRM minimalista (Google Sheets? Notion? Airtable free?)
   - Pipeline: Lead → Qualified → Demo → Trial → Customer
   - Templates de email PT-PT (5 templates essenciais)
8. **Métricas**:
   - Conversion rate por estágio
   - Tempo de ciclo
   - Razões de perda (categorizar)

[CONSTRAINTS]
- Fundador solo — sem SDR, sem AE, sem time
- Lojas pequenas — não usar techbro speak ("synergy", "platform")
- PT-PT autêntico
- Honesto — não over-promise para fechar
- Avoid CRM enterprise (não pagar Salesforce)

[OUTPUT]
- Ficheiro em Contexto/31-Sales-Playbook.md
- Script de discovery call (20 min)
- Script de demo (40 min) com 5 wow-moments destacados
- 10 objections handling em PT-PT
- 5 templates de email follow-up
- Sistema CRM minimalista (Google Sheets template ASCII)
- Métricas e quando reagir a elas
- Plano para primeiras 3 lojas amigas (Viseu/Porto)
```

---

## Prompt #31 — Audit UX/UI completo do RepairDesk + roadmap visual

```
[CONTEXTO]
RepairDesk SaaS PT em estado avançado de desenvolvimento (Sprints 14-29
concluídos). Bruno é técnico, não designer profissional. UI actual usa
Tailwind v4 + zinc/brand-500 default sky-blue.

Bruno disse expressamente: "tu sabes que a usabilidade ux design e
dashboard tem que ser melhorado... falta mesmo muita coisa para isto ser
um dashboard profissional".

Estado actual visível:
- Dashboard: 4 KPI cards, gráfico SVG 6 meses, secção Em Curso agrupada,
  top reparações, alertas de itens por cobrar e despesas órfãs, widget
  de avaliações com média/distribuição/NPS
- Listas: clientes/reparações/trabalhos/despesas/precos — todas em tabela
  ou cards com filtros básicos
- Reparação detalhe: blocos verticais (cabeçalho, workflow stepper, detalhes,
  diagnóstico guiado, fotos, despesas, preço & lucro, timeline)
- Portal cliente público: design Apple-like com timeline visual, cards
  de garantia/avaliação/health score/fotos
- Sidebar colapsável com hover (estilo Notion)
- Dark mode 3 estados

[OBJECTIVO]
Audit completo de UX/UI do RepairDesk com roadmap concreto de melhorias,
priorizadas por impacto vs esforço. Bruno está atrasado a Beta porque
sente que "ainda está fraco" — precisa de saber EXACTAMENTE o que mexer.

[REQUISITOS DA RESPOSTA]
1. **Avaliação geral** (10 critérios standard SaaS B2B):
   - Hierarquia visual (o que chama atenção primeiro)
   - Consistência (espaçamento, tipografia, cores)
   - Densidade de informação (excesso vs deserto)
   - Affordance (o que é clicável é óbvio?)
   - Feedback (loading states, success, error)
   - Mobile responsiveness
   - Acessibilidade (WCAG AA mínimo)
   - Performance percebida (cls, lcp)
   - Onboarding (empty states que ensinam)
   - Erro recovery (mensagens claras)
2. **Análise por ecrã principal**:
   - Dashboard `/`
   - Lista reparações `/reparacoes` (lista vs kanban)
   - Detalhe reparação `/reparacoes/:id`
   - Definições `/definicoes`
   - Tabela de preços `/precos`
   - Portal cliente público `/r/:slug`
   - Portal garantia `/g/:slug`
   Para cada: pontos fortes, pontos fracos, propostas concretas
3. **Quick wins** (alto impacto, baixo esforço — <1 dia cada):
   - Lista 10-15 mudanças que tornam o produto sentir-se 30% mais
     polido sem reescrever nada
4. **Refactors UX médios** (1-3 dias cada):
   - 5-7 mudanças que requerem refactor de componente
5. **Mudanças grandes** (>3 dias):
   - 2-3 mudanças estruturais que valem a pena (ex: command palette,
     unified search global, redesign do dashboard)
6. **Inspiração concreta**:
   - Que SaaS B2B fazem este tipo de coisa bem (Linear, Pipedrive,
     Stripe Dashboard, Vercel)
   - O que copiar especificamente (não "ser como Linear" mas "usar este
     padrão da Linear")
7. **Paleta + tipografia**:
   - brand-500 actual #0EA5E9 é boa escolha? Alternativas defensáveis
   - System fonts vs Inter vs outras
   - Spacing scale revisão
8. **Componentes a destacar/criar**:
   - Toast notifications (temos? falta?)
   - Modal stacking (rule e cuidados)
   - Empty states padronizados (template reutilizável)
   - Skeleton loaders vs spinners
9. **Anti-padrões a remover**:
   - Identificar 5-10 coisas que estamos a fazer mal

[CONSTRAINTS]
- Bruno é técnico não designer — propor coisas implementáveis
- Não pedir Figma / mockups gigantescos
- Foco PT-PT (não copy-paste de inglês)
- Stack actual (Tailwind v4 + React 19) — não propor mudar
- "Polished but not over-designed" — não Stripe Dashboard nível
- Realista: Bruno solo, não tem 6 meses para refactor completo
- Honesto: se algo está bom, dizer; não inventar problemas

[OUTPUT]
- Ficheiro em Contexto/32-Audit-UX-UI.md
- Score por critério (1-5 para cada um dos 10)
- Lista priorizada de mudanças (quick wins → médios → grandes)
- Mockups ASCII para 3-5 propostas-chave
- Roadmap de implementação (que sprint fazer cada coisa)
- Inspiração com links (Linear pattern X, Pipedrive feature Y)
- Lista de packages NPM úteis (se aplicável)

[VERIFICAÇÃO]
- Bruno consegue listar 10 quick wins para fazer esta semana
- Tem clareza sobre o que NÃO é problema (validação positiva)
- Sabe se a UI actual está "8/10" ou "5/10"
```

---

# Codex CODING tasks (não research)

> **Importante**: estes prompts pedem ao Codex para **escrever código** que vai ser commitado, não documentação. Cada um abre PR no branch `codex/sprint-XX-feature-Y`. Bruno faz code review com Prompt #6 antes de merge.

---

## Codex Coding #C1 — Implementar Stock de Peças (entidade + endpoints + UI)

```
[CONTEXTO]
RepairDesk SaaS PT. Stack:
- Backend: .NET 10 + EF Core 10 + SQL Server 2022, Clean Architecture
  (Core/DAL/Services/API), multi-tenant via global query filter
- Frontend: React 19 + Vite + Tailwind v4 + React Query 5 + lucide-react
- Padrões existentes para seguir (LER PRIMEIRO):
  - Entidade `Cliente` → `RepairDesk.Core/Entities/Cliente.cs`
  - Serviço `ClienteService` → `RepairDesk.Services/Clientes/ClienteService.cs`
  - Controller `ClientesController` → `RepairDesk.API/Controllers/ClientesController.cs`
  - Página `/clientes` → `frontend/src/pages/clientes/Clientes.tsx`
  - Import CSV em `ClienteService.ImportCsvAsync` (modelo a seguir)

[ESTADO ACTUAL]
Despesas (entidade `Despesa`) já existe e tem campos `Categoria` (Pecas/Material/...).
Mas não há gestão de stock — só registo de despesas pontuais.

Tabela de Preços (`PriceTableEntry`) já existe em `RepairDesk.Core/Entities/PriceTableEntry.cs`.

[OBJECTIVO]
Implementar gestão de **stock de peças** completo, alinhado com Sprint 5 do
`Contexto/RepairDesk_DOCUMENTO_DEFINITIVO.docx`.

[TAREFAS]
1. **Backend**:
   a. Criar entidade `Part` com:
      - `Sku` (string, único por tenant, opcional)
      - `Nome` (required)
      - `Categoria` (reaproveitar enum `DespesaCategoria` ou novo `PartCategoria`)
      - `Marca`, `Modelo` (relacional com PriceTableEntry — opcional FK ou texto livre)
      - `QtdStock` (int)
      - `QtdMinima` (int) — gatilho de alerta
      - `CustoUnitarioCents` (int)
      - `Fornecedor` (string, opcional)
      - `LocalArmazenamento` (string, ex: "Prateleira A3")
      - `Notas`
      - `Activo` (bool)
   b. EF Core configuration + Migration `Sprint31Stock`
   c. Repository + Service + Controller (CRUD)
   d. Endpoint específico `GET /api/parts/low-stock` (qty <= qtdMinima)
   e. Movimentos de stock — entidade `PartMovimento`:
      - `PartId`, `Quantidade` (negativa para saída, positiva para entrada)
      - `Motivo` enum (Entrada, Saida, AjusteManual, UsoEmReparacao, Devolucao)
      - `ReparacaoId` (opcional — quando peça é usada numa reparação)
      - `Notas`
   f. Endpoint `POST /api/parts/{id}/movimento` que aplica delta ao stock
   g. Testes xUnit:
      - Criar peça, ajustar stock, link a reparação, low-stock filtering
      - Multi-tenant: tenant A não vê peças de tenant B
2. **Frontend**:
   a. Nova página `/stock` no menu (entre Despesas e Preços)
   b. Lista de peças (tabela): SKU, nome, marca/modelo, stock actual, mínimo,
      custo, valor total stock (qty * custo), badge "Stock baixo" se aplicável
   c. Filtros: categoria, marca, "só stock baixo"
   d. Form criar/editar peça (modal)
   e. Modal "Ajustar stock" com motivo + qty + notas
   f. Drill-down: clicar numa peça → ver histórico de movimentos
   g. Import CSV (mesma UX dos clientes/reparações)
3. **Integração com Reparação** (importante mas separável):
   a. No detalhe da reparação, secção "Peças usadas" — autocomplete por SKU/nome
   b. Ao adicionar peça à reparação, registar `PartMovimento` (Saida, qty -1, link ReparacaoId)
   c. Recalcula `CustoPecasCents` automaticamente

[CONSTRAINTS]
- Seguir padrões existentes (não inventar arquitectura nova)
- Multi-tenant: TODOS os queries com global filter (NUNCA usar IgnoreQueryFilters sem motivo)
- Migration forward-only, não destrutiva
- Frontend usa `lucide-react` para icons (não emojis)
- Usar componente `Button` em `frontend/src/components/ui/Button.tsx` e
  `StatusBadge` em `frontend/src/components/ui/StatusBadge.tsx`
- Usar `toast.success/error` de `sonner` para feedback
- PT-PT em todos os labels
- NÃO mexer em ficheiros fora do scope (não refactor Cliente.cs, etc.)
- NÃO commitar credenciais
- Adicionar `void Remove(Part part)` ao repo (soft delete via BaseEntity)
- Tipos: usar `int` para cents (não decimal), Guid para IDs

[OUTPUT ESPERADO]
- Branch: `codex/sprint-31-stock-pecas`
- Commits atomicos (entidade → migration → repository → service → controller → tests → frontend)
- Lista de ficheiros criados/alterados
- `dotnet build` passa
- `dotnet test` passa (com novos testes)
- `npm run build` passa
- README/CHANGELOG actualizado

[VERIFICAÇÃO]
- Bruno cria peça via UI
- Cria reparação e adiciona peça → stock decrementa
- Lista de "stock baixo" mostra peças correctamente
- Importa CSV de 10 peças → todas aparecem
- Sem warnings novos no build
```

---

## Codex Coding #C2 — Implementar Onboarding Wizard (conforme spec 12-Onboarding-Wizard.md)

```
[CONTEXTO]
RepairDesk SaaS PT. O onboarding wizard está especificado em
`Contexto/12-Onboarding-Wizard.md` (LER PRIMEIRO).

[ESTADO ACTUAL]
- Página `/definicoes` já existe com tabs Empresa/Fiscal/Pagamentos/Pós-venda/Aparência
- Quando um utilizador faz signup, vai direto para `/` (Dashboard) sem onboarding
- Tenant `IsActive` campo existe mas não é usado para gates

[OBJECTIVO]
Implementar wizard de 5 passos que aparece na primeira sessão de um
utilizador novo e leva-o de "criou conta" a "primeira reparação registada"
em menos de 30 minutos.

[TAREFAS]
1. **Backend**:
   a. Adicionar campo `OnboardingCompletado` (bool) a `Tenant`
   b. Migration `Sprint32OnboardingFlag`
   c. Endpoint `POST /api/tenant-settings/me/onboarding/complete` → marca true
   d. Endpoint `GET /api/tenant-settings/me/onboarding/status` → retorna
      progresso (que steps já estão preenchidos baseado em estado actual)
2. **Frontend**:
   a. Componente `OnboardingWizard` em rota separada `/bemvindo`
   b. 5 passos conforme `12-Onboarding-Wizard.md`:
      1. Dados da empresa (logo, NIF, IBAN) — reutilizar form de Definições
      2. Primeiro cliente (com opção "saltar com cliente exemplo")
      3. Primeira reparação demo
      4. Tour rápido do dashboard (modal overlay com 3-4 tooltips)
      5. Convidar funcionário (skipável)
   c. Detect: se `tenant.OnboardingCompletado === false` E é primeira sessão,
      redirect automático para `/bemvindo`
   d. Progress bar visível ("Passo 2 de 5") + skip individual de passos
   e. Botão "Sair do wizard" no canto (marca como completo, sem perder dados)
   f. Empty state actual no Dashboard deve referir o wizard se incompleto
3. **UI**:
   a. Layout limpo, mobile-first
   b. Usar `Button` e `StatusBadge` existentes
   c. Animação suave entre passos (não slidey-disco — fade simples)
   d. Tooltips do tour usam `Popover` (criar se não existir)

[CONSTRAINTS]
- Multi-tenant: cada tenant tem seu próprio status de onboarding
- Wizard é skipável a qualquer momento — não bloqueio
- Não criar 20 ficheiros novos — manter em ~6-8
- PT-PT autêntico
- NÃO usar bibliotecas pesadas para tour (Intro.js, Shepherd) — implementar com
  primitivos Tailwind + Headless UI (já temos? confirmar)
- Não partir testes existentes

[OUTPUT ESPERADO]
- Branch: `codex/sprint-32-onboarding-wizard`
- Migration + endpoints + UI funcionais
- Lista de ficheiros criados/alterados
- `dotnet test` passa
- Bruno cria uma conta nova de teste e completa wizard em <5 min

[VERIFICAÇÃO]
- Conta nova: redirect automático para /bemvindo
- Conta existente com flag true: nunca vê wizard
- Saltar passos individuais funciona
- "Sair" marca completo sem perder dados
```

---

## Codex Coding #C3 — Primitives UI (PageHeader + EmptyState + Skeleton + applicação global)

```
[CONTEXTO]
RepairDesk frontend usa React 19 + Tailwind v4 + lucide-react + sonner.
O `Contexto/32-Audit-UX-UI.md` aponta:
- Quick win #3: criar `PageHeader` reutilizável (título, descrição, estado, acção principal)
- Quick win #4: criar `EmptyState` padrão com título, texto, CTA e exemplo
- Quick win #6: substituir textos "A carregar..." por skeletons em KPIs/listas/cards
- Quick win #10: adicionar `focus-visible` ring consistente

Já existem (LER PRIMEIRO):
- `frontend/src/components/ui/Button.tsx` (Button reutilizável)
- `frontend/src/components/ui/StatusBadge.tsx` (StatusBadge reutilizável)
- `frontend/src/lib/toast.ts` (wrapper sonner)
- Páginas a actualizar: `Dashboard.tsx`, `Reparacoes.tsx`, `Clientes.tsx`, `Trabalhos.tsx`,
  `Despesas.tsx`, `Precos.tsx`, `Definicoes.tsx`

[OBJECTIVO]
Criar 3 primitives reutilizáveis e aplicá-los a TODAS as páginas internas,
sem partir layouts existentes nem mudar URLs/comportamentos.

[TAREFAS]
1. **Componentes novos** em `frontend/src/components/ui/`:
   a. `PageHeader.tsx`:
      - Props: `title` (string), `description?` (string), `breadcrumb?` (string[]),
        `actions?` (ReactNode), `meta?` (ReactNode — ex: total count)
      - Layout: título grande à esquerda, descrição cinza por baixo,
        acções à direita (alinhamento end), breadcrumb opcional em cima
      - Responsivo (acções colapsam em mobile)
   b. `EmptyState.tsx`:
      - Props: `icon` (lucide Icon component), `title`, `description`,
        `action?` (ReactNode), `compact?` (boolean — para empty inside cards)
      - Visual: icon centralizado em círculo zinc-100, título, descrição,
        CTA opcional
   c. `Skeleton.tsx` + variantes `SkeletonCard`, `SkeletonRow`, `SkeletonTable`:
      - Animação pulse com `animate-pulse` Tailwind
      - SkeletonCard: caixa rounded-xl 80px height
      - SkeletonRow: linha 12px com larguras configuráveis
      - SkeletonTable: skeleton de tabela com header + N linhas
2. **Aplicação global** (search-and-replace selectivo):
   a. Substituir cabeçalhos `<h1>... <p>... <button>+ Novo</button>` ad-hoc por
      `<PageHeader>` em: Dashboard, Reparacoes, Clientes, Trabalhos, Despesas, Precos, Definicoes
   b. Substituir empty states ad-hoc ("Sem resultados", "Sem dados") por
      `<EmptyState>` com icon apropriado de lucide-react
   c. Substituir `<div>A carregar…</div>` por skeletons apropriados (Card/Row/Table)
   d. Adicionar `focus-visible:ring-2 focus-visible:ring-brand-400` em buttons/inputs/links
      que ainda não têm
3. **Não mexer em**:
   - Componentes em /pages que já têm boa estrutura (ex: PortalCliente.tsx)
   - Comportamentos: páginas devem continuar a funcionar exactamente igual
   - Tipos: não introduzir generic types complicados

[CONSTRAINTS]
- Stack actual: React 19 + Tailwind v4 + lucide-react (NÃO usar Headless UI, Radix, etc.)
- Manter consistente com Button/StatusBadge existentes (espaçamento, raios)
- Não criar 20 ficheiros — apenas 4 (PageHeader, EmptyState, Skeleton, index.ts barrel)
- PT-PT em copy
- Mobile-first
- Tipescript estrito (no `any`)
- Não partir tests existentes

[OUTPUT ESPERADO]
- Branch: `codex/sprint-33-ui-primitives`
- 4 ficheiros novos em `components/ui/`
- ~7 páginas refactorizadas para usar os primitives
- Screenshots antes/depois (mockup ASCII no PR description é OK)
- `npm run build` passa
- Sem erros TypeScript

[VERIFICAÇÃO]
- Bruno carrega cada página e vê título consistente (PageHeader)
- Listas vazias mostram EmptyState útil (não "Sem resultados.")
- Loading shows skeleton (não texto)
- Tab navigation tem focus rings visíveis
```

---

## Codex Coding #C4 — Setup GitHub Actions CI/CD pipeline

```
[CONTEXTO]
RepairDesk SaaS PT. Estado actual:
- Repo: github.com/brunolopes9/lopestech (este projecto está em RepairDesk/)
- Sem CI/CD — Bruno corre manualmente `dotnet build && dotnet test && npm run build`
- Sem deploy automático
- 50 testes xUnit a passar
- 0 testes frontend (Playwright planeado em `27-Plano-Testes.md`)

Documentação relevante (LER PRIMEIRO):
- `Contexto/30-Release-Strategy.md` — versioning, release cadence, deploy
- `Contexto/17-Hosting-Deployment.md` — Hetzner VPS como destino
- `Contexto/27-Plano-Testes.md` — estratégia de testes

[OBJECTIVO]
Criar pipeline CI/CD completo em GitHub Actions: PRs validados
automaticamente, merge em `main` faz deploy automático para staging.
Produção fica manual (push de tag).

[TAREFAS]
1. **`.github/workflows/ci.yml`** (corre em todos os PRs e push para main):
   - Job `backend`:
     - setup .NET 10
     - `dotnet restore`
     - `dotnet build --no-restore`
     - `dotnet test --no-build --logger trx`
     - Upload test results como artifact
   - Job `frontend`:
     - setup Node 22 (compatível com Vite 8)
     - `npm ci` em RepairDesk/frontend
     - `npm run build`
   - Job `security`:
     - Dependabot config para .NET e npm
     - CodeQL scan (.NET + TypeScript)
     - Secret scanning (gitleaks action)
   - Job `lint` (frontend):
     - `npm run lint` (criar script se não existir)
2. **`.github/workflows/deploy-staging.yml`** (corre em push para main):
   - Build Docker images (api + web)
   - Push para GitHub Container Registry (ghcr.io)
   - SSH para servidor staging (Hetzner), `docker compose pull && up -d`
   - Smoke test pós-deploy (curl /api/health)
   - Slack/Discord notification (opcional, configurável via secret)
3. **`.github/workflows/deploy-production.yml`** (corre em push de tag `v*.*.*`):
   - Mesmo que staging mas para servidor produção
   - Requires manual approval (GitHub Environments)
4. **`.github/dependabot.yml`**:
   - Updates semanais para nuget, npm, docker, github-actions
5. **CHANGELOG.md** na raiz:
   - Template Keep a Changelog
   - Pre-popular com versão 0.1.0 (estado actual)
6. **`docker-compose.prod.yml`**:
   - Versão de produção: sem build, usa imagens do registry
   - Sem expor portas DB/Redis (só rede privada)
   - Healthchecks robustos
7. **README.md** actualizado com:
   - Badges de CI status
   - Instruções de release (tag → deploy)

[CONSTRAINTS]
- Stack: .NET 10, Node 22, Docker
- Free tier GitHub Actions (2000 min/mês) — não rebuild tudo em cada PR
- Cache npm e nuget agressivamente
- Sem ferramentas pagas (Datadog, Snyk Pro, etc.) — gratuitas/open-source
- Secrets via GitHub Secrets (não no código)
- SSH keys via secrets, não inline
- NÃO commitar `.env.production` real (template `.env.production.example`)
- Compatible com tags v0.1.0 → v0.2.0 → ...

[OUTPUT ESPERADO]
- Branch: `codex/sprint-34-cicd`
- 3 workflows YAML
- dependabot.yml
- CHANGELOG.md
- docker-compose.prod.yml
- README actualizado
- Documentação `Contexto/33-CI-CD-Setup.md` com:
  - Quais secrets configurar em GitHub
  - Como fazer primeiro deploy
  - Como cortar uma release

[VERIFICAÇÃO]
- Bruno abre PR de teste, vê checks a correr
- Merge em main triggera deploy staging
- Push tag `v0.1.0` triggera workflow de produção (com approval)
- Sem secrets expostos em logs
```

---

## Codex Coding #C5 — Cloudflare R2 storage adapter

```
[CONTEXTO]
RepairDesk SaaS PT já tem upload de fotos com `IPhotoStorage` interface
(em `RepairDesk.Core/Abstractions/IPhotoStorage.cs`).
Implementação actual: `LocalFileSystemPhotoStorage` (volume Docker
`/data/photos`) em `RepairDesk.Infrastructure/Storage/`.

Documentação (LER PRIMEIRO):
- `Contexto/14-Storage-Fotos.md` — decisão final: Cloudflare R2 (S3-compat, EU, zero egress)
- Cloudflare R2 SDK: usa `AWSSDK.S3` standard (endpoint customizado)

[OBJECTIVO]
Implementar `CloudflareR2PhotoStorage` como segunda implementação de
`IPhotoStorage`, registável via configuração — sem mudar código de
consumidores.

[TAREFAS]
1. **Backend**:
   a. Adicionar package `AWSSDK.S3` (versão estável compatível com .NET 10)
   b. Criar `RepairDesk.Infrastructure/Storage/CloudflareR2PhotoStorage.cs`:
      - Construtor recebe `IConfiguration` (lê `Storage:R2:*`)
      - Métodos:
        - `UploadAsync(key, stream, contentType)` → PutObjectRequest
        - `DownloadAsync(key)` → GetObject e retorna stream
        - `DeleteAsync(key)` → DeleteObject (idempotente)
        - `ExistsAsync(key)` → HeadObject (try/catch 404)
      - Reutiliza `IAmazonS3` como singleton
   c. Helper `R2StorageOptions` com validação (AccountId, AccessKey, Secret, Bucket)
   d. Selector em `Program.cs`:
      - Lê `Storage:Provider` (env var): `local` (default) ou `r2`
      - Se `r2`, regista `CloudflareR2PhotoStorage`
      - Se `local`, regista `LocalFileSystemPhotoStorage` (já existe)
   e. `appsettings.json` + `appsettings.Development.json` documentam keys (sem valores)
   f. Testes:
      - `R2StorageOptionsTests` — validação config (sem chamar API real)
      - Integration test com mock S3 (Moq + IAmazonS3)
2. **Documentação**:
   a. Actualizar `Contexto/14-Storage-Fotos.md` ou criar `Contexto/34-R2-Setup.md`:
      - Passo-a-passo de setup R2 (criar conta, bucket, API token)
      - Environment vars a configurar
      - Como migrar fotos de local → R2 (script one-shot)
      - Custos esperados
3. **Migration script** (opcional mas útil):
   - `scripts/migrate-photos-to-r2.csx` ou similar — lê de volume Docker,
     faz upload para R2, actualiza `StorageKey` na DB

[CONSTRAINTS]
- NÃO mexer no código existente que usa `IPhotoStorage` — só adicionar implementação
- R2 endpoint: `https://{accountId}.r2.cloudflarestorage.com`
- Multi-tenant: storage key já inclui `tenants/{T}/...`, não mexer
- Lazy init de IAmazonS3 (evitar crash se config ausente em dev)
- Default config: `Storage:Provider=local` (Bruno não precisa de R2 para dev)
- NÃO inventar APIs S3 — usar AWSSDK standard
- NÃO commitar credenciais (env vars only)

[OUTPUT ESPERADO]
- Branch: `codex/sprint-35-r2-storage`
- Novo ficheiro `CloudflareR2PhotoStorage.cs`
- Tests unitários
- Documentação setup
- `dotnet build` + `dotnet test` passam
- Sem regressão no upload existente (LocalFileSystem continua default)

[VERIFICAÇÃO]
- Bruno cria conta R2 (Cloudflare), bucket, API token
- Mete env vars `Storage__Provider=r2 + Storage__R2__*`
- Restart containers
- Upload de foto vai para R2 (verifica no dashboard Cloudflare)
- Switch back para `Storage__Provider=local` continua a funcionar
```

---

## Codex Coding #C6 — Backup automático SQL Server + R2

```
[CONTEXTO]
RepairDesk SaaS PT em Docker Compose (SQL Server, API .NET 10, web).
`Contexto/34-Beta-Launch-Criteria.md` marca backup automático como
MUST-HAVE bloqueador de beta. `Contexto/18-Backup-DR.md` tem o runbook
operacional (RPO 24h, RTO 4h, retention 30d, off-site EU).

[OBJECTIVO]
Backup diário automático da DB SQL Server, retention 30 dias local,
cópia para Cloudflare R2 (off-site EU). Restore testável.

[TAREFAS]
1. **Backend**:
   a. `RepairDesk.API/HostedServices/BackupHostedService.cs`:
      - `BackgroundService` que corre cron diário às 03:00 (configurável)
      - Executa `BACKUP DATABASE [RepairDesk] TO DISK = '/backups/repairdesk-{yyyyMMdd-HHmm}.bak' WITH INIT, COMPRESSION, CHECKSUM`
      - Upload do .bak para R2 bucket `backups/`
      - Apaga backups locais > 30 dias
   b. Configuração via `Backup:Enabled`, `Backup:CronSchedule`, `Backup:RetentionDays`,
      `Backup:R2:Bucket`
   c. Default `Backup:Enabled=false` em dev; `true` em produção (via env var)
   d. Endpoint admin `POST /api/admin/backup/now` (apenas role admin) para
      backup manual on-demand
   e. Endpoint admin `GET /api/admin/backup/list` lista backups
      (local + R2) com timestamp, tamanho, status
2. **Docker**:
   a. Volume `backups` no `docker-compose.yml` montado em API e SQL Server
   b. Healthcheck do backup: ficheiro mais recente < 26h (alerta se passou
      mais de 26h sem backup)
3. **Runbook**:
   a. Actualizar `Contexto/18-Backup-DR.md` com:
      - Comando exacto de restore (`RESTORE DATABASE`)
      - Como descarregar backup do R2 manualmente
      - Script `scripts/restore-from-r2.sh` parametrizado
4. **Testes**:
   - `BackupHostedServiceTests` — config validação
   - Integration test: backup local cria ficheiro, retention apaga antigos
     (sem chamar R2)

[CONSTRAINTS]
- NÃO usar SQL Server Agent (não está disponível em SQL Express/Linux container)
- BACKUP corre dentro do container SQL Server via T-SQL (chamado de fora pela API)
- Backups NÃO contêm dados de tenant exportados — é raw DB
- R2 credentials reutilizam `Storage:R2:*` se existir; senão `Backup:R2:*`
- NÃO commitar backups (`backups/` no .gitignore)
- Encriptação at-rest: R2 Cloudflare já encripta, OK para beta
- Em dev (`Backup:Enabled=false`) o BackgroundService nem se regista
- Logs estruturados: `BackupStarted`, `BackupCompleted`, `BackupFailed`,
  `BackupUploaded`, `BackupRetentionApplied`

[OUTPUT ESPERADO]
- Branch `codex/sprint-36-backup-automatico`
- HostedService + admin endpoints + tests
- docker-compose volume
- Runbook actualizado
- `Backup:Enabled=true` em produção via env var, OFF em dev

[VERIFICAÇÃO]
- Bruno corre `docker compose up`, espera 24h, verifica em /api/admin/backup/list
- Aparece um backup .bak local + R2
- Tenta restore num container fresco — `RESTORE DATABASE` funciona
- Após 31 dias, backup do dia 0 já não está local mas continua em R2
```

---

## Codex Coding #C7 — Audit log + RGPD UI (exportar + apagar definitivamente)

```
[CONTEXTO]
`Contexto/34-Beta-Launch-Criteria.md` marca dois MUST-HAVEs RGPD:
- Direito ao esquecimento (apagar definitivamente cliente + relacionados)
- Portabilidade (Art. 20.º — exportar todos os dados de 1 cliente em JSON)

Além disso falta `audit log` (quem fez o quê e quando) para defesa em
caso de incidente.

`Contexto/29-Privacy-By-Design-Audit.md` é referência.

[OBJECTIVO]
1. Audit log de operações de escrita (não overhead em reads).
2. UI RGPD para o operador de tenant: exportar JSON portable + apagar
   definitivamente um cliente com todos os relacionados.

[TAREFAS]
1. **Backend audit log**:
   a. Entidade `AuditEntry` (TenantId, AppUserId, Action enum
      [Create/Update/Delete/HardDelete/Login/Export], EntityType,
      EntityId, ChangesJson, IpAddress, UserAgent, CreatedAt)
   b. `IAuditLogger` interface + `EfAuditLogger` implementação
   c. Hook em `AppDbContext.SaveChangesAsync` que captura
      `EntityState.Added/Modified/Deleted` e regista entry
   d. NÃO registar reads (overhead). NÃO registar valores sensíveis
      (password hashes — usar lista negra)
   e. Endpoint `GET /api/audit?entityType=Cliente&entityId={id}&from=&to=`
      (admin role)
   f. Página `/auditoria` no admin — lista paginada com filtros
2. **Backend RGPD**:
   a. Endpoint `GET /api/clientes/{id}/exportar` → JSON com:
      - Cliente (todos os campos)
      - Reparações + Trabalhos + Despesas relacionadas
      - Fotos (URLs assinadas válidas 7 dias)
      - Avaliações + Garantias
      - Audit entries do cliente
   b. Endpoint `DELETE /api/clientes/{id}/hard-delete` (admin only):
      - Pede confirmação no payload `{ confirm: "APAGAR <nome>" }`
      - Apaga fisicamente o cliente + reparações + trabalhos + despesas
        + fotos (storage) + audit entries — IRREVERSÍVEL
      - Regista uma última audit entry `HardDelete` com motivo
3. **Frontend RGPD UI**:
   a. Na página do cliente (`/clientes/{id}`), botão "Exportar dados"
      que faz download do JSON
   b. Botão "Apagar definitivamente (RGPD)" — abre modal com warning
      forte, requer digitar o nome do cliente para confirmar
   c. Audit log: nova rota `/auditoria` com filtros

[CONSTRAINTS]
- Audit log: nunca usar `IgnoreQueryFilters` excepto no painel admin com
  flag explícita (`?includeAllTenants=true` requer super-admin role)
- Hard delete não usa soft delete — é remoção física
- Validar que utilizador tem permissão de admin (não basta autenticado)
- Não bloquear writes se audit log falhar (resilient — log error mas
  continua)
- Performance: audit entries crescem rápido — adicionar índice
  `(TenantId, CreatedAt DESC)` + considerar archive após 1 ano

[OUTPUT ESPERADO]
- Branch `codex/sprint-37-audit-rgpd`
- Entidade + migration + repositório + middleware
- 2 endpoints RGPD (export, hard-delete) + 1 endpoint audit list
- 2 páginas frontend (auditoria + cliente RGPD section)
- Tests: cliente é apagado mesmo com FKs; audit entry sobrevive ao
  apagar utilizador (FK NULL ON DELETE SET NULL)

[VERIFICAÇÃO]
- Bruno cria cliente fake, faz reparação, marca paga, gera fotos
- Clica "Exportar dados" → recebe JSON com tudo
- Clica "Apagar definitivamente", digita nome, confirma
- Cliente desaparece — confirma na DB que não existe (não soft-delete)
- /auditoria mostra a operação HardDelete com utilizador e timestamp
```

---

## Codex Coding #C8 — Health checks + logs estruturados + correlation IDs

```
[CONTEXTO]
RepairDesk SaaS PT precisa de observabilidade básica antes de beta.
`Contexto/34-Beta-Launch-Criteria.md` marca health checks + structured
logging como MUST-HAVE. `Contexto/19-Monitoring.md` tem a stack alvo
(Better Stack ou Uptime Kuma gratuito).

Stack actual: `Microsoft.Extensions.Logging` default. Sem correlation IDs.
Sem Serilog. `/api/health` provavelmente devolve 200 vazio.

[OBJECTIVO]
1. Logs estruturados com Serilog (sink Console + opcional Better Stack).
2. Correlation ID por request (`X-Correlation-ID` header, propagado para
   logs).
3. Health checks granulares (DB, storage, R2 se activo).
4. Endpoint `/api/metrics` Prometheus-style (opcional, atrás de flag).

[TAREFAS]
1. **Logs estruturados**:
   a. Adicionar `Serilog.AspNetCore` + `Serilog.Sinks.Console`
      (JSON formatter em produção)
   b. Configurar via appsettings — log level, enrichers
      (MachineName, ThreadId, CorrelationId, TenantId)
   c. Remover/substituir todos os `Console.WriteLine` por `_logger.LogX`
2. **Correlation ID**:
   a. Middleware `CorrelationIdMiddleware` que lê `X-Correlation-ID`
      header (cria GUID se ausente) e:
      - Propaga em `HttpContext.Items["CorrelationId"]`
      - Adiciona ao response header
      - Adiciona ao `Serilog.Context.LogContext`
   b. Todos os logs têm o correlation ID
3. **Health checks**:
   a. Endpoints (using AspNetCore.HealthChecks):
      - `GET /api/health/live` — sempre 200 (alive)
      - `GET /api/health/ready` — 200 se DB + storage OK, 503 caso contrário
      - `GET /api/health/db` — testa SELECT 1 na DB
      - `GET /api/health/storage` — testa upload/delete dum byte file
   b. Configurar em `Program.cs` com `AddHealthChecks().AddDbContextCheck<AppDbContext>()`
   c. Resposta JSON detalhada (status por dependency)
4. **Tenant ID em logs** (multi-tenant observability):
   - Enricher Serilog que extrai `TenantId` do JWT actual e injecta
     em todos os logs
5. **Docker**:
   - Healthcheck do docker-compose usa `/api/health/ready`
6. **Documentação**:
   - Actualizar `Contexto/19-Monitoring.md` com:
     - Como ligar Better Stack (free tier)
     - Como ligar Uptime Kuma self-hosted
     - Exemplo de alerta (DB down > 1min)

[CONSTRAINTS]
- Nada de overhead em quente — health checks têm cache 5s
- Logs em produção: JSON formatter para parser
- Logs em dev: human-readable formatter
- NUNCA logar passwords, tokens, refresh tokens, NIFs completos
  (mascarar últimos 4 dígitos)
- Correlation ID: 32 hex chars (GUID sem hífens)
- /api/metrics atrás de basic auth ou IP allowlist (não expor publicamente)

[OUTPUT ESPERADO]
- Branch `codex/sprint-38-observability`
- Serilog configurado + JSON em produção
- CorrelationIdMiddleware + tests
- 4 health check endpoints
- Tenant enricher
- Documentação Better Stack/Uptime Kuma
- Sem regressão em endpoints existentes

[VERIFICAÇÃO]
- Bruno faz curl com header `X-Correlation-ID: test123` → vê o ID
  nos logs do container
- /api/health/ready devolve JSON com status DB+storage
- Mata o container SQL Server, /api/health/ready vai a 503
- Logs em produção são parseable como JSON
```

---

## Codex Coding #C9 — Integração Moloni (Path A da Decisão Fiscal)

```
[CONTEXTO]
Decisão fiscal fechada em Contexto/35-Faturacao-Decisao-Final.md:
Path A — integrar Moloni (provider PT certificado, N0xxx/AT).
Cada tenant configura a sua própria conta Moloni (API key).
RepairDesk chama API Moloni em nome do tenant para emitir faturas legais.

A própria Bruno decisão: 2026-05-17 "Sim, concordo e quero o path A".

[OBJECTIVO]
Quando uma Reparacao ou Trabalho é marcado como Pago, ter botão
"Emitir fatura" que:
- chama API Moloni com dados do tenant + cliente + reparação
- recebe PDF + numero fatura assinado
- armazena InvoiceId + URL PDF em RepairDesk
- mostra "Fatura emitida #FA 2026/123" no detalhe

[TAREFAS]
1. **Entidade TenantBillingSettings** (1-1 com Tenant):
   - Provider enum (None=0, Moloni=1, InvoiceXpress=2 — só Moloni implementado agora)
   - ApiKey (encriptado at-rest com IDataProtector)
   - CompanyId (empresa no Moloni)
   - DefaultDocumentType (FaturaSimplificada/Fatura)
   - DefaultSerieId
   - SandboxMode bool
   - Migration `Sprint40Billing`

2. **Settings UI** em /definicoes tab "Faturação":
   - Selector provider (só Moloni activo por agora)
   - Campo API key (password masked, pre-fill se já gravado mostrando ****)
   - Botão "Testar conexão" → chama Moloni `/companies/getOne` valida 200
   - Botão "Sincronizar séries" → lista séries Moloni, persiste DefaultSerieId
   - Toggle "Modo sandbox" para testar sem emitir factura real

3. **Service IBillingProvider + MoloniBillingProvider**:
   - `EmitInvoiceAsync(reparacaoOrTrabalhoId, vatPercent, paymentMethod, ct)`
     → POST Moloni `/documents/invoices/insert` com items, cliente, totais
     → recebe document_id + URL PDF
     → grava em Reparacao/Trabalho: InvoiceProvider=Moloni, InvoiceExternalId, InvoicePdfUrl, InvoiceNumber, EmittedAt
   - `GetPdfStreamAsync(invoiceId, ct)` → re-fetch URL PDF
   - Idempotência: se Reparacao.InvoiceExternalId já existe, devolve esse sem re-emitir

4. **Endpoint POST /api/reparacoes/{id}/emitir-fatura** + `/api/trabalhos/{id}/emitir-fatura`:
   - 422 se reparação não estiver paga
   - 422 se TenantBillingSettings.Provider == None
   - 422 se já tem InvoiceExternalId
   - Chama IBillingProvider.EmitInvoiceAsync
   - Devolve InvoiceDto {number, pdfUrl, emittedAt}

5. **UI ReparacaoDetalhe / TrabalhoDetalhe**:
   - Botão "Emitir fatura via Moloni" (só visível se Reparação paga + Provider configurado + sem InvoiceId ainda)
   - Após emitir: mostra "Fatura #FA 2026/123 emitida em 18/05" + link PDF
   - PDF abre em new tab

6. **Tests**:
   - MoloniBillingProvider com Moq do HttpClient (resposta sucesso + 401 + 422 + 500)
   - Idempotência: chamar 2x devolve mesmo invoiceId
   - Encriptação at-rest: chave nunca em plain text na DB

[CONSTRAINTS]
- API keys NUNCA logadas — usar Serilog Destructure policy
- IDataProtector para encriptar ApiKey antes de SaveChanges
- Erro Moloni → mostrar ao operador (não engolir silenciosamente)
- NÃO emitir fatura duas vezes (idempotência por reparacaoId/trabalhoId)
- Sandbox mode: env var MOLONI_SANDBOX=true usa endpoint api-sandbox.moloni.pt
- Multi-tenant: cada tenant tem own settings + own Moloni account
- NÃO inventar formato Moloni — usar docs oficiais https://www.moloni.pt/dev/

[OUTPUT]
- Branch codex/sprint-40-moloni
- Entity TenantBillingSettings + migration + repo
- IBillingProvider + MoloniBillingProvider + tests
- 2 endpoints (reparacoes, trabalhos)
- Settings tab UI
- Botão "Emitir fatura" nos detalhes
- Documentação `Contexto/41-Moloni-Setup.md`:
  - Como criar conta Moloni (free tier permite 50 docs/mês)
  - Onde está o API key
  - Como testar conexão
  - Limites Moloni e quando upgrade

[VERIFICAÇÃO]
- Bruno cria conta Moloni sandbox, mete API key em /definicoes/faturacao
- Click "Testar conexão" → 200 OK
- Vai a Reparacao paga → click "Emitir fatura via Moloni"
- Vê toast "Fatura #FA 2026/1 emitida"
- Click link PDF → abre PDF assinado com ATCUD válido
- Verifica em Moloni sandbox que fatura existe
- Bruno faz click 2x: 2ª chamada devolve mesma fatura sem duplicar
```

---

## Codex Coding #C10 — Custom fields configuráveis em equipamento (Reddit insight #8)

```
[CONTEXTO]
Reddit insight em Contexto/37-Insights-Mercado-Reddit.md secção 8:
"Segmento IT-repair (não só telemóveis) quer custom fields em equipamento:
marca/modelo/CPU/RAM/storage/videocard etc. RepairDesk actualmente só tem
string livre 'equipamento' + opcional IMEI."

Adicionar custom fields configuráveis por tenant abre o produto a oficinas IT
sem comprometer o caso telemóvel.

[OBJECTIVO]
1. Tenant configura templates de custom fields (ex: "Laptop": CPU, RAM, Storage, GPU)
2. Ao criar reparação, escolhe template e preenche custom values
3. Custom fields aparecem no PDF orçamento + portal cliente público (configurável)

[TAREFAS]
1. **Schema novo**:
   - EquipmentFieldTemplate (Id, TenantId, Nome, Categoria DeviceCategory, IsActive)
   - EquipmentFieldDefinition (Id, TemplateId, Label, Type enum [text, number, select, boolean],
     Options json para selects, Required bool, Order int, VisibleInPortal bool)
   - EquipmentFieldValue (Id, ReparacaoId, FieldDefinitionId, Value string)
   - Migration `Sprint41CustomEquipmentFields`

2. **Seed inicial**: 3 templates default por tenant:
   - "Telemóvel" — IMEI, Marca, Modelo (mantém compatibilidade com actual)
   - "Laptop" — Marca, Modelo, CPU, RAM, Storage, GPU
   - "Desktop" — Marca, Modelo, CPU, RAM, Storage, GPU, MotherBoard

3. **API admin** (admin role):
   - GET/POST/PUT/DELETE `/api/equipment-field-templates`
   - Reordering via PATCH `/order`

4. **API público** (autenticado):
   - GET `/api/equipment-field-templates/active` (só visíveis ao operador)
   - POST `/api/reparacoes/{id}/fields` (set bulk values)

5. **Frontend**:
   - /definicoes nova tab "Campos personalizados"
   - UI dynamic form builder (add field, choose type, mark required, reorder)
   - Em /reparacoes/nova: selector "Categoria equipamento" → mostra fields do template
   - Em /reparacoes/{id}: edição inline dos custom values
   - Portal cliente (PortalCliente.tsx): mostra fields onde VisibleInPortal=true

6. **PDF**: orçamento + ficha entrada incluem custom fields visible

7. **Tests**:
   - Definition required → 422 se valor vazio
   - Multi-tenant: template tenant A não visível em tenant B
   - Soft delete preserva values históricos

[CONSTRAINTS]
- Manter compatibilidade: Reparacao.Equipamento string field actual continua a existir
  como fallback se nenhum template estiver associado
- NÃO permitir delete de template usado por reparações activas — só "deactivate"
- Limites: max 20 fields por template, max 10 templates activos por tenant
- Type select: max 50 options
- Bulk insert das values usa single SaveChanges

[OUTPUT]
- Branch codex/sprint-41-custom-fields
- 3 entities + migration + repo + service
- 2 admin controllers (Templates + Fields)
- Nova tab Definições + UI dynamic builder
- Integração em ReparacaoForm e ReparacaoDetalhe
- Portal cliente mostra fields VisibleInPortal
- Tests unitários + integration

[VERIFICAÇÃO]
- Bruno cria template "Laptop" com 6 fields
- Cria reparação categoria Laptop → vê fields automáticos para preencher
- PDF orçamento mostra "CPU: i7-12700H · RAM: 16 GB · Storage: 512 GB NVMe"
- Portal cliente mostra mesmos fields se VisibleInPortal=true
- Apaga template → bloqueado porque há reparações activas a usar
```

---

## Codex Coding #C11 — Verificação NIF via webservice AT (diferenciador real)

```
[CONTEXTO]
Bruno tem certificado de Produtor de Software AT
(`ChaveCifraPublicaAT2027.cer` em `finanças/secrets/`) e adesão ao serviço de
webservices da AT. Pode validar NIFs PT em real-time contra a base de dados
da AT — verifica nome+morada da empresa, status fiscal, etc.

Outro software de oficinas PT não faz isto. Diferenciador real:
"Insere o NIF, vê o nome da empresa automaticamente."

[OBJECTIVO]
Quando utilizador insere NIF no form de cliente:
1. Validação local Luhn (já existe NifValidator)
2. Se ok, chamar webservice AT (rate-limited)
3. Se AT confirma, sugere "Nome empresa: Lopestech Lda - Aceitar?"
4. Bruno aceita e o nome fica preenchido automaticamente

[TAREFAS]
1. **Backend service IAtNifLookupService** + `AtNifLookupService`:
   - Carrega cert PFX/CER from disk path (env AT_CERT_PATH)
   - Carrega private key (env AT_KEY_PATH + AT_KEY_PASSWORD)
   - Usa System.ServiceModel para chamar SOAP webservice
   - Endpoint: https://servicos.portaldasfinancas.gov.pt/sgdtoi/dadosTOI (produção)
              https://servicostst.portaldasfinancas.gov.pt:701/sgdtoi/dadosTOI (teste)
   - Retorna {nif, nome, morada, status} ou null se NIF inválido na AT

2. **Cache Redis** com TTL 30 dias por NIF (NIF não muda muito):
   - Key `at:nif:{nif}` value JSON
   - Reduz custo+latência

3. **Rate limit**: max 100 calls/dia por tenant (AT cobra ou pode banir)
   - Counter Redis incrementado a cada hit

4. **Endpoint** `GET /api/at/nif-lookup/{nif}`:
   - Validação local Luhn primeiro
   - Cache hit → 200 com dados
   - Cache miss → AT webservice → cache → 200
   - 429 se rate limit excedido
   - 503 se AT offline

5. **Frontend ClienteForm**:
   - Após NIF válido (Luhn ok) → query auto ao endpoint
   - Mostra spinner "A verificar AT..."
   - Resultado: "Lopestech Lda · Aceitar nome?" botão
   - Click → preenche campo `nome` automaticamente

6. **Doc `Contexto/42-AT-NIF-Lookup.md`**:
   - Setup do certificado em ambiente Docker
   - Configurar volume secrets/at no container
   - Como obter cert + chave (Bruno já tem)
   - Testar em ambiente AT testes primeiro

7. **Tests**:
   - Mock SOAP service responses (válido + 404 + AT-offline)
   - Cache miss → call AT → cache hit
   - Rate limit increments

[CONSTRAINTS]
- Certificate NUNCA hardcoded — env path
- Logs NÃO incluem NIF completo (mascarar 4 últimos dígitos)
- Idempotente: chamar 2x com mesmo NIF devolve mesmo resultado da cache
- Default em dev: usar AT testes endpoint (não production) — env AT_PRODUCTION=false
- NÃO bloquear UI se AT offline — fall back para "valida Luhn ok, AT indisponível"
- Respeitar RGPD: o que vier da AT é dado público (Portal das Finanças)
  mas guardar em cache constitui processing — documentar legal-base

[OUTPUT]
- Branch codex/sprint-42-at-nif-lookup
- IAtNifLookupService + AtNifLookupService implementation
- Redis cache key strategy
- 1 endpoint + frontend integration em ClienteForm
- Doc setup
- Tests mocked

[VERIFICAÇÃO]
- Bruno insere NIF 263758141 no form
- 200ms depois vê "Bruno Lopes (LopesTech) · Aceitar nome?"
- Click → nome preenchido
- Repete 100x mesmo NIF → cache hit, sem chamada AT real
- Rate limit excedido (em test) → mensagem amigável
- AT offline → fall back silencioso, NIF Luhn-válido continua a passar
```

---

## Codex Coding #C12 — Módulo de Vendas (POS-style)

```
[CONTEXTO]
Hoje RepairDesk só tem Reparações + Trabalhos + Despesas + Stock.
Falta: venda directa de produto (acessórios, telemóveis novos, peças
avulsas). Bruno passou para regime normal IVA e quer vender telemóveis
via dropshipping Molano — precisa de registar vendas e emitir fatura.

Mesma necessidade aparece noutras oficinas: vender capas, películas,
cabos com margem.

[OBJECTIVO]
Módulo de Vendas tipo POS rápido. Operador procura peça, mete no
carrinho, define cliente (ou anónimo), cobra, emite fatura via Moloni
(#C9 já feito), decrementa stock.

[TAREFAS]
1. **Entidades**:
   - Venda (Id, TenantId, Numero auto-incremental, Data, ClienteId?,
     TotalCents, IvaCents, PaymentMethod enum, Status enum,
     InvoiceExternalId?, InvoicePdfUrl?, Notas, CreatedBy, CreatedAt)
   - VendaItem (VendaId, PartId? + descrição livre se não-stock,
     Quantidade, PrecoUnitarioCents, DescontoCents, IvaRate decimal)
   - Migration `Sprint43Vendas`

2. **Service**:
   - `VendaService.CreateAsync` valida stock (não permite vender mais que stock)
   - Ao marcar `Paga` → cria PartMovimento UsoEmReparacao... espera, criar
     novo motivo `VendaCliente` no enum PartMovimentoMotivo
   - Emite fatura Moloni (reusa MoloniBillingProvider de #C9) se Tenant
     tem provider configurado
   - Idempotência: chamar "Emitir fatura" 2x devolve mesma

3. **Endpoints**:
   - `POST /api/vendas` (criar)
   - `GET /api/vendas?from=&to=&page=` (listar)
   - `GET /api/vendas/{id}`
   - `POST /api/vendas/{id}/marcar-paga` (com payment method)
   - `POST /api/vendas/{id}/emitir-fatura` (reusa Moloni)
   - `POST /api/vendas/{id}/cancelar`

4. **Frontend `/vendas`**:
   - Vista POS: search bar em cima (search por SKU, nome peça, ou marca)
   - Resultado: lista de peças clickable → adiciona ao carrinho
   - Carrinho lateral direito: linha por item, qty editável, sub-total
   - Buttons: "Cliente: Anónimo" (click para escolher) ; "Cobrar (€XX,YY)"
   - Modal cobrar: payment method (Numerário, MBWay, Multibanco, Transferência)
   - Após cobrar: opção "Emitir fatura Moloni" → PDF aparece
   - Mobile-friendly (POS de balcão pode estar em tablet)

5. **Dashboard**:
   - Card adicional "Vendas hoje: €X" / "Vendas mês: €Y"
   - Top produtos vendidos (Top 5 por receita)

6. **PDF receipt**:
   - Mesmo template do orçamento mas tipo "Recibo de Venda"
   - Inclui items + IVA + Total
   - Fallback se Moloni não configurado: emite recibo não-fiscal com
     label "Documento não fiscal — emitir fatura no software certificado"

7. **Tests**:
   - Multi-tenant isolation
   - Stock decrementa correctamente
   - Tentar vender mais do que stock → 422
   - Idempotência da emissão de fatura

[CONSTRAINTS]
- IVA por item (não só global) — peças podem ter taxas diferentes
- Suporte cliente anónimo (fatura simplificada Moloni FS)
- Suporte cliente cadastrado (fatura FT)
- NÃO permitir vender peça com stock 0 sem aviso
- Soft delete (cancelamento) preserva audit log
- Decimal precision: cents

[OUTPUT]
- Branch codex/sprint-43-vendas
- 2 entities + migration
- VendaService + tests
- 5 endpoints
- Página /vendas com POS UI
- Card dashboard
- PDF template Recibo

[VERIFICAÇÃO]
- Bruno cria venda: escolhe peça do stock + cliente Maria Silva
- Vê total com IVA 23%
- Marca paga via MBWay
- Click "Emitir fatura Moloni" → PDF FS aparece
- Stock dessa peça diminui em 1
- Auditoria mostra evento Venda Created
- Dashboard mostra "Vendas hoje: €XX"
```

---

## Anti-padrões a evitar nos prompts (aprendi na conversa)

### ❌ Mau: "podes melhorar isto?"
Vago demais. Codex vai inventar melhorias que não pedimos.

### ❌ Mau: "implementa o melhor caminho"
"Melhor" segundo quem? Codex vai assumir e errar.

### ❌ Mau: "vê este ficheiro" (sem dar contexto do que procurar)
Codex lê superficialmente. Tem que se dizer o quê e porquê.

### ❌ Mau: pedidos com 10 sub-tarefas misturadas
Decompor. Prompt por tarefa.

### ❌ Mau: "não inventes coisas"
Codex inventa na mesma se não tiver constraints concretas.

### ✅ Bom: contexto completo + objectivo + constraints + output esperado + verificação
Reduz alucinação drasticamente.

### ✅ Bom: pedir análise crítica antes da implementação
Codex tende a implementar logo. Forçar reflexão evita over-engineering.

### ✅ Bom: dar evidências concretas
"Cliente queixou-se de X em Reddit thread Y" > "achamos que cliente pode querer X"

---

## Checklist antes de delegar

Antes de copiar/colar prompt para Codex:

- [ ] Contexto do projeto está completo?
- [ ] Estado actual está descrito (o que já existe)?
- [ ] Objectivo é uma frase clara, não um parágrafo?
- [ ] Constraints estão explícitas (o que NÃO fazer)?
- [ ] Output esperado tem formato concreto?
- [ ] Critério de verificação é objectivo (não "fica bonito")?
- [ ] Há referência a ficheiros do repo que Codex deve ler primeiro?
- [ ] Pedi análise crítica ou pedi implementação directa? (escolher consciente)

---

## Pós-delegação — quando Codex devolve

Quando recebermos resposta do Codex:

1. **Não aceitar cegamente** (cf. `05-Reflexao-Critica.md`)
2. Verificar que ficheiros mencionados existem (paths corretos)
3. Verificar que constraints foram respeitadas
4. Correr `docker compose up -d` e testar manualmente
5. Correr `dotnet test` e `npm run build`
6. Code review com Prompt #6 (mesmo no próprio output do Codex)
7. Se algo não bater certo, **discordar** e pedir refazer com prompt mais forte
