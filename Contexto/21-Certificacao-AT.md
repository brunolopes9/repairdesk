# Certificacao AT - Plano Operacional

Atualizado: 2026-05-16  
Projeto: RepairDesk SaaS PT  
Escopo: Fase 3 apenas - certificar o RepairDesk como programa proprio de faturacao junto da Autoridade Tributaria e Aduaneira.

> Documento operacional para decisao de produto. Nao substitui parecer de contabilista certificado, advogado fiscalista, consultor de certificacao ou resposta escrita da AT. Tudo o que nao esteja confirmado em fonte oficial esta marcado com `{{confirmar}}`.

## Decisao recomendada

**Nao certificar ja.**

Para o RepairDesk, a estrategia correta continua a ser:

1. **Fase 1:** RepairDesk sem emissao fiscal. Orcamentos, fichas e garantias com "Este documento nao serve de fatura".
2. **Fase 2:** integrar provider certificado, como Moloni/InvoiceXpress/outro, e deixar o provider emitir os documentos fiscais.
3. **Fase 3:** certificar modulo proprio apenas quando houver tracao suficiente, budget e tempo para manter compliance fiscal como produto core.

Certificacao propria so faz sentido quando uma destas for verdade:

| Gatilho | Sinal pratico |
|---|---|
| Receita SaaS suficiente | 100+ lojas pagantes ou modulo fiscal capaz de gerar 1.500-3.000 EUR MRR incremental. |
| Provider externo limita produto | API impede workflow de reparacao, documentos, multi-loja, serie por tenant ou UX. |
| Custo provider fica relevante | 100 lojas x 10-15 EUR/mes por conta/provider comeca a justificar motor proprio. |
| Bruno tem runway tecnico | 3-6 meses sem bloquear features mais urgentes. |
| Ha apoio fiscal dedicado | Contabilista + consultor tecnico/fiscal disponiveis para rever casos reais. |

Leitura honesta: a certificacao AT e gratis na taxa oficial, mas nao e "barata". O custo real e tempo, risco, manutencao e responsabilidade fiscal.

## Quem certifica

Quem certifica e a **Autoridade Tributaria e Aduaneira (AT)**.

Nao e a OCC. Nao e um auditor privado. Nao identifiquei entidade terceira "autorizada" que substitua a AT neste processo.

| Entidade | Papel real |
|---|---|
| AT | Recebe o pedido, analisa, pode pedir testes de conformidade, emite numero de certificado e publica/lista o programa. |
| OCC / Contabilista Certificado | Ajuda a validar enquadramento fiscal, taxas, regimes de IVA, motivos de isencao, documentos e uso real. Nao certifica software. |
| Consultor tecnico de certificacao | Pode preparar o produto, SAF-T, hash, Modelo 24, testes e respostas a AT. Nao certifica. |
| ASSOFT | Associacao do setor de software; pode ajudar em contactos/boas praticas. Nao substitui a AT. |

Fonte oficial: o gov.pt identifica o servico "Programa de faturacao - certificacao" como responsabilidade da AT e indica que permite aos produtores de software certificar programas de faturacao.

## Processo operacional

### Visao geral

```text
0. Decisao go/no-go
1. Congelar escopo fiscal
2. Implementar motor fiscal
3. Gerar chaves e documentos de teste
4. Validar SAF-T e assinaturas
5. Preparar dossier de certificacao
6. Submeter Modelo 24 + chave publica no Portal das Financas
7. Responder a testes/perguntas da AT
8. Receber certificado
9. Publicar versao certificada
10. Manter compliance por release
```

### Onde submeter

Via online no Portal das Financas, atraves do servico:

- gov.pt: `Programa de faturacao - certificacao`
- Portal das Financas: declaracao **Modelo 24**
- Autenticacao: NIF e senha individual do Portal das Financas

O servico de consulta de estado indica que o pedido de certificacao e submetido atraves da declaracao Modelo 24 entregue no Portal das Financas por via eletronica.

### Formulario

Formulario base:

- **Modelo 24 - Certificacao de programa de faturacao**
- Publicado pela **Declaracao n.º 169/2010**, de 12/08, com instrucoes
- Entrega eletronica no Portal das Financas

