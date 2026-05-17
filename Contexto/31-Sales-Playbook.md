# Sales Playbook + Demo Flow

Atualizado: 2026-05-16

Objetivo: dar ao Bruno um processo simples para levar uma oficina de primeiro contacto a cliente beta pagante em 7-14 dias, sem CRM pago, sem SDR e sem conversa de startup.

Documentos base:

- `09-Customer-Acquisition.md`
- `07-Pricing-Proposta.md`
- `01-Estado-Actual.md`

## Princípio base

O Bruno não deve vender como "vendedor de software". Deve vender como técnico que resolveu uma dor real da própria oficina.

Frase de posicionamento:

> "Eu também faço reparações. Criei isto porque me fartei de gerir clientes, estados, margens, garantias e mensagens espalhadas por WhatsApp, papel e Excel."

Regra: vender **menos software** e mais **oficina organizada**.

## Processo comercial simples

| Etapa | Objetivo | Duração | Saída esperada |
|---|---|---:|---|
| Primeiro contacto | perceber se há abertura | 5-10 min | marcar discovery/demo |
| Discovery | perceber dores reais e qualificar | 20-30 min | decidir se vale demo |
| Demo | mostrar só o fluxo que interessa | 30-45 min | compromisso de beta |
| Setup | colocar dados reais | 30-90 min | primeira reparação criada |
| Check-in | remover bloqueios | 2-3 dias depois | uso real |
| Fecho | converter para beta paga | 7-14 dias | Pro beta 29€/mês |

Não oferecer "vou mandar link e depois vê". Loja pequena não compra assim. Precisa de ver o fluxo com dados parecidos aos dela.

## Primeiro contacto

### Presencial

```text
Olá, sou o Bruno da LopesTech, aqui de Viseu. Também faço reparações de telemóveis e estou a criar um software português para oficinas gerirem reparações, clientes, estados, garantias e margens.

Não te quero empatar muito. Posso fazer-te uma pergunta rápida?

Hoje vocês gerem as reparações como: papel, Excel, WhatsApp, ou já têm algum sistema?
```

Se responder com abertura:

```text
Faz sentido. Foi exatamente por aí que comecei. Se quiseres, mostro-te em 15-20 minutos como estou a usar isto na minha própria oficina e dizes-me sem filtros se serve ou não para uma loja como a tua.
```

### WhatsApp/telefone

```text
Olá {Nome}, sou o Bruno da LopesTech em Viseu. Também trabalho com reparações e estou a testar com algumas oficinas um software português para gerir reparações, clientes, estados, garantias QR e margens.

Não é uma ferramenta genérica. Foi feita a partir do fluxo real de oficina.

Tens 15 minutos esta semana para eu te mostrar e perceber se isto fazia sentido para a tua loja?
```

## Discovery call - 20 a 30 minutos

Objetivo: perceber se a loja tem dor, volume, capacidade de mudar e vontade mínima de testar.

Não começar por demo. Começar por workflow.

### Estrutura de 20 minutos

| Minuto | Bloco | Script |
|---:|---|---|
| 0-2 | Abertura | "Obrigado pelo tempo. Antes de mostrar o sistema, queria perceber como trabalham hoje. Se no fim não fizer sentido, tudo bem." |
| 2-10 | Workflow atual | perguntas sobre entrada, estados, orçamento, peças, pagamento, garantia |
| 10-15 | Dor e impacto | onde perdem tempo/dinheiro/confiança |
| 15-18 | Qualificação | volume, equipa, decisão, timing, orçamento |
| 18-20 | Próximo passo | demo ou fechar como não-fit |

### Perguntas de discovery

Workflow:

1. Como registam uma reparação quando o cliente entra na loja?
2. Onde fica o nome, telefone, equipamento, IMEI e avaria?
3. Como sabem em que estado está cada reparação?
4. Como avisam o cliente quando está pronto ou quando falta peça?
5. Quantas vezes por semana recebem mensagens tipo "já está pronto?"
6. Como fazem orçamento e aprovação?
7. Como registam custo da peça e lucro real?
8. Como controlam o que já está pago e o que falta cobrar?
9. Como emitem/guardam garantias?
10. Quando um equipamento volta, como veem histórico?

BANT adaptado a loja pequena:

| BANT | Versão RepairDesk | Perguntas |
|---|---|---|
| Budget | consegue pagar 29-39€/mês se resolver dor? | "Se isto te poupar tempo e evitar confusão, 29€/mês seria aceitável ou já pesa?" |
| Authority | quem decide? | "És tu que decides ferramentas da loja ou precisas validar com alguém?" |
| Need | há dor real? | "O que mais te chateia hoje neste processo?" |
| Timing | há urgência? | "Se isto fizer sentido, preferias testar já numa loja real ou mais lá para a frente?" |

Perguntas de impacto:

- Já perderam uma reparação ou esqueceram de avisar um cliente?
- Já entregaram algo sem confirmar pagamento?
- Já tiveram confusão de garantia por não haver histórico?
- Quanto tempo por dia perdem a responder estados no WhatsApp?
- Se o cliente pudesse ver o estado sozinho, isso ajudava ou era indiferente?

### Bom prospect

Sinais verdes:

- 20+ reparações/mês.
- Usa papel/Excel/WhatsApp e queixa-se de confusão.
- Tem Google Reviews ou quer melhorar reputação.
- Faz orçamentos e aprovações com alguma frequência.
- Compra peças regularmente.
- Dono responde com exemplos concretos.
- Aceita pôr 5-10 reparações reais para teste.
- 29€/mês não bloqueia imediatamente.

### Perda de tempo

Sinais vermelhos:

- "Não temos problema nenhum" mas aceitou chamada por curiosidade.
- Quer software de faturação certificado completo já.
- Quer customizações antes de testar.
- Pede gratuito indefinido.
- Tem 2-3 reparações/mês e pouco volume.
- Não é decisor e não quer envolver o decisor.
- Só quer comparar preço com Excel.

Como sair com elegância:

```text
Pelo que me dizes, acho que agora ainda não é o melhor momento. Prefiro ser honesto: isto faz mais sentido quando há volume suficiente para a organização pagar o custo. Posso voltar a falar contigo daqui a uns meses?
```

## Demo flow - 40 minutos

Objetivo: mostrar uma mini-história completa, não uma lista de menus.

História da demo:

> "Cliente entra com iPhone/Samsung. Registamos, diagnosticamos, enviamos orçamento, cliente acompanha, aprovamos, entregamos, garantia fica pronta, e a loja vê lucro."

### Preparação

Antes da demo:

- criar tenant demo com logo LopesTech ou nome fictício;
- ter 3 clientes fictícios;
- ter 5 reparações em estados diferentes;
- ter 1 reparação entregue com garantia;
- ter 1 reparação com diagnóstico guiado e Health Score;
- abrir no portátil e no telemóvel;
- ter link do portal público pronto.

Não improvisar com base vazia. Produto vazio parece fraco.

### Script 40 min

| Minuto | Bloco | O que mostrar |
|---:|---|---|
| 0-3 | Recap da dor | "Pelo que disseste, o problema maior é X. Vou mostrar só isso." |
| 3-8 | Entrada de reparação | cliente, equipamento, IMEI, avaria, orçamento, estado |
| 8-13 | Gestão visual | lista/Kanban, estados, filtros, timeline |
| 13-19 | Wow 1: portal cliente | abrir `/r/{slug}` no telemóvel |
| 19-24 | Wow 2: diagnóstico guiado + Health Score | checklist, score, destaques |
| 24-29 | Wow 3: orçamento/PDF + QR | PDF profissional, QR para portal |
| 29-33 | Wow 4: garantia digital QR | entregar, garantia `/g/{slug}` |
| 33-36 | Wow 5: margem e pagamentos | receita, custos, lucro, alertas por cobrar |
| 36-39 | Pricing beta | Pro beta 29€/mês, setup assistido |
| 39-40 | Fecho | marcar setup com dados reais |

