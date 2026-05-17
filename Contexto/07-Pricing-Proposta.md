# Pricing Proposta — RepairDesk PT

Atualizado: 2026-05-15  
Task: #56 — análise de pricing dos concorrentes

## Decisão recomendada

O pricing deve ser **por loja**, não por utilizador. O mercado português de pequenas oficinas é sensível a custos fixos e muitas lojas têm 1-3 pessoas. Cobrar por user demasiado cedo cria a sensação de penalizar crescimento interno, enquanto cobrar por loja é mais simples de explicar: "quanto custa ter a oficina organizada".

Proposta base:

| Tier | Preço mensal | Cliente-alvo | Ideia central |
|---|---:|---|---|
| Starter | 19€/mês por loja | Técnico solo / loja pequena com ~30 reparações/mês | Tirar a loja do papel, WhatsApp e Excel |
| Pro | 39€/mês por loja | Loja ativa com 1-5 pessoas | Operação completa, cliente informado e lucro visível |
| Enterprise | 89€/mês por empresa | Multi-loja / operação profissional | Multi-loja, whitelabel, automação e suporte prioritário |

Os preços devem ser comunicados como **preços sem IVA quando aplicável**. Enquanto a LopesTech estiver no regime de isenção do Art. 53.º do CIVA, a fatura não liquida IVA. Quando sair do regime, o preço público B2B deve passar a ser apresentado como `+ IVA` para não comprimir margem.

Nota importante: o prompt indica salário mínimo PT 2026 = 870€, mas a referência oficial atual do Governo para 2026 é 920€. Uso 870€ como cenário conservador de sensibilidade e 920€ como referência atual. Com 920€, os tiers representam cerca de 2,1%, 4,2% e 9,7% de um salário mínimo mensal.

## Assunções de mercado

- Portugal tem muitas lojas pequenas, familiares ou com 1-3 funcionários.
- O comprador quer simplicidade: clientes, reparações, estado, mensagens, fotos, orçamentos, custos e lucro.
- A loja de 30 reparações/mês não deve pagar mais do que ~0,60€-1,30€ por reparação na fase inicial.
- A diferenciação local deve ser: português nativo, workflows de reparação reais, MBWay, fiscalidade portuguesa, garantia por QR, tracking do cliente e suporte próximo.
- O produto não deve competir de frente com ERPs ou software certificado de faturação no dia 1. Primeiro deve gerir reparações e registar referências fiscais; faturação certificada vem depois de validação e processo legal.

## Tiers propostos

### Starter — 19€/mês por loja

Para técnicos solo e oficinas pequenas que hoje usam papel, WhatsApp, Excel ou memória.

Limites:

| Limite | Valor |
|---|---:|
| Lojas | 1 |
| Utilizadores incluídos | 2 |
| Tickets/reparações | 75 por mês |
| Clientes | 1.000 |
| Storage | 5 GB |
| Histórico | Completo enquanto subscrição ativa |
| Automações | Básicas |

Incluído:

- Clientes e histórico básico.
- Dispositivos por cliente.
- Reparações com estados configuráveis.
- Fotos antes/depois.
- Timeline interna da reparação.
- Orçamentos simples em PDF não fiscal.
- Recibo/folha de entrada não fiscal.
- Click-to-WhatsApp com mensagens pré-preenchidas.
- Dashboard básico: reparações abertas, prontas, entregues, receita registada, custos e lucro estimado.
- Garantia digital simples com QR/link.
- Export CSV.

Não incluído:

- SMS/WhatsApp automático por API.
- Faturação certificada / SAF-T / comunicação AT.
- Stock avançado.
- Portal cliente avançado.
- Multi-loja.
- API pública.
- Whitelabel.

Racional: a loja com 30 reparações/mês paga 0,63€ por reparação. É defensável mesmo para uma oficina pequena, desde que poupe 1-2 horas por mês ou evite perder uma reparação.

### Pro — 39€/mês por loja

Plano principal. Deve ser o plano que queremos vender à maioria das lojas.

Limites:

| Limite | Valor |
|---|---:|
| Lojas | 1 |
| Utilizadores incluídos | 5 |
| Tickets/reparações | Ilimitados |
| Clientes | 10.000 |
| Storage | 25 GB |
| Histórico | Completo |
| Automações | Incluídas |

Incluído:

- Tudo do Starter.
- Portal público do cliente com tracking estilo "Recebido > Diagnóstico > Aguarda peça > Em reparação > Pronto".
- Aprovação/recusa de orçamento pelo cliente.
- Pedido de sinal/pagamento manual ou MBWay quando integrado.
- Templates automáticos por estado.
- Email e push web para staff.
- WhatsApp/SMS automático como add-on por créditos.
- Stock básico de peças.
- Fornecedores e custos por peça.
- Margem por reparação.
- Garantia digital avançada por QR.
- Google Reviews funnel: se avaliação interna for 4-5, pedir review pública; se for baixa, notificar gestor.
- Follow-up automático 30/90/180 dias.
- Dashboard de rentabilidade: receita, custo de peças, lucro, tempo médio, taxa de retorno.
- Base de conhecimento interna: avaria, solução, peças usadas, tempo médio.
- Registo de referências fiscais/faturas emitidas fora do sistema.

Futuro incluído quando legalmente pronto:

- Módulo fiscal PT básico, se a estratégia for usar faturação como diferencial do Pro.
- Alternativa mais conservadora: faturação certificada como add-on de 15€-25€/mês para Starter e Pro.

Racional: a 39€/mês, uma loja com 30 reparações paga 1,30€ por reparação. Se o portal cliente, reviews e follow-up gerarem apenas 1 reparação adicional por mês, o plano paga-se sozinho.

### Enterprise — 89€/mês por empresa

Para operações com mais volume, várias lojas, equipa maior ou necessidade de marca própria.

Limites:

| Limite | Valor |
|---|---:|
| Lojas incluídas | 2 |
| Utilizadores incluídos | 10 |
| Tickets/reparações | Ilimitados |
| Storage | 100 GB |
| API | Incluída |
| Suporte | Prioritário |

Incluído:

- Tudo do Pro.
- Multi-loja: visão por loja e visão global.
- Permissões avançadas por função.
- Whitelabel: logo, cores, domínio próprio quando tecnicamente pronto.
- Portal cliente com marca da loja.
- Auditoria avançada.
- API pública.
- Relatórios por técnico, loja, serviço e fornecedor.
- Regras de automação avançadas.
- Onboarding assistido incluído.
- Importação assistida de clientes/reparações via CSV.
- Prioridade em suporte e roadmap.

Add-ons Enterprise:

- Loja extra: 20€/mês.
- Storage extra: 10€/mês por 100 GB.
- Domínio whitelabel gerido: 10€/mês.
- Pacote de migração/importação: 99€-299€ one-time, conforme volume.
- Integrações contabilísticas/AT avançadas: preço a definir após validação legal.

Racional: fica abaixo do RepairDesk Growth e competitivo com RO App Enterprise, mas com diferenciação local portuguesa.

## Add-ons recomendados

| Add-on | Preço sugerido | Notas |
|---|---:|---|
| Créditos SMS/WhatsApp | Custo + margem de 20%-30% | Evitar margem negativa em mensagens |
| Utilizador extra | 5€/mês | Só acima dos utilizadores incluídos |
| Storage extra 25 GB | 5€/mês | Especialmente por fotos/vídeos |
| Módulo fiscal PT certificado | 15€-25€/mês | Só vender quando legalmente seguro |
| Onboarding Pro | 49€ one-time | Incluído no Enterprise |
| Migração de dados | 99€-299€ one-time | CSV, Excel, limpeza de dados |
| API pública no Pro | 15€/mês | Incluída no Enterprise |

Regra: add-ons devem cobrir custos variáveis reais ou valor empresarial claro. Não bloquear o core operacional atrás de micro-pagamentos.

## Comparação com concorrentes

Preços públicos confirmados em 2026-05-15 quando disponíveis. Valores podem mudar.

| Produto | Plano entrada | Plano intermédio | Plano alto | Modelo | Notas |
|---|---:|---:|---:|---|---|
| RepairDesk.co | $99/loja/mês Essential | $149/loja/mês Growth | Advanced custom | Por loja, users incluídos | Site oficial mostra $99/store/mês com 5 users, não $99/user/mês. Add-ons pagos existem. |
| RO App | €15/mês Hobby | €29/mês Startup | €69 Business / €99 Enterprise | Base + employees/locations extra | Trial 7 dias sem cartão. Startup inclui 3 employees; extra employee €5/mês; extra location €15/mês. |
| Nosso Starter | 19€/loja/mês | - | - | Por loja | Mais caro que RO Hobby, mas com foco PT e reparação local. |
| Nosso Pro | - | 39€/loja/mês | - | Por loja | Plano principal; deve ganhar por localização, portal cliente, garantia QR e fiscalidade PT futura. |
| Nosso Enterprise | - | - | 89€/empresa/mês | Empresa/multi-loja | Abaixo de RepairDesk Growth e próximo de RO Enterprise, com suporte PT/local. |

