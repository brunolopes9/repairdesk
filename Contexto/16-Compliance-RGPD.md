# Compliance RGPD - RepairDesk SaaS PT

Atualizado: 2026-05-16  
Projeto: RepairDesk SaaS - oficinas de reparacao em Portugal  
Responsavel interno: Bruno Lopes / LopesTech  
Status: pacote MVP legal para beta. Validar com advogado antes de venda publica em escala.

> Este documento e operacional, nao e parecer juridico. Serve para o Bruno conseguir publicar Privacy, Cookies e Termos num dia, enviar um DPA a primeira loja beta, e saber exatamente o que tem de rever com advogado.

---

## 1. Conclusao executiva

O RepairDesk tem dois papeis RGPD diferentes:

1. **Responsavel pelo tratamento** dos dados dos proprios utilizadores SaaS: dono da loja, funcionarios, leads, faturacao da subscricao, suporte, cookies e analytics do site.
2. **Subcontratante** das lojas que usam o RepairDesk: dados dos clientes finais da loja, dispositivos, IMEI, historico de reparacoes, fotos futuras, mensagens WhatsApp futuras, documentos e IBANs tratados por conta da loja.

Isto muda a documentacao:

- **Politica de Privacidade publica**: fala sobretudo dos utilizadores SaaS e visitantes do site.
- **DPA / Contrato de Processamento de Dados**: regula os dados dos clientes finais das lojas.
- **Termos de Servico**: contrato comercial de utilizacao do RepairDesk.
- **Politica de Cookies + banner**: site publico e app, com cookies essenciais sem consentimento e analytics/marketing so com consentimento.

Minimo antes da beta:

- Privacy publicada.
- Termos publicados.
- DPA modelo pronto e aceite pela loja beta.
- Cookie banner se houver analytics/marketing cookies; se so houver cookies essenciais, mostrar politica de cookies simples e dispensar banner intrusivo.
- Retencao definida para logs e backups.
- Procedimento escrito para pedidos RGPD e data breach.
- Lista de subcontratantes/sub-processadores real, mesmo que curta.

Pontos onde advogado e recomendado:

- Limite de responsabilidade dos Termos.
- DPA final, especialmente auditorias, sub-processadores e transferencias internacionais.
- Uso futuro de WhatsApp/Meta e storage de fotos.
- DPIA se o produto crescer para muitas lojas, fotos, IMEI, automacoes WhatsApp e matching de IMEI com bases externas.

---

## 2. Base legal consultada

Fontes oficiais usadas:

- RGPD - Regulamento (UE) 2016/679: https://eur-lex.europa.eu/legal-content/PT/TXT/?uri=CELEX:32016R0679
- Lei 58/2019, execucao do RGPD em Portugal: https://diariodarepublica.pt/dr/detalhe/lei/58-2019-123815982
- CNPD - Violacao de dados / data breach: https://www.cnpd.pt/organizacoes/outras-obrigacoes/violacao-de-dados-ou-data-breach/
- CNPD - Registo de atividades de tratamento: https://www.cnpd.pt/organizacoes/outras-obrigacoes/registo-de-atividades-de-tratamento/
- CNPD - Encarregado de protecao de dados: https://www.cnpd.pt/organizacoes/outras-obrigacoes/encarregado-de-protecao-de-dados/
- CNPD - Nota informativa sobre cookies: https://www.cnpd.pt/media/x2zdus50/nota-informativa-cnpd_cookies_20210625.pdf
- CNPD - Consentimento: https://www.cnpd.pt/organizacoes/areas-tematicas/consentimento/
- EDPB - Guia PME / DPIA: https://www.edpb.europa.eu/sme-data-protection-guide/be-compliant_en
- Comissao Europeia - SCCs Art. 28 responsavel-subcontratante: https://commission.europa.eu/publications/standard-contractual-clauses-controllers-and-processors-eueea_en

Pontos legais confirmados:

- O artigo 28.º do RGPD exige contrato ou outro ato juridico entre responsavel e subcontratante.
- O artigo 30.º do RGPD exige registo de atividades tambem para subcontratantes; a CNPD sublinha que a derrogacao para empresas com menos de 250 trabalhadores e excecional.
- O artigo 33.º do RGPD exige notificacao de violacoes de dados pessoais a autoridade de controlo em ate 72 horas quando haja risco para direitos/liberdades.
- O artigo 34.º do RGPD exige comunicacao aos titulares quando o risco for elevado.
- A Lei 58/2019 concretiza regras em Portugal e define contraordenacoes, incluindo falhas de transparencia, direitos dos titulares, transferencias internacionais e DPIA quando obrigatoria.
- A CNPD indica que cookies de analitica exigem consentimento quando aplicavel, nos termos do artigo 5.º da Lei 41/2004.

---

## 3. Mapa de dados RepairDesk

| Categoria | Exemplos | Papel RepairDesk | Papel loja | Observacao |
|---|---|---|---|---|
| Utilizadores SaaS | nome, email, password hash, role, tenant, logs de login | Responsavel | N/A | Dados para prestar o SaaS |
| Leads/site | nome, email, telefone, mensagem | Responsavel | N/A | Pedidos de demo/contacto |
| Subscricao/faturacao | dados empresa, NIF, morada, plano, pagamentos | Responsavel | Cliente contratual | Retencao legal fiscal/contabilistica |
| Clientes finais da loja | nome, telefone, email, NIF, notas | Subcontratante | Responsavel | Tratado por conta da loja |
| Dispositivos | equipamento, IMEI/serial, avaria, fotos futuras | Subcontratante | Responsavel | IMEI pode identificar indiretamente pessoa/equipamento |
| Reparacoes | estados, diagnostico, orcamentos, custos, garantias | Subcontratante | Responsavel | Dados operacionais da loja |
| Comunicacoes | WhatsApp templates, mensagens futuras | Subcontratante | Responsavel | Requer opt-in/legitimidade definida pela loja |
| Logs tecnicos | IP, userId, erro, endpoint, timestamp | Responsavel para seguranca; subcontratante quando contem dados da loja | Responsavel pelos dados de negocio | Minimizar dados pessoais em logs |
| Cookies | sessao, preferencias, analytics | Responsavel | N/A | Analytics/marketing so com consentimento |

Regra de arquitetura:

> Dados de clientes finais nunca devem ser usados para marketing da LopesTech, treino de IA, benchmarking publico ou prospecao comercial sem base legal e acordo explicito da loja.

