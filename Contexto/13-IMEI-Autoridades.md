# IMEI x Autoridades - decisao de produto

Atualizado: 2026-05-16  
Projeto: RepairDesk SaaS - oficinas de reparacao em Portugal  
Fundador: Bruno Lopes / LopesTech

> Research tecnico/legal para decisao de produto. Nao substitui parecer juridico, parecer RGPD/DPO, resposta escrita da PSP/GNR/PJ/MAI, nem proposta comercial da GSMA/CheckMEND. Onde o preco ou acesso nao e publico, fica marcado como `{{confirmar}}`.

## Resumo executivo

**Decisao recomendada: AVANCAR, mas faseado.**

Avancar ja com **Fase A - registo interno robusto**: IMEI obrigatorio em telemoveis, validacao Luhn, suporte a IMEI2/eSIM, historico interno por IMEI/serial, logs de auditoria e texto RGPD no recibo de entrada. Isto e barato, reduz erro operacional, melhora prova de boa-fe da loja e nao depende de terceiros.

**Nao avancar ja com promessa "ligado a autoridades".** Em Portugal nao foi encontrada API publica da PSP, GNR, PJ, MAI ou ANACOM para lojas consultarem equipamentos roubados por IMEI. A feature deve ser comunicada como "verificacao de identificadores e alertas de risco", nao como "consulta policial".

**Fase B deve ser piloto pago com CheckMEND/GSMA Device Check**, apenas para planos Pro/Enterprise ou lojas com volume suficiente. O preco publico CheckMEND para conta web em 2026 e USD 1,99/check unitario, USD 1,39/check em pacote 100, e USD 1,00/check em pacote 1000; API/corporate e contrato por volume/geografia/add-ons, sem preco publico. Preco GSMA Device Check direto: `{{confirmar}}`.

**Fase C deve ser institucional e lenta**: carta formal a PSP/GNR/PJ/SGMAI pedindo orientacao, nao acesso imediato. Objetivo realista: protocolo de cooperacao, fluxo de denuncia, wording aprovado, eventual projeto-piloto com lojas voluntarias. Horizonte: 6-18 meses, dependente de resposta publica.

### O que Bruno deve decidir agora

1. Implementar Fase A nos sprints 16-17.
2. Pedir proposta comercial CheckMEND corporate/API antes de escrever integracao.
3. Pedir parecer RGPD curto sobre IMEI + consulta externa + retencao de logs.
4. Contactar PSP/GNR/PJ/SGMAI com pedido formal de orientacao e nao com pedido vago de parceria.

## 1. Validacao tecnica

### 1.1 IMEI e Luhn

O IMEI e um identificador de 15 digitos para equipamentos moveis. A GSMA descreve o TAC como os primeiros 8 digitos do IMEI e refere que o IMEI completo tem 15 digitos; a GSMA tambem descreve a estrutura IMEI como TAC, numero de serie e digito de controlo. Fontes: GSMA TAC Allocation e GSMA IMEI Database.

Formato pratico:

| Campo | Tamanho | Descricao |
|---|---:|---|
| TAC | 8 digitos | Type Allocation Code; identifica fabricante/modelo. |
| Serial/SNR | 6 digitos | Numero individual atribuido pelo fabricante dentro do TAC. |
| Check digit | 1 digito | Digito final calculado por Luhn/mod 10 sobre os primeiros 14 digitos. |

Validacao recomendada no RepairDesk:

```text
Entrada aceite: apenas digitos, depois de remover espacos, hifens e labels como "IMEI:"
IMEI normal: 15 digitos e Luhn valido
IMEI incompleto/sem check digit: 14 digitos, marcar como "sem digito de controlo" e pedir confirmacao manual
IMEISV: 16 digitos, nao aplicar Luhn ao ultimo digito; tratar como variante tecnica e guardar em campo proprio se aparecer
```

Luhn confirma se o numero tem estrutura plausivel. **Nao prova que o equipamento existe, que pertence ao cliente, nem que nao foi clonado.** E uma validacao de formato, nao uma verificacao criminal.

### 1.2 IMEI, IMEI2, serial e eSIM

