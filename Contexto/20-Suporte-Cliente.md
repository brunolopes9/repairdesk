# Modelo de Suporte ao Cliente - RepairDesk SaaS PT

Atualizado: 2026-05-16  
Contexto: fundador solo, 0 EUR budget, objetivo escalar de 1 a 100 lojas sem suporte dedicado ate ser inevitavel.

---

## 1. Decisao executiva

Stack recomendada para o RepairDesk:

| Camada | Escolha | Porquê |
|---|---|---|
| Canal principal | Email `suporte@repairdesk.pt` ou `suporte@lopestech.pt` | Assincrono, barato, escalavel, nao interrompe o Bruno a cada 5 minutos |
| Canal secundario | Widget in-app com formulario/ticket, nao chat live permanente | Parece moderno, mas continua batched; evita promessa de resposta imediata |
| KB | Notion publico no inicio; migrar para docs proprias quando houver SEO forte | Mais rapido para publicar 20 artigos; custo zero; facil editar |
| Video | YouTube nao listado ou publico, videos ate 2 min embebidos na KB | Reduz tickets repetidos; melhor para lojas pequenas |
| Ticketing | Comecar com Gmail + labels + snippets; depois Crisp Free ou Help Scout quando justificar | Nao pagar antes da dor existir |
| CSAT | Link de 1 clique no fim de cada resposta resolvida | Mede qualidade sem ferramenta cara |

Decisao clara:

> O RepairDesk deve ter 2 portas de suporte: email e formulario in-app. WhatsApp fica reservado para incidentes criticos, beta muito inicial e clientes Enterprise/futuro, nao para suporte diario.

Porquê nao WhatsApp como canal principal:

- Em Portugal parece natural, mas para fundador solo e uma armadilha.
- Cria expectativa de resposta imediata.
- Mistura suporte, vendas, vida pessoal e urgencias falsas.
- Dificulta medir SLA, historico, CSAT e handoff para freelancer.

Mensagem publica:

> Suporte humano, em portugues, por email e dentro da app. Respondemos em horario util e damos prioridade a problemas que bloqueiam trabalho real na loja.

---

## 2. Comparacao de canais

Precos consultados em 2026-05-16. Confirmar antes de comprar.

| Canal | Custo mes | Carga para fundador solo | Qualidade percebida | Escala com part-time | Veredicto |
|---|---:|---|---|---|---|
| Email | 0-6 EUR | Baixa/media; permite responder por blocos | Boa se respostas forem rapidas e claras | Excelente | Canal principal |
| WhatsApp Business | 0 EUR app; API paga no futuro | Alta; muitas interrupcoes | Muito alta em PT | Media/baixa se nao houver processo | Nao usar como principal |
| In-app chat live | Tawk.to free; Crisp free basico; pagos sobem rapido | Alta se prometer live chat | Alta | Boa se houver equipa | Usar como formulario, nao live |
| Discord publico | 0 EUR | Media; requer moderacao | Baixa/media para lojas tradicionais | Boa para comunidade tecnica | Adiar |
| Forum/Discourse | Hosting/gestao | Alta cedo; vazio parece abandono | Baixa no inicio | Boa a escala grande | Nao MVP |
| Help Scout | ~25 USD/user/mes no Standard segundo fontes de mercado | Baixa; muito bom para email | Alta | Excelente | Bom aos 20-50 clientes |
| GitBook KB | Free publico; planos pagos podem ficar caros | Baixa | Alta | Boa | Usar se Notion ficar curto |

Notas de ferramentas:

- Tawk.to anuncia chat gratuito e opcao de agentes pagos por hora. Bom custo, mas chat live pode destruir foco se ficar sempre aberto.
- Crisp tem plano free com chat basico; planos pagos recentes aparecem em tiers bem mais caros. Bom para comecar, mas nao assumir que fica barato para sempre.
- Help Scout e mais profissional para suporte por email, mas antes de 20-30 lojas pode ser luxo.
- GitBook e elegante para docs, mas o Notion publico ganha por velocidade e custo zero no MVP.

---

## 3. Stack recomendada por fase

### MVP / 0-10 lojas

Stack:

- Email: `suporte@repairdesk.pt` ou `suporte@lopestech.pt`
- Gmail/Google Workspace ou mailbox equivalente
- Labels:
  - `P0 Incidente`
  - `P1 Bloqueia trabalho`
  - `P2 Bug normal`
  - `P3 Como fazer`
  - `Billing`
  - `Feature request`
