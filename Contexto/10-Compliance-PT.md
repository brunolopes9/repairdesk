# Compliance Fiscal PT - SAF-T, ATCUD, e-Fatura e Certificação AT

Atualizado: 2026-05-15
Projeto: RepairDesk SaaS - oficinas de reparação em Portugal
Fundador: Bruno Lopes / LopesTech

> Research técnico/legal para produto. Não substitui parecer da contabilista, advogado fiscalista ou confirmação escrita da AT. Antes de vender faturação fiscal dentro do RepairDesk, validar este documento com contabilista e, idealmente, consultor de certificação de software de faturação.

## Conclusão executiva

A estratégia mais segura para o RepairDesk é:

1. **Fase 1:** não emitir faturas no RepairDesk. Emitir apenas orçamentos, fichas de reparação, garantias e tracking operacional, claramente separados de documentos fiscais. A fatura é emitida no Portal das Finanças ou num software certificado externo.
2. **Fase 2:** integrar com um software/provider português certificado. O RepairDesk continua a ser o backoffice; o provider certificado emite a fatura, gera ATCUD/QR, comunica à AT e exporta SAF-T.
3. **Fase 3:** depois de validação comercial, construir e certificar o módulo próprio de faturação do RepairDesk junto da AT.

Ponto crítico: **se o RepairDesk emitir faturas pelo seu próprio software, mesmo para uma oficina pequena ou em regime de isenção do artigo 53.º do CIVA, entra no tema de software certificado**, porque o artigo 4.º, n.º 1, alínea b), do Decreto-Lei n.º 28/2019 obriga à utilização de programa certificado quando o sujeito passivo utiliza programa informático de faturação.

Outro ponto crítico: **200.000 EUR não é o limite geral para oficinas usarem software não certificado.** Esse valor aparece no artigo 29.º, n.º 3, alínea a), do CIVA para certas pessoas coletivas de direito público, organismos sem finalidade lucrativa e IPSS que pratiquem exclusivamente operações isentas. Não é a regra normal para lojas de reparação.

## 1. Decreto-Lei 28/2019 - resumo prático

O Decreto-Lei n.º 28/2019 regula o processamento de faturas e outros documentos fiscalmente relevantes e a conservação de livros, registos e documentos de suporte. Base legal: DL 28/2019, artigo 1.º.

### 1.1 Meios de processamento permitidos

Segundo o DL 28/2019, artigo 3.º, as faturas e demais documentos fiscalmente relevantes devem ser processados por uma das seguintes formas:

| Meio | Base legal | Impacto para RepairDesk |
|---|---|---|
| Programa informático de faturação, incluindo aplicações da AT | DL 28/2019, artigo 3.º, alínea a) | Se o RepairDesk emitir faturas, passa a ser programa de faturação. |
| Outros meios eletrónicos, como máquinas registadoras, terminais ou balanças | DL 28/2019, artigo 3.º, alínea b) | Pouco relevante para SaaS. Só pode ser usado para faturas simplificadas, nos termos do DL 28/2019, artigo 4.º, n.º 5, e CIVA artigo 40.º. |
| Documentos pré-impressos em tipografia autorizada | DL 28/2019, artigo 3.º, alínea c) | Alternativa manual, não produto SaaS. |

### 1.2 Quando é obrigatório software certificado

O sujeito passivo deve usar exclusivamente programas informáticos previamente certificados pela AT quando se verifique qualquer uma destas condições:

| Condição | Base legal | Leitura prática |
|---|---|---|
| Volume de negócios do ano anterior superior a 50.000 EUR | DL 28/2019, artigo 4.º, n.º 1, alínea a) | Lojas que passem este patamar devem usar software certificado. |
| Utilização de programa informático de faturação | DL 28/2019, artigo 4.º, n.º 1, alínea b) | Mesmo abaixo de 50.000 EUR, se a loja usa software para faturar, esse software tem de ser certificado. |
| Contabilidade organizada obrigatória ou por opção | DL 28/2019, artigo 4.º, n.º 1, alínea c) | Empresas com contabilidade organizada entram no obrigatório. |

**Exigência legal:** se o RepairDesk gerar faturas, faturas simplificadas, faturas-recibo, notas de crédito ou outros documentos fiscalmente relevantes com eficácia externa, deve ser tratado como programa de faturação e cumprir certificação AT.

**Boa prática:** mesmo antes da certificação própria, desenhar o modelo de dados já alinhado com SAF-T, séries, ATCUD, imutabilidade e auditoria, para não reconstruir tudo depois.

### 1.3 Quando não é obrigatório software certificado

A não obrigatoriedade só é defensável quando o sujeito passivo **não** cai nas condições do DL 28/2019, artigo 4.º, n.º 1. Exemplos:

| Situação | Base legal | Pode dispensar software certificado? |
|---|---|---|
| Loja abaixo de 50.000 EUR, sem contabilidade organizada, que não usa programa informático de faturação | DL 28/2019, artigo 4.º, n.º 1, alíneas a), b), c) | Sim, em princípio, usando Portal da AT, documentos de tipografia autorizada ou outro meio permitido. Confirmar caso concreto. |
| Sujeito passivo no artigo 53.º do CIVA que emite pelo Portal das Finanças | CIVA artigo 53.º; DL 28/2019, artigo 3.º, alínea a); FAQ AT Séries/ATCUD | Sim para o RepairDesk, porque quem emite é a aplicação da AT. |
| Sujeito passivo no artigo 53.º do CIVA que quer emitir faturas no RepairDesk | CIVA artigo 29.º, n.º 1, alínea b); CIVA artigo 53.º; DL 28/2019, artigo 4.º, n.º 1, alínea b) | Não. Se o RepairDesk for o programa de faturação, precisa de certificação. |
| Pessoas coletivas de direito público, organismos sem fim lucrativo e IPSS com operações exclusivamente isentas e rendimentos até 200.000 EUR | CIVA artigo 29.º, n.º 3, alínea a); DL 28/2019, artigo 10.º | Exceção específica. Não é a regra para oficinas de reparação. |

### 1.4 Isenção Artigo 53.º não elimina fatura

O artigo 53.º do CIVA dá isenção de IVA a sujeitos passivos que cumpram os requisitos, incluindo volume de negócios até 15.000 EUR e ausência de contabilidade organizada obrigatória, importações/exportações ou atividades excluídas. Base legal: CIVA artigo 53.º, n.º 1.

Mas o CIVA artigo 29.º, n.º 1, alínea b), mantém a obrigação de emitir fatura por cada transmissão de bens ou prestação de serviços, mesmo que o cliente não peça. A fatura deve indicar o motivo da não aplicação do imposto quando aplicável. Base legal: CIVA artigo 36.º, n.º 5, alínea e), e CIVA artigo 40.º, n.º 2, alínea e), para faturas simplificadas.

**Leitura para produto:** uma oficina em Artigo 53.º pode não liquidar IVA, mas continua a precisar de faturas. A isenção fiscal não é uma licença para o RepairDesk emitir documentos fiscais sem cumprir as regras de software.

### 1.5 Faturas simplificadas

O CIVA artigo 40.º permite faturas simplificadas em certos casos:

| Caso | Base legal | Nota de produto |
|---|---|---|
| Retalhistas/vendedores ambulantes a não sujeitos passivos até 1.000 EUR | CIVA artigo 40.º, n.º 1, alínea a) | Pode cobrir vendas balcão, se aplicável. |
| Outras transmissões de bens e prestações de serviços até 100 EUR | CIVA artigo 40.º, n.º 1, alínea b) | Limite baixo para muitas reparações. |
| Sujeitos passivos abrangidos pelo artigo 53.º | CIVA artigo 40.º, n.º 1, alínea c) | Alteração relevante para pequenas lojas em isenção. |

Mesmo em fatura simplificada, se emitida por programa informático, aplica-se o tema de certificação do DL 28/2019, artigo 4.º, n.º 1, alínea b).

### 1.6 Documentos não fiscais do RepairDesk

O DL 28/2019, artigo 2.º, alínea b), define documentos fiscalmente relevantes de forma ampla: documentos de transporte, recibos e outros documentos suscetíveis de apresentação ao cliente que permitam conferência de mercadorias ou prestação de serviços.

O DL 28/2019, artigo 7.º, n.º 2, exige que documentos de conferência processados por meios eletrónicos indiquem, entre outros dados, que **não constituem fatura**. O Despacho n.º 8632/2014, ponto 1.2, reforça que documentos não fatura suscetíveis de apresentação ao cliente devem conter expressão do tipo "Este documento não serve de fatura".

**Boa prática para Fase 1:** usar labels claros:

- Orçamento - Este documento não serve de fatura
- Ficha de Reparação - Documento operacional, não fiscal
- Relatório de Garantia - Este documento não serve de fatura

**Risco:** a FAQ e-Fatura da AT indica que faturas proforma/documentos de conferência podem ter comunicação obrigatória. Por isso, evitar chamar documentos RepairDesk de "fatura proforma" e rever layouts com contabilista.

### 1.7 Regimes transitórios e exceções

| Tema | Base legal | Estado prático em 2026 |
|---|---|---|
| Em 2019 o limite transitório era 75.000 EUR | DL 28/2019, artigo 43.º, n.º 2 | Histórico; hoje usar 50.000 EUR salvo alteração futura. |
| Comunicação de estabelecimentos/equipamentos teve calendário transitório em 2019 | DL 28/2019, artigo 43.º, n.º 4 | Histórico. Para produto novo, tratar como obrigação normal. |
| Produção de efeitos do ATCUD/séries | DL 28/2019, artigo 45.º, n.º 2; Portaria 195/2020, artigos 7.º e 8.º | Regime já em vigor. |
| Tipografias autorizadas | DL 28/2019, artigos 15.º a 18.º | Alternativa manual, não moat SaaS. |