Elementos minimos oficiais:

| Elemento | Fonte | Nota RepairDesk |
|---|---|---|
| Declaracao de modelo oficial | Portaria 363/2010, artigo 4.º | Modelo 24. |
| Chave publica | Portaria 363/2010, artigo 4.º | Par da chave privada usada para assinar documentos. |
| Chave publica e chave privada do programa | gov.pt servico certificacao | A chave privada fica sempre sob controlo do produtor. |

Elementos que a AT pode pedir:

| Elemento | Fonte | Preparar antes de submeter |
|---|---|---|
| Exemplar do programa | Portaria 363/2010, artigo 5.º, n.º 3 | Ambiente demo/staging com dados teste. |
| Documentacao necessaria | Portaria 363/2010, artigo 5.º, n.º 3 | Manual utilizador + manual tecnico. |
| Dicionario de dados | Portaria 363/2010, artigo 5.º, n.º 3 | Tabelas fiscais, campos SAF-T, estados, hashes. |
| SAF-T de teste | Pagina AT certificacao/validador | XML com documentos normais, anulados, notas credito, series. |
| Ficheiro txt com chave publica | Pagina AT certificacao | Usado na aplicacao de validacao de assinaturas. |

Codigo-fonte: nao encontrei fonte oficial que diga que o codigo-fonte e entregue por defeito. A AT pode pedir o exemplar do programa e documentacao/dicionario de dados. Se pedir acesso mais profundo, responder caso a caso com consultor.

## Requisitos tecnicos que o RepairDesk tem de cumprir

### 1. SAF-T(PT)

Obrigatorio:

- exportar SAF-T(PT) XML;
- usar estrutura XSD vigente;
- incluir documentos comerciais, clientes, produtos/servicos, impostos, totais e estados;
- garantir unicidade de documentos;
- exportar `Hash` e `HashControl`;
- nao permitir ao utilizador escolher arbitrariamente que documentos fiscais entram ou nao entram no SAF-T.

Fonte: Portaria 363/2010, artigo 3.º; Despacho 8632/2014, ponto 2.6; pagina AT SAF-T(PT).

Versao pratica a validar antes de desenvolvimento: estrutura SAF-T **1.04_01** associada a Portaria 302/2016 aparece na pagina oficial AT. Confirmar antes de implementar: `{{confirmar versao SAF-T/XSD atual antes de codigo}}`.

### 2. Assinatura/hash chain

A Portaria 363/2010, artigo 6.º, exige assinatura RSA com chave privada do produtor.

Para faturas/documentos equivalentes/taloes de venda, a mensagem a assinar concatena:

```text
InvoiceDate;SystemEntryDate;InvoiceNo;GrossTotal;HashAnteriorDaMesmaSerie
```

O Despacho 8632/2014 reforca:

- assinar faturas, documentos retificativos, documentos de transporte e documentos de conferencia com eficacia externa;
- gravar a assinatura na base de dados;
- gravar a versao da chave privada;
- no primeiro documento de uma serie/tipo, o hash anterior fica vazio;
- documentos so podem ser impressos/enviados depois de finalizados e assinados;
- documento ja assinado nao pode ser alterado em informacao fiscalmente relevante;
- mudanca do par de chaves so apos comunicacao a AT via Modelo 24 e upload da nova chave publica.

Impacto tecnico:

| Tema | Implementacao RepairDesk |
|---|---|
| Chave privada | Guardar fora da DB, em secret manager/HSM simples; acesso so ao servico fiscal. |
| Versao da chave | Campo `PrivateKeyVersion` inteiro sequencial. |
| Hash anterior | Buscar ultimo documento assinado da mesma tenant + tipo + serie, com transacao/lock. |
| Concorrencia | Lock por `TenantId + DocumentType + Series` para nunca duplicar numero/hash. |
| Alteracoes | Proibir update fiscal; corrigir com nota de credito/debito. |
| Backup restore | Se houver restore antigo, encerrar series e usar series de recuperacao conforme Despacho 8632/2014. |

### 3. ATCUD e series

Fonte: DL 28/2019, artigo 35.º; Portaria 195/2020, artigos 2.º a 4.º.

