# Plano fiscal e juridico pessoal - LopesTech

Atualizado: 2026-05-16  
Pessoa: Bruno Lopes / LopesTech  
NIF: 263758141  
Situacao atual: empresario em nome individual / trabalhador independente  
Regime IVA atual: Isencao Art. 53 CIVA  
CAE: 62100 principal + 47401, 58290, 95101, 95102 secundarios

> Isto NAO substitui parecer profissional. E um mapa de decisao para falar com contabilista/advogado em Portugal em 2026. Antes de mudar regime, constituir sociedade, mexer em IVA, pedir subsidio de desemprego ou otimizar IRS/IRC, validar por escrito com contabilista certificado.

## Decisao curta

**Ano 1 (10-25k):** manter nome individual, regime simplificado, conta bancaria separada e software de faturacao certificado/Portal das Financas. Sair do Art. 53 se ultrapassar os limites. Contratar contabilista antes de chegar aos 15k, nao depois.

**Ano 2 (30-60k):** operar ja em regime normal de IVA. Continuar em nome individual se o risco ainda for baixo e nao houver equipa/contratos grandes. Preparar Lda: simular IRS vs Lda, separar propriedade intelectual, contratos, conta bancaria e pricing com IVA.

**Ano 3 (80-150k):** forte recomendacao de passar para **Sociedade Unipessoal por Quotas, Lda** antes de escalar SaaS, contratar, assumir SLAs, vender Enterprise ou internacionalizar. A Lda nao e magia fiscal; e sobretudo separacao patrimonial, organizacao, credibilidade e capacidade de reinvestir lucros.

Regra simples:

```text
< 15k/ano: manter simples, mas organizado.
15k-30k/ano: regime normal IVA + contabilista leve.
30k-60k/ano: decidir Lda com numeros reais.
60k-80k+/ano ou risco B2B: constituir Lda.
80k-150k/ano: Lda praticamente inevitavel para SaaS serio.
```

## 1. Tabela de decisao por volume

| Volume anual | IVA | IRS/IRC recomendado | Forma juridica | Contabilidade | Custo contabilidade estimado | Decisao |
|---:|---|---|---|---|---:|---|
| 0-15k | Art. 53 CIVA, se cumprir requisitos | IRS Cat. B, regime simplificado | Nome individual | Sem contabilidade organizada, mas com registos bons | 0-50 EUR/mes se apoio pontual | Manter simples. Conta separada e disciplina. |
| 15k-25k | Regime normal IVA quando sair da isencao | IRS Cat. B, simplificado | Nome individual | Simplificado | 40-90 EUR/mes `{{confirmar 2026}}` | Contratar contabilista. Preparar pricing com IVA. |
| 30k-60k | Regime normal IVA | IRS Cat. B simplificado ou contabilidade organizada se despesas reais forem altas | Nome individual ou preparar Lda | Simplificado ou organizada por opcao | 60-150 EUR/mes `{{confirmar 2026}}` | Fazer simulacao anual. Se SaaS crescer, preparar Lda. |
| 60k-80k | Regime normal IVA | Comparar IRS Cat. B vs Lda | Recomendado constituir Lda se houver contratos, risco, equipa ou lucro para reinvestir | Organizada na Lda | 100-200 EUR/mes `{{confirmar 2026}}` | Gatilho forte para Lda. |
| 80k-150k | Regime normal IVA | IRC + salario/dividendos planeados | Lda | Organizada obrigatoria | 150-300 EUR/mes `{{confirmar 2026}}` | Lda como default. Rever advogado/contabilista. |
| >200k Cat. B | Regime normal IVA | IRS com contabilidade organizada obrigatoria se continuar individual | Individual ja pouco recomendavel | Organizada obrigatoria | 150-300+ EUR/mes | Nao ficar aqui como ENI sem razao forte. |

Notas:

- Os custos de contabilista sao estimativas de mercado para microempresa/ENI/Lda simples em Portugal. Pedir 3 propostas.
- Em SaaS, o risco de contratos, dados, RGPD, backups, SLAs e clientes B2B pesa tanto como o imposto.
- Nao usar a Lda para misturar despesas pessoais. Isso aumenta risco fiscal.