| Identificador | O que e | Onde aparece | Como usar no RepairDesk |
|---|---|---|---|
| IMEI | Identificador do modulo radio/celular. | `*#06#`, Definicoes, caixa, bandeja SIM em alguns modelos, etiqueta de reparacao. | Campo principal para telemoveis/tablets celulares. Obrigatorio em telemoveis. |
| IMEI2 | Segundo identificador em dual-SIM/dual-eSIM. | `*#06#` e Definicoes. | Guardar como campo separado. Consultar ambos em Fase B se o provider cobrar por consulta e o plano permitir. |
| Serial number | Identificador de fabricante do equipamento. | Definicoes, caixa, fatura, superficie do produto. | Obrigatorio quando nao ha IMEI: laptops, consolas, tablets Wi-Fi, acessorios caros. Em Apple, usar tambem para prova operacional, nao para Activation Lock via API. |
| eSIM | Perfil SIM digital; nao e o IMEI. | Definicoes de rede. | Nao guardar ICCID/eSIM salvo necessidade especifica. O eSIM usa o mesmo equipamento com IMEI/IMEI2. |
| EID | Identificador do chip eSIM. | Definicoes em equipamentos eSIM. | Evitar por defeito: mais sensivel, pouco util para reparacao e verificacao de roubo. |

### 1.3 Captura no balcao

Opcoes UX:

1. **Manual**: tecnico escreve IMEI/IMEI2. Simples, barato, sujeito a erro. Mitigar com mascara, grupos visuais e Luhn.
2. **Mostrar `*#06#` no telemovel**: em Android e iPhone, o codigo costuma mostrar IMEI(s); tambem existe em Definicoes. Nao assumir que funciona sempre em equipamento bloqueado, sem ecra ou morto.
3. **Foto + OCR**: fotografar caixa, bandeja SIM, ecran `*#06#` ou etiqueta. Bom para reduzir erro e guardar prova, mas tem custo RGPD/storage. Fase A pode preparar o modelo; a feature de foto deve alinhar com o plano de storage.
4. **Scanner de codigo de barras/QR**: util em lojas com volume e equipamentos em caixa. Normalizar output para extrair apenas o identificador.

Recomendacao de produto: comecar por manual + validacao + campo "fonte do IMEI" (`teclado *#06#`, `Definicoes`, `caixa`, `fatura`, `bandeja`, `outro`). Adicionar OCR so quando o storage de fotos estiver fechado.

## 2. Bases de dados disponiveis

### 2.1 Comparacao

| Fonte | Cobertura/qualidade | Acesso para RepairDesk | API/SDK | Preco 2026 | Latencia/SLA | Decisao |
|---|---|---|---|---|---|---|
| **GSMA Device Check** | Fonte de referencia do ecossistema movel; consulta estado no GSMA Device Registry/Block List. A GSMA diz que serve governos e empresas como retailers, repair centers, recyclers e seguradoras. | Comercial, via conta/contrato. KYC/compliance provavel. Contacto: `devicechecksupport@gsma.com`. | Ha servico online; API/termos comerciais a confirmar diretamente. | `{{confirmar}}` - nao encontrei preco publico direto GSMA. | GSMA/CTIA falam em dados "real-time"/consulta instantanea; SLA publico nao encontrado. | Melhor fonte global, mas preco/acesso ainda incertos. |
| **CheckMEND / Recipero** | Muito forte para recommerce. Agrega fontes globais, policia, seguradoras, retailers e redes; inclui dispositivos alem de telemoveis. | Conta web rapida; corporate/API por proposta. Mais realista para piloto. | Sim. A pagina corporate refere API para cenarios de alto volume e documentacao API. | Web publico: USD 1,99/check; USD 1,79 em 10; USD 1,39 em 100; USD 1,00 em 1000. API corporate: `{{confirmar}}`. | Site diz resultado instantaneo; API desenhada para high-volume/process-critical; SLA publico nao encontrado. | **Escolha Fase B**. Primeiro proposta corporate. |
| **CTIA Stolen Phone Checker (EUA)** | Public service dos EUA, powered by GSMA Device Check. | O proprio site indica que utilizadores comerciais devem aceder via GSMA Device Check. | Nao para SaaS PT. | Publico/consumer, mas nao apropriado para integracao comercial. | N/A | Nao usar em produto; util como referencia de modelo. |
| **Immobilise / NMPR (UK)** | Registo nacional britanico de propriedade, usado por policia; ligado ao ecossistema Recipero/CheckMEND. | Relevante para UK, nao para Portugal. Acesso policial ao NMPR, nao SaaS PT. | Via Recipero/CheckMEND para mercado comercial. | Publico gratis para cidadaos; comercial via CheckMEND/Recipero. | N/A | Referencia institucional, nao integracao PT direta. |
| **National Mobile Phone Crime Unit / NMPR (UK)** | Ecossistema policial britanico para moveis/propriedade. | Sem relevancia operacional direta para loja PT, salvo expansao UK. | Nao identificado acesso SaaS estrangeiro. | `{{confirmar}}` | N/A | Monitorizar apenas se RepairDesk entrar no UK. |
| **CEIR nacional PT** | CEIR e conceito de registo central de IMEI existe globalmente; para Portugal nao encontrei portal/API publica nacional. Operadores podem bloquear IMEI nos seus sistemas, mas isso nao equivale a API publica para lojas. | Sem acesso publico encontrado. Confirmar com ANACOM/MAI/operadoras. | Nao encontrado. | N/A | N/A | Tratar como hipotese institucional, nao dependencia de produto. |
| **Apple Activation Lock** | Muito relevante para iPhone/iPad/Mac/Watch. Apple confirma que Activation Lock impede reativacao e recomenda nao aceitar/comprar equipamento bloqueado. | Nao ha API publica oficial para terceiros verificarem Activation Lock por IMEI/serial. CheckCoverage verifica garantia, nao roubo/Activation Lock. | Nao usar scraping. Apple DeviceCheck API e para atestacao/app fraud, nao Activation Lock. | N/A | N/A | Implementar checklist manual: pedir Hello screen/sem conta anterior; nao prometer API. |
| **Stolen911 / bases publicas diversas** | Qualidade irregular, dependente de reportes voluntarios, duplicados e dados desatualizados. | Potencialmente acessivel, mas risco alto de falso positivo/negativo. | Varia. | Varia. | Varia. | Nao usar para decisao automatica. No maximo research, nunca bloquear cliente. |
| **GSMA Device Registry / Block List via operadores** | Fonte de bloqueio por operadoras. | Normalmente via operadores/GSMA, nao via publico. | Via GSMA Device Check. | `{{confirmar}}` | N/A | Coberto por GSMA/CheckMEND. |