Antes de emitir, cada serie/tipo de documento tem de ser comunicada a AT para obter codigo de validacao.

O ATCUD e:

```text
ATCUD = codigoValidacaoSerie + "-" + numeroSequencial
```

Requisitos:

- comunicar identificador da serie;
- comunicar tipo de documento;
- comunicar inicio da numeracao;
- comunicar data prevista de inicio;
- guardar codigo de validacao devolvido pela AT;
- imprimir ATCUD em todos os documentos fiscalmente relevantes;
- em documentos multipagina, ATCUD em todas as paginas.

Para SaaS multi-tenant:

| Decisao | Recomendacao |
|---|---|
| Serie por loja | Sim, cada tenant/loja tem series proprias. |
| Serie anual | Sim, exemplo `FT2027`, `FS2027`, `NC2027`, por tenant. |
| Serie por doc type | Sim, uma serie/codigo por tipo de documento. |
| Reutilizar serie | Nao. Nunca repetir no mesmo contribuinte/tipo/programa. |
| Fechar serie | Implementar, mesmo que nem sempre seja obrigatorio. |

### 4. QR Code

Fonte: Portaria 195/2020, artigos 5.º e 6.º.

Obrigatorio em documentos emitidos por programas certificados:

- gerar QR segundo especificacoes tecnicas da AT;
- garantir legibilidade;
- em multipagina, QR pode constar na primeira ou ultima pagina;
- ATCUD deve ficar imediatamente acima do QR quando aplicavel.

### 5. e-Fatura / comunicacao de documentos

Fase 3 deve suportar:

- webservice e-Fatura;
- ficheiro multidocumento baseado em SAF-T como fallback;
- subutilizadores do Portal das Financas em vez de pedir senha principal;
- logs de comunicacao;
- estados de comunicacao e erros por documento;
- reenvio controlado.

Fonte: DL 198/2012, artigo 3.º; manuais AT e-Fatura.

Na comunicacao por webservice, os manuais AT usam SOAP, certificados digitais e credenciais do sujeito passivo/subutilizador. Para Comunicacao de Series, o manual indica perfil `WSE - Comunicacao e Gestao de Series por webservice`.

### 6. Controlo de acessos

Fonte: Portaria 363/2010, artigo 3.º.

O programa deve:

- obrigar autenticacao por utilizador;
- ter perfis/permissoes;
- registar quem emite/anula/comunica;
- impedir alteracao fiscal sem evidencia agregada a informacao original;
- manter audit log imutavel.

### 7. Layout/documentos

Fonte: Portaria 363/2010, artigo 6.º; Despacho 8632/2014.

Documentos assinados devem conter:

- quatro caracteres da assinatura nas posicoes 1.ª, 11.ª, 21.ª e 31.ª, separados por hifen;
- expressao `Processado por programa certificado n.º XXXX/AT`;
- data em formato permitido;
- tipo interno do documento, serie e numero sequencial;
- NIF ou "Consumidor final" quando aplicavel;
- motivo legal de isencao de IVA quando aplicavel;
- sem valores negativos indevidos; usar notas de credito/debito;
- 2.ª via preserva dados originais e indica que nao e original;
- multipagina com tipo, numero, pagina, total de paginas e valores transportados.

## Roadmap passo-a-passo

### Fase 0 - Go/no-go (1 semana)

Objetivo: decidir se vale a pena iniciar certificacao propria.

Checklist:

- [ ] Confirmar que Fase 2 provider certificado ja esta em producao ou em plano.
- [ ] Medir numero de lojas pagantes/interessadas no modulo fiscal.
- [ ] Estimar receita incremental do modulo fiscal.
- [ ] Pedir resposta e-balcao sobre duvidas concretas.
- [ ] Pedir 3 propostas: contabilista/fiscal, consultor certificacao, advogado/contrato.
- [ ] Nomear responsavel fiscal interno: Bruno, sem delegar no futuro.

Saida: decisao escrita `GO` ou `ADIAR`.

### Fase 1 - Escopo fiscal e desenho (2-3 semanas)

Objetivo: bloquear o que o RepairDesk vai certificar.