---

## 4. Anexo A - Politica de Privacidade publica

Texto pronto a publicar em `/privacidade` ou `lopestech.pt/privacidade`.

### Politica de Privacidade da LopesTech / RepairDesk

Ultima atualizacao: 2026-05-16

A LopesTech respeita a tua privacidade. Esta politica explica que dados pessoais tratamos quando visitas o nosso site, nos contactas ou usas o RepairDesk enquanto utilizador de uma loja.

O RepairDesk e um software de gestao para oficinas de reparacao. Quando uma loja usa o RepairDesk para gerir os seus proprios clientes, a loja e a responsavel pelo tratamento desses dados. A LopesTech trata esses dados como subcontratante, de acordo com as instrucoes da loja e com o contrato de processamento de dados aplicavel.

#### 1. Quem somos

O RepairDesk e desenvolvido pela LopesTech, projeto de Bruno Lopes, em Portugal.

Contacto para privacidade e protecao de dados:  
`privacidade@lopestech.pt`

Enquanto nao existir encarregado de protecao de dados formalmente designado, este contacto e gerido pelo Bruno Lopes. Se no futuro a LopesTech designar um Encarregado de Protecao de Dados, os contactos serao atualizados nesta pagina e comunicados quando legalmente necessario.

#### 2. Que dados recolhemos

Podemos tratar os seguintes dados:

**Visitantes do site**

- endereco IP, informacao tecnica do browser/dispositivo e paginas visitadas;
- cookies essenciais para funcionamento do site;
- cookies de analitica ou marketing apenas se forem ativados e aceites.

**Pessoas que nos contactam**

- nome;
- email;
- telefone, se fornecido;
- empresa/loja;
- mensagem enviada;
- historico de contacto.

**Utilizadores do RepairDesk**

- nome;
- email;
- palavra-passe em formato protegido/hash;
- loja/empresa a que pertencem;
- perfil/permissoes;
- historico de login e atividade essencial de seguranca;
- definicoes da conta e preferencias.

**Clientes da LopesTech**

- dados da empresa ou atividade;
- NIF;
- morada;
- email de faturacao;
- plano contratado;
- historico de pagamentos e faturas.

**Dados que as lojas colocam no RepairDesk**

As lojas podem inserir dados dos seus proprios clientes finais, como nome, telefone, email, NIF, equipamento, IMEI/serial, avarias, historico de reparacoes, fotos e comunicacoes. Nestes casos, a loja e responsavel pelo tratamento e a LopesTech atua como subcontratante.

#### 3. Para que usamos os dados

| Finalidade | Dados | Base legal |
|---|---|---|
| Prestar o RepairDesk | conta, login, loja, permissoes, dados operacionais | Execucao de contrato |
| Gerir clientes e faturacao da LopesTech | dados de empresa, NIF, morada, pagamentos | Execucao de contrato e obrigacao legal |
| Responder a contactos e pedidos de demo | nome, email, telefone, mensagem | Diligencias pre-contratuais ou interesse legitimo |
| Segurança, prevencao de abuso e logs | IP, userId, eventos tecnicos, login | Interesse legitimo e cumprimento de obrigacoes de seguranca |
| Melhorar o produto | metricas agregadas e, sempre que possivel, anonimizadas | Interesse legitimo |
| Enviar comunicacoes sobre o servico | email da conta, avisos de produto, seguranca, faturacao | Execucao de contrato ou interesse legitimo |
| Marketing direto da LopesTech | email/contacto, quando aplicavel | Consentimento ou interesse legitimo, conforme o caso |
| Cookies de analitica/marketing | identificadores online | Consentimento |

Nao vendemos dados pessoais. Nao usamos dados dos clientes finais das lojas para marketing da LopesTech.

#### 4. Com quem partilhamos dados

Podemos usar fornecedores tecnicos para operar o RepairDesk, por exemplo:

- alojamento/cloud e base de dados;
- armazenamento de ficheiros e backups;
- email transacional;
- faturacao/subscricoes;
- suporte tecnico;
- analytics, se ativado;
- provider de faturacao certificado, quando a loja ativar integracao;
- WhatsApp/Meta ou outro provider de mensagens, quando a loja ativar essa funcionalidade.

Estes fornecedores so podem tratar dados na medida necessaria para prestar o servico. Sempre que atuem como subcontratantes, devem estar sujeitos a obrigacoes de protecao de dados.

A lista atualizada de subcontratantes pode ser disponibilizada em pagina propria ou mediante pedido para `privacidade@lopestech.pt`.

#### 5. Transferencias para fora do EEE

Sempre que possivel, usamos fornecedores com tratamento de dados no Espaco Economico Europeu. Se for necessario transferir dados para fora do EEE, aplicaremos mecanismos adequados previstos no RGPD, como decisoes de adequacao, clausulas contratuais-tipo ou outras garantias aplicaveis.

Alguns servicos futuros, como WhatsApp/Meta, podem envolver transferencias internacionais. Essas integracoes devem ser avaliadas antes de serem ativadas em producao.

#### 6. Durante quanto tempo guardamos os dados

Guardamos os dados apenas pelo tempo necessario para as finalidades descritas:

- conta RepairDesk: durante a vigencia da conta;
- dados de faturacao: pelo prazo legal aplicavel a documentos fiscais/contabilisticos;
- contactos comerciais sem contrato: ate 24 meses apos o ultimo contacto, salvo pedido de apagamento;
- tickets de suporte: ate 24 meses, salvo necessidade de manter historico para defesa de direitos;
- logs tecnicos de seguranca: normalmente 90 dias, salvo investigacao de incidente;
- backups: rotacao tecnica prevista na politica de backups, normalmente 30 a 90 dias;
- dados inseridos pelas lojas: durante a subscricao e, apos cancelamento, durante o periodo de exportacao/apagamento definido nos Termos e no DPA.

Quando a LopesTech trata dados por conta de uma loja, a retencao principal e definida pela loja, enquanto responsavel pelo tratamento.

#### 7. Direitos dos titulares

Nos termos do RGPD, podes ter direito a:

- aceder aos teus dados;
- corrigir dados incorretos;
- pedir apagamento;
- limitar o tratamento;
- opor-te a certos tratamentos;
- pedir portabilidade;
- retirar consentimento, quando o tratamento se baseie em consentimento;
- apresentar reclamacao junto da CNPD.