### Os 5 wow moments

#### Wow 1 - Portal cliente Uber-style

Frase:

```text
Este é o ponto que reduz WhatsApps repetidos. Em vez do cliente perguntar sempre "já está?", recebe um link e vê o estado.
```

Mostrar:

- telemóvel com timeline;
- estado atual;
- botão WhatsApp/telefone;
- aprovação de orçamento.

Não exagerar:

```text
Isto não elimina todas as mensagens. Mas corta muitas perguntas repetidas e dá uma imagem mais profissional.
```

#### Wow 2 - Diagnóstico guiado + Health Score

Frase:

```text
Isto ajuda a loja a não depender só da memória do técnico. Fica registado o que foi testado e como estava o equipamento.
```

Mostrar:

- checklist OK/Marginal/Avaria;
- score 0-100;
- destaques no portal.

Valor:

- menos discussões depois;
- melhor prova de estado à entrada;
- processo mais consistente.

#### Wow 3 - PDF de orçamento com QR

Frase:

```text
Isto dá ao cliente algo apresentável, mas continua a ser documento operacional, não estou a prometer faturação certificada nesta fase.
```

Mostrar:

- logo;
- NIF/morada/IBAN;
- linhas de orçamento;
- QR.

#### Wow 4 - Garantia digital QR

Frase:

```text
Quando entregas, a garantia fica organizada. Se o cliente voltar, não andas à procura de papel ou mensagem antiga.
```

Mostrar:

- página `/g/{slug}`;
- datas;
- cobertura/exclusões;
- dias restantes.

#### Wow 5 - Margem, pagos e alertas

Frase:

```text
Aqui é onde deixa de ser só organização e passa a dinheiro: o que faturaste, o que gastaste em peças e o que ainda está por cobrar.
```

Mostrar:

- dashboard financeiro;
- reparações por cobrar;
- custo da peça;
- lucro.

### Adaptar por tipo de loja

| Tipo de loja | Dor provável | Mostrar mais | Mostrar menos |
|---|---|---|---|
| Técnico solo | tempo, memória, WhatsApp | entrada rápida, portal, garantia | dashboards complexos |
| Loja telemóveis ativa | volume, estados, clientes impacientes | Kanban, portal, WhatsApp, margem | fiscalidade futura |
| Informática/PC | trabalhos longos, orçamento, histórico | Trabalhos, PDF, clientes, despesas | IMEI |
| Multi-técnico pequeno | coordenação | Kanban, timeline, permissões futuras | detalhes de setup |
| Loja sensível a preço | ROI simples | 1 reparação extra paga o mês | feature tour |

### O que NÃO mostrar

- Swagger/API.
- Definições técnicas.
- Roadmap grande.
- Features que ainda não estão estáveis.
- Faturação certificada como se já estivesse pronta.
- WhatsApp automático como se já estivesse ativo.
- Tabelas longas sem relação com a dor.
- Bugs conhecidos sem contexto.

Regra: se a pessoa não disse que tem essa dor, não mostres.

## Objeções - PT-PT

### 1. "Já uso Excel há 10 anos"

Resposta:

```text
Percebo. E se o Excel está mesmo a funcionar, não vale a pena trocar só por trocar.

A diferença aqui é que o Excel não manda o cliente ver o estado, não cria garantia QR, não liga diagnóstico, orçamento, pagamento e histórico do equipamento no mesmo sítio.

A pergunta é: hoje o Excel está só a guardar dados, ou está mesmo a poupar-te tempo e mensagens?
```

Pergunta de avanço:

```text
Queres testar só com 5 reparações reais e comparar com o teu Excel durante uma semana?
```

### 2. "É caro para mim"

Resposta:

```text
Compreendo. Para uma loja pequena, qualquer custo fixo pesa.

Por isso a beta é 29€/mês, sem fidelização. A conta que eu faço é simples: se isto poupar 1 hora por mês, evitar uma reparação esquecida, ou ajudar a fechar mais um orçamento, já se pagou.

Se ao fim de 14 dias não vires valor, não faz sentido continuares.
```

### 3. "Vou pensar"

Resposta:

```text
Claro. Só para eu perceber: é pensar no preço, na confiança, no tempo para mudar, ou em falar com alguém?
```

Se responder vago:

```text
Fazemos assim: em vez de deixarmos isto no ar, marco contigo 15 minutos na próxima semana. Se fizer sentido avançamos com 5 reparações reais; se não fizer, fechamos sem problema.
```

### 4. "Não confio em mais um SaaS"

Resposta:

```text
É uma preocupação justa. Os dados da loja não podem ficar presos numa ferramenta.

No RepairDesk tens export CSV, sem fidelização, e a ideia é exatamente o contrário de lock-in. Os dados são teus. Também estou a documentar backups, RGPD e processos antes de abrir isto a mais lojas.
```

Não prometer:

- "nunca vai falhar";
- "é 100% seguro";
- "somos enterprise".

### 5. "Os meus técnicos não usam computador"

Resposta:

```text
Então o sistema tem de funcionar no telemóvel e com o mínimo de cliques. Não quero que o técnico vire administrativo.

Podemos começar só com o balcão/dono a registar entrada e estados principais. Se depois fizer sentido, os técnicos entram no diagnóstico.
```

### 6. "Não tenho tempo para aprender isto"

Resposta:

```text
Também não tens tempo para andar sempre à procura de mensagens antigas. O setup inicial eu ajudo-te a fazer.

A meta não é aprender tudo. É conseguires criar uma reparação, mudar estado e mandar o link ao cliente no primeiro dia.
```

### 7. "Tenho medo de perder os dados"

Resposta:

```text
É legítimo. Por isso há exportação e backups. Na beta, antes de meteres tudo, começamos com poucas reparações. Não te vou pedir para mudares a loja toda num dia.
```

### 8. "O meu sistema atual já faz isso"

Resposta:

```text
Boa. Então a pergunta é se faz da forma certa para uma oficina portuguesa pequena.

O que eu queria perceber é: o teu sistema atual resolve bem portal cliente, garantia QR, margem por reparação e WhatsApp? Ou só regista tickets?
```

### 9. "Preciso de faturação certificada"

Resposta:

```text
Perfeito, e é importante não brincar com isso. Nesta fase o RepairDesk não substitui faturação certificada. Gera documentos operacionais, orçamentos, garantias e gestão da reparação.

A faturação certificada está no roadmap por integração com provider certificado, mas não te vou vender isso como pronto.
```

### 10. "Manda-me só o link"

Resposta:

```text
Mando, claro. Mas para ser honesto, olhar sozinho para uma conta vazia não mostra o valor.

O que costuma funcionar é eu mostrar 15 minutos com um caso real e depois deixo-te o link para mexeres. Pode ser?
```

## Pricing conversation

### Quando introduzir preço

Não abrir com preço. Introduzir depois de:

1. dor confirmada;
2. demo ligada a essa dor;
3. prospect admite que ajudava.

Transição:

```text
Faz sentido falar de preço para veres se isto cabe na realidade da loja?
```

### Anchoring

Usar comparação diária e por reparação:

```text
O preço público do Pro será 39€/mês por loja. Para os primeiros clientes beta, estou a fazer 29€/mês enquanto mantiverem a subscrição.

Isto dá menos de 1€ por dia. Se tiveres 30 reparações por mês, é menos de 1€ por reparação para ter cliente, estado, orçamento, garantia e margem organizados.
```

### Beta especial 29€/mês

Condições:

- válido para primeiras 10-20 lojas;
- sem fidelização;
- setup assistido incluído;
- feedback mensal obrigatório;
- desconto mantém-se enquanto subscrição estiver ativa;
- pode usar testemunho/case study só com autorização.

Frase:

```text
Faço-te 29€/mês porque estás a entrar cedo e porque quero feedback real de oficina. Não é um desconto por o produto valer menos; é por estares a ajudar a construir a versão certa.
```

### Como dar desconto sem desvalorizar

Bom desconto:

- beta 29€/mês;
- setup gratuito;
- primeiro mês a 19€ se fechar hoje e fizer feedback;
- anual com 2 meses grátis depois de usar 30 dias.

Mau desconto:

- "paga o que quiseres";
- gratuito indefinido;
- 50% vitalício sem contrapartida;
- customizações grátis.

## Close

### Soft close

Usar quando há interesse mas alguma hesitação:

```text
Pelo que vimos, achas que valia a pena testar isto com 5 reparações reais durante 14 dias?
```

### Hard close honesto

Usar quando a dor está clara e o decisor está na chamada:

```text
Queres que deixemos já marcado o setup para terça? Em 45 minutos metemos a loja criada, importamos alguns clientes e crias a primeira reparação real.
```

### Trial vs primeiro mês a pagar

Recomendação para lojas amigas:

- 14 dias de teste assistido;
- pedir autorização para converter para 29€/mês no dia 15 se houver uso real;
- idealmente cobrar primeiro mês no setup quando a relação já é forte.

Frase:

```text
Podemos fazer de duas formas. Ou testas 14 dias e decidimos no fim, ou fazes já o primeiro mês beta a 29€ e eu ajudo-te no setup. Se no fim da primeira semana vires que não faz sentido, paramos sem stress.
```

Melhor para validação: **primeiro mês pago a 29€**, porque prova valor.  
Melhor para amizade/confiança: **14 dias assistidos com data de decisão marcada**.

## Follow-up

Regra: follow-up curto, concreto e com próximo passo. Não escrever romances.

### 1 hora depois

Objetivo: recapitular e não deixar arrefecer.

### 24 horas

Objetivo: confirmar próxima ação.

### 48 horas

Objetivo: remover objeção.

### 7 dias

Objetivo: fechar ciclo ou pausar.

Quando parar:

- depois de 2 follow-ups sem resposta;
- se disser "não";
- se adiar sem razão concreta 2 vezes;
- se não for decisor e não trouxer decisor.

Mensagem final elegante:

```text
Sem problema, não te chateio mais com isto agora. Vou continuar a melhorar o RepairDesk com outras oficinas. Se daqui a uns tempos fizer sentido, falamos.
```

## 5 templates de email follow-up

### 1. Depois da discovery/demo

Assunto: Resumo da conversa - RepairDesk

```text
Olá {Nome},

Obrigado pelo tempo de hoje.

Pelo que percebi, os pontos principais na {Loja} são:
- {dor 1}
- {dor 2}
- {dor 3}

O RepairDesk pode ajudar sobretudo em:
- registar reparações e estados;
- reduzir mensagens repetidas com o portal cliente;
- organizar orçamento, garantia e margem por reparação.

Próximo passo combinado: {próximo passo}, em {data/hora}.

Abraço,
Bruno
```

### 2. Marcar setup beta

Assunto: Setup beta RepairDesk

```text
Olá {Nome},

Para avançarmos com o teste beta, proponho começarmos simples:

1. criar a conta da loja;
2. configurar dados básicos;
3. importar/criar 5-10 clientes;
4. criar a primeira reparação real;
5. testar o link do portal cliente.

Demora cerca de 45 minutos.

Tenho disponibilidade {opção 1} ou {opção 2}. Qual te dá mais jeito?

Bruno
```

### 3. Follow-up 24h sem resposta

Assunto: Re: RepairDesk