Decisoes:

- tipos de documento: FT, FS, FR, NC, ND, recibos, documentos de conferencia;
- documentos de transporte: adiar salvo necessidade real;
- multi-loja/multi-tenant: series e utilizadores por NIF;
- Artigo 53.º: motivos de isencao e frases legais;
- regime normal IVA: taxas, isencoes, autoliquidacao, intracomunitario;
- notas de credito/debito;
- anulacao/cancelamento;
- recuperacao apos falha/backup restore;
- SAF-T scope: faturacao primeiro, contabilidade so se necessario.

Entregaveis:

- especificacao fiscal;
- matriz documento -> campos -> SAF-T -> e-Fatura;
- matriz estados;
- plano de testes.

### Fase 2 - Implementacao do core fiscal (8-12 semanas)

Objetivo: ter motor fiscal testavel sem clientes reais.

Implementar:

- `FiscalDocument`;
- `FiscalSeries`;
- `FiscalDocumentLine`;
- `TaxCode`;
- `FiscalCustomerSnapshot`;
- `FiscalProductSnapshot`;
- contador transacional por serie;
- assinatura RSA/hash chain;
- PDF fiscal;
- QR;
- ATCUD;
- SAF-T export;
- notas credito/debito;
- audit log;
- permissoes;
- modo demo/testes;
- testes unitarios e integracao.

Regra de ouro: depois de assinado, nada se edita. Corrige-se com documento novo.

### Fase 3 - Webservices AT e comunicacao (4-8 semanas)

Objetivo: automatizar series e comunicacao de documentos.

Implementar:

- comunicacao de series/ATCUD;
- webservice e-Fatura;
- fallback ficheiro multidocumento;
- gestao de subutilizadores;
- certificados digitais AT;
- retries idempotentes;
- logs tecnicos sem expor senhas;
- dashboard de documentos por comunicar;
- alerta antes do prazo mensal.

Preparar:

- pedido de certificado digital de testes;
- chave publica do Sistema de Autenticacao para cifrar credenciais;
- contacto `asi-cd@at.gov.pt` se necessario, conforme manual AT.

### Fase 4 - Pre-certificacao interna (2-4 semanas)

Objetivo: reprovar internamente antes de a AT reprovar.

Testar:

- fatura normal com NIF;
- fatura consumidor final;
- fatura simplificada;
- fatura-recibo se suportada;
- nota de credito parcial;
- nota de credito total;
- anular documento quando aplicavel;
- documento com isencao Artigo 53.º;
- documento com IVA normal 23%;
- series anuais novas;
- primeira fatura de serie;
- concorrencia: 10 utilizadores a emitir ao mesmo tempo;
- restore de backup e series de recuperacao;
- SAF-T de periodo vazio;
- SAF-T com documentos anulados/retificados;
- PDF multipagina;
- QR legivel;
- ATCUD em todas as paginas;
- webservice e-Fatura em ambiente teste.

Entregaveis:

- relatorio interno de testes;
- XML SAF-T de teste;
- outputs da aplicacao de validacao AT;
- PDFs exemplo;
- dicionario de dados.

### Fase 5 - Submissao AT (1 semana para submeter, 1-3 meses para iterar)

Passos:

1. Gerar par de chaves RSA do produtor.
2. Guardar chave privada em local seguro.
3. Exportar chave publica.
4. Validar assinaturas com a aplicacao AT de validacao.
5. Preparar Modelo 24.
6. Submeter Modelo 24 + chave publica no Portal das Financas.
7. Guardar identificacao da declaracao, data e hora de rececao.
8. Acompanhar estado do pedido no servico de consulta.
9. Responder a pedidos de teste/documentacao da AT.
10. Corrigir e repetir validacoes.

Prazo oficial:

- gov.pt e Portaria 363/2010 indicam **30 dias**.
- Se houver testes de conformidade, o prazo fica suspenso ate conclusao dos testes.

Planeamento realista:

- melhor caso: 30-45 dias apos submissao;
- esperado: 2-4 meses com uma ronda de testes/correcoes;
- pior caso: 6+ meses se houver falhas de SAF-T/hash/modelo fiscal.