- Snippets Gmail / templates.
- Notion publico: `help.repairdesk.pt` quando possivel.
- In-app: botao "Ajuda" com:
  - pesquisar artigos;
  - "Contactar suporte";
  - formulario que envia email com contexto.

Nao instalar chat live permanente nesta fase.

### 10-50 lojas

Stack:

- Continuar email como principal.
- Adicionar Crisp Free ou Tawk.to apenas como widget in-app configurado para modo assíncrono:
  - texto: "Normalmente respondemos em horario util."
  - pedir email, loja, URL atual, severidade.
- KB publica com 20-40 artigos.
- CSAT por link.
- Relatorio mensal de tickets.

Quando o volume passar 25-40 tickets/semana, testar Help Scout por 1 mes.

### 50-100+ lojas

Stack:

- Help Scout ou ferramenta semelhante.
- Freelancer part-time PT 5-10h/semana.
- KB madura com SEO.
- Macros + triagem.
- Pagina de estado simples.
- Suporte prioritario para Enterprise.

---

## 4. Politica de canais

### Canal principal: email

Endereco:

- Ideal: `suporte@repairdesk.pt`
- Alternativa temporaria: `suporte@lopestech.pt`

Copy no site/app:

```text
Precisas de ajuda?

Envia email para suporte@repairdesk.pt ou usa o formulario dentro da app.
Respondemos em portugues, em horario util, e damos prioridade a problemas que bloqueiam a operacao da loja.
```

### Canal secundario: formulario in-app

Campos:

- Assunto
- Tipo: Bug / Como fazer / Faturacao / Sugestao / Incidente
- Severidade: Bloqueia loja / Bloqueia uma reparacao / Duvida normal
- Mensagem
- Anexar screenshot

Campos automaticos:

- userId
- tenantId
- email
- URL atual
- browser
- timestamp
- app version

Antes de submeter:

```text
Antes de enviares:

1. Ja procuraste na Ajuda?
2. Ja tentaste atualizar a pagina?
3. Se for sobre uma reparacao, inclui o numero da reparacao.

[Pesquisar ajuda] [Enviar pedido]
```

### WhatsApp

Uso permitido:

- beta inicial com 2-3 lojas amigas;
- incidentes criticos;
- combinacao de chamadas/demos;
- futuro Enterprise.

Mensagem automatica WhatsApp Business:

```text
Obrigado pela mensagem. Para suporte do RepairDesk, envia por favor o pedido para suporte@repairdesk.pt ou usa Ajuda dentro da app.

Assim fica registado, consigo responder melhor e nao se perde no meio das conversas.

Se for uma urgencia que bloqueia a loja inteira, escreve "URGENTE" e o nome da loja.
```

---

## 5. SLA publico proposto

Pagina: `/suporte`

### Suporte RepairDesk

O suporte e humano, em portugues, e pensado para lojas pequenas que precisam de respostas claras, nao de burocracia.

Horario normal:

- Segunda a sexta, 09:30-18:00, hora de Portugal.
- Fins-de-semana e feriados: apenas incidentes criticos, quando possivel.

Tempos de resposta alvo:

| Severidade | Exemplo | Primeira resposta | Objetivo de resolucao |
|---|---|---:|---:|
| P0 Incidente critico | app indisponivel para varias lojas, perda/acesso indevido a dados | 2h em horario util; 12h fora horario | Mitigacao no proprio dia |
| P1 Bloqueia trabalho | loja nao consegue criar/abrir reparacoes ou login falha | 4h em horario util | 1 dia util ou workaround |
| P2 Bug normal | erro numa funcao com alternativa | 1 dia util | 3-5 dias uteis ou agendamento |
| P3 Duvida de uso | "como faco X?" | 2 dias uteis | resposta ou artigo |
| P4 Sugestao | pedido de feature/melhoria | 5 dias uteis | avaliado no roadmap |

Compromisso honesto:

```text
Estes tempos sao objetivos de suporte, nao garantias financeiras. Se houver um problema serio, damos prioridade a transparencia, workaround e correcao. Preferimos prometer pouco e responder bem.
```

O que nao esta incluido:

- configuracao completa da loja feita pelo suporte;
- importacao manual extensa fora de plano/acordo;
- formacao personalizada ilimitada;
- suporte fiscal/juridico/contabilistico;
- suporte a equipamentos ou reparacoes fisicas do cliente final.

---

## 6. Self-service primeiro

Meta:

> 80% das duvidas comuns devem ser resolvidas sem contactar o Bruno.

Camadas:

1. Onboarding wizard: cria primeira reparacao em menos de 30 min.
2. Empty states que ensinam.
3. Tooltips contextuais nos pontos de friccao.
4. KB pesquisavel.
5. Videos curtos dentro dos artigos.
6. Formulario de suporte com sugestoes antes de enviar.

Sugestoes automaticas antes do ticket:

| Assunto escrito | Artigos sugeridos |
|---|---|
| "criar reparacao" | Como criar a primeira reparacao; Como criar cliente novo |
| "pdf" / "orcamento" | Como gerar um orcamento PDF; Porque o documento nao e fatura |
| "cliente" | Como editar cliente; Telefone opcional |
| "pago" / "pagamento" | Como marcar uma reparacao como paga |
| "portal" / "link" | Como enviar o portal ao cliente |
| "despesa" / "peca" | Como associar custos a reparacao |

Regra:

Se a mesma duvida aparecer 3 vezes, vira artigo ou tooltip.

---

## 7. Base de conhecimento

### Tipo de conteudo

Mix recomendado:

- Artigos curtos com screenshots.
- Videos de 60-120 segundos para fluxos visuais.
- FAQ para decisoes simples.
- Tutoriais passo-a-passo.
- Artigos publicos SEO para temas que tambem atraem leads.

Formato ideal:

```text
# Como fazer X

Para que serve
Quando usar
Passo a passo
Erros comuns
Perguntas frequentes
Relacionado
```

### Plataforma

| Plataforma | Custo | Vantagem | Risco | Decisao |
|---|---:|---|---|---|
| Notion publico | 0 EUR | Publica hoje, facil editar | SEO/branding limitado | Escolha MVP |
| Blog proprio | custo dev | SEO e branding fortes | demora a construir | Migrar artigos SEO |
| GitBook | free/pago | docs bonitas e pesquisaveis | planos pagos caros | Opcao futura |
| Outline | hosting | bom self-host | manutencao | Nao agora |
| Discourse | hosting | comunidade | vazio cedo | Nao agora |

Decisao:

- MVP: Notion publico em `Ajuda RepairDesk`.
- SEO: duplicar/adaptar os melhores 5-10 artigos para o blog publico depois.

---

## 8. 20 artigos essenciais para lancamento

Prioridade A: publicar na primeira semana.

| # | Titulo | Outline |
|---:|---|---|
| 1 | Comecar no RepairDesk em 30 minutos | criar conta, dados da loja, primeiro cliente, primeira reparacao, dashboard |
| 2 | Como criar a primeira reparacao | cliente, equipamento, avaria, IMEI opcional, orcamento, estado inicial |
| 3 | Como criar e editar clientes | campos minimos, telefone opcional, NIF, notas, historico |
| 4 | Como mudar o estado de uma reparacao | Recebido, Diagnostico, Aguarda peca, Em reparacao, Reparado, Entregue |
| 5 | Como marcar uma reparacao como paga | estados de pagamento, pago parcial, receita pendente, erros comuns |

Prioridade B: publicar antes da beta alargada.

| # | Titulo | Outline |
|---:|---|---|
| 6 | Como gerar um orcamento PDF | dados da loja, logo, NIF, IBAN, aviso nao fiscal |
| 7 | Porque os documentos do RepairDesk nao sao fatura | diferenca entre ficha/orcamento e fatura, provider externo futuro, ligacao a compliance PT |
| 8 | Como enviar o portal da reparacao ao cliente | link/QR, o que o cliente ve, privacidade, quando enviar |
| 9 | Como usar mensagens WhatsApp pre-preenchidas | click-to-WhatsApp, templates, cuidado com consentimento |
| 10 | Como associar despesas e pecas a uma reparacao | custo de peca, fornecedor, lucro real, investimento em stock |
| 11 | Como ler o dashboard financeiro | receita, pendente, lucro realizado, custos, alertas |
| 12 | Como encontrar rapidamente uma reparacao | pesquisa por cliente/equipamento/IMEI, filtros, estados |
| 13 | Como convidar um funcionario | roles, tecnico vs admin, remover acesso |
| 14 | Como configurar dados da loja | logo, nome legal, NIF, IBAN, CAE, termos |
| 15 | Como exportar os dados da loja | CSV, dados incluidos, quando usar, cancelamento |