```text
Olá {Nome},

Só para confirmar se ainda faz sentido avançarmos com o teste.

Não queria deixar isto no ar: se agora não for prioridade, tudo bem. Se fizer sentido, marcamos 45 minutos e colocamos 5 reparações reais no sistema.

Abraço,
Bruno
```

### 4. Depois de 7 dias de trial

Assunto: Como está a correr o teste?

```text
Olá {Nome},

Já passou cerca de uma semana desde que começámos o teste.

Queria perceber 3 coisas:

1. Criaram reparações reais?
2. O portal/estado ajudou ou ainda não entrou no hábito?
3. O que está a bloquear o uso diário?

Se estiver a trazer valor, combinamos a passagem para o plano beta Pro a 29€/mês. Se não estiver, prefiro perceber porquê e corrigir.

Bruno
```

### 5. Fecho perdido / pausa

Assunto: Fecho o ciclo por agora

```text
Olá {Nome},

Como não consegui confirmar o próximo passo, vou fechar o ciclo por agora para não insistir.

Obrigado pelo tempo e pelo feedback. Vou continuar a melhorar o RepairDesk com outras oficinas e, se daqui a uns tempos fizer sentido, terei gosto em voltar a mostrar-te.

Abraço,
Bruno
```

## CRM minimalista

Usar Google Sheets. Não pagar Pipedrive/HubSpot nesta fase.

### Pipeline

```text
Lead -> Contactado -> Qualified -> Demo Marcada -> Demo Feita -> Trial/Setup -> Customer -> Lost
```

### Template Google Sheets

```text
| id | loja | cidade | tipo | contacto | origem | estado | dor_principal | volume_mes | decisor | plano_sugerido | valor | proxima_acao | data_proxima_acao | ultimo_contacto | probabilidade | motivo_perda | notas |
| 001 | Loja X | Viseu | Telemóveis | 9xx/email | amigo | Qualified | WhatsApp e estados | 40 | Sim | Pro beta | 29 | demo | 2026-05-20 | 2026-05-16 | 40% | | usa Excel |
```

Estados:

| Estado | Definição |
|---|---|
| Lead | loja identificada, ainda sem conversa |
| Contactado | primeira mensagem/chamada feita |
| Qualified | tem dor, volume e decisor |
| Demo Marcada | data marcada |
| Demo Feita | viu produto |
| Trial/Setup | conta criada ou setup agendado |
| Customer | pagou ou aceitou beta paga |
| Lost | não avança |

Campos obrigatórios:

- `dor_principal`
- `proxima_acao`
- `data_proxima_acao`
- `motivo_perda`

Se não há próxima ação, não há oportunidade. Há só esperança.

## Métricas

### Funil inicial

| Métrica | Meta inicial | Reagir se |
|---|---:|---|
| Contactado -> Responde | >20% | <10%: mensagem errada ou leads frias |
| Responde -> Qualified | >50% | <30%: targeting fraco |
| Qualified -> Demo | >60% | <40%: proposta pouco clara |
| Demo -> Trial/Setup | >40% | <25%: demo não mostra valor |
| Trial -> Pago | >25% | <15%: produto/onboarding/preço falha |
| Ciclo contacto -> pago | 7-14 dias | >30 dias: sem urgência ou close fraco |

### Razões de perda

Usar uma destas, não texto livre infinito:

- sem dor;
- sem volume;
- caro;
- falta confiança;
- já tem sistema;
- precisa faturação certificada;
- sem tempo;
- não decisor;
- produto incompleto;
- sem resposta.

Como reagir:

| Padrão | Ação |
|---|---|
| Muitas perdas por "caro" | melhorar ROI e vender Starter/Pro correto; não baixar logo preço |
| Muitas por "sem tempo" | oferecer setup assistido e fluxo de 15 min |
| Muitas por "falta confiança" | mostrar export, backups, RGPD, LopesTech real |
| Muitas por "produto incompleto" | identificar feature comum; se 3 lojas pedem, priorizar |
| Muitas por "faturação" | explicar limite; preparar integração certificada, não prometer |

