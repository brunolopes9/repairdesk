# Onboarding Wizard - RepairDesk SaaS PT

Ultima atualizacao: 2026-05-16  
Status: especificacao para Sprint 20 / beta 2-3 lojas amigas  
Objetivo: levar uma loja de "criou conta" a "primeira reparacao registada" em menos de 30 minutos, sem chamada com o Bruno.

---

## 1. Principio central

O onboarding nao e uma visita guiada ao software. E a primeira reparacao da loja feita com rodas pequenas.

Meta operacional:

- Em menos de 2 minutos: loja percebe onde esta e que pode saltar o setup.
- Em menos de 10 minutos: dados minimos da loja e primeiro cliente existem.
- Em menos de 20 minutos: primeira reparacao demo ou real esta registada.
- Em menos de 30 minutos: dono viu o dashboard, percebeu o proximo passo e sabe voltar sozinho.

Regra de produto:

> Pedir o minimo antes do primeiro valor. Tudo o que nao desbloqueia a primeira reparacao fica para depois.

Para lojas pequenas, o dono esta no balcao, a atender WhatsApp, a abrir telemoveis e a fazer contas. O onboarding tem de funcionar nesse caos.

---

## 2. Referencias de onboarding a aplicar

### 2.1 RO App / Revolio - concorrencia direta

Fontes consultadas:

- RO App, pagina "Software de Loja de Reparos Telefonicos": https://roapp.io/pt/phone-repair-shop/
- Revolio, pagina publica do produto: https://www.revolio.pt/

O que o RO App comunica bem:

- Comeca pelo resultado pratico: tickets, clientes, pecas, pagamentos e stock num sitio.
- Tem trial sem cartao e demo personalizada.
- Vende "integracao personalizada" e centro de ajuda como parte do arranque.
- Mostra features ligadas ao fluxo real: tickets com IMEI, estados personalizados, tarefas, atualizacoes por SMS/email/WhatsApp, estimativas, pagamentos, inventario e app movel para tecnicos.
- Promete suporte em todas as etapas, chat ao vivo e guias.

O que isto significa para o RepairDesk:

- Nao competir com "temos 100 features". Competir com "em 20 minutos tens a primeira reparacao criada".
- RO App usa onboarding assistido como argumento. RepairDesk deve ter onboarding self-serve forte e oferecer ajuda humana apenas como fallback.
- O wizard deve criar objetos reais: loja, cliente, reparacao. Nada de slideshow.
- Como o RO App e generalista, RepairDesk deve soar mais especifico para Portugal e eletronica: "telemovel", "IMEI", "orcamento", "peca", "cliente por WhatsApp", "NIF", "IBAN".

Decisao:

RepairDesk nao deve copiar a promessa de "integracao personalizada" como requisito. Deve prometer: "Consegues comecar sozinho. Se bloqueares, o Bruno ajuda."

### 2.2 Shopify - checklist orientada ao lancamento

Fonte: Shopify Help Center, "General checklist for starting a new Shopify store":  
https://help.shopify.com/en/manual/intro-to-shopify/initial-setup/new-to-shopify-checklists/general-checklist

Principios a aplicar:

- Checklist por fases, nao formulario gigante.
- Separar setup basico, organizacao, teste e lancamento.
- Produtos no Shopify exigem poucos campos obrigatorios no inicio, como titulo e preco; o resto pode ser enriquecido depois.
- A propria Shopify recomenda testar o fluxo antes de abrir ao publico.

Traducao para RepairDesk:

- Setup basico: dados da loja.
- Operacao: cliente + reparacao.
- Teste: reparacao demo e portal cliente.
- Lancamento: convidar funcionario e usar numa reparacao real.

### 2.3 Intercom - checklist visivel e tours acionaveis

Fonte: Intercom Help, "Product Tours explained":  
https://www.intercom.com/help/en/articles/2900885-product-tours-explained

Principios a aplicar:

- Tours multi-pagina para onboarding.
- Pointers ligados a elementos reais da interface.
- Tours podem ser partilhados por link em email/mensagem para reativar utilizadores.
- O utilizador deve avancar clicando ou fazendo a acao, nao apenas carregando em "seguinte".

