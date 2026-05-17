# WhatsApp Templates RepairDesk

Última atualização: 2026-05-16

Objetivo: dar ao técnico mensagens WhatsApp prontas a usar por estado da reparação, em PT-PT, com tom humano e campos de substituição claros. A ideia não é "automatizar simpatia"; é poupar escrita repetitiva sem tirar naturalidade ao contacto.

---

## Campos de substituição

| Campo | Uso recomendado |
|---|---|
| `{{cliente_nome}}` | Primeiro nome ou nome da entidade. Ex.: `Bruno`, `Junta de Freguesia de X`. |
| `{{equipamento}}` | Categoria + modelo quando existir. Ex.: `telemóvel iPhone 12`, `computador Lenovo`, `tablet Samsung`. |
| `{{marca_modelo}}` | Marca/modelo sem categoria. Ex.: `iPhone 12`, `Lenovo ThinkPad T480`. |
| `{{numero_reparacao}}` | Número interno da reparação. Usar sobretudo em clientes profissionais. |
| `{{valor}}` | Valor total do orçamento. Ex.: `89,90 EUR`. |
| `{{loja_nome}}` | Nome público da loja/oficina. Ex.: `LopesTech`. |
| `{{morada_loja}}` | Morada de levantamento, se houver mais do que uma loja. |
| `{{horario_loja}}` | Horário simples. Ex.: `segunda a sexta, 9h-18h`. |
| `{{link_aprovacao}}` | Link curto para aprovar orçamento, quando existir. |
| `{{link_review_google}}` | Link direto para avaliação no Google. Usar apenas com opt-in adequado. |
| `{{prazo_estimado}}` | Prazo honesto. Ex.: `2 a 3 dias úteis`, `até sexta-feira`. |
| `{{peca_nome}}` | Nome simples da peça. Ex.: `ecrã`, `bateria`, `conector de carga`. |
| `{{data_pronto}}` | Data em que ficou pronto. Ex.: `2026-05-16`. |

Regra de produto: se um campo essencial não existir, o RepairDesk deve esconder essa frase ou sugerir uma alternativa curta. Nunca inventar valores, prazos ou peças.

---

## Variações por categoria

Os templates principais usam `{{equipamento}}`. A variação por categoria deve entrar na forma como o campo é preenchido e, quando fizer sentido, numa frase curta opcional de diagnóstico ou levantamento.

| Categoria | `{{equipamento}}` recomendado | Nota opcional em diagnóstico | Nota opcional em pronto/levantamento |
|---|---|---|---|
| Telemóvel | `telemóvel {{marca_modelo}}` | `Vamos confirmar ecrã/touch, carregamento, bateria, câmaras e rede.` | `Ao levantar, convém testar chamadas, carregamento e desbloqueio.` |
| Computador | `computador {{marca_modelo}}` ou `portátil {{marca_modelo}}` | `Vamos verificar arranque, disco/SSD, memória, temperaturas e sistema.` | `Ao levantar, convém confirmar arranque, Wi-Fi, teclado e ficheiros principais.` |
| Tablet | `tablet {{marca_modelo}}` | `Vamos testar ecrã/touch, carregamento, bateria e Wi-Fi.` | `Ao levantar, convém testar touch, carregamento e conta associada.` |

Exemplo: o template "o teu `{{equipamento}}` está em diagnóstico" fica "o teu telemóvel iPhone 12 está em diagnóstico", "o teu computador Lenovo está em diagnóstico" ou "o teu tablet Samsung está em diagnóstico".

---

## Templates por estado