Para exercer direitos sobre a tua conta RepairDesk ou contacto com a LopesTech, escreve para `privacidade@lopestech.pt`.

Se fores cliente final de uma loja que usa RepairDesk, deves contactar primeiro essa loja, porque e ela a responsavel pelo tratamento dos teus dados. A LopesTech ajudara a loja a responder, quando necessario.

#### 8. Segurança

Aplicamos medidas tecnicas e organizativas adequadas ao risco, incluindo controlo de acessos, autenticacao, encriptacao em transito, backups, logs de seguranca, separacao por loja/tenant e principio do menor acesso.

Nenhum sistema e 100% imune a incidentes. Se ocorrer uma violacao de dados pessoais com impacto relevante, seguiremos os procedimentos previstos no RGPD e, quando aplicavel, notificaremos as entidades e pessoas afetadas.

#### 9. Alteracoes a esta politica

Podemos atualizar esta politica quando o produto, fornecedores ou requisitos legais mudarem. A versao mais recente ficara sempre disponivel nesta pagina.

---

## 5. Anexo B - Termos de Servico

Texto pronto a publicar em `/termos`.

### Termos de Servico RepairDesk

Ultima atualizacao: 2026-05-16

Estes Termos regulam a utilizacao do RepairDesk, um software SaaS de gestao para oficinas de reparacao.

Ao criar conta ou usar o RepairDesk, a loja aceita estes Termos.

#### 1. Quem presta o servico

O RepairDesk e prestado pela LopesTech, projeto de Bruno Lopes, em Portugal.

Contacto geral: `geral@lopestech.pt`  
Contacto privacidade: `privacidade@lopestech.pt`

#### 2. Quem pode usar

O RepairDesk destina-se a profissionais, empresas e trabalhadores independentes que gerem reparacoes, clientes, equipamentos, orcamentos, despesas e operacao de oficina.

Ao usar o RepairDesk em nome de uma loja ou empresa, declaras que tens autorizacao para aceitar estes Termos em nome dessa entidade.

#### 3. O que o RepairDesk faz

O RepairDesk ajuda a gerir:

- clientes;
- reparacoes;
- estados de reparacao;
- equipamentos e IMEI/serial;
- orcamentos e documentos operacionais nao fiscais;
- custos, despesas e margem;
- portal cliente;
- comunicacoes e integracoes futuras.

Salvo indicacao expressa em contrario, o RepairDesk nao substitui aconselhamento juridico, fiscal, contabilistico ou tecnico.

#### 4. Documentos fiscais e faturacao

Enquanto o RepairDesk nao tiver modulo fiscal certificado ou integracao certificada ativa, os documentos gerados dentro do RepairDesk sao documentos operacionais, como orcamentos, fichas de reparacao ou garantias, e nao substituem faturas legalmente obrigatorias.

Quando existir integracao com provider de faturacao certificado, a emissao fiscal sera feita atraves desse provider, nos termos e limites da integracao.

A loja continua responsavel por cumprir as suas obrigacoes fiscais, contabilisticas e legais.

#### 5. Conta e seguranca

A loja e responsavel por:

- usar dados corretos;
- proteger credenciais de acesso;
- dar acesso apenas a pessoas autorizadas;
- remover utilizadores que ja nao trabalhem na loja;
- verificar que os dados inseridos sao licitos e necessarios.

A LopesTech pode suspender acessos em caso de abuso, risco de seguranca, utilizacao ilegal ou incumprimento grave destes Termos.

#### 6. Propriedade dos dados

Os dados inseridos pela loja no RepairDesk pertencem sempre a loja ou aos respetivos titulares, conforme aplicavel.

A LopesTech nao vende os dados da loja, nao os usa para marketing proprio e nao os usa para competir com a loja.

A loja pode pedir exportacao dos seus dados em formato razoavel e utilizavel. O objetivo do RepairDesk e evitar lock-in.

#### 7. Protecao de dados

Para dados dos clientes finais da loja, a loja e responsavel pelo tratamento e a LopesTech atua como subcontratante.

O tratamento desses dados e regulado pelo Contrato de Processamento de Dados (DPA), que faz parte destes Termos quando a loja usa o RepairDesk para tratar dados pessoais de terceiros.

#### 8. Planos, pagamentos e cancelamento

Os planos e precos sao apresentados na pagina de pricing ou proposta comercial aceite.

Salvo acordo diferente:

- a subscricao e mensal ou anual;
- a loja pode cancelar no fim do periodo pago;
- nao ha fidelizacao obrigatoria nos planos standard;
- depois do cancelamento, a conta pode ficar em modo leitura/exportacao durante 30 dias;
- apos esse periodo, os dados podem ser apagados de sistemas ativos e, posteriormente, dos backups por rotacao tecnica.

Durante beta, podem existir condicoes especiais, descontos ou acesso gratuito, definidos por escrito.

#### 9. Disponibilidade e SLA realista

A LopesTech fara esforcos razoaveis para manter o RepairDesk disponivel e seguro.

SLA MVP/beta:

- objetivo de disponibilidade: 99% mensal, excluindo manutencoes programadas, falhas de terceiros, casos de forca maior e problemas fora do controlo razoavel da LopesTech;
- suporte em horario util portugues, com resposta inicial pretendida em ate 2 dias uteis durante beta;
- incidentes criticos de seguranca ou indisponibilidade prolongada terao prioridade.

Durante beta, o servico pode ter alteracoes, bugs e manutencoes mais frequentes. A LopesTech comunicara incidentes relevantes de forma honesta.

#### 10. Backups

A LopesTech deve manter backups tecnicos com periodicidade adequada ao estado do produto. Backups nao substituem a obrigacao da loja de exportar e guardar documentos que tenha de conservar legalmente.

#### 11. Uso aceitavel

Nao e permitido usar o RepairDesk para:

- atividades ilegais;
- inserir dados obtidos de forma ilicita;
- tentar aceder a contas ou dados de outras lojas;
- interferir com a seguranca ou disponibilidade do servico;
- enviar spam ou comunicacoes sem legitimidade;
- carregar malware ou conteudo abusivo.

#### 12. Limitacao de responsabilidade

Na medida permitida por lei, a LopesTech nao sera responsavel por perdas indiretas, lucros cessantes, perda de negocio, perda de reputacao ou danos resultantes de uso indevido do RepairDesk pela loja.