Traducao para RepairDesk:

- Tour nao deve aparecer logo no primeiro segundo. Primeiro wizard; depois tooltips contextuais no dashboard.
- Cada tooltip deve apontar para uma decisao ou acao real.
- O tour deve poder ser relancado por link no email/WhatsApp de rescue.

### 2.4 Notion - zero-friction start e empty states com templates

Fontes:

- Notion, "Creating a page": https://www.notion.com/help/guides/creating-a-page
- Notion, "Start with a template": https://www.notion.com/en-gb/help/start-with-a-template

Principios a aplicar:

- Comecar com uma superficie vazia mas pronta a criar, nao com configuracao pesada.
- Starter templates escolhidos pelo que o utilizador disse no onboarding.
- Empty state nao deve dizer so "sem dados"; deve oferecer um exemplo ou proximo passo.

Traducao para RepairDesk:

- Reparacoes vazias: mostrar "Criar reparacao demo" e "Criar reparacao real".
- Dashboard vazio: mostrar uma reparacao exemplo ate existir a primeira real.
- Clientes vazios: permitir criar "Cliente exemplo" com um clique ou inserir cliente real em 3 campos.

---

## 3. Decisao UX: demo vs real

Pergunta critica: no Passo 2 deve ser primeiro cliente ficticio ou real?

Resposta: oferecer os dois, mas recomendar real.

Copy:

> Tens uma reparacao em maos? Usa um cliente real. Se so queres testar, criamos uma demo que podes apagar depois.

Comportamento:

- Opcao primaria: "Usar cliente real"
- Opcao secundaria: "Criar demo"
- Opcao terciaria: "Saltar por agora"

Por defeito, se o utilizador escolher demo:

- Cliente: "Cliente Demo"
- Telefone: vazio
- NIF: vazio
- Notas: "Criado automaticamente pelo onboarding. Pode ser apagado."

Reparacao demo:

- Equipamento: "iPhone 11"
- Avaria: "Ecra partido"
- IMEI: vazio
- Orcamento: 89,00 EUR
- Estado inicial: "Recebido"
- Notas: "Demo para aprender o fluxo. Nao conta para metricas reais."

Requisito tecnico:

- Todos os objetos demo devem ter `isDemo = true` ou equivalente.
- Dashboard e metricas financeiras devem excluir demo por defeito.
- Banner permanente na reparacao demo: "Isto e uma demo. Podes apagar quando quiseres."

---

## 4. Wizard ideal

### Visao geral

URL proposta: `/onboarding`  
Entrada: apos primeiro login de owner/admin quando `onboarding.completedAt == null`.  
Saida: dashboard com checklist persistente.

Passos:

1. Dados da empresa
2. Primeiro cliente
3. Primeira reparacao
4. Explorar dashboard
5. Convidar funcionario

Footer fixo em todos os passos:

- Progresso: "Passo X de 5"
- Botao principal contextual
- Botao "Saltar por agora" quando permitido
- Link discreto: "Guardar e sair"

Obrigatorio para completar wizard:

- Passo 1: nome comercial ou legal da loja.
- Passo 2: cliente real ou demo.
- Passo 3: reparacao real ou demo.

Skipavel:

- Logo, NIF, IBAN, CAE, morada completa.
- Convidar funcionario.
- Tour dashboard.

Nao pedir no wizard:

- Cartao de credito.
- Integracao faturacao.
- Stock completo.
- WhatsApp Business API.
- Termos e condicoes longos.
- Configuracao de estados.

---

## 5. Passo a passo com mockups

### Passo 1 - Dados da empresa

Objetivo: personalizar documentos e dar confianca, sem bloquear.

O que mostrar:

- Preview pequeno de um cabecalho de orcamento com logo/nome/NIF.
- Explicacao curta: "Isto aparece nos orcamentos, fichas e mensagens."

O que pedir:

- Nome da loja: obrigatorio, pre-preenchido com tenant name.
- Nome legal: opcional.
- NIF: opcional, validacao PT se preenchido.
- IBAN: opcional, validacao basica PT50 se preenchido.
- Logo: opcional.

Mockup:

```text
+---------------------------------------------------------------+
| RepairDesk                         Passo 1 de 5  [====.....] |
+---------------------------------------------------------------+
| Dados da empresa                                            X |
|                                                               |
| Isto vai aparecer nos orcamentos e fichas de reparacao.       |
|                                                               |
| Nome da loja *                                                |
| [ LopesTech                                                ]  |
|                                                               |
| Nome legal                                                    |
| [ Bruno Miguel Lopes                                      ]   |
|                                                               |
| NIF                         IBAN                              |
| [ 263758141              ]  [ PT50 ....                    ]  |
|                                                               |
| Logo                                                          |
| [ + Carregar logo ]        Preview do documento               |
|                            +----------------------------+     |
|                            | LopesTech                  |     |
|                            | NIF 263758141              |     |
|                            | Orcamento #001             |     |
|                            +----------------------------+     |
|                                                               |
| [Guardar e continuar]                         [Saltar detalhes]|
+---------------------------------------------------------------+
```

Validacao:

- Nome da loja: 2-80 caracteres.
- NIF: se preenchido, 9 digitos e checksum PT se existir helper.
- IBAN: se preenchido, normalizar espacos e validar formato.
- Logo: PNG/JPG/WebP, max 2 MB.

Feedback de progresso:

- Ao preencher nome: "Dados essenciais completos."
- Ao adicionar NIF/IBAN/logo: check pequeno ao lado de cada campo.

### Passo 2 - Primeiro cliente

Objetivo: criar a entidade necessaria para a reparacao.

O que mostrar:

- Escolha clara: cliente real ou demo.
- Formulario curto.
- Mensagem anti-friccao: telefone opcional porque ha clientes do Messenger/Instagram.

O que pedir:

- Nome: obrigatorio.
- Telefone: opcional.
- NIF: opcional.
- Email: opcional.

Mockup:

```text
+---------------------------------------------------------------+
| RepairDesk                         Passo 2 de 5  [======...] |
+---------------------------------------------------------------+
| Primeiro cliente                                             |
|                                                               |
| Tens uma reparacao em maos? Usa um cliente real.              |
| So queres testar? Criamos uma demo que podes apagar.          |
|                                                               |
| (o) Cliente real        ( ) Criar demo                         |
|                                                               |
| Nome *                                                        |
| [ Ana Martins                                             ]   |
|                                                               |
| Telefone                           NIF                         |
| [ 912 345 678                  ]   [                       ]   |
|                                                               |
| Email                                                         |
| [                                                           ]  |
|                                                               |
| [Criar cliente e continuar]                    [Criar demo]   |
+---------------------------------------------------------------+
```

Validacao:

- Nome: obrigatorio.
- Telefone: aceitar vazio; se preenchido, normalizar espacos.
- NIF/email: validar so se preenchidos.

Feedback de progresso:

- Toast: "Cliente criado. Agora vamos registar a reparacao."
- Checklist lateral: "Cliente: completo".

### Passo 3 - Primeira reparacao

Objetivo: activation real. Este e o passo mais importante.

O que mostrar:

- Formulario igual ao produto real, mas simplificado.
- Estado inicial predefinido: "Recebido".
- Preview do ticket.
- Botao para usar demo se ainda nao houver dados reais.

O que pedir:

- Cliente: pre-selecionado do passo 2.
- Equipamento: obrigatorio.
- Avaria: obrigatorio.
- IMEI/serial: opcional.
- Orcamento estimado: opcional.
- Notas internas: opcional.

Mockup:

```text
+---------------------------------------------------------------+
| RepairDesk                         Passo 3 de 5  [========.] |
+---------------------------------------------------------------+
| Primeira reparacao                                           |
|                                                               |
| Cliente                                                       |
| [ Ana Martins                                           v ]   |
|                                                               |
| Equipamento *                     IMEI/Serial                 |
| [ iPhone 12                    ]  [                        ]  |
|                                                               |
| Avaria *                                                      |
| [ Ecra partido e touch falha                              ]   |
|                                                               |
| Orcamento estimado                                            |
| [ 89,00 EUR                                                ]  |
|                                                               |
| Estado inicial                                                |
| [ Recebido                                                v ] |
|                                                               |
| Preview                                                       |
| +---------------------------------------------------------+   |
| | Reparacao #001 - Recebido                              |   |
| | Ana Martins - iPhone 12 - Ecra partido                 |   |
| +---------------------------------------------------------+   |
|                                                               |
| [Criar reparacao]                         [Usar reparacao demo]|
+---------------------------------------------------------------+
```

Validacao:

- ClienteId: obrigatorio.
- Equipamento: 2-120 caracteres.
- Avaria: 3-500 caracteres.
- Orcamento: aceitar vazio; se preenchido, converter para cents.
- IMEI: se tiver 15 digitos, validar Luhn quando existir; se falhar, aviso nao bloqueante.

Feedback de progresso:

- Apos criar: ecran de sucesso curto, nao modal pesado.
- Copy: "Reparacao #001 criada. Este e o momento em que a loja deixa de estar vazia."

### Passo 4 - Explorar dashboard

Objetivo: mostrar onde o trabalho vive depois de criado.

O que mostrar:

- Dashboard real com overlay leve.
- Card "Em curso" com a reparacao criada.
- Card "Receita pendente" se houver orcamento.
- Link para abrir reparacao.

O que pedir:

- Nada obrigatorio.
- Apenas acao recomendada: abrir reparacao ou concluir mini-tour.

Mockup:

```text
+---------------------------------------------------------------+
| RepairDesk                         Passo 4 de 5  [==========]|
+---------------------------------------------------------------+
| Dashboard                                                     |
|                                                               |
| +----------------+ +----------------+ +--------------------+  |
| | Em curso       | | Receita pend.  | | Lucro realizado    |  |
| | 1 reparacao    | | 89,00 EUR      | | 0,00 EUR           |  |
| +----------------+ +----------------+ +--------------------+  |
|                                                               |
| Reparacoes que precisam de atencao                            |
| +---------------------------------------------------------+   |
| | #001 iPhone 12 - Ana Martins - Recebido       [Abrir]  |   |
| +---------------------------------------------------------+   |
|                                                               |
|       ^                                                       |
|       | Aqui aparecem as reparacoes que ainda precisam de ti. |
|                                                               |
| [Ver reparacao]                              [Continuar]      |
+---------------------------------------------------------------+
```

Validacao:

- Passo completa quando o utilizador clica em "Ver reparacao", "Continuar" ou fecha o overlay.

Feedback de progresso:

- Checklist: "Dashboard visto".
- Microcopy: "Quando voltares ao RepairDesk, este e o teu ponto de partida."

### Passo 5 - Convidar funcionario

Objetivo: preparar lojas com mais de uma pessoa, sem castigar solo owners.

O que mostrar:

- Pergunta simples: "Trabalhas sozinho ou com equipa?"
- Se sozinho: marcar passo como completo com "Podes convidar alguem mais tarde."
- Se equipa: email/role.

O que pedir:

- Email do funcionario.
- Role: Tecnico ou Admin.

Mockup:

```text
+---------------------------------------------------------------+
| RepairDesk                         Passo 5 de 5  [==========]|
+---------------------------------------------------------------+
| Equipa                                                        |
|                                                               |
| Trabalhas sozinho ou queres convidar alguem?                  |
|                                                               |
| [ Trabalho sozinho ]       [ Convidar funcionario ]           |
|                                                               |
| Email                                                         |
| [ tecnico@loja.pt                                         ]   |
|                                                               |
| Permissao                                                     |
| (o) Tecnico - ve reparacoes e atualiza estados                 |
| ( ) Admin - gere loja, clientes e definicoes                   |
|                                                               |
| [Enviar convite]                         [Fazer isto depois]  |
+---------------------------------------------------------------+
```

Validacao:

- Se escolher "Trabalho sozinho": sem input.
- Se convidar: email valido e role obrigatoria.

Feedback de progresso:

- Sucesso: "Convite enviado. A loja esta pronta para usar."
- Skip: "Sem problema. Podes convidar pessoas em Definicoes."

---

## 6. Tabela de implementacao

| Passo | UI | Input minimo | Validacao | Proximo |
|---|---|---|---|---|
| 1. Dados empresa | Form com preview de documento | Nome da loja | Nome obrigatorio; NIF/IBAN/logo so validam se preenchidos | Criar/atualizar tenant settings |
| 2. Primeiro cliente | Escolha real/demo + form curto | Nome ou demo | Nome obrigatorio; telefone/email/NIF opcionais | Criar cliente e guardar `onboarding.firstClienteId` |
| 3. Primeira reparacao | Form de reparacao simplificado | Cliente, equipamento, avaria | Cliente/equipamento/avaria obrigatorios; orcamento opcional | Criar reparacao e guardar `onboarding.firstReparacaoId` |
| 4. Dashboard | Dashboard real com overlay | Nenhum | Completa ao interagir ou continuar | Marcar `dashboardSeenAt` |
| 5. Funcionario | Escolha solo/equipa | Nenhum se solo; email+role se equipa | Email valido se convite | Marcar `completedAt` |

---

## 7. Checklist persistente pos-wizard

Aparece no topo/lateral do dashboard ate completar ou fechar por 14 dias.

Titulo:

> Arranque da loja

Subtitulo:

> Falta pouco para o RepairDesk ficar pronto para o dia-a-dia.

Items:

1. Dados da loja completos
2. Primeiro cliente criado
3. Primeira reparacao criada
4. Ver portal do cliente
5. Gerar PDF/orcamento
6. Convidar funcionario
7. Criar primeira despesa ligada a reparacao

Regras:

- Itens 1-3 ficam completos pelo wizard.
- Itens 4-7 podem ficar para depois.
- Cada item tem um botao de acao, nao uma explicacao longa.
- O utilizador pode minimizar a checklist, mas nao deve desaparecer ate completar ou escolher "Nao mostrar durante 7 dias".

Mockup dashboard:

```text
+---------------------------------------------------------------+
| Dashboard                                                     |
|                                                               |
| +---------------------------------------------------------+   |
| | Arranque da loja                                  3/7   |   |
| | [x] Dados da loja completos                             |   |
| | [x] Primeiro cliente criado                             |   |
| | [x] Primeira reparacao criada                           |   |
| | [ ] Ver portal do cliente             [Abrir portal]    |   |
| | [ ] Gerar PDF/orcamento               [Gerar]           |   |
| | [ ] Convidar funcionario              [Convidar]        |   |
| | [ ] Criar primeira despesa            [Adicionar]       |   |
| +---------------------------------------------------------+   |
|                                                               |
| KPIs...                                                       |
+---------------------------------------------------------------+
```

---

## 8. Tour interactivo e tooltips contextuais

Recomendacao: usar tour proprio simples ou biblioteca estilo Intro.js/React Joyride. Para React, preferir `react-joyride` se o bundle e styling forem aceitaveis. Se o design ficar intrusivo, implementar overlay proprio com 6 passos.

O tour nao substitui o wizard. Ele ensina a interface depois de haver dados.

### Tooltips exactos