## 2. Isencao Art. 53 CIVA

### 2.1 Regra 2026

O Art. 53 do CIVA permite isencao de IVA para sujeitos passivos com sede/domicilio em Portugal que, entre outras condicoes, nao tenham atingido no ano civil anterior volume de negocios anual em territorio nacional superior a **15.000 EUR**.

Fonte AT: CIVA Art. 53 e FAQ AT "Quem pode beneficiar do regime especial de isencao previsto no art. 53 do CIVA".

### 2.2 Quando Bruno sai da isencao

Ha dois gatilhos principais:

| Gatilho | Efeito | Prazo |
|---|---|---|
| No ano civil anterior, volume nacional > 15.000 EUR | Sai do Art. 53 a 1 de janeiro do ano seguinte | Entregar declaracao de alteracoes ate 15 dias uteis apos o final do ano civil |
| No ano em curso ultrapassa 15.000 EUR em mais de 25%, ou seja, > 18.750 EUR | Exclusao imediata a partir do momento em que ultrapassa | Declaracao de alteracoes ate 15 dias uteis apos ultrapassar |
| Deixa de cumprir condicoes do Art. 53 | Exclusao imediata | Declaracao de alteracoes ate 15 dias uteis apos o facto |

Fonte AT FAQ 5620.

### 2.3 Consequencias praticas

Ao sair do Art. 53:

- passa a liquidar IVA nas faturas, em regra 23% no continente, salvo operacoes com regras especiais;
- passa a poder deduzir IVA suportado em despesas elegiveis da atividade;
- passa a entregar declaracoes periodicas de IVA;
- se volume anual < 650.000 EUR, regra geral pode estar em IVA trimestral, salvo excecoes/opcoes;
- o preco B2B deve ser comunicado como "X EUR + IVA" ou com IVA incluido, conforme canal/comunicacao.

Para a LopesTech:

- antes de sair, atualizar propostas e pricing: `39 EUR + IVA` para B2B ou indicar claramente se IVA incluido;
- rever faturas recorrentes do RepairDesk;
- garantir software de faturacao certificado ou Portal das Financas/provider certificado, alinhado com `Contexto/10-Compliance-PT.md`;
- avisar clientes beta se o preco final passa a incluir IVA.

## 3. Regime simplificado vs contabilidade organizada

### 3.1 Regime simplificado IRS Cat. B

Regra CIRS Art. 28:

- regime simplificado aplica-se a Cat. B quando o montante anual iliquido de rendimentos da categoria B no periodo anterior nao ultrapassa **200.000 EUR**, salvo opcao por contabilidade organizada;
- cessa quando o limite de 200.000 EUR for ultrapassado em dois anos consecutivos ou em mais de 25% num so ano;
- pode optar por contabilidade organizada por declaracao de alteracoes ate final de marco do ano em que pretende aplicar.

No regime simplificado, o rendimento tributavel resulta de coeficientes do CIRS Art. 31.

Coeficientes relevantes:

| Tipo de rendimento | Coeficiente CIRS Art. 31 | Leitura para LopesTech |
|---|---:|---|
| Vendas de mercadorias/produtos | 0,15 | Pode aplicar a venda de hardware/pecas, se fiscalmente classificado como venda. |
| Atividades profissionais da tabela Art. 151 | 0,75 | Provavel para desenvolvimento/consultoria informatica, dependendo de enquadramento. |
| Prestacoes de servicos nao previstas nas alineas anteriores | 0,35 | Pode ser relevante para certas receitas SaaS/licenciamento, mas tem de ser confirmado. |
| Propriedade intelectual/industrial/know-how e certos rendimentos | 0,95 | Risco se contratos forem mal redigidos como cedencia/licenca de IP/royalties. |

Ponto critico: **a classificacao fiscal das receitas RepairDesk deve ser validada**. SaaS, desenvolvimento custom, reparacao, venda de pecas e licenciamento de software podem cair em categorias/coeficientes diferentes.

### 3.2 Contabilidade organizada em nome individual

Obrigatoria se:

- ultrapassar os limites do regime simplificado conforme CIRS Art. 28;
- ou Bruno optar por esse regime.

Pode fazer sentido por opcao se:

- despesas reais forem muito altas;
- houver stock relevante;
- houver subcontratacao, cloud, hardware, viaturas, renda, salarios;
- o coeficiente simplificado estiver a tributar lucro ficticio superior ao lucro real.

Mas para Ano 1-2, se o negocio e sobretudo servicos/SaaS com poucos custos, o simplificado costuma ser mais simples.

### 3.3 Quando contratar contabilista

Gatilhos:

1. **Agora**, pelo menos uma consulta paga de 1h para confirmar enquadramento CAE/CIRS/IVA.
2. **Antes de 15k faturados em 2026**, para preparar saida do Art. 53.
3. **Antes de vender SaaS recorrente a varias lojas**, para configurar faturacao, IVA, contratos e recibos.
4. **Antes de Lda**, para simular salario/dividendos/IRC/IRS/SS.

## 4. Quando passar a Lda

### 4.1 Recomendacao

Criar **Sociedade Unipessoal por Quotas, Lda** quando acontecer o primeiro destes gatilhos:

- faturacao anual previsivel > 60.000-80.000 EUR;
- RepairDesk passa a ter 10+ lojas pagantes;
- contratos B2B com SLAs, RGPD, backups, suporte ou responsabilidades relevantes;
- Bruno quer contratar alguem;
- quer separar claramente patrimonio pessoal e negocio;
- pretende vender para fora de Portugal;
- quer reinvestir lucro no produto em vez de retirar tudo para esfera pessoal;
- entra socio, investidor, grant ou contrato publico.

Se o Ano 2 apontar para 30-60k mas com crescimento acelerado de SaaS, comecar a preparacao da Lda mesmo antes dos 60k.

### 4.2 Custos

| Item | Estimativa |
|---|---:|
| Constituicao online/Empresa na Hora | 360 EUR com pacto elaborado pelos socios; alguns casos online/modelo podem ser 220 EUR, confirmar no servico |
| Certificado admissibilidade/nome, se aplicavel | `{{confirmar 2026}}` |
| Contabilista Lda | 100-200 EUR/mes para micro Lda simples; 200-300+ EUR se payroll, IVA, reconciliacoes, internacional |
| Conta bancaria empresa | 0-20 EUR/mes, depende do banco |
| Software faturacao certificado | 5-30 EUR/mes ou incluido no contabilista/provider |
| Advogado/pacto/contratos IP | 300-1500+ EUR, conforme complexidade `{{confirmar 2026}}` |

Fonte gov.pt: criar empresa online / sociedade por quotas; custo comum 360 EUR.

### 4.3 Vantagens da Lda

- separa NIF pessoal e NIPC da empresa;
- melhora credibilidade B2B;
- facilita contratar, vender SaaS, ter contratos e DPAs;
- limita responsabilidade dos socios ao capital/entradas, salvo excecoes legais, fiscais, garantias pessoais, abuso ou gestao culposa;
- permite manter lucros na sociedade para reinvestir com IRC, em vez de tributar tudo imediatamente em IRS pessoal;
- melhora organizacao contabilistica e reporting;
- prepara entrada de socio/investidor.

Fonte CSC/gov.pt: nas sociedades por quotas, o capital e dividido em quotas e a responsabilidade dos socios e limitada ao capital social, exceto casos previstos na lei. Ver CSC Art. 197 e pagina gov.pt "Sociedade por quotas - constituicao".

### 4.4 Desvantagens da Lda

- custo fixo mensal de contabilista;
- contabilidade organizada obrigatoria;
- mais declaracoes e formalismo;
- salario de gerente e/ou dividendos exigem planeamento;
- dinheiro da empresa nao e dinheiro pessoal;
- pode haver tributacao autonoma em despesas/viaturas;
- se Bruno der garantias pessoais, a protecao pratica reduz-se.

## 5. IRS Cat. B vs Lda: leitura simples

### 5.1 Nome individual / IRS Cat. B

Vantagens:

- simples;
- barato;
- bom para validar mercado;
- adequado a 10-30k se risco baixo;
- menos burocracia.

Desvantagens:

- responsabilidade mais colada ao patrimonio pessoal;
- todo o rendimento/lucro cai no IRS pessoal;
- pode ficar pesado quando a faturacao sobe;
- menos profissional para Enterprise/SaaS internacional;
- mistura mental entre "eu" e "empresa".

### 5.2 Lda / IRC

Em 2026, pela fonte AT consultada:

- taxa geral de IRC: **17%**;
- PME/Small Mid Cap: **15% sobre os primeiros 50.000 EUR de materia coletavel**, aplicando-se taxa geral ao excedente;
- derrama municipal pode acrescer ate 1,5%, dependendo do municipio;
- dividendos para pessoa singular podem ser sujeitos a retencao/taxa liberatoria de 28% ou englobamento, conforme estrategia fiscal `{{confirmar 2026}}`;
- salario de gerente entra em IRS/Seguranca Social.

Conclusao: Lda e fiscalmente interessante quando a sociedade **retem lucro para reinvestir**. Se Bruno tirar quase tudo todos os meses para viver, a diferenca fiscal pode ser pequena ou ate pior, por causa de custos fixos e dupla camada IRC + tributacao pessoal.

## 6. Retencao na fonte, pagamentos por conta e deducoes

### 6.1 Retencao na fonte IRS Cat. B

Regra geral CIRS Art. 101:

- entidades com contabilidade organizada que paguem rendimentos Cat. B podem ter obrigacao de reter;
- atividades profissionais da tabela Art. 151: taxa de retencao **23%** em 2026 segundo CIRS Art. 101;
- outras situacoes podem ter 11,5%, 16,5% ou outras taxas, conforme natureza do rendimento.

Dispensa CIRS Art. 101-B:

- pode haver dispensa de retencao para Cat. B quando Bruno preve auferir valor anual inferior ao limite do Art. 53 CIVA;
- a dispensa e facultativa e deve constar no recibo com a mencao legal.

Decisao pratica:

- enquanto <15k e Art. 53: usar dispensa se aplicavel e se fizer sentido de tesouraria;
- quando passar >15k/ano: esperar retencoes em clientes B2B PT com contabilidade organizada;
- guardar 25-35% do recebido numa "conta impostos" ate haver historico real.

### 6.2 Pagamentos por conta IRS

CIRS Art. 102:

- rendimentos Cat. B podem gerar tres pagamentos por conta: julho, setembro e dezembro;
- a AT comunica os valores com base em formula legal e liquidacoes anteriores;
- nao e exigivel se cada pagamento for inferior a 50 EUR.

Impacto:

- Ano 1 pode parecer leve;
- Ano 2/3 pode trazer pagamentos por conta baseados no ano anterior;
- reservar caixa para isso evita sustos.

### 6.3 Deducoes e despesas

No regime simplificado:

- parte da despesa e presumida pelo coeficiente;
- para atividades com coeficiente 0,75, pode existir necessidade de justificar uma parcela de despesas para beneficiar totalmente do regime simplificado, dependendo das regras em vigor `{{confirmar 2026}}`;
- contribuicoes para Seguranca Social contam de forma relevante.

Despesas a separar e guardar fatura:

- software/subscricoes;
- dominio/hosting/cloud;
- computador, perifericos;
- ferramentas, pecas, materiais;
- telefone/internet na proporcao profissional;
- formacao;
- contabilista;
- deslocacoes justificadas;
- publicidade/marketing;
- comissoes Stripe/Mollie/SIBS/Meta/Cloudflare/etc.

Regra de ouro: fatura com NIF certo e finalidade profissional clara. Se for Lda, fatura com NIPC da Lda, nao NIF pessoal.

## 7. Seguranca Social

### 7.1 Contribuicoes TI

Fonte Seguranca Social:

- taxa contributiva trabalhador independente: **21,4%**;
- empresarios em nome individual/EIRL: **25,2%**;
- rendimento relevante em declaracao trimestral: em regra 70% da prestacao de servicos e 20% de vendas/producao de bens;
- declaracao trimestral nos meses de janeiro, abril, julho e outubro;
- em contabilidade organizada, rendimento relevante pode corresponder ao lucro tributavel do ano anterior.