Prioridade C: publicar antes de 20 lojas.

| # | Titulo | Outline |
|---:|---|---|
| 16 | Erros comuns ao criar reparacoes | cliente duplicado, IMEI errado, orcamento vazio, estado errado |
| 17 | Como gerir garantias de reparacao | prazo, comprovativo, notas, historico |
| 18 | Como trabalhar com clientes sem telefone | Messenger/Instagram, telefone opcional, alternativas |
| 19 | Como preparar a loja para importar dados | template CSV, campos obrigatorios, limpeza de Excel |
| 20 | Como pedir suporte de forma rapida | numero da reparacao, screenshot, severidade, tempos de resposta |

Regra editorial:

- Cada artigo deve ter menos de 700 palavras.
- Cada video deve ter menos de 2 minutos.
- O titulo deve ser o que o cliente pesquisa, nao linguagem interna.

---

## 9. Templates de resposta

### 1. Recebemos o pedido

```text
Ola {nome},

Obrigado pelo contacto. Recebi o teu pedido e vou analisar.

Resumo do que percebi:
- Loja: {loja}
- Tema: {tema}
- Impacto: {impacto}

Se tiveres um screenshot ou o numero da reparacao, envia-me por favor. Ajuda-me a chegar la mais rapido.

Bruno
RepairDesk
```

### 2. Pedido incompleto

```text
Ola {nome},

Consigo ajudar, mas falta-me um detalhe para nao andar as cegas.

Podes enviar:
- numero da reparacao, se existir;
- screenshot do erro;
- o que estavas a tentar fazer;
- se acontece sempre ou so uma vez?

Assim consigo perceber se e bug, configuracao ou fluxo normal.
```

### 3. Como criar primeira reparacao

```text
Ola {nome},

Para criar a primeira reparacao:

1. Vai a Reparacoes
2. Clica em Nova reparacao
3. Escolhe ou cria o cliente
4. Preenche equipamento e avaria
5. Guarda

O minimo e mesmo so cliente + equipamento + avaria. IMEI, orcamento e notas podem ficar para depois.

Guia com screenshots:
{link_artigo}
```

### 4. Documento nao e fatura

```text
Ola {nome},

O PDF gerado pelo RepairDesk nesta fase e um documento operacional: orcamento/ficha/garantia. Nao substitui fatura.

Para fatura legal, por agora deves usar o Portal das Financas ou o teu software certificado. A integracao com provider certificado esta no roadmap.

Expliquei isto aqui:
{link_artigo}
```

### 5. Bug com workaround

```text
Ola {nome},

Confirmei que isto e um bug. Obrigado por avisares.

Workaround por agora:
{workaround}

Vou corrigir e aviso-te quando estiver resolvido. Como nao bloqueia a loja inteira, fica como prioridade P2.

Bruno
```

### 6. Bug critico

```text
Ola {nome},

Isto e critico e vou tratar com prioridade.

O que ja sei:
- Impacto: {impacto}
- Lojas afetadas: {escopo}
- Workaround: {workaround_ou_nao_existe}

Vou atualizando este email ate ficar resolvido. Proxima atualizacao ate {hora}.

Bruno
```

### 7. Feature request

```text
Ola {nome},

Faz sentido. Registei como sugestao de produto.

Para eu avaliar bem, podes dizer-me:
1. Que problema isto resolvia no dia-a-dia?
2. Quantas vezes por semana acontece?
3. Como resolves hoje sem o RepairDesk?

Nao prometo data ja, mas uso este feedback para priorizar o roadmap.
```

### 8. Pedido fora de scope

```text
Ola {nome},

Percebo o pedido, mas isto neste momento fica fora do scope do RepairDesk.

O foco atual e: clientes, reparacoes, estados, orcamentos operacionais, portal cliente, custos e lucro.

Prefiro ser direto do que prometer uma coisa que nao consigo manter bem.
```

### 9. Fecho com CSAT