| Target | Quando aparece | Texto exacto | CTA |
|---|---|---|---|
| Card "Em curso" no dashboard | Primeiro acesso ao dashboard pos-wizard | "Aqui aparecem as reparacoes que ainda precisam de atencao. Se o balcao estiver cheio, comeca por aqui." | "Percebi" |
| Card "Receita pendente" | Se existir reparacao com preco/orcamento nao pago | "Tudo o que ja foi entregue ou esta pronto mas ainda nao foi pago aparece aqui. Isto evita dinheiro esquecido." | "Ver pendentes" |
| Botao "Nova reparacao" | Primeira visita a `/reparacoes` sem tour feito | "Este e o botao mais importante. Cliente entrou? Cria a reparacao antes de pousar o telemovel na bancada." | "Criar uma" |
| Pesquisa em reparacoes | Apos existirem 3+ reparacoes | "Pesquisa por cliente, equipamento ou IMEI. A meta e encontrares qualquer reparacao em menos de 5 segundos." | "Ok" |
| Estado da reparacao | Ao abrir primeira reparacao | "Muda o estado sempre que a reparacao avancar. O dashboard e o portal do cliente dependem disto." | "Ok" |
| Botao WhatsApp | Quando cliente tem telefone | "Usa isto para falar com o cliente sem copiar texto a mao. Mais tarde estes templates podem ser automaticos." | "Abrir WhatsApp" |
| Portal publico `/r/{slug}` | Quando clica "Ver portal cliente" | "Este e o link que o cliente pode abrir no telemovel para ver o estado sem te ligar." | "Copiar link" |
| Despesas imputadas | Ao abrir secao de despesas na reparacao | "Liga pecas e custos a reparacao certa. Assim o lucro deixa de ser adivinhado." | "Adicionar despesa" |
| Definicoes da loja | Primeira ida a `/definicoes` | "Aqui ficam logo, NIF, IBAN, termos e dados que aparecem nos documentos." | "Editar" |

Regras dos tooltips:

- Maximo 2 linhas de texto.
- Nunca bloquear uma acao critica.
- Nao repetir depois de `seenAt`.
- Botao "Saltar tour" sempre visivel.
- Reabrir em Ajuda: "Ver tour inicial".

---

## 9. Empty states que ensinam

### Dashboard sem dados

Nao mostrar:

> Sem dados.

Mostrar:

```text
Ainda nao ha reparacoes.
Cria uma reparacao real ou usa uma demo para ver como o RepairDesk funciona.

[Criar reparacao real] [Criar demo]
```

### Reparacoes vazias

```text
O balcao ainda esta vazio.
Quando um cliente entra, cria aqui a ficha antes de comecar a mexer no equipamento.

[Nova reparacao]
```

### Clientes vazios

```text
Ainda nao ha clientes.
Podes criar so com o nome. Telefone, NIF e email ficam para depois.

[Novo cliente]
```

### Despesas vazias numa reparacao

```text
Sem pecas ou custos ligados a esta reparacao.
Quando comprares um ecra, bateria ou material, liga a despesa aqui para saberes o lucro real.

[Adicionar despesa]
```

### Definicoes incompletas

```text
Os documentos ainda estao incompletos.
Adiciona NIF, IBAN e logo para os orcamentos parecerem profissionais.

[Completar dados]
```

---

## 10. Metricas a medir

### Eventos principais

| Evento | Propriedades | Quando dispara |
|---|---|---|
| `onboarding_started` | tenantId, userId, createdAt | Primeiro acesso ao wizard |
| `onboarding_step_viewed` | step, stepName, elapsedSeconds | Ao entrar em cada passo |
| `onboarding_step_completed` | step, skipped, usedDemo, elapsedSeconds | Ao completar/saltar passo |
| `onboarding_abandoned` | lastStep, elapsedSeconds | Sessao termina sem continuar apos 30 min ou logout |
| `tenant_settings_min_completed` | hasName, hasNif, hasIban, hasLogo | Apos passo 1 |
| `first_cliente_created` | isDemo, hasPhone, hasNif | Apos passo 2 |
| `first_reparacao_created` | isDemo, hasImei, hasBudget, status | Apos passo 3 |
| `dashboard_seen_after_onboarding` | hasChecklistVisible | Passo 4 |
| `employee_invite_sent` | role | Passo 5 |
| `onboarding_completed` | totalSeconds, usedDemo, invitedEmployee | Fim do wizard |
| `activation_real_repair_created` | secondsFromSignup | Primeira reparacao nao-demo |
| `activation_real_repair_updated` | statusFrom, statusTo | Primeira mudanca de estado nao-demo |
| `activation_pdf_generated` | documentType | Primeiro PDF/orcamento |
| `activation_public_portal_opened` | source | Primeira abertura de portal publico |