Fontes principais:

- GSMA Device Check: https://devicecheck.gsma.com/
- GSMA Device Check FAQ: https://devicecheck.gsma.com/rtlapp/faqs/
- GSMA Device Check service page: https://www.gsmaservices.com/device-services/device-check/
- CheckMEND corporate/API: https://www.checkmend.com/us/corporate-accounts
- CheckMEND web pricing: https://www.checkmend.com/us/trader/account-pricing
- CTIA Stolen Phone Checker: https://stolenphonechecker.org/spc/about.jsp
- Immobilise/NMPR: https://www.immobilise.com/
- Apple Activation Lock: https://support.apple.com/en-la/108794

### 2.2 Estimativa de custo Fase B

Preco publico CheckMEND web em 2026, USD:

| Consultas/mes | Pacote/preco assumido | Custo mensal USD | Custo por loja se 10 lojas | Nota |
|---:|---:|---:|---:|---|
| 50 | USD 1,39/check, usando pacote 100 | USD 69,50 | USD 6,95 | Volume baixo: bom para piloto manual/web, nao API. |
| 100 | USD 1,39/check | USD 139,30 | USD 13,93 | Ja pesa em plano barato. |
| 500 | USD 1,00/check, se aproximar pacote 1000 | USD 500 | USD 50 | So faz sentido em Pro/Enterprise ou add-on. |
| 1000 | USD 995 por 1000 | USD 995 | USD 99,50 | Volume suficiente para negociar corporate/API. |
| 5000 | `{{confirmar}}` corporate | `{{confirmar}}` | `{{confirmar}}` | Pedir proposta. |

Conversao para EUR: usar cambio do dia quando houver proposta. Como referencia operacional, USD 1.000/mes sera aproximadamente da mesma ordem em EUR, mas nao fixar preco comercial sem cambio e contrato.

Regra de decisao:

- Se o RepairDesk cobrar EUR 29-49/mes, **nao incluir verificacoes externas ilimitadas**.
- Se cobrar EUR 79-149/mes Pro, incluir X consultas/mes e overage.
- Para lojas de compra/venda/recondicionados, vender como add-on com custo por consulta ou margem minima.
- Nao fazer Fase B se a margem por loja nao cobrir pelo menos 3x o custo medio das consultas.

## 3. Caminho institucional PT

### 3.1 Realidade atual

Nao foi encontrada fonte publica que indique existir em Portugal:

- portal de "lojas confiaveis" para consulta de IMEI roubado;
- API PSP/GNR/PJ/MAI para lojas consultarem IMEI/serial;
- CEIR nacional publico gerido por ANACOM com acesso a retalho/reparacao;
- mecanismo formal de buy-back trusted equivalente ao UK/Immobilise.