Leitura competitiva:

- Contra RO App, não ganhamos por ser mais barato. Ganhamos por ser mais português, mais vertical para reparação de telemóveis/informática e por integrar workflows locais como MBWay, garantia QR e fiscalidade portuguesa.
- Contra RepairDesk, ganhamos por preço, proximidade, UX mais simples e localização PT/EU.
- O Starter precisa de ser simples e muito bom. Se parecer limitado demais, o cliente compara com RO Hobby a 15€ e escolhe o mais barato.
- O Pro é onde deve estar o verdadeiro valor: tracking cliente, orçamento digital, reviews, follow-ups, stock e lucro.

## CAC vs LTV estimado

Estas estimativas não são dados reais; são hipóteses de planeamento para validar nos primeiros clientes.

Assunções:

- Margem bruta SaaS: 85% antes de suporte humano pesado.
- CAC inicial por founder-led sales: visitas locais, demos, mensagens, recomendações, conteúdo orgânico.
- Fórmula: `LTV = ARPA mensal * margem bruta / churn mensal`.
- Payback: `CAC / margem bruta mensal`.

| Tier | ARPA | Churn mensal assumido | CAC assumido | LTV estimado | Payback | LTV/CAC |
|---|---:|---:|---:|---:|---:|---:|
| Starter | 19€ | 4,0% | 80€ | 404€ | 5,0 meses | 5,0x |
| Pro | 39€ | 2,5% | 160€ | 1.326€ | 4,8 meses | 8,3x |
| Enterprise | 89€ | 1,5% | 600€ | 5.043€ | 7,9 meses | 8,4x |

Interpretação:

- Starter só funciona se o CAC for baixo. Paid ads agressivo pode matar margem.
- Pro é o melhor equilíbrio entre preço aceitável e LTV saudável.
- Enterprise pode sustentar suporte e onboarding, mas não deve distrair antes do Pro estar validado.

Benchmarks internos de decisão:

| Métrica | Meta saudável |
|---|---:|
| Payback Starter | <= 6 meses |
| Payback Pro | <= 6 meses |
| Payback Enterprise | <= 9 meses |
| LTV/CAC mínimo | 3x |
| Conversão trial > pago | 15%-25% inicialmente |
| Churn mensal Pro | < 3% |

## Killer features que justificam upgrade

### Starter > Pro

1. **Portal cliente com tracking visual**  
   Reduz mensagens repetidas no WhatsApp: "já está pronto?", "em que estado está?", "quando chega a peça?".

2. **Orçamento digital com aprovação e sinal**  
   Transforma orçamento em decisão rápida. Isto tem impacto direto em receita e fluxo de caixa.

3. **Google Reviews + follow-up automático**  
   Ajuda a loja a crescer localmente. Para pequenas oficinas, reviews podem valer mais do que relatórios bonitos.

### Pro > Enterprise

1. **Multi-loja + dashboard global**  
   Justifica upgrade quando já existe operação com mais do que uma localização.

2. **Whitelabel / domínio próprio**  
   Valor claro para marcas que querem parecer maiores e controlar a experiência do cliente.

3. **API + permissões avançadas + auditoria**  
   Necessário para operações mais maduras, integrações e controlo interno.

## Política de descontos

### Mensal vs anual

- Mensal: preço cheio.
- Anual: 2 meses grátis, equivalente a ~16,7% desconto.
- Não fazer desconto anual superior a 20% no início.

Tabela anual:

| Tier | Mensal | Anual recomendado |
|---|---:|---:|
| Starter | 19€/mês | 190€/ano |
| Pro | 39€/mês | 390€/ano |
| Enterprise | 89€/mês | 890€/ano |

### Early adopters

Para os primeiros 10-20 clientes beta:

- 30% desconto vitalício enquanto mantiverem subscrição ativa.
- Em troca: feedback mensal, autorização para case study ou testemunho, e permissão para usar métricas agregadas anonimizadas.

### Multi-loja

- Enterprise inclui 2 lojas.
- Loja extra: 20€/mês.
- A partir de 5 lojas: desconto negociado, mas nunca abaixo de 15€/loja extra.

### ONG / escolas / projetos sociais

- 30% desconto.
- Sem customização gratuita.
- Suporte normal, não prioritário.

### Migração de concorrentes

- 50% desconto nos primeiros 3 meses para clientes que venham de outro software.
- Importação CSV básica incluída no Pro se o cliente preparar o ficheiro no template.

## Trial / freemium policy

Recomendação: **trial sem cartão**, sem cobrança automática.

### Trial