### KPIs de produto

| Metrica | Definicao | Meta beta |
|---|---|---|
| Tempo ate primeira reparacao | Signup -> primeira reparacao real ou demo | P50 < 20 min |
| Tempo ate primeira reparacao real | Signup -> primeira reparacao `isDemo=false` | P50 < 48h nas lojas beta |
| Abandono por passo | % que entra no passo e nao completa | Nenhum passo > 25% |
| Activation rate A | % cria cliente + reparacao demo/real | > 80% |
| Activation rate B | % cria reparacao real em 7 dias | > 60% beta |
| Completion rate wizard | % completa 5 passos | > 70% |
| Checklist completion | % completa 5/7 itens em 14 dias | > 50% |
| Time to second session | Signup -> segunda visita | < 3 dias |
| Rescue recovery | Inativos 7 dias que voltam apos mensagem | > 20% |

### Instrumentacao necessaria

Backend:

- Tabela `OnboardingProgress` por tenant:
  - `TenantId`
  - `StartedAt`
  - `CurrentStep`
  - `CompletedAt`
  - `SkippedAt`
  - `FirstClienteId`
  - `FirstReparacaoId`
  - `UsedDemoData`
  - `DashboardSeenAt`
  - `EmployeeInviteSeenAt`
  - `ChecklistDismissedUntil`
- Tabela/event stream `ProductEvents`:
  - `Id`
  - `TenantId`
  - `UserId`
  - `EventName`
  - `OccurredAt`
  - `PropertiesJson`
  - `SessionId`

Frontend:

- Helper `track(eventName, properties)`.
- `sessionId` gerado por tab/sessao.
- Eventos enviados em background; se falhar, nao bloquear UX.
- Ambiente dev pode logar no console.

Privacidade:

- Nao enviar nomes de clientes, telefones, NIFs, IMEIs ou emails em eventos.
- Propriedades devem ser booleanas/contagens/status, nao dados pessoais.

---

## 11. Estrategia de rescue - loja inactiva 7 dias

Definicao de inativa:

- Criou conta.
- Nao tem `activation_real_repair_created`.
- Nao fez login nos ultimos 7 dias.
- Nao pediu para apagar conta.

Sequencia recomendada:

### Dia 1 apos signup incompleto

Email automatico curto.

Assunto:

> A tua primeira reparacao no RepairDesk fica pronta em 5 minutos

Corpo:

```text
Ola {primeiro_nome},

Vi que ainda nao terminaste o arranque do RepairDesk.

O caminho mais rapido e:
1. Criar um cliente
2. Criar uma reparacao
3. Ver essa reparacao no dashboard

Podes continuar aqui:
{resume_link}

Se estiveres so a testar, usa a demo. Da para apagar depois.

Bruno
```

### Dia 3 sem primeira reparacao

WhatsApp se houver consentimento/contacto; caso contrario email.

```text
Ola {primeiro_nome}, daqui e o Bruno da RepairDesk.

Ainda nao criaste a primeira reparacao. Queres que te mande um video de 90 segundos com o fluxo completo?

Link direto para continuar:
{resume_link}
```

Botao/link:

- "Ver video 90s"
- "Continuar onboarding"

Video:

- Maximo 90 segundos.
- Conteudo: criar cliente -> criar reparacao -> mudar estado -> ver dashboard.
- Sem intro longa, sem musica, sem marketing.

### Dia 7 inativo

Email de rescue com escolha clara.

Assunto:

> Vale a pena guardar a tua conta RepairDesk?

Corpo:

```text
Ola {primeiro_nome},

Parece que o RepairDesk ainda nao entrou no teu dia-a-dia.

Se foi falta de tempo, podes continuar pelo ponto onde ficaste:
{resume_link}

Se alguma coisa bloqueou, responde a este email com uma frase. Eu leio.

Se ja nao fizer sentido, tambem esta tudo bem. A conta fica parada e nao ha cobrancas surpresa.

Bruno
```

### Dia 14 sem retorno