Formula simples para servicos:

```text
Rendimento relevante mensal = faturacao servicos do trimestre x 70% / 3
Contribuicao mensal = rendimento relevante mensal x 21,4%
```

Exemplo operacional:

```text
3.000 EUR de servicos no trimestre
70% = 2.100 EUR
2.100 / 3 = 700 EUR base mensal
700 x 21,4% = 149,80 EUR/mes
```

Nota: se Bruno estiver fiscalmente como **empresario em nome individual** para atividades comerciais/industriais, confirmar se a taxa aplicavel e 21,4% ou 25,2% conforme enquadramento real. Isto e pergunta obrigatoria para a Seguranca Social/contabilista.

### 7.2 Primeiro enquadramento / isencoes

O primeiro enquadramento no regime dos trabalhadores independentes produz efeitos no primeiro dia do 12.o mes posterior ao inicio de atividade, salvo antecipacao. Mas isto depende do historico de atividade independente do Bruno.

Como Bruno ja tem atividade aberta e saiu do emprego em abril de 2026, confirmar:

- quando abriu atividade pela primeira vez;
- se teve isencao por acumulacao com trabalho por conta de outrem;
- se a isencao acabou ao sair do emprego;
- quando deve entregar a proxima declaracao trimestral.

### 7.3 Desemprego apos sair do emprego

Regra geral: subsidio de desemprego protege perda involuntaria de emprego. O IEFP indica que o subsidio de desemprego e apoio a quem perdeu emprego de forma involuntaria e esta inscrito no IEFP.

Implicacao:

- se Bruno se despediu por iniciativa propria, em regra nao ha subsidio de desemprego;
- se houve despedimento, fim de contrato ou acordo com enquadramento valido, pode haver direito, mas deve ser analisado;
- prazo geral: pedido nos 90 dias seguintes a data em que deixou de trabalhar;
- se ja pediu/recebe subsidio e inicia atividade independente, pode existir subsidio de desemprego parcial se cumprir condicoes.

Acao: se ainda houver duvida sobre direito a desemprego, ligar/ir ao IEFP/Seguranca Social **imediatamente**, porque o prazo de 90 dias conta desde a cessacao.

## 8. Estrategia operacional

### 8.1 Conta separada

Abrir conta bancaria separada para LopesTech ja, mesmo antes da Lda.

Fluxo recomendado:

```text
Conta LopesTech recebe tudo.
Conta LopesTech paga despesas profissionais.
Todos os meses Bruno transfere "ordenado pessoal" fixo para conta pessoal.
Conta impostos guarda IVA/IRS/SS estimados.
```

Percentagens praticas enquanto nao ha contabilista:

| Situacao | Guardar para impostos/SS |
|---|---:|
| Art. 53, <15k, sem IVA | 25-35% |
| Regime normal IVA | IVA cobrado fica intocavel + 25-35% do liquido |
| Com Lda | seguir mapa do contabilista; separar IRC, IVA, SS, salario |

### 8.2 Software

Agora:

- Portal das Financas ou software certificado barato;
- Moloni, TOConline, InvoiceXpress, Vendus ou similar, a escolher com contabilista;
- RepairDesk nao deve emitir faturas fiscais proprias enquanto nao for software certificado ou integrado com provider certificado.

Quando houver Lda:

- software certificado obrigatorio na pratica;
- contabilista deve ter acesso;
- reconciliacao bancaria mensal.

### 8.3 Dossiers minimos

Criar pastas:

```text
financas/
  2026/
    01-faturas-emitidas/
    02-faturas-recebidas/
    03-banco/
    04-iva/
    05-irs-ss/
    06-contratos/
    07-contabilista/
```

Guardar:

- faturas emitidas;
- faturas recebidas;
- extratos;
- contratos clientes;
- recibos verdes/faturas-recibos;
- declaracoes AT;
- comprovativos Seguranca Social;
- propostas e DPAs relevantes.

## 9. Roadmap por ano

### Ano 1 - 2026: transicao, 10-25k