## 2. ATCUD - Código Único de Documento

### 2.1 O que é

O ATCUD identifica univocamente um documento. Base legal: DL 28/2019, artigo 7.º, n.º 3; DL 28/2019, artigo 35.º; Portaria n.º 195/2020, artigos 2.º a 4.º.

Segundo a Portaria n.º 195/2020, artigo 3.º, o ATCUD é composto por:

~~~text
ATCUD = código de validação da série + "-" + número sequencial do documento
Exibição no documento: ATCUD:TES123TE-4561
Valor em QR/SAF-T: TES123TE-4561
~~~

A FAQ da AT confirma que o código de validação é específico por tipo de documento e série. Exemplo: a mesma série comercial para faturas e notas de crédito exige dois códigos distintos.

### 2.2 Séries e sequência

| Regra | Base legal / fonte | Impacto |
|---|---|---|
| As séries são comunicadas antes da utilização | DL 28/2019, artigo 35.º, n.º 1 | RepairDesk não pode emitir documentos fiscais sem código de validação da série. |
| AT atribui código por série | DL 28/2019, artigo 35.º, n.º 2 | Código entra no ATCUD. |
| Elementos comunicados: identificador da série, tipo de documento, início da numeração, data prevista de início | Portaria 195/2020, artigo 2.º | Modelo FiscalSeries deve guardar estes campos. |
| Numeração progressiva e contínua dentro da série | DL 28/2019, artigo 7.º, n.º 4; Despacho 8632/2014, ponto 1.6 | Nunca reutilizar ou reiniciar sequência numa série já usada. |
| Não reiniciar numeração mantendo o mesmo código | FAQ AT Séries/ATCUD, pergunta 4318 | Para reinício anual, criar série nova, ex. FT2026, FT2027. |
| ATCUD deve constar no momento da emissão | FAQ AT Séries/ATCUD, pergunta 4323 | Não preencher depois. |
| Documentos no Portal das Finanças recebem ATCUD automaticamente | FAQ AT Séries/ATCUD, pergunta 4305 | Bom para MVP manual. |

### 2.3 Como pedir séries à AT

Há duas vias oficiais:

| Via | Fonte | Uso recomendado |
|---|---|---|
| Manual no Portal das Finanças | Página AT Comunicação de Séries / ATCUD; FAQ pergunta 4536 | Fase 1, quando a loja emite manualmente no Portal. |
| Webservice | Página AT Comunicação de Séries / ATCUD com WSDL; FAQ pergunta 4536 | Fase 2/3, quando o RepairDesk ou provider certificado automatiza. |

A comunicação da série deve ser feita **antes** da emissão. Não é uma comunicação mensal; é uma comunicação por série/tipo de documento/meio de processamento antes de uso. A finalização de série existe, mas a FAQ da AT indica que não é obrigatória; é uma funcionalidade de gestão/segurança.

### 2.4 QR Code

O DL 28/2019, artigo 7.º, n.º 3, exige QR e ATCUD nas faturas e demais documentos fiscalmente relevantes, nos termos da Portaria 195/2020. A Portaria 195/2020, artigo 6.º, liga a geração do QR aos documentos emitidos por programas certificados pela AT.

A FAQ da AT indica ainda que meios não certificados como máquinas registadoras têm de exibir ATCUD, mas o QR é exigido a documentos emitidos por programas informáticos certificados.

**Produto:** quando houver emissão fiscal própria, o layout PDF/HTML tem de reservar espaço estável para ATCUD e QR, incluindo documentos multipágina.

## 3. Comunicação de Faturas à AT - e-Fatura

### 3.1 Quem comunica

O DL 198/2012, artigo 3.º, n.º 1, obriga pessoas singulares/coletivas com sede, estabelecimento estável ou domicílio fiscal em Portugal, que pratiquem operações sujeitas a IVA, a comunicar à AT os elementos das faturas emitidas nos termos do CIVA, bem como documentos de conferência e recibos abrangidos após alterações do DL 28/2019.

A FAQ e-Fatura da AT resume que estão obrigadas as entidades sujeitas às regras de emissão de faturação em território português nos termos do CIVA artigo 35.º-A e que aqui pratiquem operações sujeitas a IVA.

### 3.2 Prazo

| Fonte | Prazo indicado | Decisão de produto |
|---|---|---|
| DL 198/2012, artigo 3.º, n.º 2, versão consultada no DRE | Até dia 8 do mês seguinte | Base legal a confirmar antes de produção. |
| FAQ e-Fatura AT, pergunta 4936 | Documentos desde 2023 até dia 5 do mês seguinte | Implementar alerta/submissão até dia 5, por ser mais conservador. |