A responsabilidade total da LopesTech por danos relacionados com o servico fica limitada ao valor pago pela loja nos 3 meses anteriores ao evento que originou a responsabilidade, salvo nos casos em que a lei nao permita essa limitacao.

Este ponto deve ser validado por advogado antes do lancamento publico.

#### 13. Alteracoes ao servico e aos Termos

A LopesTech pode melhorar, alterar ou remover funcionalidades, tentando evitar impacto injustificado no uso normal da loja.

Alteracoes materiais aos Termos serao comunicadas com antecedencia razoavel. Se a loja nao aceitar alteracoes materiais, pode cancelar a subscricao e exportar os dados.

#### 14. Lei e foro

Estes Termos sao regidos pela lei portuguesa.

Salvo norma legal imperativa em contrario, qualquer litigio sera submetido aos tribunais portugueses competentes.

---

## 6. Anexo C - DPA / Contrato de Processamento de Dados

Modelo pronto a enviar a loja beta. Recomenda-se converter para PDF e assinar manualmente ou por assinatura digital simples. Validar com advogado antes de uso comercial em escala.

### Contrato de Processamento de Dados

**Entre:**

**Responsavel pelo Tratamento:**  
`[Nome legal da loja]`, NIF `[NIF]`, com sede/morada em `[morada]`, representada por `[nome]`, doravante "Loja".

**Subcontratante:**  
LopesTech / Bruno Lopes, NIF `[NIF LopesTech]`, com contacto `privacidade@lopestech.pt`, doravante "RepairDesk" ou "Subcontratante".

Este Contrato regula o tratamento de dados pessoais realizado pelo RepairDesk por conta da Loja, nos termos do artigo 28.º do RGPD.

#### 1. Objeto

O RepairDesk presta a Loja um software SaaS para gestao de oficina, incluindo clientes, reparacoes, dispositivos, estados, documentos operacionais, despesas, portal cliente e integracoes.

No ambito desse servico, o RepairDesk trata dados pessoais por conta e segundo instrucoes da Loja.

#### 2. Duracao

Este Contrato vigora enquanto a Loja usar o RepairDesk e enquanto o RepairDesk tratar dados pessoais por conta da Loja, incluindo o periodo tecnico necessario para exportacao, apagamento e rotacao de backups apos cessacao.

#### 3. Natureza e finalidade do tratamento

O tratamento consiste em recolha, registo, organizacao, conservacao, consulta, alteracao, disponibilizacao, exportacao, apagamento e outras operacoes tecnicas necessarias para prestar o RepairDesk.

Finalidades:

- gerir clientes da Loja;
- gerir reparacoes e dispositivos;
- acompanhar estados de reparacao;
- gerar documentos operacionais;
- permitir comunicacao com clientes finais quando ativado;
- permitir portal cliente;
- suportar operacao, seguranca, backups e suporte tecnico;
- executar integracoes autorizadas pela Loja.

O RepairDesk nao pode tratar estes dados para finalidades proprias incompatíveis com este Contrato.

#### 4. Categorias de titulares

- clientes finais da Loja;
- representantes ou contactos de clientes empresariais;
- funcionarios/utilizadores da Loja;
- fornecedores ou contactos associados a reparacoes, quando inseridos pela Loja.

#### 5. Categorias de dados pessoais

- nome;
- telefone;
- email;
- NIF;
- morada, se inserida;
- dados de equipamento;
- IMEI/serial;
- avaria, diagnostico, notas e historico de reparacao;
- fotos antes/depois, quando a funcionalidade existir;
- comunicacoes e templates, quando a funcionalidade existir;
- dados de pagamento/faturacao externa, quando integracoes existirem;
- logs tecnicos associados ao uso do sistema.

O RepairDesk nao foi desenhado para tratar categorias especiais de dados do artigo 9.º do RGPD. A Loja nao deve inserir dados de saude, biometria, origem racial/etnica, opinioes politicas, religiao, vida sexual ou outros dados sensiveis, salvo se tiver base legal propria e acordo escrito especifico.

#### 6. Instrucoes da Loja

O RepairDesk tratara os dados apenas:

- para prestar o servico;
- de acordo com estes Termos, DPA e configuracoes feitas pela Loja;
- mediante instrucoes documentadas da Loja;
- quando exigido por lei aplicavel.

Se o RepairDesk entender que uma instrucao viola o RGPD ou outra norma de protecao de dados, informara a Loja, salvo se a lei o impedir.

#### 7. Confidencialidade

O RepairDesk garante que pessoas com acesso a dados pessoais estao sujeitas a dever de confidencialidade.

O acesso interno deve obedecer ao principio do menor privilegio e ser limitado ao necessario para suporte, manutencao, seguranca e prestacao do servico.

#### 8. Medidas tecnicas e organizativas

O RepairDesk implementara medidas adequadas ao risco, incluindo, conforme aplicavel:

- isolamento logico por loja/tenant;
- autenticacao por utilizador;
- controlo de permissoes;
- encriptacao em transito via HTTPS/TLS;
- hashing de palavras-passe;
- backups;
- logs de seguranca;
- atualizacoes de seguranca;
- restricao de acesso administrativo;
- procedimentos de resposta a incidentes;
- minimizacao de dados em logs;
- testes e revisoes tecnicas razoaveis.

Anexo tecnico minimo:

| Medida | Estado MVP |
|---|---|
| HTTPS/TLS em producao | Obrigatorio |
| Password hashing forte | Obrigatorio |
| Separacao por tenant | Obrigatorio |
| Backups encriptados | Obrigatorio antes de beta real |
| Logs com retencao definida | Obrigatorio |
| MFA para administradores internos | Recomendado antes de beta; obrigatorio antes de publico |
| Audit log de acoes criticas | Recomendado beta; obrigatorio publico |
| Export por tenant | Obrigatorio antes de publico |

#### 9. Subcontratantes posteriores

A Loja autoriza o RepairDesk a recorrer a subcontratantes posteriores para prestar o servico, desde que estes estejam sujeitos a obrigacoes de protecao de dados substancialmente equivalentes.

Lista inicial / prevista:

| Subcontratante | Finalidade | Localizacao prevista | Estado |
|---|---|---|---|
| Hosting/cloud provider a definir | alojamento app, API, base de dados | UE/EEE preferencial | Obrigatorio escolher antes da beta |
| Provider de email transacional a definir | emails de login, suporte, notificacoes | UE/EEE preferencial | Pendente |
| Provider de backups/storage a definir | backups e ficheiros | UE/EEE preferencial | Pendente |
| Cloudflare R2 ou equivalente | storage de fotos/documentos | Regiao UE se usado | Futuro |
| Moloni/InvoiceXpress/Vendus ou equivalente | faturacao certificada, se ativada | Portugal/UE | Futuro |
| Meta/WhatsApp ou provider WhatsApp | mensagens WhatsApp, se ativado | Possivel transferencia internacional | Futuro - requer avaliacao |
| Stripe/Mollie/Easypay/SIBS | pagamentos, se ativado | UE/EEE ou garantias adequadas | Futuro |

O RepairDesk deve manter lista atualizada de subcontratantes. Alteracoes relevantes devem ser comunicadas a Loja com antecedencia razoavel, quando possivel, permitindo objecao fundamentada por motivos de protecao de dados.

#### 10. Transferencias internacionais

O RepairDesk deve tentar manter dados no EEE sempre que razoavel.

Se houver transferencia internacional de dados pessoais, o RepairDesk deve assegurar mecanismo valido nos termos dos artigos 44.º a 49.º do RGPD, como decisao de adequacao ou clausulas contratuais-tipo.

Integracoes futuras com WhatsApp/Meta ou outros fornecedores fora do EEE exigem avaliacao previa.

#### 11. Apoio aos direitos dos titulares

A Loja e responsavel por responder aos pedidos dos titulares dos dados.

O RepairDesk ajudara a Loja, dentro do razoavel e tendo em conta a natureza do tratamento, a cumprir pedidos de:

- acesso;
- retificacao;
- apagamento;
- limitacao;
- portabilidade;
- oposicao.

Se um cliente final contactar diretamente o RepairDesk, o RepairDesk encaminhara o pedido para a Loja, salvo obrigacao legal diferente.

#### 12. Violacoes de dados pessoais

O RepairDesk notificara a Loja sem demora injustificada apos tomar conhecimento de uma violacao de dados pessoais que afete dados tratados por conta da Loja.

Objetivo operacional RepairDesk:

- aviso inicial a Loja em ate 24 horas apos confirmacao razoavel do incidente;
- informacao progressiva a medida que for conhecida;
- apoio a Loja para avaliar notificacao a CNPD e titulares.

A notificacao inicial deve incluir, se disponivel:

- natureza do incidente;
- categorias e volume aproximado de dados afetados;
- categorias e numero aproximado de titulares afetados;
- medidas ja tomadas;
- medidas recomendadas;
- ponto de contacto.

A Loja continua responsavel por notificar a CNPD quando legalmente exigido, sem prejuizo de o RepairDesk apoiar esse processo.

#### 13. Apagamento e devolucao no fim do contrato

No fim da subscricao, a Loja pode exportar os seus dados durante o periodo definido nos Termos.

Apos esse periodo, o RepairDesk apagagara ou anonimizara os dados dos sistemas ativos, salvo quando a conservacao seja exigida por lei ou necessaria para defesa de direitos.

Os dados em backups serao apagados por rotacao tecnica, normalmente em 30 a 90 dias.

#### 14. Auditoria e informacao

O RepairDesk disponibilizara a Loja informacao razoavel para demonstrar cumprimento deste DPA.

Durante a fase MVP, auditorias presenciais ou tecnicas extensas podem ser substituidas por:

- questionario de seguranca;
- resumo de medidas tecnicas;
- evidencias documentais;
- reuniao de esclarecimento.

Auditorias mais profundas devem ser combinadas com antecedencia, limitadas ao necessario, sujeitas a confidencialidade e sem comprometer seguranca ou dados de outras lojas.

Este ponto deve ser revisto por advogado antes de clientes maiores.

#### 15. Responsabilidade

A responsabilidade comercial entre as partes segue os Termos de Servico ou contrato principal, sem prejuizo dos direitos dos titulares e competencias das autoridades de controlo previstos no RGPD.

Validar esta clausula com advogado.

#### 16. Lei aplicavel

Este Contrato e regido pela lei portuguesa e pelo direito da Uniao Europeia aplicavel em materia de protecao de dados.

Assinaturas:

Pela Loja: ___________________________ Data: ____ / ____ / ______

Pelo RepairDesk/LopesTech: ___________________________ Data: ____ / ____ / ______

---

## 7. Anexo D - Politica de Cookies

Texto pronto a publicar em `/cookies`.

### Politica de Cookies

Ultima atualizacao: 2026-05-16

Esta politica explica como a LopesTech usa cookies e tecnologias semelhantes no site e no RepairDesk.

#### 1. O que sao cookies

Cookies sao pequenos ficheiros guardados no teu dispositivo pelo browser. Podem servir para manter a sessao iniciada, guardar preferencias, medir utilizacao do site ou apoiar marketing.

#### 2. Que tipos de cookies usamos

| Tipo | Para que servem | Consentimento |
|---|---|---|
| Essenciais | login, seguranca, sessao, preferencias tecnicas, protecao contra abuso | Nao exigem consentimento |
| Analitica | perceber paginas visitadas, erros, funis e uso agregado | Exigem consentimento antes de ativar |
| Marketing | medir campanhas, remarketing, pixels de publicidade | Exigem consentimento antes de ativar |

#### 3. Cookies essenciais

Sao necessarios para o site/app funcionar. Sem estes cookies, nao seria possivel iniciar sessao, manter seguranca ou guardar certas preferencias.

Exemplos:

- cookie de sessao/autenticacao;
- preferencia de idioma/tema;
- cookie de consentimento;
- protecao anti-CSRF ou antifraude, se aplicavel.

#### 4. Cookies de analitica

So serao usados se deres consentimento. Servem para perceber como o site e usado e melhorar o produto.

Exemplos futuros:

- PostHog, Plausible, Umami, Google Analytics ou equivalente.

Recomendacao RepairDesk: preferir Plausible/Umami self-hosted ou analytics privacy-friendly sem cookies quando possivel.

#### 5. Cookies de marketing

So serao usados se deres consentimento. Servem para medir campanhas ou publicidade.

Estado atual recomendado: nao usar cookies de marketing antes da beta.

#### 6. Como gerir preferencias

Podes aceitar, rejeitar ou alterar preferencias de cookies nao essenciais a qualquer momento atraves do link "Preferencias de cookies" no rodape do site.

Tambem podes apagar cookies nas definicoes do browser.

---

## 8. Anexo E - Banner de cookies