| Estado | Template padrão | Template informal | Template profissional |
|---|---|---|---|
| Orcamento | Olá `{{cliente_nome}}`, já temos o orçamento para o teu `{{equipamento}}`: `{{valor}}`. Se estiver tudo bem para ti, responde a esta mensagem com "Aprovo" ou usa `{{link_aprovacao}}` para avançarmos. | Olá `{{cliente_nome}}`, já vimos o `{{equipamento}}` e a reparação fica por `{{valor}}`. Diz-nos se queres avançar; basta responderes por aqui. | Olá `{{cliente_nome}}`, segue a estimativa para o `{{equipamento}}`: `{{valor}}`. Para autorizar a intervenção, por favor responda a esta mensagem ou aprove em `{{link_aprovacao}}`. |
| Recebido | Olá `{{cliente_nome}}`, confirmamos a entrada do teu `{{equipamento}}` na `{{loja_nome}}`. Vamos registar tudo e começar a análise; assim que houver novidades falamos contigo por aqui. | Olá `{{cliente_nome}}`, o teu `{{equipamento}}` já ficou connosco. Vamos tratar da análise e damos notícias assim que houver algo concreto. | Olá `{{cliente_nome}}`, confirmamos a receção do `{{equipamento}}` na `{{loja_nome}}`, com o processo `{{numero_reparacao}}`. Assim que a análise estiver concluída, enviaremos atualização por este contacto. |
| Diagnostico | Olá `{{cliente_nome}}`, o teu `{{equipamento}}` está em diagnóstico. Estamos a testar com cuidado para perceber a origem do problema e voltamos a contactar assim que tivermos uma conclusão. | Olá `{{cliente_nome}}`, estamos agora a ver o que se passa com o `{{equipamento}}`. Mal saibamos ao certo, mandamos mensagem. | Olá `{{cliente_nome}}`, o `{{equipamento}}` encontra-se em análise técnica. Enviaremos o diagnóstico e os próximos passos assim que os testes estiverem concluídos. |
| AguardaPeca | Olá `{{cliente_nome}}`, a reparação do teu `{{equipamento}}` está a aguardar a chegada da peça `{{peca_nome}}`. A previsão atual é `{{prazo_estimado}}`; avisamos-te assim que chegar. | Olá `{{cliente_nome}}`, já pedimos a peça para o `{{equipamento}}`. Agora é aguardar a chegada; contamos ter novidades por volta de `{{prazo_estimado}}`. | Olá `{{cliente_nome}}`, a intervenção no `{{equipamento}}` está pendente da peça `{{peca_nome}}`, já encomendada. A previsão de chegada é `{{prazo_estimado}}` e enviaremos nova atualização quando houver novidades. |
| EmReparacao | Olá `{{cliente_nome}}`, começámos a reparação do teu `{{equipamento}}`. Se tudo correr dentro do previsto, voltamos a falar contigo até `{{prazo_estimado}}`. | Olá `{{cliente_nome}}`, o `{{equipamento}}` já está em bancada. Assim que estiver pronto, ou se surgir alguma coisa fora do previsto, dizemos-te. | Olá `{{cliente_nome}}`, informamos que a reparação do `{{equipamento}}` já está em curso. Daremos nova atualização até `{{prazo_estimado}}`, ou antes se houver alguma alteração relevante. |
| Pronto | Olá `{{cliente_nome}}`, o teu `{{equipamento}}` já está pronto para levantamento na `{{loja_nome}}`. Podes passar quando der jeito dentro do nosso horário: `{{horario_loja}}`. | Olá `{{cliente_nome}}`, já temos o `{{equipamento}}` pronto. Quando conseguires, podes passar na `{{loja_nome}}` para levantar. | Olá `{{cliente_nome}}`, o `{{equipamento}}` encontra-se reparado e disponível para levantamento na `{{loja_nome}}`. O horário de atendimento é `{{horario_loja}}`. |
| Entregue | Olá `{{cliente_nome}}`, obrigado por teres confiado em nós para tratar do teu `{{equipamento}}`. Se notares alguma coisa estranha nos próximos dias, responde por aqui. | Olá `{{cliente_nome}}`, obrigado pela confiança. Qualquer coisa com o `{{equipamento}}`, manda mensagem por aqui e vemos isso contigo. | Olá `{{cliente_nome}}`, agradecemos a confiança na `{{loja_nome}}`. Se precisar de apoio adicional relacionado com o `{{equipamento}}`, estamos disponíveis por este contacto. |
| Cancelado | Olá `{{cliente_nome}}`, confirmamos o cancelamento da reparação do teu `{{equipamento}}`. Quando quiseres, podes combinar connosco o levantamento ou os próximos passos. | Olá `{{cliente_nome}}`, fica então cancelada a reparação do `{{equipamento}}`. Diz-nos quando queres passar para levantar ou se precisas de mais alguma coisa. | Olá `{{cliente_nome}}`, confirmamos que a intervenção no `{{equipamento}}` foi cancelada. Por favor indique-nos quando pretende proceder ao levantamento ou se deseja algum esclarecimento adicional. |