Marcar: `{{confirmar tempo medio real com consultor/produtor certificado em 2026}}`.

### Fase 6 - Publicacao e operacao continua

Antes de ligar a clientes:

- [ ] certificado recebido;
- [ ] programa aparece/estado confirmado na lista AT;
- [ ] numero de certificado no PDF;
- [ ] certificado no e-Fatura/webservice configurado;
- [ ] serie real comunicada e codigo de validacao obtido;
- [ ] backups e restore testados;
- [ ] logs e auditoria ativos;
- [ ] termos/DPA atualizados;
- [ ] suporte preparado para erros fiscais.

Depois:

- monitorizar documentos por comunicar;
- rever alteracoes legais;
- testar SAF-T por release;
- bloquear deploy fiscal se testes de hash/SAF-T falharem;
- manter changelog fiscal;
- confirmar com AT/consultor quando uma nova versao fiscal exige nova submissao/atualizacao.

## Prazos, validade e renovacao

| Tema | Regra operacional | Fonte / nota |
|---|---|---|
| Prazo oficial do pedido | 30 dias apos rececao da declaracao. | gov.pt e Portaria 363/2010, artigo 5.º. |
| Suspensao do prazo | Se a AT pedir testes de conformidade, o prazo fica suspenso ate conclusao dos testes. | gov.pt e Portaria 363/2010, artigo 5.º, n.º 2. |
| Validade do certificado do programa | Nao encontrei uma renovacao periodica fixa tipo "anual". O certificado fica sujeito a manutencao dos requisitos e pode ser revogado. | Portaria 363/2010, artigo 7.º; lista AT e atualizada regularmente com novos pedidos e revogacoes. |
| Versoes do programa | A lista AT inclui programas e respetivas versoes. Tratar cada release fiscalmente relevante como candidata a validacao/submissao. | `{{confirmar com AT quando uma nova versao RepairDesk exige nova submissao/Modelo 24}}`. |
| Venda/extincao da empresa/programa | Extincao da empresa proprietaria ou venda do programa certificado implica novo pedido e novo numero. | gov.pt servico certificacao. |
| Mudanca de chaves | Mudanca do par de chaves so depois de comunicacao a AT via Modelo 24 e upload da chave publica. | Despacho 8632/2014, ponto 2.1.4. |
| Certificado digital para webservices AT | Manual de Comunicacao de Series indica validade atual de 24 meses e renovacao pelo menos 1 mes antes do fim. | Manual AT Comunicacao de Series Documentais. |
| Series/ATCUD | Nao e "renovacao"; e gestao por serie/tipo/data. Para reinicio anual, criar e comunicar serie nova antes de usar. | Portaria 195/2020 e FAQ AT series/ATCUD referida em `10-Compliance-PT.md`. |

Politica RepairDesk:

- releases fiscais so saem com testes SAF-T/hash/QR/ATCUD verdes;
- qualquer alteracao em assinatura, SAF-T, series, documentos, impostos, PDF fiscal ou webservices cria uma checklist de impacto AT;
- certificado digital AT tem alerta de expiracao 60/30/14 dias;
- series anuais sao criadas antes de janeiro e bloqueadas se nao houver codigo de validacao.

## Custos 2026

### Custos oficiais confirmados

| Item | Custo | Fonte |
|---|---:|---|
| Pedido de certificacao AT | **0 EUR** | gov.pt indica "Gratuito". |
| Consulta do estado do pedido | **0 EUR** | gov.pt indica "E gratuito". |
| Consulta da lista de programas certificados | **0 EUR** | gov.pt indica "E gratuito". |

### Custos operacionais a confirmar

Estes valores nao sao taxa oficial. Sao estimativas de planeamento e devem ser substituidos por propostas reais.