Marcar para confirmação anual com contabilista/AT: {{confirmar prazo e-Fatura vigente antes de produção}}.

### 3.3 Vias de comunicação

| Via | Base legal / fonte | Quando usar |
|---|---|---|
| Webservice / transmissão em tempo real | DL 198/2012, artigo 3.º, n.º 1, alínea a); FAQ e-Fatura pergunta 4937 | Fase 2/3, ideal para automação. |
| Ficheiro multidocumento estruturado com base em SAF-T | DL 198/2012, artigo 3.º, n.º 1, alínea b); FAQ e-Fatura pergunta 4937 | Bom fallback mensal. |
| Inserção direta no Portal das Finanças | DL 198/2012, artigo 3.º, n.º 1, alínea c); FAQ e-Fatura pergunta 4937 | Fase 1 manual. |
| Outra via eletrónica definida por portaria | DL 198/2012, artigo 3.º, n.º 1, alínea d) | Confirmar se aplicável. |

A FAQ informática da AT indica que, para webservice/ficheiro multidocumento, deve ser criado um subutilizador com perfil WFA-Comunicação de dados de faturas. Isto é importante: RepairDesk não deve pedir a senha principal das Finanças da loja.

### 3.4 Elementos comunicados

O DL 198/2012, artigo 3.º, n.º 4, e a FAQ e-Fatura pergunta 4938 indicam elementos como NIF do emitente, número do documento, data, tipo, NIF do adquirente quando aplicável, valor tributável, taxas, motivo de não aplicação de imposto, IVA/Imposto do Selo liquidado, certificado do programa, documento de origem/retificado, país/região do imposto e código único de documento.

**Produto:** guardar estes campos de forma estruturada desde o primeiro MVP, mesmo que a emissão fiscal seja externa.

### 3.5 WSDLs e certificados

A AT publica WSDLs e manuais nas páginas oficiais de e-Fatura e Comunicação de Séries/ATCUD. Para RepairDesk interessam:

| Integração | WSDL / documentação | Nota |
|---|---|---|
| Comunicação de faturas e documentos | e-Fatura - Especificação do Webservice (WSDL), normalmente referido como Fatcorews em integrações | {{confirmar nome/versão exata do WSDL em produção}} |
| Comunicação de séries / ATCUD | Comunicação de Séries Documentais - WSDL | Necessário antes de emitir documentos fiscais próprios. |
| Documentos de transporte | WSDL documentos de transporte | Só se o RepairDesk evoluir para logística/movimentação de peças com transporte. |
| Autofaturação | WSDL de autofaturação | Pouco provável no MVP. |

A tua ChaveCifraPublicaAT2027.cer ajuda na comunicação segura com webservices da AT, mas **não substitui** a certificação do programa de faturação. São temas diferentes: autenticação/cifra do canal vs certificação fiscal do software.

## 4. SAF-T (PT)

### 4.1 O que é

O SAF-T(PT) é um ficheiro XML normalizado para exportar registos contabilísticos, de faturação, documentos de transporte e recibos. Base: Portaria n.º 321-A/2007; página oficial da AT SAF-T(PT).

A página oficial da AT indica que o SAF-T permite exportar dados em qualquer altura, num formato comum, sem afetar a estrutura interna do programa, e que os programas certificados têm de exportar XML SAF-T para validação de assinaturas.

### 4.2 Estrutura XML de alto nível

Estrutura conceptual a preparar no RepairDesk:

~~~text
AuditFile
  Header
  MasterFiles
    Customer
    Supplier
    Product
    TaxTable
  GeneralLedgerEntries        (quando aplicável a contabilidade)
  SourceDocuments
    SalesInvoices
      Invoice
        DocumentStatus
        SpecialRegimes
        Line
          Tax
        DocumentTotals
    MovementOfGoods           (se houver documentos de transporte)
    WorkingDocuments          (documentos de conferência/orçamentos abrangidos)
    Payments                  (recibos/pagamentos abrangidos)
~~~

Campos fundamentais para faturação: InvoiceNo, ATCUD, InvoiceStatus, Hash, HashControl, Period, InvoiceDate, InvoiceType, SourceID, SystemEntryDate, CustomerID, Line, Tax, NetTotal, TaxPayable, GrossTotal.

Versão prática: a Portaria n.º 302/2016 estabeleceu estrutura e XSD atualizados com efeitos a 2017. Confirmar sempre a versão XSD vigente antes de implementar: {{confirmar versão SAF-T/XSD atual}}.

### 4.3 Quando exportar