## Plano para primeiras 3 lojas amigas

Objetivo: não "vender a toda a gente". É aprender com 3 perfis ligeiramente diferentes.

### Loja 1 - Viseu, confiança alta

Perfil ideal:

- conhece Bruno ou LopesTech;
- 20-50 reparações/mês;
- usa WhatsApp/papel/Excel;
- dono decide rápido.

Plano:

| Dia | Ação |
|---:|---|
| 1 | conversa presencial + discovery 20 min |
| 2 | demo 40 min |
| 3 | setup assistido com 5 reparações reais |
| 5 | check-in WhatsApp |
| 7 | corrigir bloqueios |
| 10-14 | converter para Pro beta 29€/mês |

Objetivo de aprendizagem:

- onboarding;
- se portal cliente é percebido;
- se 29€/mês parece justo.

### Loja 2 - Porto, mais profissional

Perfil ideal:

- mais volume;
- equipa 2-5 pessoas;
- talvez já use ferramenta parcial;
- mais exigente.

Plano:

| Dia | Ação |
|---:|---|
| 1 | discovery remoto |
| 3 | demo focada em volume/Kanban/dashboard |
| 4 | proposta beta com setup |
| 7 | setup ou lost claro |
| 14 | decisão paga |

Objetivo de aprendizagem:

- objeções de confiança;
- multi-utilizador;
- concorrência com sistemas existentes.

### Loja 3 - Informática/PC

Perfil ideal:

- faz PCs, portáteis, upgrades, diagnósticos;
- reparações mais longas;
- menos IMEI, mais trabalhos/orçamentos.

Plano:

| Dia | Ação |
|---:|---|
| 1 | discovery sobre trabalhos, orçamentos e histórico |
| 2 | demo adaptada: Trabalhos + PDF + despesas + garantia |
| 3-5 | setup com 3 trabalhos reais |
| 10 | avaliar se RepairDesk é demasiado mobile ou serve informática |

Objetivo de aprendizagem:

- validar categoria informática;
- perceber se "Trabalhos" está suficientemente bom;
- ajustar pitch.

## Checklist antes de cada demo

- [ ] Saber nome da loja e pessoa.
- [ ] Saber tipo de loja.
- [ ] Ter 3 hipóteses de dor.
- [ ] Demo tenant com dados preenchidos.
- [ ] Link portal pronto no telemóvel.
- [ ] PDF orçamento pronto.
- [ ] Garantia QR pronta.
- [ ] Preço beta claro: 29€/mês.
- [ ] Próximo passo preparado: setup 45 min.
- [ ] Google Sheet aberto para notas logo depois.

## Checklist depois de cada demo

Preencher no CRM:

- dor principal;
- volume estimado;
- decisor sim/não;
- objeções;
- features que causaram interesse;
- features que confundiram;
- próximo passo;
- data;
- probabilidade;
- motivo se lost.

Mensagem interna do Bruno após cada demo:

```text
Esta loja pagaria por isto hoje? Se não, porquê?
```

## O que Bruno deve evitar

- pedir desculpa pelo produto estar em beta a cada 2 minutos;
- mostrar tudo;
- aceitar gratuito sem data de fim;
- prometer faturação certificada;
- prometer WhatsApp automático antes de estar pronto;
- discutir preço antes de dor;
- fazer customização para 1 loja sem padrão;
- acabar reunião sem próximo passo.

## Resumo operacional

Para a primeira loja, o objetivo não é parecer empresa grande. É parecer confiável, útil e próximo.

Script mental:

1. "Também sou técnico."
2. "Quero perceber como trabalhas."
3. "Vou mostrar só o que resolve essa dor."
4. "Testamos com 5 reparações reais."
5. "Se trouxer valor, fica Pro beta a 29€/mês."
6. "Se não trouxer, dizes-me sem filtros."

Este é o playbook todo.