| Item | Min | Esperado | Max | Nota |
|---|---:|---:|---:|---|
| Contabilista/consultor fiscal | 250 EUR | 750-2.000 EUR | 4.000 EUR | `{{confirmar com contabilista/OCC/3 propostas}}` |
| Consultor tecnico certificacao | 0 EUR | 2.000-8.000 EUR | 15.000 EUR | Se Bruno fizer tudo sozinho, custo cash baixa mas risco sobe. |
| Auditoria externa opcional | 0 EUR | 1.000-3.000 EUR | 8.000 EUR | Nao encontrei obrigatoriedade oficial; util antes de submeter. |
| Advogado/contrato white-label/provider | 0 EUR | 500-2.000 EUR | 5.000 EUR | Necessario se houver reseller/white-label. |
| Tempo Bruno | 250h | 400-700h | 1000h+ | Custo de oportunidade, nao cash. |
| Infra/testes | 0 EUR | 50-200 EUR | 500 EUR | Ambientes, dominios, storage, certificados quando aplicavel. |

### Total estimado

| Cenario | Cash | Tempo | Leitura |
|---|---:|---:|---|
| Minimo agressivo | 250-1.000 EUR | 250-400h | Bruno implementa tudo e so paga validacao pontual. Alto risco. |
| Esperado sensato | 4.000-12.000 EUR | 4-6 meses | Consultor fiscal + tecnico em pontos criticos. Melhor equilibrio. |
| Conservador | 15.000-30.000 EUR | 6-9 meses | Consultoria pesada, auditoria e varias rondas. Para quando houver receita. |

Comparacao: uma integracao Moloni/InvoiceXpress pode custar ~6-16 EUR/mes por conta/plano, mais tempo de integracao. Para 10-30 lojas, provider certificado e quase sempre mais barato que certificacao propria.

## Alternativas operacionais

### Alternativa A - Provider certificado externo

Exemplos a avaliar:

| Provider | Prova publica | Custo publico visto em 2026 | Nota |
|---|---|---:|---|
| Moloni | Certificado AT n.º 2860, API no plano Flex | Desde 6,49 EUR/mes; API no Flex 10,90 EUR/mes + IVA | Bom candidato Fase 2. Validar API multi-tenant. |
| InvoiceXpress | Certificado AT n.º 192 | Desde 6 EUR/mes; API a confirmar por plano | Historico SaaS/API forte. Validar custos por tenant. |
| Atura/outros API-first | Site anuncia API e certificacao | `{{confirmar 2026}}` | So usar se certificado constar na lista AT e houver contrato/DPA claro. |

Como funciona:

- RepairDesk cria a intencao fiscal.
- Provider certificado emite a fatura em nome da loja/tenant.
- RepairDesk guarda numero, serie, ATCUD, PDF, certificado do programa e estado de comunicacao.
- O documento fiscal e do provider certificado, nao do RepairDesk.

Decisao: **usar esta alternativa ate haver escala**.

### Alternativa B - White-label de motor fiscal certificado

Pode ser interessante se um produtor certificado vender API/white-label onde a UX e RepairDesk mas o motor fiscal e dele.

Due diligence obrigatoria:

- numero de certificado na lista AT;
- quem e o produtor responsavel;
- quem guarda chave privada;
- quem comunica series;
- quem responde a falhas legais;
- como exportar dados se mudar provider;
- SLA de mudancas legais;
- contrato/DPA;
- indemnizacao/limite responsabilidade;
- direito de migrar historico fiscal.

Risco: se parecer "RepairDesk certificou" mas na verdade e outro programa, o contrato e os documentos tem de ser cristalinos.

### Alternativa C - Comprar empresa/programa ja certificado

Nao e atalho magico.

O gov.pt indica que a extincao da empresa proprietaria dos direitos de autor ou a venda do programa certificado a outra empresa implica novo pedido de certificacao e novo numero de certificado.

Comprar empresa/programa pode trazer:

- codigo;
- equipa;
- conhecimento;
- clientes;
- historico de certificacao.

Mas nao elimina:

- due diligence legal;
- novo pedido em caso de venda/alteracao relevante;
- risco de divida fiscal/comercial;
- manutencao legislativa.

Decisao: nao recomendado para Bruno solo em fase inicial.

### Alternativa D - Portal das Financas manual

Serve para Fase 1 e para a propria LopesTech.

Vantagens:

- zero custo;
- zero certificacao RepairDesk;
- ATCUD automatico quando emitido no Portal.

Limites:

- UX fraca;
- duplicacao de trabalho;
- nao escala para 100 lojas;
- nao cria moat tecnico.

## Quando vale a pena certificar proprio

Usar esta regra:

```text
Certificar proprio so quando:
receita incremental anual esperada >= 2x custo anual de manter compliance
e
ha 6 meses de runway
e
provider externo ja provou o workflow fiscal com clientes reais
```

Exemplo:

| Lojas pagantes | Extra mensal por modulo fiscal | ARR incremental | Decisao |
|---:|---:|---:|---|
| 10 | 10 EUR | 1.200 EUR/ano | Nao certificar. |
| 30 | 10 EUR | 3.600 EUR/ano | Ainda nao. Provider externo. |
| 100 | 10 EUR | 12.000 EUR/ano | Comeca a fazer sentido se provider limitar margem/UX. |
| 100 | 20 EUR | 24.000 EUR/ano | Boa altura para preparar Fase 3. |
| 250 | 10-20 EUR | 30.000-60.000 EUR/ano | Certificacao propria provavelmente justifica. |

Para o RepairDesk, a melhor decisao e:

- ate 30 lojas: provider certificado;
- 30-100 lojas: melhorar integracao, medir custos e dores;
- 100+ lojas: iniciar projeto de certificacao propria se o modulo fiscal for diferencial comercial;
- antes de 100 lojas: so certificar se aparecer parceiro/cliente grande a pagar o custo.

## Riscos principais

| Risco | Severidade | Mitigacao |
|---|---|---|
| Certificar antes de product-market fit | Alta | Fase 2 provider; so iniciar Fase 3 com receita. |
| Hash chain errado em concorrencia | Critica | Locks transacionais por serie/tipo/tenant e testes de corrida. |
| Editar documento assinado | Critica | Imutabilidade, snapshots, NC/ND. |
| SAF-T invalido | Alta | Testes automaticos + validador AT por release. |
| Series/ATCUD mal comunicadas | Alta | Bloquear emissao sem codigo validacao. |
| Chave privada exposta | Critica | Secret manager, acesso minimo, rotacao comunicada por Modelo 24. |
| Restore de backup quebra sequencias | Critica | Runbook com fecho de series e recuperacao conforme Despacho 8632/2014. |
| Mudanca legal inesperada | Alta | Monitorizar AT/OCC/ASSOFT e reservar sprint fiscal. |
| AT pede testes adicionais | Media | Planear meses, nao semanas. |
| Comprar programa certificado sem novo pedido | Alta | Due diligence; gov.pt indica novo pedido em venda/extincao. |
| Provider externo nao cobre caso real | Media | Piloto com 3 lojas e matriz de documentos antes de escalar. |

## Lista de contactos e links uteis

### AT

| Tema | Contacto/link |
|---|---|
| Submissao certificacao | gov.pt `Programa de faturacao - certificacao` -> Portal das Financas / Modelo 24 |
| Estado do pedido | gov.pt `Consultar estado da Certificacao de Programa de Faturacao` |
| Lista certificados | gov.pt `Consultar o Programa de faturacao certificado` |
| Duvidas tecnicas | e-balcao: Autenticacao -> Registar nova questao -> Outras Obrigacoes -> Certif Softw -> Questoes tecnicas |
| Produtores software | https://faturas.portaldasfinancas.gov.pt/painelInicialProdSoftware.action |
| Certificado digital testes/chave autenticacao | `asi-cd@at.gov.pt` conforme manual AT Comunicacao de Series |

### OCC

| Tema | Contacto |
|---|---|
| OCC geral | https://portal.occ.pt/pt-pt/contacts |
| Telefone geral | 217 999 700 |
| Email geral | geral@occ.pt |
| Consultorio OCC | consultorio@occ.pt |
| Representacao Viseu | occ.viseu@occ.pt, 232 452 189 |

Nota: OCC ajuda a encontrar contabilista/validar temas fiscais, mas nao certifica o programa.

### ASSOFT

| Tema | Contacto |
|---|---|
| Associacao Portuguesa de Software | https://www.assoft.org/pt/16/contactos |
| Email | geral@assoft.org |
| Telefone | 213 617 040 |