Isto nao significa que nao existam sistemas internos, listas de operadores ou cooperacao policial. Significa apenas que **nao ha evidencia publica suficiente para desenhar produto dependente disso**.

Tambem nao encontrei iniciativa portuguesa equivalente a "trusted buy-back / trusted repair shop" com validacao publica por autoridades. Existem lojas e marcas de recondicionados que afirmam fazer verificacao de IMEI/serial nos seus processos internos, mas isso e garantia comercial propria, nao uma rede institucional aberta a oficinas.

Clarificacao sobre contactos: **DCIAP nao e PSP**; e um departamento do Ministerio Publico para criminalidade grave/complexa. Nao deve ser o primeiro contacto para validar uma feature SaaS. So faz sentido envolver Ministerio Publico/DCIAP se houver indicios de rede organizada, volume relevante de matches ou encaminhamento formal por PSP/GNR/PJ.

### 3.2 Mapa de contactos e prioridade

| Prioridade | Organismo | Porque contactar | Pedido concreto | Contacto publico |
|---:|---|---|---|---|
| 1 | **PSP - Direcao Nacional / Departamento de Investigacao Criminal** | Oficinas urbanas e furtos/roubos em areas PSP; entidade natural para orientar fluxo de denuncia. | "Existe protocolo para oficinas verificarem IMEI? Como deve uma loja proceder perante match numa base comercial? Podemos submeter piloto?" | Via contactos MAI: PSP 218 111 000 / contacto@psp.pt |
| 2 | **GNR - Comando-Geral / Investigacao Criminal** | Cobre grande parte do territorio e muitas lojas fora de centros urbanos. | Mesmo pedido da PSP; perguntar se o procedimento difere por area territorial. | GNR 213 217 000 / gnr@gnr.pt |
| 3 | **Policia Judiciaria** | Criminalidade organizada, recetação, redes de furto/revenda, cooperacao internacional. | Pedido de orientacao sobre cooperacao em caso de padroes/matches multiplos, nao atendimento de ocorrencias de balcao. | PJ sede geral: confirmar canal institucional em pj.pt; contacto publico comum 211 967 000. |
| 4 | **SGMAI / MAI** | Tutela PSP/GNR e possivel enquadramento de projeto-piloto/interoperabilidade. | Pedido formal de encaminhamento para area competente e avaliacao de protocolo. | sec.geral.mai@sg.mai.gov.pt; contactos SGMAI publicados. |
| 5 | **ANACOM** | Regulador das comunicacoes; confirmar se existe CEIR/EIR nacional ou obrigacao de operadores. | "Existe em PT registo centralizado de IMEI roubados? Ha acesso por empresas de reparacao?" | 800 206 665 / portal ANACOM. |
| 6 | **Operadoras MEO/NOS/Vodafone/Digi/Nowo** | Podem alimentar bloqueios de IMEI e GSMA; tambem podem ter processos de fraude. | Confirmar se aceitam reportes de loja ou apenas do titular/autoridade; perguntar sobre GSMA Block List. | Canais corporate/regulatorios. |
| 7 | **Ministerio Publico / DCIAP** | Apenas se surgirem padroes de criminalidade organizada ou se PSP/GNR/PJ encaminharem. | Nao pedir API; pedir orientacao juridico-processual se houver piloto com reportes recorrentes. | Contacto institucional a confirmar no momento. |

Dentro da PSP, se o primeiro email geral responder, pedir encaminhamento para a area de **Investigacao Criminal** ou para gabinete/area de planeamento operacional competente. Evitar escrever diretamente para "gabinetes" sem confirmacao do canal certo: o objetivo inicial e obter dono interno e procedimento escrito.

Fonte de contactos MAI/PSP/GNR: https://www.sg.mai.gov.pt/Paginas/Contactos.aspx

### 3.3 Email formal inicial