| Contexto | Frequência | Base / fonte | Produto |
|---|---|---|---|
| Inspeção/auditoria tributária | Quando solicitado | Página AT SAF-T; Portaria 321-A/2007 | Export on demand por período. |
| Comunicação e-Fatura por ficheiro | Mensal, até ao prazo legal de comunicação | DL 198/2012, artigo 3.º; FAQ e-Fatura perguntas 4936/4937 | Gerar ficheiro multidocumento baseado em SAF-T. |
| Validação de software certificado | Durante certificação e manutenção | Portaria 363/2010, artigos 3.º a 5.º; página AT Certificação | Exportar ficheiros de teste e produção. |
| Contabilidade/IES/SAF-T contabilístico | Anual ou conforme regime | {{confirmar com contabilista}} | Não incluir no MVP fiscal salvo necessidade. |

### 4.4 Como submeter

| Submissão | Fonte | Nota |
|---|---|---|
| Upload no Portal e-Fatura | FAQ informática e-Fatura pergunta 4997 | Para ficheiro multidocumento até 40 MB. |
| Aplicação de linha de comandos (JAR) | FAQ informática e-Fatura pergunta 4997 | Alternativa sem limite prático indicado pela FAQ; confirmar ambiente atual. |
| Webservice | DL 198/2012, artigo 3.º, n.º 1, alínea a); FAQ e-Fatura | Envia elementos/documentos, não substitui necessariamente exportação SAF-T completa para auditoria. |
| Entrega a inspeção | Página AT SAF-T | Exportar ficheiro quando solicitado. |

## 5. Certificação de Software de Faturação

### 5.1 Base legal e técnica

| Tema | Base legal / fonte |
|---|---|
| Obrigação de programa certificado | DL 28/2019, artigo 4.º |
| Requisitos e procedimento por portaria | DL 28/2019, artigo 4.º, n.º 2 |
| Requisitos de certificação | Portaria 363/2010, artigo 3.º |
| Declaração e chave pública | Portaria 363/2010, artigo 4.º |
| Emissão do certificado | Portaria 363/2010, artigo 5.º |
| Assinatura/hash de documentos | Portaria 363/2010, artigos 6.º e 7.º |
| Requisitos técnicos detalhados | Despacho n.º 8632/2014 |
| QR e ATCUD | Portaria 195/2020 |

### 5.2 Requisitos essenciais

A Portaria 363/2010, artigo 3.º, exige cumulativamente:

- Exportação SAF-T(PT).
- Sistema de identificação/gravação de faturas e documentos retificativos com algoritmo de cifra assimétrica e chave privada exclusiva do produtor.
- Controlo de acesso com autenticação de cada utilizador.
- Impossibilidade de alterar informação fiscal sem evidência agregada à informação original.
- Cumprimento dos requisitos técnicos definidos pela AT.

O Despacho n.º 8632/2014 acrescenta regras operacionais: documentos assinados antes de imprimir/enviar, séries progressivas, impossibilidade de editar documentos já assinados, menções de programa certificado, regras de 2.ª via, documentos de formação, integração de documentos externos e expressão "Este documento não serve de fatura" quando aplicável.

### 5.3 Processo formal

| Passo | O que fazer | Base / nota |
|---|---|---|
| 1 | Implementar motor fiscal com séries, numeração, ATCUD, QR, tax codes, documentos retificativos, imutabilidade e auditoria | DL 28/2019; Portaria 195/2020; Despacho 8632/2014 |
| 2 | Implementar SAF-T(PT) e validar XML/XSD | Portaria 321-A/2007 e alterações; página AT SAF-T |
| 3 | Gerar par de chaves do produtor e assinar documentos com RSA | Portaria 363/2010, artigos 3.º, 6.º e 7.º |
| 4 | Validar SAF-T e assinaturas com aplicação da AT | Página AT Certificação de Software |
| 5 | Submeter declaração oficial e chave pública à AT | Portaria 363/2010, artigo 4.º; referência a Modelo 24 na página da AT e Despacho 8632/2014 |
| 6 | Aguardar certificado e eventuais testes de conformidade | Portaria 363/2010, artigo 5.º |
| 7 | Publicar versão certificada e manter controlo de versões | Portaria 363/2010, artigo 5.º, n.º 4 e n.º 5 |

A Portaria 363/2010, artigo 5.º, n.º 1, diz que a AT emite o certificado no prazo de 30 dias a contar da receção da declaração. Mas o n.º 2 permite testes de conformidade e suspende esse prazo até conclusão dos testes. Portanto, para planeamento de produto, assumir meses, não semanas: {{confirmar tempo real médio com consultor/fornecedor certificado}}.

### 5.4 Custos e OCC

Não identifiquei, nas fontes oficiais consultadas, uma taxa pública única/obrigatória da AT para a certificação: {{confirmar se existe taxa oficial atual}}.

Custos prováveis do projeto:

- desenvolvimento fiscal e QA;
- consultoria fiscal/técnica;
- testes de conformidade;
- manutenção contínua quando a lei/XSD/WSDL muda;
- responsabilidade legal e suporte a clientes.