Objetivo: sobreviver limpo e aprender com dados reais.

Moves:

- consulta com contabilista agora;
- confirmar CAE/CIRS/coeficientes;
- acompanhar faturacao acumulada mensalmente;
- antes dos 15k, preparar saida Art. 53;
- se passar 18.750 no ano, comunicar a AT em 15 dias uteis;
- conta separada;
- software de faturacao certificado/Portal AT;
- guardar 25-35% para IRS/SS;
- confirmar Seguranca Social apos saida do emprego;
- zero despesas pessoais misturadas.

Nao fazer:

- abrir Lda por vaidade;
- comprar viatura pela atividade sem simulacao;
- prometer SaaS internacional sem base fiscal/contratual;
- deixar IVA para "ver depois".

### Ano 2 - 2027: crescimento, 30-60k

Objetivo: profissionalizar sem burocracia excessiva.

Moves:

- regime normal de IVA;
- contabilista mensal;
- simular Lda no Q1;
- rever pricing para IVA;
- separar receitas por linha: reparacoes, websites, software custom, SaaS;
- preparar contratos RepairDesk;
- decidir se RepairDesk IP fica pessoal temporariamente ou passa para Lda;
- com 10 lojas pagantes, iniciar constituicao Lda ou plano de transicao.

Gatilho:

```text
Se forecast anual ultrapassar 60k OU houver contratos B2B com SLA/RGPD, abrir Lda.
```

### Ano 3 - 2028: escala, 80-150k

Objetivo: operar como empresa, nao como freelancer com produto.

Moves:

- Lda como default;
- contabilidade organizada;
- salario gerente planeado;
- politica de dividendos/reinvestimento;
- contratos B2B e DPAs formais;
- seguros a avaliar: responsabilidade civil profissional/cyber;
- pagina publica de sub-processadores/status;
- avaliar apoio juridico recorrente.

Gatilho:

```text
Se RepairDesk for fonte principal de receita e tiver clientes externos, nao manter tudo no NIF pessoal.
```

## 10. Checklist de moves operacionais

Esta semana:

- [ ] Marcar reuniao com contabilista certificado.
- [ ] Confirmar se atividade atual esta como TI, ENI, ou outro enquadramento.
- [ ] Confirmar taxa Seguranca Social aplicavel: 21,4% vs 25,2%.
- [ ] Confirmar se Bruno ainda tem alguma isencao/obrigacao trimestral SS.
- [ ] Abrir/usar conta bancaria separada LopesTech.
- [ ] Criar folha de controlo: faturacao acumulada 2026, IVA, IRS, SS.
- [ ] Escolher software faturacao/Portal AT para faturas legais.

Antes de 15k:

- [ ] Preparar declaracao de alteracoes se necessario.
- [ ] Atualizar pricing com IVA.
- [ ] Avisar clientes recorrentes.
- [ ] Garantir que todas as faturas novas ficam corretas.

Antes de 30k:

- [ ] Contabilista mensal.
- [ ] Simular simplificado vs organizada.
- [ ] Separar receitas por CAE/tipo.
- [ ] Rever contratos RepairDesk.

Antes de 60k:

- [ ] Simular Lda.
- [ ] Escolher nome/NIPC/modelo sociedade.
- [ ] Decidir transferencia/licenca de IP RepairDesk para Lda.
- [ ] Abrir conta empresa.
- [ ] Criar politica de salario/retiradas.

Antes de 100k:

- [ ] Lda operacional.
- [ ] Seguro responsabilidade/cyber avaliado.
- [ ] Contratos/DPA revistos por advogado.
- [ ] Processos de contabilidade, IVA, payroll e reporting estabilizados.

## 11. Perguntas para o contabilista

Levar esta lista e pedir respostas por escrito.