---

## Casos especiais

| Caso | Quando usar | Template |
|---|---|---|
| Equipamento há mais de 14 dias na loja | Quando a reparação está parada, pendente de resposta ou esquecida, sem culpar o cliente. | Olá `{{cliente_nome}}`, o teu `{{equipamento}}` continua connosco há mais de 14 dias e está guardado em segurança. Queremos só confirmar contigo se queres manter o processo em aberto, combinar levantamento ou falar sobre o próximo passo. |
| Cliente recusou orçamento | 1 a 3 dias após recusa, se ainda não levantou o equipamento. | Olá `{{cliente_nome}}`, obrigado por nos teres dado resposta ao orçamento do `{{equipamento}}`. Só queríamos confirmar se pretendes levantar o equipamento ou se queres que o guardemos mais uns dias. |
| Pedido de Google Review | 5 dias após Entregue, apenas se não houve reclamação e se o cliente aceitou este tipo de contacto. | Olá `{{cliente_nome}}`, passaram alguns dias desde que levantaste o `{{equipamento}}`. Se ficou tudo bem, ajudava-nos muito deixares uma avaliação no Google: `{{link_review_google}}`. Obrigado pela confiança. |
| Lembrete de levantamento | 7 dias após Pronto, se o cliente ainda não levantou. | Olá `{{cliente_nome}}`, o teu `{{equipamento}}` está pronto para levantamento desde `{{data_pronto}}` e continua guardado na `{{loja_nome}}`. Quando puderes, passa dentro do horário `{{horario_loja}}` ou diz-nos se precisas de combinar outro momento. |

---

## Desculpas e exceções

| Situação | Template |
|---|---|
| Atrasou-se a peça | Olá `{{cliente_nome}}`, a peça `{{peca_nome}}` para o teu `{{equipamento}}` atrasou-se e ainda não chegou à loja. Lamentamos a demora; estamos a acompanhar a encomenda e voltamos a contactar-te assim que tivermos nova previsão. |
| Não conseguimos reparar | Olá `{{cliente_nome}}`, terminámos os testes ao `{{equipamento}}` e, com as condições atuais, não conseguimos fazer uma reparação segura e fiável. Podemos devolver o equipamento e explicar-te o que encontrámos quando passares pela `{{loja_nome}}`. |
| Orçamento precisa de revisão | Olá `{{cliente_nome}}`, durante a análise ao `{{equipamento}}` encontrámos um ponto que pode alterar o orçamento inicial. Antes de avançar, queremos explicar-te tudo com clareza; podes responder por aqui quando tiveres disponibilidade. |
| Prazo vai derrapar | Olá `{{cliente_nome}}`, a reparação do teu `{{equipamento}}` vai demorar mais do que o previsto. Preferimos avisar-te já: a nova previsão é `{{prazo_estimado}}`, e se mudar voltamos a contactar. |

---

## Defaults recomendados no produto

Usar **Template padrão** como default para clientes particulares. É o melhor equilíbrio: trata por "tu", soa próximo e continua profissional.

Usar **Template profissional** automaticamente quando o cliente tiver NIF de empresa, nome de entidade, contacto associado a Junta de Freguesia/autarquia, ou quando a reparação estiver marcada como B2B. Em Portugal, a variante profissional deve preferir "o seu/a sua" e verbos na 3.ª pessoa; usar "você" apenas se a loja já comunicar assim.