- Pausar emails de onboarding.
- Manter conta.
- Marcar `onboarding_rescue_status = dormant`.
- Se for loja beta amiga, Bruno pode contactar manualmente uma unica vez.

Nao fazer:

- Nao mandar 6 emails.
- Nao fingir urgencia falsa.
- Nao ameacar apagar dados.
- Nao pedir reuniao obrigatoria.

---

## 12. Copy PT-PT essencial

Botao principal:

- "Guardar e continuar"
- "Criar cliente e continuar"
- "Criar reparacao"
- "Ver reparacao"
- "Terminar arranque"

Botoes secundarios:

- "Saltar por agora"
- "Criar demo"
- "Fazer isto depois"
- "Guardar e sair"

Mensagens:

- "Podes alterar isto mais tarde em Definicoes."
- "Telefone e opcional. Alguns clientes chegam pelo Messenger ou Instagram."
- "A demo nao entra nas tuas metricas reais."
- "Este passo ajuda os documentos a parecerem profissionais, mas nao bloqueia o uso."
- "Conseguiste: a primeira reparacao esta registada."

Evitar:

- "Configure a sua organizacao"
- "Complete o perfil fiscal"
- "Parametros avancados"
- "Sem dados"
- "Obrigatorio para continuar" em campos que nao sao realmente obrigatorios.

---

## 13. Estados tecnicos e permissoes

Regras de exibicao:

- Mostrar wizard apenas a owner/admin.
- Tecnico convidado nao ve wizard completo; ve mini-tour "Como atualizar uma reparacao".
- Se wizard for fechado, mostrar checklist no dashboard.
- Se tenant ja tem reparacao real antes do wizard existir, marcar passos 2 e 3 como completos.

Rotas propostas:

- `/onboarding`
- `/onboarding?step=empresa`
- `/onboarding?step=cliente`
- `/onboarding?step=reparacao`
- `/onboarding?step=dashboard`
- `/onboarding?step=equipa`

APIs propostas:

- `GET /api/onboarding/me`
- `PUT /api/onboarding/me/step`
- `POST /api/onboarding/demo-data`
- `POST /api/onboarding/complete`
- `POST /api/events`

Campos a adicionar mais tarde:

- `Cliente.IsDemo`
- `Reparacao.IsDemo`
- `Tenant.OnboardingCompletedAt` ou tabela separada `OnboardingProgress`

Recomendacao:

Usar tabela separada. Evita poluir `Tenants` com muitos timestamps e permite evoluir checklist.

---

## 14. Criterios de aceitacao

Para considerar pronto para beta:

- Uma loja nova consegue completar wizard em mobile e desktop.
- Da para criar reparacao real sem preencher NIF, IBAN, IMEI ou email.
- Demo data e claramente marcada e excluida das metricas.
- Se fechar browser a meio, volta ao passo certo.
- Checklist aparece no dashboard depois do wizard.
- Cada passo emite analytics sem dados pessoais.
- Empty states nunca dizem apenas "sem dados".
- Copy esta em PT-PT.
- Nenhum video/tutorial usado no fluxo tem mais de 2 minutos.
- Bruno consegue mostrar este documento a uma loja beta e dizer: "E assim que vai correr."

---

## 15. Ordem recomendada de implementacao

1. Criar modelo `OnboardingProgress` + endpoints basicos.
2. Criar rota `/onboarding` com stepper e persistencia.
3. Integrar Passo 1 com `tenantSettingsApi`.
4. Integrar Passo 2 com `clientesApi`.
5. Integrar Passo 3 com `reparacoesApi`.
6. Adicionar flags demo e exclusao de metricas.
7. Adicionar checklist persistente no dashboard.
8. Adicionar eventos de analytics.
9. Adicionar tooltips contextuais.
10. Criar emails/WhatsApp de rescue.

Prioridade real:

- Primeiro wizard funcional.
- Depois checklist.
- Depois analytics.
- Depois tours.
- Depois rescue automation.

Sem analytics, a beta fica cega. Sem wizard, a beta perde lojas. Sem tour, ainda e aceitavel.