1. Com os CAE 62100, 58290, 47401, 95101 e 95102, que rendimentos ficam em cada coeficiente do CIRS Art. 31?
2. Receitas RepairDesk SaaS devem ser tratadas como prestacao de servicos 0,75, servicos nao previstos 0,35, licenciamento/IP 0,95 ou outra classificacao?
3. A venda de pecas/hardware pode ser separada com coeficiente 0,15?
4. O Bruno esta como trabalhador independente ou empresario em nome individual para efeitos de Seguranca Social? Taxa 21,4% ou 25,2%?
5. A isencao/contribuicao SS muda por ter saido do emprego em abril de 2026?
6. Ha algum direito a subsidio de desemprego ou subsidio parcial, dado o motivo de saida do emprego?
7. Quando exatamente deve entregar declaracao de alteracoes se ultrapassar 15k ou 18.750?
8. Em regime normal de IVA, trimestral chega? Alguma excecao pela atividade?
9. Para 30k, 60k e 100k de faturacao, qual a simulacao IRS Cat. B vs Lda?
10. Qual o salario minimo/otimo de gerente numa Lda LopesTech?
11. Dividendos fazem sentido ou e melhor reinvestir?
12. Como transferir ou licenciar o RepairDesk desenvolvido em nome pessoal para a futura Lda sem criar problema fiscal?
13. Que software de faturacao recomenda e porque?
14. Que despesas posso afetar a atividade sem risco fiscal excessivo?
15. A LopesTech deve ter contabilidade organizada por opcao antes de Lda?
16. Quais declaracoes mensais/trimestrais/anuais Bruno tera em cada regime?
17. Qual o custo mensal exato do servico contabilistico e o que inclui?

## 12. Fontes consultadas

- AT - CIVA Art. 53: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/civa_rep/Pages/artigo-53-o-do-civa.aspx
- AT FAQ enquadramento IVA/IRS/IRC, Art. 53 e regime simplificado: https://info.portaldasfinancas.gov.pt/pt/apoio_contribuinte/questoes_frequentes/Pages/faqs-00315.aspx
- AT - CIRS Art. 28: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/cirs_rep/ra/Pages/irs28.aspx
- AT - CIRS Art. 31: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/cirs_rep/ra/Pages/irs31ra_202503.aspx
- AT - CIRS Art. 101: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/cirs_rep/Pages/irs101.aspx
- AT - CIRS Art. 101-B: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/cirs_rep/Pages/irs101b.aspx
- AT - CIRS Art. 102: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/cirs_rep/Pages/irs102.aspx
- AT - CIRC Art. 87: https://info.portaldasfinancas.gov.pt/pt/informacao_fiscal/codigos_tributarios/CIRC_2R/Pages/irc87.aspx
- AT - Modelo 22 / derrama municipal 2026: https://info.portaldasfinancas.gov.pt/pt/destaques/Paginas/Modelo_22_IRC_2026.aspx
- gov.pt - Criar empresa online: https://www2.gov.pt/pt/servicos/criar-uma-empresa-online
- gov.pt - Sociedade por quotas - constituicao: https://www.gov.pt/servicos/sociedade-por-quotas-constituicao
- gov.pt - Empresa na Hora: https://www2.gov.pt/pt/servicos/criar-uma-empresa-na-hora
- DRE - Codigo das Sociedades Comerciais, sociedade por quotas: https://diariodarepublica.pt/dr/legislacao-consolidada/decreto-lei/1986-34443975-46019375
- Seguranca Social - Codigo Contributivo: https://www.seg-social.pt/documents/10152/15009350/C%C3%B3digo_Contributivo/1e56fad5-0e2a-42c2-b94c-194c4aa64f74
- Seguranca Social - Trabalhadores independentes, novo regime: https://www.seg-social.pt/documents/10152/14965/1009%20Trabalhador%20independente%20-%20novo%20regime/87b6e00c-523d-4718-8a88-942ea804c18a
- Seguranca Social - Perguntas frequentes trabalhadores independentes: https://www.seg-social.pt/documents/10152/14965/1009%20Trabalhador%20independente%20-%20novo%20regime%20-%20Perguntas%20Frequentes/9b8e7299-98c0-4d5b-9556-2e650604b72a
- IEFP - Subsidio de desemprego: https://www.iefp.pt/subsidio-desemprego
- Seguranca Social - Subsidio de desemprego parcial: https://www.seg-social.pt/pt/subsidio-parcial-de-desemprego