```text
Assunto: Pedido de orientacao institucional - verificacao de IMEI/serial em oficinas de reparacao

Exmos. Senhores,

A LopesTech desenvolve o RepairDesk, software portugues para gestao de oficinas de reparacao de equipamentos eletronicos. Estamos a estudar uma funcionalidade para registar IMEI/numero de serie no momento de entrada do equipamento e alertar a oficina quando existam indicios de que o equipamento possa ter sido reportado como perdido, roubado ou sujeito a bloqueio.

Antes de implementar qualquer integracao ou comunicacao publica, solicitamos orientacao sobre:

1. se existe em Portugal algum procedimento, protocolo ou ponto de contacto para oficinas consultarem ou comunicarem identificadores de equipamentos suspeitos;
2. qual o procedimento recomendado quando uma oficina recebe um alerta de uma base comercial internacional, sem acusar o cliente nem reter indevidamente o bem;
3. se a entidade estaria disponivel para avaliar um projeto-piloto ou indicar a entidade competente.

O objetivo e reduzir o risco de rececao de bens furtados/roubados, proteger consumidores e criar um fluxo responsavel de cooperacao, respeitando RGPD, presuncao de inocencia e direitos dos clientes.

Com os melhores cumprimentos,
Bruno Lopes
LopesTech / RepairDesk
```

## 4. Compliance legal PT/EU

### 4.1 RGPD - base legal

IMEI/serial pode ser dado pessoal indireto quando associado a cliente, reparacao, contacto, fatura ou historico. Deve ser tratado como dado pessoal operacional.

Base legal recomendada para Fase A:

- **Art. 6.º(1)(b) RGPD - execucao de contrato/diligencias pre-contratuais**: registar identificador do equipamento para prestar reparacao, evitar trocas, gerir garantia e provar entrada/devolucao.
- **Art. 6.º(1)(f) RGPD - interesse legitimo**: prevenir fraude, proteger a loja, evitar rececao de bens de origem ilicita e manter auditoria proporcional.

Base legal para Fase B:

- **Art. 6.º(1)(f)** continua a ser a melhor base para loja privada consultar uma base externa de risco, desde que haja teste de ponderacao, minimizacao e transparencia.
- **Art. 6.º(1)(e)** so e defensavel se houver enquadramento legal/protocolo que atribua funcao de interesse publico ou cooperacao formal. Para uma loja privada sem protocolo, nao usar como base principal.

Fontes:

- RGPD Art. 6.º: https://eur-lex.europa.eu/eli/reg/2016/679/oj?locale=PT
- O art. 6.º(1)(e) exige funcao de interesse publico/autoridade; o considerando 45 indica que deve assentar em direito da Uniao ou Estado-Membro.

### 4.2 Art. 10 RGPD - dados de infracoes

Se o RepairDesk guardar apenas:

- IMEI;
- resultado tecnico "flagged/not flagged";
- provider e data/hora;
- recomendacao operacional;

e nao guardar condenacoes, acusacoes ou dados criminais sobre uma pessoa, o risco e menor. Mas um match "roubado" aproxima-se de informacao relacionada com infracoes. Por prudencia:

- nao mostrar "cliente suspeito";
- nao guardar narrativas criminais;
- nao fazer perfil criminal de clientes;
- limitar acessos;
- criar politica de retencao;
- pedir parecer juridico antes da Fase B.

### 4.3 Texto para recibo de entrada

Versao Fase A, sem base externa:

```text
Identificacao do equipamento: para efeitos de reparacao, garantia, prevencao de trocas, seguranca e auditoria, a oficina regista o IMEI, IMEI2 e/ou numero de serie do equipamento entregue. Estes dados sao tratados no ambito da prestacao do servico e do interesse legitimo da oficina em prevenir fraude e proteger clientes e equipamentos. O cliente confirma que entrega o equipamento de boa-fe e que esta autorizado a solicitar a intervencao.
```

Versao Fase B, com verificacao externa:

```text
Verificacao de identificadores: quando aplicavel, o IMEI, IMEI2 e/ou numero de serie do equipamento podera ser verificado junto de bases de dados de risco, operadores ou prestadores especializados, para confirmar se existe registo de perda, roubo, bloqueio, reclamacao de propriedade, fraude ou impedimento de revenda/reparacao. A verificacao destina-se a proteger o cliente, a oficina e terceiros, sendo realizada com base na execucao do servico e/ou no interesse legitimo da oficina em prevenir fraude e rececao de bens de origem ilicita. Um alerta nao constitui acusacao nem prova definitiva; pode exigir confirmacao adicional, documentacao de propriedade ou contacto com as autoridades competentes.
```

Versao curta para checkbox/assinatura:

```text
Li e aceito que a oficina registe e, quando aplicavel, verifique o IMEI/serial do equipamento para reparacao, garantia, seguranca e prevencao de fraude, nos termos da politica de privacidade.
```

Notas:

- Nao depender de consentimento como unica base legal, porque se a verificacao for necessaria para a seguranca operacional da loja, o consentimento pode nao ser livre.
- Dar informacao antes da recolha, idealmente no recibo e politica de privacidade.
- Em Fase B, atualizar DPA/subcontratante com CheckMEND/GSMA e avaliar transferencias internacionais.

### 4.4 Denuncia e cooperacao com autoridades

Correcao importante: a referencia operacional nao deve ser "art. 244.º CP". O tema esta no **Codigo de Processo Penal**:

- **Art. 242.º CPP - denuncia obrigatoria**: obrigatoria para entidades policiais e funcionarios, na acecao do art. 386.º do Codigo Penal, quanto a crimes conhecidos no exercicio das funcoes.
- **Art. 244.º CPP - denuncia facultativa**: qualquer pessoa que tiver noticia de um crime pode denuncia-lo ao Ministerio Publico, autoridade judiciaria ou orgaos de policia criminal, salvo crimes dependentes de queixa/acusacao particular.

Para uma loja privada comum, a regra geral e **facultativa**, nao obrigatoria. Ainda assim, por prudencia comercial e reputacional, o RepairDesk deve facilitar um fluxo de contacto com autoridades quando houver match serio.

Fonte DRE CPP: https://diariodarepublica.pt/dr/legislacao-consolidada/decretolei/1987-34570075

### 4.5 Responsabilidade por falsos positivos

Riscos:

- IMEI clonado: equipamento legitimo pode partilhar IMEI com equipamento ilicito.
- Base desatualizada: equipamento recuperado/desbloqueado pode continuar flagged.
- Reporte abusivo: alguem pode tentar bloquear IMEI indevidamente junto de operador.
- Erro de digitacao/OCR: tecnico pode consultar IMEI errado.
- Ambiguidade de propriedade: cliente pode ser reparador, familiar, comprador de usados ou trabalhador de empresa.

Mitigacoes obrigatorias:

1. Linguagem: "alerta de risco", "match a confirmar", nunca "roubado" como acusacao ao cliente.
2. Dupla confirmacao: repetir leitura do IMEI no proprio equipamento antes de qualquer acao.
3. Evidencia: guardar provider, timestamp, query hash/ID do relatorio, tecnico e versao do resultado.
4. Processo: pedir comprovativo de propriedade ou autorizacao; escalar a gerente; nao discutir em publico.
5. Autoridades: se houver suspeita forte, contactar PSP/GNR local ou linha institucional definida; nao reter fisicamente sem base legal clara.
6. Contestacao: permitir marcar "falso positivo/contestacao" e anexar documentos.

Responsabilidade possivel:

- Civil: recusar servico, reter equipamento ou acusar cliente sem cautela pode gerar dano reputacional/patrimonial.
- Penal: retencao indevida, difamacao/injuria ou tratamento abusivo podem ser risco se a loja agir como autoridade.
- RGPD: exposicao indevida do alerta a funcionarios sem necessidade ou a terceiros.

## 5. Modelo UK como referencia

O UK tem dois elementos relevantes:

1. **Mobile Telephones (Re-programming) Act 2002**: criminaliza alterar/interferir com identificadores unicos de telemoveis em certas circunstancias. Fonte: https://www.legislation.gov.uk/ukpga/2002/31/contents
2. **Modelo operativo com bases nacionais/comerciais**: Immobilise/NMPR/CheckMEND cria ponte entre registo de propriedade, policia e comercio de usados.

Isto e bom modelo conceptual para Portugal, mas nao e transponivel diretamente. RepairDesk pode usar como argumento institucional: "queremos uma versao portuguesa responsavel para oficinas".

## 6. Tradeoffs UX

### 6.1 Antes ou depois de aceitar trabalho?

Recomendacao: **antes de aceitar formalmente o trabalho**, mas depois de criar uma "pre-entrada" com dados minimos.

Fluxo:

1. Tecnico seleciona categoria "Telemovel".
2. Sistema exige IMEI principal; sugere IMEI2 se dual-SIM.
3. Valida Luhn.
4. Mostra historico interno.
5. Se Fase B ativa, consulta externa.
6. So depois gera recibo final de entrada.

Se o equipamento estiver morto e nao for possivel obter IMEI:

- permitir excecao "IMEI indisponivel";
- exigir motivo;
- pedir serial/foto;
- marcar reparacao como risco maior.

### 6.2 O cliente tem direito a ver o resultado?

Sim, em principio tem direito a informacao sobre dados pessoais tratados e logica basica da decisao, mas ha limites quando o provider ou autoridades impuserem confidencialidade. UX recomendada:

- Resultado limpo: nao dramatizar; fica no recibo interno.
- Alerta: comunicar verbalmente e de forma neutra.
- Entregar ao cliente apenas informacao minima: "o identificador devolveu um alerta numa base de verificacao; precisamos confirmar propriedade/procedimento".
- Nao expor detalhes de fontes policiais, scoring interno ou dados de terceiros.

### 6.3 Como gerir match positivo

Estados recomendados:

| Estado | Mensagem interna | Acao |
|---|---|---|
| Limpo | "Sem alerta externo conhecido nesta consulta." | Prosseguir. |
| Inconclusivo | "Nao foi possivel confirmar; verificar manualmente." | Prosseguir com cautela ou pedir documentos. |
| Watch-list | "Existe alerta de risco; confirmar dados e propriedade." | Gerente revê; pedir comprovativo. |
| Reportado/bloqueado | "Identificador consta como flagged; nao acusar cliente." | Pausar entrada; confirmar IMEI; contactar provider/autoridade conforme procedimento. |

Texto para tecnico:

```text
Este alerta nao prova que o cliente cometeu qualquer ilicito. Confirme o IMEI diretamente no equipamento, chame o responsavel de loja e siga o procedimento interno. Nao acuse o cliente e nao retenha o equipamento sem orientacao adequada.
```

## 7. Plano por fases

### Fase A - registo interno robusto (sprints 16-17)

Objetivo: valor imediato sem dependencia externa.

Scope:

- Tornar IMEI obrigatorio em categoria "Telemovel", com excecao justificada.
- Campos: IMEI, IMEI2, serial, fonte do identificador, "nao foi possivel obter".
- Validacao Luhn para 15 digitos.
- Normalizacao de input.
- Historico interno: "este IMEI/serial ja entrou nesta loja" e, se multi-tenant permitido, "ja entrou noutra loja RepairDesk" apenas com privacidade e opt-in.
- Recibo de entrada com IMEI/serial.
- Log de alteracoes: quem alterou IMEI, quando, antes/depois.
- Permissao separada para editar IMEI apos entrada.
- Texto RGPD Fase A no recibo/politica.

Nao incluir:

- Consulta externa.
- Claim de "autoridades".
- Bloqueio automatico de reparacoes.

Criterio de sucesso:

- 95% das entradas de telemoveis com IMEI valido ou excecao justificada.
- Reducao de erros/trocas.
- Bruno usa 2 semanas em dogfooding.

### Fase B - integracao com 1 BD externa

Provider recomendado: **CheckMEND corporate/API**, com GSMA Device Check direto como alternativa a negociar.

Pre-requisitos:

- Proposta comercial escrita.
- DPA/subcontratante e localizacao/transferencias de dados.
- Documentacao API, limites, rate limit, SLA, suporte, ambiente sandbox.
- Confirmar se cada IMEI2 conta como nova consulta.
- Confirmar retention/status change alerts de 7/14/30 dias.
- Parecer juridico RGPD.
- UX de falso positivo aprovada.

MVP Fase B:

- Integracao so para tenants opt-in.
- Consulta manual por botao no inicio, nao automatica para todos.
- Guardar resultado resumido + ID do relatorio.
- Nunca guardar mais dados do que necessario.
- Feature flag por loja/plano.
- Limites mensais e alerta de custo.

Volume minimo para valer:

- Abaixo de 100 consultas/mes totais: usar web/manual, nao API.
- 100-1000 consultas/mes: piloto com preco por consulta e pacote Pro.
- 1000+ consultas/mes: negociar API/corporate e margem.

### Fase C - parceria institucional PT (horizon 4)

Objetivo: legitimidade, procedimento e eventualmente acesso/encaminhamento.

Passos:

1. Preparar one-pager institucional com problema, salvaguardas e piloto.
2. Enviar pedido formal a PSP, GNR, PJ, SGMAI e ANACOM.
3. Pedir reuniao de 30 minutos com foco em procedimento, nao venda.
4. Propor piloto com 3-5 lojas voluntarias, sem partilha massiva de dados.
5. Validar texto de recibo e protocolo de match.
6. So depois comunicar publicamente.

Mensagem externa segura:

```text
RepairDesk ajuda oficinas a registar identificadores de equipamentos e a reduzir o risco de aceitar bens sinalizados em bases de verificacao, com procedimentos que respeitam o cliente, a privacidade e a cooperacao com autoridades.
```