```text
Ola {nome},

Fico contente por estar resolvido.

Podes avaliar rapidamente o suporte?

{link_csat}

E so 1 clique. Ajuda-me a perceber se estou a responder bem ou se tenho de melhorar.

Bruno
```

### 10. Incidente resolvido

```text
Ola {nome},

O problema ficou resolvido as {hora}.

Resumo:
- O que aconteceu: {causa_curta}
- Impacto: {impacto}
- O que foi feito: {correcao}
- Como vou evitar repeticao: {prevencao}

Obrigado pela paciencia. Se ainda vires algum comportamento estranho, responde a este email.

Bruno
```

---

## 10. Macros e triagem

### Labels

| Label | Uso |
|---|---|
| `P0 Incidente` | app down, dados, seguranca |
| `P1 Bloqueia trabalho` | login, criar reparacao, guardar dados |
| `P2 Bug` | erro com workaround |
| `P3 Duvida` | como fazer |
| `Billing` | pagamento/plano/fatura LopesTech |
| `Feature` | sugestao |
| `KB Candidate` | duvida que deve virar artigo |
| `Waiting customer` | falta info do cliente |

### Rotina diaria solo

Blocos:

- 09:30-10:00: triagem e P0/P1.
- 13:30-14:00: respostas normais.
- 17:30-18:00: fecho e follow-ups.

Regra:

> Fora de P0/P1, nao viver dentro da inbox.

### Rotina semanal

Sexta-feira, 30 minutos:

- contar tickets por categoria;
- identificar top 3 duvidas repetidas;
- criar/atualizar 1 artigo KB;
- rever bugs abertos;
- rever CSAT.

---

## 11. KPIs

| KPI | Como medir | Meta 0-10 lojas | Meta 10-50 lojas | Alerta |
|---|---|---:|---:|---|
| First response time | criado -> primeira resposta humana | <1 dia util | P1 <4h, normal <1 dia | >2 dias |
| Resolution time | criado -> resolvido | P1 1 dia, P2 5 dias | idem | P1 >2 dias |
| Tickets/loja/mes | tickets / lojas ativas | <2 | <1,5 | >3 |
| Tickets por tipo | labels | P3 deve cair com KB | bugs devem cair | P3 repetido |
| CSAT | 1-5 ou bom/neutro/mau | >80% bom | >85% bom | <75% |
| Tempo Bruno suporte | calendario/manual | <2h/semana | <6h/semana | >8h/semana |
| KB deflection | artigo visto antes do ticket | começar a medir | >20% | 0% |
| Reabertura | ticket reaberto / resolvidos | <10% | <8% | >15% |

CSAT simples:

No fim de cada ticket resolvido:

```text
Como foi o suporte?
[Bom] [Ok] [Mau]
```

Se "Mau":

```text
O que faltou?
[Demorou] [Nao resolveu] [Resposta confusa] [Outro]
```

---

## 12. Quando contratar o primeiro humano

Nao contratar por ansiedade. Contratar quando o suporte estiver a roubar produto/vendas.

Critérios:

- Mais de 8h/semana de suporte durante 4 semanas seguidas.
- Mais de 25 tickets/semana.
- First response normal passa 2 dias uteis.
- MRR >= 1.500-2.000 EUR e churn/risco justifica ajuda.
- KB ja existe; caso contrario vais contratar alguem para repetir caos.

Perfil:

- Portugues nativo.
- Calmo, claro, organizado.
- Confortavel com SaaS e lojas pequenas.
- Nao precisa ser tecnico de telemoveis, mas tem de perceber workflow de reparacoes.
- 5-10h/semana inicialmente.

Onde procurar:

- Indicacao pessoal primeiro.
- Comunidades PT de freelancers.
- Workana/Malt/LinkedIn.
- Ex-assistente administrativo/a de loja ou suporte tecnico.
- Estudante part-time com boa escrita, se houver processo claro.

Nao usar Toptal para isto: caro e desalinhado.

### Onboarding do freelancer

Semana 1:

- ler KB inteira;
- ver 5 demos gravadas;
- responder tickets ficticios;
- observar Bruno em 10 tickets reais.

Semana 2:

- responder P3/P4 com aprovacao do Bruno;
- criar 2 artigos KB;
- atualizar macros.

Semana 3:

- assumir P3/P4;
- triagem P1/P2 para Bruno;
- relatorio semanal.