Sobre a OCC: não encontrei a OCC como entidade certificadora no processo formal oficial. A certificação é da AT. A contabilista/Contabilista Certificado é essencial para validar enquadramento, parametrizações fiscais e uso real pelas lojas, mas não substitui certificação AT. Marcar: {{confirmar papel prático da OCC/contabilista no processo}}.

### 5.5 Alternativas à certificação própria

| Alternativa | Vantagem | Risco / cuidado |
|---|---|---|
| Portal das Finanças manual | Zero certificação RepairDesk; rápido para MVP | Pouca automação; UX fraca; duplicação de trabalho. |
| API de provider certificado português | Melhor caminho Fase 2; reduz risco legal | Confirmar que quem emite é software certificado e que o certificado aparece na lista AT. |
| White-label de motor fiscal certificado | Acelera go-to-market com UX RepairDesk | Contrato, responsabilidades, versionamento e suporte fiscal têm de ser muito claros. |
| Certificação própria RepairDesk | Moat forte e controlo total | Custo, tempo, risco legal e manutenção permanente. |

## 6. Tabela resumo por regime

| Regime / situação | Precisa | Não precisa / dispensa | Nota para RepairDesk |
|---|---|---|---|
| Artigo 53.º CIVA, emissão manual no Portal AT | Fatura; motivo de não liquidação; comunicação já coberta quando emitida no Portal; ATCUD automático | Certificação do RepairDesk, se o RepairDesk não emitir documento fiscal | Fase 1 recomendada para Bruno e lojas pequenas. |
| Artigo 53.º CIVA, emissão em RepairDesk | Programa certificado; séries; ATCUD; QR se programa certificado; e-Fatura; SAF-T | Não há dispensa só por estar em Artigo 53.º | CIVA artigo 53.º isenta IVA, não certificação do software. |
| Regime normal IVA, abaixo de 50.000 EUR, sem programa de faturação e sem contabilidade organizada | Fatura; e-Fatura; ATCUD se meio usado exigir; regras CIVA | Pode não precisar de programa certificado se usar meios permitidos e não cair no DL 28/2019 artigo 4.º | Caso estreito; não basear produto nisto. |
| Regime normal IVA, abaixo de 50.000 EUR, usando software | Software certificado; e-Fatura; séries; ATCUD; QR; SAF-T | Não dispensa certificação | DL 28/2019 artigo 4.º, n.º 1, alínea b). |
| Volume de negócios acima de 50.000 EUR | Software certificado | Não pode usar software não certificado | DL 28/2019 artigo 4.º, n.º 1, alínea a). |
| Contabilidade organizada obrigatória ou por opção | Software certificado | Não pode usar software não certificado | DL 28/2019 artigo 4.º, n.º 1, alínea c). |
| Entidade pública/sem fins lucrativos/IPSS com operações exclusivamente isentas e rendimentos até 200.000 EUR | Documentos específicos, se aplicável | Pode estar dispensada de fatura nos termos do CIVA artigo 29.º, n.º 3, alínea a) | Não é o caso normal de oficina de reparação. |

## 7. Fluxograma de decisão

~~~mermaid
flowchart TD
  A["Vou emitir um documento para o cliente?"] --> B{"É só interno?"}
  B -->|Sim| C["Documento operacional interno: sem valor fiscal"]
  B -->|Não| D{"Titula venda, serviço, pagamento ou conferência da reparação?"}
  D -->|Não| E["Orçamento claro: 'Este documento não serve de fatura' e rever layout"]
  D -->|Sim| F{"É fatura, fatura simplificada, recibo ou documento fiscalmente relevante?"}
  F -->|Sim| G{"Vai ser emitido no RepairDesk?"}
  F -->|Talvez| H["Tratar como risco fiscal: validar com contabilista/AT"]
  G -->|Não, Portal AT| I["Portal das Finanças: ATCUD automático, sem certificação RepairDesk"]
  G -->|Não, provider certificado| J["Guardar ID fiscal externo, PDF e estado de comunicação"]
  G -->|Sim| K{"A loja usa programa informático de faturação?"}
  K -->|Sim| L["RepairDesk precisa de certificação AT antes de emitir"]
  K -->|Não| M["Usar tipografia/Portal/outro meio permitido; RepairDesk não emite fiscal"]
  L --> N["Implementar séries, ATCUD, QR, SAF-T, hash RSA, e-Fatura, auditoria"]
~~~

## 8. Estratégia recomendada para RepairDesk

### Fase 1 - Artigo 53.º / MVP sem faturação própria

**Resposta curta:** o RepairDesk não deve emitir documentos fiscais próprios nesta fase.

Pode emitir documentos operacionais não fiscais, desde que não substituam a fatura e sejam claros. A fatura real deve ser emitida no Portal das Finanças ou em software certificado. Isto protege o MVP enquanto validas produto, preço e workflow de oficinas.