Usar **Template informal** apenas como sugestão para clientes habituais, clientes jovens ou contactos que a loja já trata com maior proximidade. Não deve ser o default global.

Automatização recomendada:

| Estado/evento | Modo recomendado |
|---|---|
| Recebido | Envio automático se houver opt-in de atualizações por WhatsApp. |
| Diagnostico | Sugestão automática; envio automático só se a loja quiser atualizações muito granulares. |
| Orcamento | Sugestão forte ou envio automático com link de aprovação, porque há CTA claro. |
| AguardaPeca | Envio automático se houver prazo ou peça definida. |
| EmReparacao | Sugestão no compositor; nem todos os clientes precisam desta notificação. |
| Pronto | Envio automático recomendado, com horário e loja. |
| Entregue | Envio automático curto de agradecimento; pedido de review apenas 5 dias depois e com regra própria. |
| Cancelado | Sugestão manual; contexto pode ser sensível. |
| Erros/desculpas | Sempre revisão manual antes de enviar. |

Primeira versão do produto: mostrar o template já preenchido dentro do compositor WhatsApp, com botão "Copiar/Enviar". Depois de validar com clientes reais, ativar envios automáticos por tenant e por estado.

---

## Notas RGPD e opt-in

Notas de produto, não aconselhamento jurídico. Antes de produção, validar com contabilista/jurista ou DPO quando existir.

1. Separar **atualizações transacionais** de **marketing/reviews**. Mensagens sobre estado da reparação podem enquadrar-se na execução do serviço/contrato, mas pedir review no Google já deve ter consentimento/opt-in separado ou, no mínimo, uma base legal bem documentada.
2. Recolher opt-in claro no momento de entrada do equipamento: `Aceito receber atualizações sobre esta reparação por WhatsApp.` Para reviews/marketing, usar checkbox separada.
3. Guardar prova do opt-in: data/hora, telefone, texto aceite, loja/tenant, utilizador que recolheu e origem do consentimento.
4. Permitir opt-out fácil: se o cliente responder `PARAR`, `STOP` ou pedir para não receber mensagens, o RepairDesk deve marcar o contacto como bloqueado para envios automáticos.
5. Minimizar dados pessoais nas mensagens. Evitar IMEI, passwords, códigos de desbloqueio, moradas completas ou detalhes técnicos sensíveis. Usar `{{numero_reparacao}}` quando for preciso identificar o processo.
6. Incluir WhatsApp/Meta e eventuais fornecedores de envio na política de privacidade/subcontratantes, se o produto usar API oficial ou intermediários.
7. Registar histórico de mensagens enviadas, mas com retenção limitada e exportável. O cliente deve conseguir exercer direitos RGPD sem depender de trabalho manual impossível.

Fontes de referência: [CNPD sobre consentimento](https://www.cnpd.pt/organizacoes/areas-tematicas/consentimento/), [CNPD sobre comunicações eletrónicas promocionais não solicitadas](https://www.cnpd.pt/comunicacao-publica/noticias/comunicacoes-promocionais-nao-solicitadas-e-cookies-destacados-em-conferencia-de-marketing-direto/) e [Orientações 05/2020 do EDPB sobre consentimento](https://www.edpb.europa.eu/our-work-tools/our-documents/guidelines/guidelines-052020-consent-under-regulation-2016679_en).

---

## Verificação com Bruno

Teste recomendado com 3 clientes reais antes de automatizar:

1. **Orçamento telemóvel**: enviar template padrão de `Orcamento` com `{{valor}}` e medir se o cliente percebe como aprovar.
2. **Pronto computador**: enviar template padrão de `Pronto` e confirmar se o cliente aparece sem perguntas extra sobre horário/local.
3. **Atraso de peça tablet**: enviar template de desculpa e perceber se soa honesto ou demasiado "empresa grande".

Pergunta de validação interna: "Eu, Bruno, mandaria isto a um cliente sem mexer?" Se a resposta for não, o template ainda está demasiado artificial.