### Versao recomendada para MVP sem analytics

Se o site/app usar apenas cookies essenciais:

```text
Usamos apenas cookies essenciais para o site funcionar e manter a tua sessao segura.

[Percebi] [Saber mais]
```

Isto nao deve bloquear a navegacao.

### Versao com analytics

Se houver analytics com cookies ou identificadores:

```text
Usamos cookies essenciais para o RepairDesk funcionar. Com a tua autorizacao, usamos tambem cookies de analitica para perceber como o site e usado e melhorar o produto.

[Aceitar todos] [Rejeitar nao essenciais] [Gerir preferencias]
```

Painel de preferencias:

```text
Preferencias de cookies

[x] Essenciais
Necessarios para login, seguranca e funcionamento. Estao sempre ativos.

[ ] Analitica
Ajudam-nos a perceber utilizacao agregada e melhorar o produto.

[ ] Marketing
Ajudam-nos a medir campanhas. Atualmente nao usamos esta categoria.

[Guardar preferencias]
```

Regras tecnicas:

- Nao carregar analytics/marketing antes do consentimento.
- "Rejeitar nao essenciais" deve ser tao facil como "Aceitar todos".
- Guardar consentimento com versao da politica, timestamp e categorias.
- Link permanente para alterar consentimento.
- Nao usar dark patterns.

Implementacao simples:

- Guardar `cookie_consent_v1` em localStorage ou cookie essencial.
- Valor: `{ analytics: false, marketing: false, version: "2026-05-16", acceptedAt: "..." }`
- So inicializar scripts de analytics se `analytics === true`.
- Para app autenticada, opcionalmente sincronizar preferencia no perfil do utilizador.

---

## 9. Procedimentos internos

### 9.1 Pedido de acesso, retificacao, apagamento ou portabilidade

Canal: `privacidade@lopestech.pt`

Prazo RGPD:

- responder sem demora injustificada e, em regra, no prazo de 1 mes;
- pode ser prorrogado em casos complexos, informando o titular.

Fluxo:

1. Registar pedido em `PrivacyRequests`.
2. Confirmar identidade razoavelmente.
3. Identificar se o pedido e:
   - utilizador SaaS / lead / cliente LopesTech;
   - cliente final de uma loja.
4. Se for cliente final de loja, encaminhar para a loja responsavel e apoiar tecnicamente.
5. Recolher dados relevantes.
6. Aplicar acao: acesso, correcao, export, apagamento, limitacao.
7. Responder com linguagem simples.
8. Guardar registo da resposta.

Tabela tecnica recomendada:

| Campo | Tipo |
|---|---|
| Id | uuid |
| ReceivedAt | datetime |
| RequesterEmail | string |
| RequesterName | string/null |
| TenantId | uuid/null |
| RequestType | Access/Rectification/Erasure/Portability/Objection/Restriction |
| IsEndCustomerRequest | bool |
| Status | Open/InProgress/WaitingForController/Completed/Rejected |
| DueAt | datetime |
| ClosedAt | datetime/null |
| Notes | text |

### 9.2 Pedido de apagamento de cliente final

Como subcontratante, RepairDesk nao deve apagar dados de cliente final sem instrucao da loja, salvo obrigacao legal.

Fluxo:

1. Receber pedido.
2. Identificar loja associada.
3. Encaminhar para contacto admin da loja.
4. Aguardar instrucao documentada.
5. Executar apagamento/anonimizacao conforme instrucao e limites legais.
6. Confirmar a loja.

Nota de produto:

- Implementar `soft delete` para operacao normal.
- Para pedido RGPD real, criar funcao de anonimização: nome -> "Cliente anonimizado", telefone/email/NIF removidos, mantendo dados financeiros/reparacao quando necessario para obrigacao legal da loja.
- A decisao entre apagar e anonimizar deve ser da loja, com apoio de advogado/contabilista quando existirem faturas/documentos legais.

### 9.3 Data breach - primeiras 72 horas

Base: RGPD artigos 33.º e 34.º; orientacao CNPD.

Objetivo:

- descobrir rapidamente o que aconteceu;
- conter o incidente;
- avisar lojas afetadas;
- permitir que a loja cumpra a sua obrigacao de notificar CNPD quando necessario;
- se a LopesTech for responsavel pelo tratamento naquele contexto, notificar diretamente a CNPD quando aplicavel.

#### 0-2 horas

- Abrir incidente interno.
- Congelar logs relevantes.
- Identificar sistemas afetados.
- Conter: revogar tokens, bloquear contas, desligar endpoint, rodar chaves, se necessario.
- Nomear dono do incidente: Bruno.

#### 2-12 horas

- Determinar se ha dados pessoais.
- Determinar se dados sao:
  - dados da LopesTech como responsavel;
  - dados de lojas como subcontratante;
  - ambos.
- Estimar titulares afetados, categorias de dados, tenants afetados.
- Classificar risco: baixo / risco / risco elevado.
- Preparar timeline.

#### 12-24 horas

- Notificar lojas afetadas sem demora injustificada, se dados delas forem afetados.
- Se a LopesTech for responsavel pelo tratamento e houver risco, preparar notificacao CNPD.
- Preparar comunicacao preliminar mesmo que ainda haja informacao incompleta.

#### 24-72 horas

- Submeter notificacao CNPD se obrigatoria.
- Atualizar lojas com detalhes.
- Se houver risco elevado para titulares, preparar comunicacao aos titulares com a loja.
- Documentar tudo, mesmo que se conclua que nao era notificavel.

Registo minimo de breach:

| Campo | Conteudo |
|---|---|
| IncidentId | identificador |
| DetectedAt | quando foi detetado |
| ConfirmedAt | quando houve confirmacao razoavel |
| Systems | sistemas afetados |
| Tenants | lojas afetadas |
| DataCategories | categorias de dados |
| DataSubjectsEstimate | estimativa titulares |
| RiskAssessment | baixo/risco/elevado |
| Notifications | quem foi notificado e quando |
| MeasuresTaken | contencao e correcao |
| LessonsLearned | melhorias |

### 9.4 Retencao de logs

Recomendacao MVP:

| Tipo de log | Conteudo | Retencao | Nota |
|---|---|---|---|
| App logs tecnicos | erros, endpoint, tenantId/userId, requestId | 30 dias | Sem dados pessoais de cliente final no corpo |
| Security logs | login, falhas login, reset password, alteracoes role | 12 meses | Necessario para investigacao |
| Audit logs negocio | criar/editar/apagar reparacao, export, acesso admin | 24 meses | Pode ser mais se clientes Pro/Enterprise exigirem |
| Web server logs | IP, user agent, URL | 30-90 dias | Minimizar querystrings |
| Breach records | registo incidentes | 5 anos | Defesa e accountability |

Regras:

- Nao logar passwords, tokens, NIFs, IMEIs, telefones ou conteudo de notas.
- Mascarar dados quando necessario: `912***678`, `263***141`.
- Separar logs por ambiente.
- Acesso a logs apenas a admins tecnicos.

### 9.5 Backups e encriptacao

Antes da beta com lojas reais:

- backups automaticos da base de dados;
- backups encriptados em repouso;
- encriptacao em transito;
- acesso restrito por credenciais separadas;
- rotacao 30-90 dias;
- teste de restore mensal;
- documentar RPO/RTO.

Recomendacao inicial:

| Item | Valor MVP |
|---|---|
| Frequencia backup DB | diario |
| Retencao | 30 dias diario + 3 mensais, se custo permitir |
| Encriptacao | obrigatoria |
| Restore test | mensal |
| RPO alvo | 24h |
| RTO alvo | 24-48h em beta |

---

## 10. Registo de atividades de tratamento

Mesmo sendo pequeno, manter registo simples. A CNPD disponibiliza modelos para responsaveis e subcontratantes.

### 10.1 Registo como responsavel - LopesTech

| Atividade | Finalidade | Dados | Base legal | Retencao |
|---|---|---|---|---|
| Gestao de contas SaaS | prestar RepairDesk | nome, email, role, login | contrato | duracao da conta + 90 dias |
| Faturacao LopesTech | cobrar subscricao | empresa, NIF, morada, plano, pagamentos | contrato/obrigacao legal | prazo fiscal/contabilistico |
| Suporte | responder pedidos | email, mensagens, logs associados | contrato/interesse legitimo | 24 meses |
| Marketing opt-in | novidades/produto | email, nome, consentimento | consentimento | ate retirar consentimento |
| Site analytics | melhorar site | cookies/identificadores | consentimento | conforme ferramenta |
| Seguranca | proteger servico | IP, login, user agent, eventos | interesse legitimo | 90 dias a 12 meses |

### 10.2 Registo como subcontratante - RepairDesk por conta das lojas

| Categoria | Descricao |
|---|---|
| Responsaveis | lojas/oficinas clientes |
| Finalidade | gestao de reparacoes, clientes, dispositivos, documentos e comunicacoes |
| Dados | clientes finais, contactos, NIF, equipamentos, IMEI, reparacoes, fotos futuras, mensagens futuras |
| Titulares | clientes finais das lojas, funcionarios, contactos |
| Sub-processadores | hosting, storage, email, faturacao, WhatsApp, pagamentos quando ativados |
| Transferencias | preferir EEE; avaliar Meta/WhatsApp e outros fora EEE |
| Medidas | HTTPS, tenant isolation, backups, controlo acesso, logs, minimizacao |
| Retencao | conforme contrato/instrucoes da loja; export/apagamento no cancelamento |

---

## 11. Riscos identificados

| Risco | Severidade | Estado | Mitigacao |
|---|---|---|---|
| Sem DPA com lojas beta | Critico | Atual | Assinar DPA antes de dados reais |
| Logs com dados pessoais sem retencao | Alto | Atual | Definir retencao e mascarar payloads |
| Backups sem politica/encriptacao | Alto | Atual | Implementar backups encriptados antes da beta |
| Sub-processador fora UE sem SCC/TIA | Alto | Futuro | Preferir UE; validar contratos e transferencias |
| WhatsApp/Meta envolve transferencias e metadata | Alto | Futuro | DPIA/avaliacao antes de automatizar |
| Fotos antes/depois podem conter dados excessivos | Medio/Alto | Futuro | Instrucoes UX, consentimento/base legal pela loja, blur/limpeza metadata |
| IMEI pode ser dado pessoal indireto | Medio | Atual/futuro | Minimizar, proteger, evitar exposicao em logs |
| Portal publico `/r/{slug}` expor reparacao | Alto | Atual/futuro | slugs fortes, rate limiting, sem NIF/telefone, opcional PIN |
| Cookie analytics sem consentimento | Medio | Pendente | Bloquear analytics ate consentimento |
| Bruno como DPO interino sem independencia | Medio | Atual | Tratar como contacto privacidade; avaliar se DPO formal e obrigatorio |
| Falta DPIA se escala aumentar | Medio/Alto | Futuro | Fazer DPIA antes de fotos+WhatsApp+IMEI matching em escala |
| Termos limitacao responsabilidade mal calibrada | Alto | Pendente | Validar com advogado |

---

## 12. DPIA - e necessario?

Para a beta com 2-3 lojas, sem fotos obrigatorias, sem WhatsApp automatico e sem matching externo de IMEI, a DPIA provavelmente nao e obrigatoria, mas uma mini-avaliacao de risco e recomendada.

Fazer DPIA antes de:

- fotos de equipamentos/clientes em volume;
- WhatsApp automatizado e historico de mensagens;
- integracao com Meta/WhatsApp Business;
- matching de IMEI com bases externas;
- analytics comportamental detalhada por funcionario;
- 20+ lojas com muitos clientes finais;
- decisoes automaticas relevantes sobre clientes ou equipamentos.

Razao:

O EDPB indica que DPIA e obrigatoria quando o tratamento for suscetivel de resultar em alto risco, e aponta criterios como larga escala, dados de natureza altamente pessoal, combinacao de datasets, monitorizacao sistematica, tecnologias inovadoras e impedimento de exercicio de direitos/servicos.

---

## 13. Checklist tecnica de implementacao

### Antes da beta com dados reais

- [ ] Publicar `/privacidade`.
- [ ] Publicar `/termos`.
- [ ] Publicar `/cookies`.
- [ ] Adicionar banner se houver analytics/marketing.
- [ ] Criar PDF DPA e assinar com cada loja beta.
- [ ] Escolher hosting/cloud e confirmar regiao.
- [ ] Criar lista real de sub-processadores.
- [ ] Configurar backups encriptados.
- [ ] Definir retencao Serilog/app logs.
- [ ] Remover dados pessoais de logs.
- [ ] Garantir HTTPS em producao.
- [ ] Confirmar password hashing forte.
- [ ] Confirmar tenant isolation em todos endpoints.
- [ ] Criar email `privacidade@lopestech.pt`.
- [ ] Criar procedimento de data breach em ficheiro interno.
- [ ] Criar registo simples de atividades de tratamento.
- [ ] Adicionar consent/version tracking para Termos e Privacy.