- 14 dias de trial Pro.
- Sem cartão de crédito.
- Dados exportáveis no fim.
- Depois do trial: conta fica em modo leitura/export durante 30 dias.
- Botão claro para escolher Starter, Pro ou Enterprise.

Porquê 14 dias e não 7:

- Uma loja pode não ter volume diário suficiente para testar em 7 dias.
- O ciclo real de uma reparação pode apanhar diagnóstico, encomenda de peça e entrega.
- 14 dias permite sentir o valor do tracking, orçamento e follow-up.

### Freemium

Não recomendo freemium completo no início. Atrai utilizadores que consomem suporte e raramente convertem.

Alternativa aceitável:

- Demo workspace pública com dados fictícios.
- Conta gratuita limitada a 20 reparações totais, sem automações, sem storage permanente e sem fiscalidade.
- Objetivo: experimentar, não operar eternamente.

Regra ética:

- Sem dark patterns.
- Sem trial que começa a cobrar sem confirmação.
- Sem esconder exportação de dados.
- Cancelamento simples.

## Preços recomendados para lançamento

Lançamento público:

| Tier | Mensal | Anual | Posição |
|---|---:|---:|---|
| Starter | 19€ | 190€ | Entrada acessível |
| Pro | 39€ | 390€ | Plano recomendado |
| Enterprise | 89€ | 890€ | Multi-loja / avançado |

Beta/primeiros clientes:

| Tier | Mensal beta com -30% | Observação |
|---|---:|---|
| Starter | 13,30€ | Pode arredondar para 13€ ou 15€ |
| Pro | 27,30€ | Pode arredondar para 29€ |
| Enterprise | 62,30€ | Pode arredondar para 59€ ou 69€ |

Sugestão prática: vender beta Pro a **29€/mês vitalício** para as primeiras lojas que aceitarem feedback regular. Isto dá validação e fica defensável contra RO App Startup.

## Como defender o preço em vendas

Mensagem curta:

> "Por menos de 1,50€ por dia, tens clientes, reparações, orçamentos, fotos, garantia, estado online e lucro organizado num sistema feito para oficinas portuguesas."

Para loja pequena:

> "Se isto te poupar uma hora por mês ou evitar uma reparação esquecida, já está pago."

Para loja ativa:

> "O Pro não é só gestão. É menos mensagens repetidas, mais aprovações de orçamento, mais reviews Google e melhor controlo de margem."

Para multi-loja:

> "O Enterprise dá-te controlo por loja, por técnico e por margem, sem perder a experiência com a tua marca."

## Fontes consultadas

- Contexto interno: `Contexto/02-Concorrentes.md`.
- RepairDesk pricing oficial: https://www.repairdesk.co/pricing/
- RO App pricing oficial: https://roapp.io/pricing/
- Governo de Portugal, salário mínimo 2026: https://www.portugal.gov.pt/pt/gc25/comunicacao/noticia?i=governo-aumenta-salario-minimo-para-920-euros-em-2026
- Portal das Finanças, taxa normal IVA Art. 18.º CIVA: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/civa_rep/Pages/iva18.aspx
- Portal das Finanças, regime de isenção Art. 53.º CIVA: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/civa_rep/ra/Pages/iva53ra_202503.aspx

## Riscos

### 1. Preço baixo demais

Cenário: Starter a 9€-12€/mês ou Pro abaixo de 29€/mês.  
Risco: atrai clientes muito sensíveis a preço, aumenta suporte, reduz margem e torna difícil pagar infraestrutura, mensagens, storage e tempo de desenvolvimento. Também passa imagem de ferramenta pequena, não de sistema profissional.

Mitigação: manter Starter a 19€ público, usar descontos beta temporários e proteger add-ons com custo variável.

### 2. Preço alto demais

Cenário: entrar logo a 59€-99€/mês como plano principal.  
Risco: oficinas portuguesas pequenas com 30-60 reparações/mês comparam com RO App e com soluções manuais, adiam decisão e pedem "para pensar". O produto ainda sem marca forte não tem confiança suficiente para preço premium.

Mitigação: Pro a 39€, trial sem cartão, prova por ROI simples e beta local com acompanhamento.

### 3. Modelo errado

Cenário: cobrar por utilizador desde cedo, meter features core como add-ons ou vender faturação certificada antes de estar legalmente segura.  
Risco: fricção comercial, confusão, suporte pesado e risco legal. O cliente sente que cada funcionário custa mais e o produto parece menos transparente.

Mitigação: preço por loja, users incluídos, add-ons só para custos variáveis/valor avançado, e módulo fiscal lançado apenas quando houver segurança técnica e legal.