Implementar no RepairDesk:

- ExternalFiscalDocumentNumber
- ExternalFiscalProvider
- FiscalDocumentUrl/PdfPath
- FiscalStatus: NotIssued, IssuedExternally, CancelledExternally, CreditNoteExternally
- aviso no ticket: Fatura ainda não anexada
- templates com "Este documento não serve de fatura"

### Fase 2 - Regime normal abaixo de 50.000 EUR / integração certificada

**Resposta curta:** se o RepairDesk emitir a fatura, precisa de certificação; se integrar com provider certificado que emite, o RepairDesk pode ficar como backoffice operacional.

Implementar:

- integração API com provider certificado;
- sincronização de clientes, artigos/serviços, impostos e documentos;
- botão Emitir fatura que chama provider;
- guardar número, série, ATCUD, QR/PDF, certificado do programa e estado e-Fatura;
- nunca gerar PDF fiscal localmente fora do provider;
- webhooks de estado e anulação/nota de crédito.

Esta fase já é um moat forte vs concorrentes internacionais, porque resolve compliance PT sem assumir desde cedo o peso de certificação própria.

### Fase 3 - Acima de 50.000 EUR / contabilidade organizada / certificação própria

**Resposta curta:** certificação obrigatória se o RepairDesk for o programa de faturação. O limite operacional relevante é 50.000 EUR, não 200.000 EUR, e a utilização de software já obriga a certificação.

Implementar motor fiscal próprio:

- tenant fiscal profile por loja/empresa;
- séries por estabelecimento, tipo de documento e ano;
- comunicação de séries/ATCUD via webservice;
- geração de QR;
- assinatura RSA e cadeia de hash por tipo/série;
- imutabilidade de documentos emitidos;
- notas de crédito/débito como retificação, sem editar fatura original;
- SAF-T(PT) export e validação;
- comunicação e-Fatura via webservice e fallback ficheiro;
- logs de auditoria e backups;
- modo formação com série própria;
- recuperação de documentos manuais em série própria;
- restrição total de alteração de layouts fiscais por utilizador final.

## 9. Roadmap de implementação

| Fase | Objetivo | Entregáveis | Critério de saída |
|---|---|---|---|
| 0 - Validação legal | Fechar interpretação com contabilista | Parecer interno sobre Art. 53, documentos operacionais e provider | Bruno/contabilista aprovam. |
| 1 - Não fiscal | Evitar risco enquanto se valida o SaaS | Orçamentos, fichas, garantias, anexação de fatura externa | Nenhum documento RepairDesk se confunde com fatura. |
| 2 - Fiscal externo | Automatizar sem certificação própria | Integração com provider certificado; estados fiscais; PDF externo | Lojas emitem documentos legais sem sair do RepairDesk. |
| 3 - Core fiscal interno | Preparar certificação | Séries, impostos, documentos, SAF-T, hash, QR, ATCUD em ambiente teste | Validação técnica interna e com consultor. |
| 4 - Certificação AT | Obter certificado | Submissão à AT, testes, correções, certificado | Programa listado/certificado pela AT. |
| 5 - Produto fiscal completo | Moat comercial | e-Fatura, SAF-T, dashboards fiscais, alertas de prazo, auditoria | Módulo vendável como diferencial PT. |

## 10. Killer features de compliance PT

1. **Faturação legal portuguesa embutida no fluxo da reparação.** Ticket -> orçamento -> aprovação -> reparação -> fatura, sem duplicar dados.
2. **Alertas fiscais preventivos.** Séries sem ATCUD, faturas por comunicar, prazo dia 5, documentos externos em falta.
3. **Modo Artigo 53.º simples.** Frases legais, motivo de isenção, fatura simplificada quando aplicável, sem a loja ter de perceber SAF-T.
4. **Arquivo fiscal por equipamento/cliente.** Fatura, garantia, fotos antes/depois e histórico de reparação no mesmo caso.
5. **Migração suave.** Começa com Portal/provider e evolui para certificação própria sem mudar o workflow da loja.

## 11. Riscos legais identificados