### Campos tecnicos recomendados

Utilizador:

- `TermsAcceptedAt`
- `TermsVersion`
- `PrivacyAcceptedAt`
- `PrivacyVersion`
- `MarketingConsentAt`
- `MarketingConsentWithdrawnAt`

Tenant:

- `DpaAcceptedAt`
- `DpaVersion`
- `DpaAcceptedByUserId`
- `DataRegion`
- `DataRetentionPolicy`
- `ExportRequestedAt`
- `DeletionScheduledAt`

Cookie consent:

- `ConsentId`
- `UserId` ou anonymous id
- `Version`
- `AnalyticsAccepted`
- `MarketingAccepted`
- `CreatedAt`
- `UpdatedAt`

Data requests:

- tabela `PrivacyRequests`, conforme secao 9.1.

Breach:

- tabela ou ficheiro controlado `SecurityIncidents`, conforme secao 9.3.

### Depois da beta / antes publico

- [ ] Advogado revê Termos + DPA.
- [ ] Politica de sub-processadores publica.
- [ ] Processo formal de exportacao por tenant.
- [ ] Processo de apagamento/anonimizacao por tenant.
- [ ] MFA para admins internos.
- [ ] Audit log de acoes criticas.
- [ ] Pagina status/incidentes simples.
- [ ] Revisao de seguranca basica antes de abrir publico.

---

## 14. Roadmap MVP legal -> full compliance

| Fase | Quando | Entregaveis | Pode esperar? |
|---|---|---|---|
| 0 - Esta semana | antes de qualquer loja real | Privacy, Termos, Cookies, DPA, email privacidade | Nao |
| 1 - Pre-beta | antes de beta 2-3 lojas | backups, logs, tenant isolation verificado, sub-processadores listados | Nao |
| 2 - Beta | primeiras lojas | registo atividades, privacy requests, breach procedure testado | Parcial |
| 3 - Pre-publico | antes pricing publico | advogado revê documentos, export/delete flows, MFA admin, audit logs | Nao para publico |
| 4 - Produto maduro | 10-20 lojas | DPIA se features crescerem, pagina sub-processadores, security page | Sim |
| 5 - Escala | 50+ lojas | DPO externo se fizer sentido, pentest, ISO-like controls, DPAs automatizados | Sim |

---

## 15. Lista "advogado precisa rever"

Prioridade alta:

- Termos de Servico: limitacao de responsabilidade, foro, cancelamento, beta disclaimers.
- DPA: auditorias, responsabilidade, sub-processadores, transferencias internacionais.
- Politica de Privacidade: bases legais e retencoes finais.
- Cookies: implementacao concreta se usares Google Analytics/Meta Pixel/ads.

Prioridade media:

- Se Bruno deve ou nao designar DPO formal agora. A CNPD indica que empresas so sao obrigadas em certos casos, como tratamento em larga escala de dados sensiveis/criminais ou controlo regular e sistematico em larga escala. Para MVP, provavelmente nao, mas validar quando o produto crescer.
- DPIA para fotos, WhatsApp e IMEI matching.
- Clausulas com providers fora da UE.

Prioridade futura:

- Contratos Enterprise.
- SLA pago.
- Certificacoes ou auditorias de seguranca.

---

## 16. Decisoes recomendadas para o Bruno

1. **Nao te apresentes como DPO formal ja.** Usa "contacto de privacidade". Se designares DPO formal, ha deveres de independencia, comunicacao e publicacao de contactos.
2. **Nao uses Google Analytics/Meta Pixel no MVP.** Usa analytics sem cookies ou so ativa apos consentimento. Menos risco, menos banner chato.
3. **Escolhe infraestrutura UE por defeito.** Isto corta metade das dores de transferencias internacionais.
4. **Nao guardes payloads completos em logs.** O Serilog pode ser excelente ou uma fuga silenciosa. Mascara dados.
5. **DPA antes de dados reais.** Mesmo lojas amigas. Amizade nao substitui artigo 28.º.
6. **Export gratis e cancelamento simples.** Isto tambem e compliance cultural: dados sao do cliente.
7. **DPIA quando entrarem fotos + WhatsApp + IMEI externo.** Antes disso, mini-avaliacao chega para beta pequena.

---

## 17. Mini-plano de 1 dia para publicar

Manha:

1. Criar paginas no site:
   - `/privacidade`
   - `/termos`
   - `/cookies`
2. Criar email `privacidade@lopestech.pt`.
3. Colocar links no footer.

Tarde:

4. Gerar DPA em PDF com dados da loja beta.
5. Confirmar se ha analytics. Se sim, implementar banner; se nao, usar aviso simples de cookies essenciais.
6. Criar ficheiro interno `RGPD-Operacoes.md` com:
   - processo pedido direitos;
   - processo breach;
   - lista sub-processadores;
   - retencao logs/backups.

Fim do dia:

7. Enviar a primeira loja beta:

```text
Ola {nome},

Antes de colocarmos dados reais no RepairDesk, envio-te os documentos base:

- Termos de Servico
- Politica de Privacidade
- Contrato de Processamento de Dados

Resumo simples: os dados dos teus clientes continuam a ser teus; a LopesTech trata-os apenas para prestar o RepairDesk; podes exportar os dados; e se houver algum incidente relevante eu aviso-te sem rodeios.

Bruno
```

---

## 18. Fontes e notas para manter vivo

Rever este documento quando:

- muda o hosting/storage;
- entra WhatsApp Business;
- entra upload de fotos;
- entra provider de faturacao;
- entra pagamentos;
- existem mais de 10 lojas reais;
- ha primeiro cliente Enterprise;
- ha incidente de seguranca;
- ha alteracao relevante da CNPD/EDPB.

Documentos relacionados:

- `Contexto/10-Compliance-PT.md` - fiscalidade, SAF-T, ATCUD, faturacao.
- `Contexto/11-WhatsApp-Templates.md` - opt-in e templates WhatsApp.
- `Contexto/12-Onboarding-Wizard.md` - onboarding e eventos, com nota para nao enviar dados pessoais em analytics.