Nunca delegar cedo:

- incidentes de seguranca;
- bugs criticos;
- billing sensivel;
- clientes zangados com risco de churn;
- promessas de roadmap.

---

## 13. Roadmap de suporte

### 0-10 lojas

Objetivo:

- Parecer humano e presente sem abrir 5 canais.

Fazer:

- email de suporte;
- 5 artigos KB criticos;
- snippets;
- SLA publico;
- formulario in-app simples;
- CSAT manual.

Nao fazer:

- chat live 24/7;
- Discord;
- forum;
- AI chatbot;
- suporte por WhatsApp publico.

### 10-50 lojas

Objetivo:

- Reduzir repeticao e medir carga.

Fazer:

- KB 20+ artigos;
- widget in-app assíncrono;
- labels/macros consistentes;
- dashboard mensal de suporte;
- artigos baseados em tickets repetidos;
- testar Help Scout/Crisp se Gmail doer.

Nao fazer:

- contratar antes de medir;
- prometer SLA financeiro;
- aceitar suporte custom infinito por cliente.

### 50-100+ lojas

Objetivo:

- Tirar P3/P4 do Bruno e proteger produto/vendas.

Fazer:

- Help Scout ou equivalente;
- freelancer 5-10h/semana;
- KB 40+ artigos;
- pagina status;
- processos de escalamento;
- relatorios trimestrais de suporte/produto.

Possivel:

- WhatsApp Business apenas para Pro/Enterprise com regras;
- comunidade fechada de clientes, se houver massa critica.

---

## 14. Pagina publica `/suporte`

Texto pronto:

```text
# Suporte RepairDesk

O suporte do RepairDesk e humano, em portugues, e pensado para oficinas que precisam de respostas claras.

## Como pedir ajuda

Dentro da app: Ajuda -> Contactar suporte  
Por email: suporte@repairdesk.pt

Para resolvermos mais rapido, inclui:
- nome da loja;
- numero da reparacao, se existir;
- screenshot do problema;
- o que estavas a tentar fazer.

## Horario

Segunda a sexta, 09:30-18:00, hora de Portugal.
Fins-de-semana e feriados: apenas incidentes criticos, quando possivel.

## Tempos de resposta alvo

- Incidente critico: primeira resposta ate 2h em horario util.
- Problema que bloqueia trabalho: ate 4h em horario util.
- Bug normal: ate 1 dia util.
- Duvida de utilizacao: ate 2 dias uteis.
- Sugestao de melhoria: ate 5 dias uteis.

Estes tempos sao objetivos realistas, nao promessas vazias. Se houver um problema serio, a prioridade e dar workaround, corrigir e comunicar com clareza.

## Antes de contactar

Consulta a Ajuda RepairDesk. Muitos fluxos têm guia passo-a-passo e videos curtos.

[Abrir Ajuda]
```

---

## 15. Fontes consultadas

Fontes externas:

- tawk.to pricing: https://www.tawk.to/pricing/
- Crisp pricing: https://crisp.chat/en/pricing/
- GitBook pricing: https://www.gitbook.com/pricing
- Help Scout pricing, referencia de mercado 2026: https://automationatlas.io/answers/help-scout-pricing-explained-2026/

Fontes internas:

- `Contexto/07-Pricing-Proposta.md`
- `Contexto/09-Customer-Acquisition.md`
- `Contexto/12-Onboarding-Wizard.md`
- `Contexto/16-Compliance-RGPD.md`

---

## 16. Plano de 1 dia para o Bruno

Manha:

1. Criar email `suporte@repairdesk.pt` ou alias.
2. Criar labels no Gmail.
3. Guardar os 10 templates como snippets.
4. Criar pagina `/suporte` com o SLA.

Tarde:

5. Criar Notion publico "Ajuda RepairDesk".
6. Publicar os 5 artigos A:
   - Comecar no RepairDesk em 30 minutos
   - Como criar a primeira reparacao
   - Como criar e editar clientes
   - Como mudar o estado de uma reparacao
   - Como marcar uma reparacao como paga
7. Adicionar link "Ajuda" no footer/app.

Fim do dia:

8. Testar enviar um pedido do formulario in-app para o email.
9. Responder com template.
10. Fechar ticket com CSAT manual.

Se isto estiver feito, o suporte MVP existe.