| Risco | Severidade | Base legal / fonte | Mitigação |
|---|---|---|---|
| Emitir faturas no RepairDesk sem certificação | Crítica | DL 28/2019, artigo 4.º, n.º 1, alínea b) | Fase 1 sem emissão; Fase 2 provider certificado; Fase 3 certificação própria. |
| Confundir ficha/orçamento com fatura | Alta | CIVA artigo 29.º, n.º 19; DL 28/2019, artigo 7.º, n.º 2; Despacho 8632/2014, ponto 1.2 | Layouts claros e expressão "Este documento não serve de fatura". |
| Assumir que Artigo 53.º dispensa faturação | Alta | CIVA artigo 29.º, n.º 1, alínea b); CIVA artigo 53.º | Documentar que há fatura, mas sem liquidação de IVA. |
| Usar o limite de 200.000 EUR como regra geral | Alta | CIVA artigo 29.º, n.º 3, alínea a) | Tratar 200.000 EUR como exceção para entidades específicas, não oficinas. |
| Falhar comunicação e-Fatura | Alta | DL 198/2012, artigo 3.º; FAQ e-Fatura AT | Alertas até dia 5, jobs automáticos, logs e retries. |
| Gerar ATCUD errado ou série não comunicada | Alta | DL 28/2019, artigo 35.º; Portaria 195/2020 | Bloquear emissão sem código de validação. |
| Permitir editar documentos emitidos | Crítica | Portaria 363/2010, artigo 3.º; Despacho 8632/2014 | Imutabilidade, notas de crédito/débito, audit log. |
| Guardar credenciais AT de forma insegura | Crítica | RGPD + boas práticas de segurança | Subutilizador WFA, encriptação, vault, permissões mínimas. |
| Provider certificado não cobrir todos os casos | Média | Contrato/API/provider | Validar lista AT, SLA, documentos suportados, exportação e responsabilidade. |
| Certificação demorar mais que previsto | Média | Portaria 363/2010, artigo 5.º, n.º 2 | Começar Fase 2 com provider; só certificar após tração comercial. |

## 12. Pontos para confirmar com a contabilista / AT

- {{confirmar prazo e-Fatura vigente antes de produção}}: dia 5 vs texto consolidado do DL 198/2012.
- {{confirmar versão SAF-T/XSD atual}} e schemas a suportar.
- {{confirmar se documentos RepairDesk como orçamento/ficha de reparação entram em documentos fiscalmente relevantes quando enviados ao cliente}}.
- {{confirmar motivo legal/frase exata para Artigo 53.º nos documentos emitidos por provider}}.
- {{confirmar se existe taxa oficial atual de certificação AT}}.
- {{confirmar papel prático da OCC/contabilista no processo de certificação}}.
- {{confirmar nomes/versões exatas dos WSDLs de produção e testes}}.

## 13. Fontes oficiais consultadas

- Diário da República - Decreto-Lei n.º 28/2019: https://diariodarepublica.pt/dr/detalhe/decreto-lei/28-2019-119622094
- Diário da República - Decreto-Lei n.º 198/2012: https://diariodarepublica.pt/dr/detalhe/decreto-lei/198-2012-174543
- Portal das Finanças - CIVA artigo 29.º: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/civa_rep/Pages/iva29.aspx
- Portal das Finanças - CIVA artigo 36.º: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/civa_rep/Pages/iva36.aspx
- Portal das Finanças - CIVA artigo 40.º: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/civa_rep/Pages/iva40.aspx
- Portal das Finanças - CIVA artigo 53.º: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/civa_rep/ra/Pages/iva53ra_202503.aspx
- Portal das Finanças - Comunicação de Séries à AT e ATCUD: https://info.portaldasfinancas.gov.pt/pt/apoio_ao_contribuinte/Negocios/Faturacao/Regras_mecanismos_comunicacao/Comunicacao_de_series_ATCUD/Comunicacao_Series_a_AT_e_ATCUD/Paginas/default.aspx
- Portal das Finanças - FAQ Séries/ATCUD: https://info.portaldasfinancas.gov.pt/pt/apoio_contribuinte/questoes_frequentes/Pages/faqs-00883.aspx
- Portal das Finanças - e-Fatura FAQ: https://info.portaldasfinancas.gov.pt/pt/faturas/Pages/faqs-00978.aspx
- Portal das Finanças - e-Fatura webservice e ficheiro multidocumento: https://info.portaldasfinancas.gov.pt/pt/apoio_contribuinte/questoes_frequentes/pages/faqs-00996.aspx
- Portal das Finanças - SAF-T(PT): https://info.portaldasfinancas.gov.pt/pt/apoio_ao_contribuinte/Negocios/Faturacao/SAF_T_PT/SAF_T_PT_Versao_PT/Paginas/default.aspx
- Portal das Finanças - Certificação de Software de Faturação: https://info.portaldasfinancas.gov.pt/pt/apoio_ao_contribuinte/Negocios/Faturacao/Regras_mecanismos_comunicacao/Certificacao_programas/Certificacao_software_faturacao/Paginas/default.aspx
- Portaria n.º 195/2020 - QR e ATCUD: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/legislacao/diplomas_legislativos/Documents/Portaria_195_2020.pdf
- Portaria n.º 363/2010 - certificação de programas: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/legislacao/diplomas_legislativos/Documents/Portaria_363_2010.pdf
- Despacho n.º 8632/2014 - requisitos técnicos: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/legislacao/diplomas_legislativos/Documents/Despacho_n%C2%BA_8632_2014_03_07.pdf