Mensagem a evitar:

```text
RepairDesk consulta diretamente bases da PSP/Interpol e apanha telemoveis roubados.
```

## 8. Riscos identificados

| Risco | Tipo | Severidade | Mitigacao |
|---|---|---:|---|
| Falso positivo acusa cliente inocente | Reputacional/legal | Alta | Linguagem neutra, dupla confirmacao, gerente, processo de contestacao. |
| Provider caro torna feature deficitario | Financeiro | Alta | Add-on/Pro, limites mensais, custo por consulta, piloto antes de bundling. |
| Sem acesso PSP/MAI | Produto | Media | Fase A e CheckMEND nao dependem disso; Fase C como opcao. |
| Promessa comercial exagerada | Reputacional | Alta | Nao usar "autoridades" sem protocolo escrito. |
| Tratamento RGPD insuficiente | Legal | Alta | Base legal documentada, informacao no recibo, DPA, minimizacao, logs. |
| IMEI clonado/desatualizado | Tecnico | Alta | Nao decidir automaticamente; exigir confirmacao manual. |
| Dependencia GSMA/CheckMEND | Estrategico | Media | Abstracao provider, feature flags, fallback manual. |
| Latencia/API indisponivel | Operacional | Media | Nao bloquear rececao se provider falhar; estado "inconclusivo". |
| Tecnico retem equipamento sem base | Legal | Alta | Procedimento claro: pausar, confirmar, gerente, contactar autoridade; nao "confiscar". |
| Cliente pede resultado completo | RGPD/contrato | Media | Politica de acesso; mostrar resumo proporcional; respeitar direitos RGPD. |

## 9. Proximo passo concreto

Se a decisao for AVANCAR:

1. Criar ticket "Fase A - IMEI robusto" para sprints 16-17.
2. Implementar Luhn + campos IMEI/IMEI2/serial + historico interno + logs.
3. Adicionar clausula Fase A ao recibo de entrada.
4. Em paralelo, enviar pedido de proposta a CheckMEND corporate:

```text
We are building RepairDesk, a SaaS for electronics repair shops in Portugal/EU. We want to integrate device due diligence checks for IMEI/serial at repair intake. Please provide commercial terms, API documentation/access requirements, KYC process, geographic coverage for Portugal/EU, rate limits, SLA/support, data processing terms, status change monitoring options, and pricing for 100, 500, 1000 and 5000 checks/month.
```

5. Enviar carta institucional a PSP/GNR/PJ/SGMAI/ANACOM.
6. Rever com advogado/DPO antes da Fase B.

## 10. Fontes consultadas

- GSMA Device Check: https://devicecheck.gsma.com/
- GSMA Device Check FAQ: https://devicecheck.gsma.com/rtlapp/faqs/
- GSMA Device Check service page: https://www.gsmaservices.com/device-services/device-check/
- GSMA TAC Allocation: https://www.gsmaservices.com/device-services/tac-allocation
- GSMA IMEI Database: https://www.gsma.com/get-involved/working-groups/terminal-steering-group/imei-database/
- CheckMEND corporate/API: https://www.checkmend.com/us/corporate-accounts
- CheckMEND web pricing: https://www.checkmend.com/us/trader/account-pricing
- CTIA Stolen Phone Checker: https://stolenphonechecker.org/spc/about.jsp
- Immobilise / NMPR: https://www.immobilise.com/
- Apple Activation Lock iPhone/iPad: https://support.apple.com/en-la/108794
- Apple Activation Lock removal: https://support.apple.com/en-us/108934
- Apple warranty/check coverage: https://support.apple.com/en-us/HT204293
- RGPD, Regulamento (UE) 2016/679: https://eur-lex.europa.eu/eli/reg/2016/679/oj?locale=PT
- Codigo de Processo Penal consolidado, DRE: https://diariodarepublica.pt/dr/legislacao-consolidada/decretolei/1987-34570075
- SGMAI contactos PSP/GNR/MAI: https://www.sg.mai.gov.pt/Paginas/Contactos.aspx
- GNR Investigacao Criminal: https://www.gnr.pt/atrib_invCriminal.aspx/layout/bannerRotator/SlideOut/js/ProgEsp_main.aspx
- PSP atribuicoes/investigacao criminal: https://www.psp.pt/Pages/sobre-nos/quem-somos/o-que-e-a-psp.aspx?lang=en
- UK Mobile Telephones (Re-programming) Act 2002: https://www.legislation.gov.uk/ukpga/2002/31/contents