Util para networking com produtores de software, boas praticas e eventual consultoria/encaminhamento. Nao e entidade certificadora AT.

### Providers Fase 2

| Provider | Link |
|---|---|
| Moloni planos/API/certificacao | https://www.moloni.pt/campanhas/planos-e-precos/ |
| Moloni legalidade | https://www.moloni.pt/suporte/legalidade-do-software-moloni-em-portugal |
| Moloni API | https://www.moloni.pt/dev/ |
| InvoiceXpress certificacao | https://invoicexpress.com/faqs/ix/requisitos-legais/ |
| InvoiceXpress pricing/API | https://facturacao.invoicexpress.com/ |

## Checklist antes de iniciar Fase 3

- [ ] 100+ lojas pagantes ou cliente/parceiro a financiar certificacao.
- [ ] Provider certificado Fase 2 validado com clientes reais.
- [ ] 3 propostas recebidas para consultoria fiscal/tecnica.
- [ ] Resposta e-balcao guardada para duvidas especificas.
- [ ] Escopo documental fechado.
- [ ] Budget minimo reservado: 5.000-10.000 EUR ou equivalente em tempo Bruno.
- [ ] 4-6 meses de margem sem comprometer produto core.
- [ ] Plano de testes fiscal aprovado por contabilista.
- [ ] Runbook de backups/restore cobre series fiscais.
- [ ] Decisao escrita: por que certificar agora e nao continuar com provider.

## Fontes consultadas

Fontes oficiais:

- gov.pt - Programa de faturacao - certificacao: https://www.gov.pt/servicos/programa-de-faturacao-certificacao
- gov.pt - Consultar estado da certificacao: https://www.gov.pt/servicos/consultar-estado-da-certificacao-de-programa-de-faturacao
- gov.pt - Consultar programa certificado: https://www.gov.pt/servicos/consultar-o-programa-de-faturacao-certificado
- Portaria n.º 363/2010: https://diariodarepublica.pt/dr/detalhe/portaria/363-2010-335230
- Decreto-Lei n.º 28/2019: https://diariodarepublica.pt/dr/detalhe/decreto-lei/28-2019-119622094
- Despacho n.º 8632/2014: https://diariodarepublica.pt/dr/detalhe/despacho/8632-2014-25703782
- Portaria n.º 195/2020 - QR e ATCUD: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/legislacao/diplomas_legislativos/Documents/Portaria_195_2020.pdf
- Portal das Financas - Certificacao de Software de Faturacao: https://info.portaldasfinancas.gov.pt/pt/apoio_ao_contribuinte/Negocios/Faturacao/Regras_mecanismos_comunicacao/Certificacao_programas/Certificacao_software_fatura%C3%A7%C3%A3o/Paginas/default.aspx
- Portal das Financas - SAF-T(PT): https://info.portaldasfinancas.gov.pt/pt/apoio_ao_contribuinte/Negocios/Faturacao/SAF_T_PT/SAF_T_PT_Versao_PT/Paginas/default.aspx
- Manual AT Comunicacao de Series Documentais: https://info.portaldasfinancas.gov.pt/pt/apoio_ao_contribuinte/Negocios/Faturacao/Regras_mecanismos_comunicacao/Comunicacao_de_series_ATCUD/Comunicacao_Series_a_AT_e_ATCUD/Documents/Comunicacao_de_Series_Documentais_Manual_de_Integracao_de_SW_Aspetos_Genericos.pdf
- Manual AT e-Fatura webservice: https://info.portaldasfinancas.gov.pt/pt/apoio_ao_contribuinte/Outras_entidades/Suporte_tecnologico/Webservice/e_Fatura/Documents/Comunicacao_dos_elementos_dos_documentos_de_faturacao.pdf

Fontes complementares:

- OCC contactos: https://portal.occ.pt/pt-pt/contacts
- ASSOFT contactos: https://www.assoft.org/pt/16/contactos
- Moloni planos/certificacao/API: https://www.moloni.pt/campanhas/planos-e-precos/ e https://www.moloni.pt/dev/
- InvoiceXpress certificacao: https://invoicexpress.com/faqs/ix/requisitos-legais/
