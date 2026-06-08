# 92 - Audit Frontend Premium Mender (2026-06-07)

Pedido Bruno: auditoria geral ao design/frontend atual do Mender, com foco em reduzir scroll vertical
infinito, evitar divs full-width a ocupar espaco inutil e levar o produto para um nivel SaaS premium.

## Veredicto curto

O Mender ja tem profundidade de produto. O problema atual nao e falta de features; e a composicao
dos ecra. Algumas paginas ja parecem SaaS operacional moderno (`Catalogo`, `Balcao`, `Clientes`
lista com inspector), mas outras ainda funcionam como documentos verticais gigantes.

O maior offender e `ReparacaoDetalhe.tsx`: tem quase 1900 linhas e empilha workflow, cliente,
acoes, timer, documentos, detalhes, diagnostico, fotos, pecas, lucro, tarefas, devices,
comunicacoes e timeline numa so coluna. Num desktop grande isto desperdica largura, obriga scroll
excessivo e esconde o que interessa: estado, proxima acao, cliente, pagamento e margem.

## Implementado nesta ronda

- Criado `frontend/src/components/ui/Workspace.tsx` com `DetailWorkspace`, `InspectorRail` e
  `ViewTabs`.
- `ReparacaoDetalhe.tsx` passou a usar tabs internas (`Resumo`, `Pecas & fotos`, `Financeiro`,
  `Operacao`, `Timeline`) e rail sticky com cliente, proxima acao, financeiro, documentos e estado
  de auto-save.
- Os detalhes e o bloco financeiro da ficha foram compactados em grids desktop para evitar inputs
  full-width desnecessarios.
- `Reparacoes.tsx` passou a usar a mesma `DetailWorkspace`, com filtros agrupados numa superficie
  operacional e inspector reutilizavel tambem em lista/kanban.
- `ClienteDetalhe.tsx` passou para CRM 360 com tabs (`Perfil`, `Historico`, `Comunicacao`, `RGPD`)
  e rail lateral sticky com contacto, consentimento, notas e KPIs.
- `Produtos.tsx` passou para `Catalogo & Stock`, com tabs funcionais (`Tudo`, `Na loja online`,
  `Stock fisico`, `Stock virtual`, `Ocultos`) e rail sticky com metricas de modelos, variantes,
  stock fisico, dropshipping, ocultos, alertas e regra UX para conteudo por modelo.
- `Balcao.tsx` alinhado ao mesmo `DetailWorkspace`/`InspectorRail`, mantendo a unificacao POS +
  caixa sem tocar na logica critica de cobranca.
- Frontend build validado com `npm run build`.

## Evidencia no codigo

- `frontend/src/pages/reparacoes/ReparacaoDetalhe.tsx`: 1903 linhas.
- `frontend/src/pages/reparacoes/Reparacoes.tsx`: 1707 linhas.
- `frontend/src/pages/definicoes/Definicoes.tsx`: 2465 linhas.
- `frontend/src/pages/produtos/Produtos.tsx`: 1177 linhas.
- `frontend/src/pages/vendas/Vendas.tsx`: 1080 linhas.

Padroes bons ja existentes:

- `Catalogo.tsx` usa `xl:grid-cols-[1fr_400px]` com inspector lateral sticky.
- `Balcao.tsx` usa `xl:grid-cols-[1fr_300px]` e rail "Caixa do dia".
- `Clientes.tsx` ja tem lista + inspector de perfil.
- `Reparacoes.tsx` ja tem base de lista + inspector, apesar de ainda ser muito grande.

Padroes maus recorrentes:

- Cards full-width em sequencia vertical para informacao que devia estar lado a lado.
- Form fields de 1200px de largura para inputs curtos.
- Modais longos com scroll interno, em vez de drawer/stepper/tabs.
- Componentes de pagina monoliticos, onde layout, data fetching, mutations e UI vivem juntos.
- `rounded-xl` e sombras repetidas em todo o lado sem uma hierarquia clara entre painel, card,
  linha clicavel e alerta.

## Principios de refactor

1. Cada ecra deve responder em 5 segundos: "o que esta a acontecer?" e "qual e a proxima acao?".
2. Desktop nao deve ser mobile esticado. Desktop usa duas colunas: trabalho principal + contexto.
3. Formularios raramente devem ocupar a largura toda. Campos curtos ficam em grid de 2/3 colunas.
4. Listas importantes devem ter inspector lateral. Clicar numa linha nao deve sempre trocar de pagina.
5. Informacao rara ou historica vai para tabs/accordion, nao para o caminho principal.
6. Um CTA primario por ecra. Exportar/importar/destruir ficam em menu secundario.
7. O design system deve impor layout, nao so cor/raio.

## Novo contrato de layout

Criar primitives antes de redesenhar paginas:

- `PageShell`: largura, spacing, header e toolbar padrao.
- `EntityHeader`: breadcrumb, titulo, estado, primary action, saved state.
- `DataWorkspace`: filtros + tabela/lista + inspector lateral.
- `DetailWorkspace`: coluna principal + rail sticky.
- `InspectorRail`: painel lateral com contexto e acoes.
- `SectionPanel`: painel compacto com header, body e actions.
- `StickyActionBar`: guardar/criar/acoes em mobile e modais longos.
- `ViewTabs`: tabs internas com overflow controlado.

Regra tecnica: paginas novas ou refactors grandes nao devem montar sequencias longas de
`<section className="rounded-xl ...">` diretamente. Devem usar estes contratos.

## Reparacao detalhe - alvo P0

Hoje a ficha de reparacao esta a tentar ser tudo ao mesmo tempo. O layout alvo:

Desktop:

```text
--------------------------------------------------------------------------+
| #8 iPhone 13 Pro Max       Diagnostico       Guardado ha 12s     Acoes  |
| Cliente: Joao Silva        Recebido ha 2d     Valor: 129,00 EUR          |
+-----------------------------------------------+--------------------------+
| Tabs: Resumo Diagnostico Pecas Fotos Financas | CLIENTE                  |
|                                               | WhatsApp / Ligar / Email |
| Resumo                                        |                          |
| - Equipamento + IMEI                          | PROXIMA ACAO             |
| - Avaria + diagnostico                        | Avancar para Aguarda peca|
| - ETA + tecnico                               |                          |
|                                               | FINANCEIRO               |
| Diagnostico guiado / checklist                | PVP, custo, lucro, pago  |
|                                               |                          |
| Pecas usadas / compras fornecedor             | DOCUMENTOS               |
|                                               | Orcamento, fatura, label |
+-----------------------------------------------+--------------------------+
```

Conteudo principal:

- Tab `Resumo`: equipamento, IMEI, avaria, diagnostico, ETA, tecnico.
- Tab `Diagnostico`: diagnostico guiado, campos tecnicos, checklist.
- Tab `Pecas`: pecas usadas, compras ao fornecedor, kits.
- Tab `Fotos`: fotos antes/depois.
- Tab `Financeiro`: preco, lucro, IVA, pagamento, fatura.
- Tab `Comunicacoes`: WhatsApp/email/notas de contacto.
- Tab `Timeline`: historico e assinaturas.

Rail sticky:

- Cliente: nome, NIF/badge, telefone, canais, alerta "nao contactar".
- Estado/proxima acao: botao de avancar estado, notas de transicao.
- Financeiro: PVP, custo pecas, lucro, pago/por cobrar, fatura.
- Documentos: etiqueta, comprovativo entrada, recibo entrega, portal cliente.
- Device/garantia: IMEI match, garantia ativa, outros equipamentos.

Acceptance criteria:

- No primeiro viewport desktop devem aparecer: cliente, estado, proxima acao, valor, pagamento,
  workflow compacto e dados principais da reparacao.
- Timeline e assinaturas nao aparecem no fluxo principal por defeito.
- Inputs curtos nunca ocupam mais de metade da largura em desktop.
- `ReparacaoDetalhe.tsx` deve baixar para <500 linhas; subcomponentes <300 linhas cada.

## Auditoria por area

### Layout global

Bom:

- Sidebar permanente e topbar ja existem.
- Pesquisa global e CTA rapido existem.
- `main` tem largura ampla suficiente para workspaces reais.

Problemas:

- Falta um contrato de layout por tipo de pagina.
- O CTA global "Nova reparacao" domina mesmo quando o contexto e cliente, compras ou catalogo.
- As paginas ainda escolhem spacing/raio/estrutura individualmente.

Recomendacao:

- Header global fica estavel, mas cada pagina deve ter `PageToolbar` contextual.
- A largura grande deve ser usada com grids e rails, nao com cards full-width.

### Reparacoes lista

Bom:

- Lista + Kanban existem.
- Ja ha inspector lateral e metricas.

Problemas:

- Componente grande demais.
- Kanban horizontal pode virar scroll cansativo.
- Import/export/alertas competem com "Nova".

Recomendacao:

- Manter lista + inspector como modo default.
- Kanban desktop com colunas compactas; mobile vira lista agrupada por estado.
- Acoes secundarias em menu `Mais`.

### Clientes

Bom:

- Lista + inspector ja esta no caminho certo.
- Tags, preferencias de contacto e consentimento RGPD dao base CRM real.

Problemas:

- Ficha de cliente ainda pode virar scroll de secoes.
- Contactos, historico, devices e RGPD devem ser separados por tabs.

Recomendacao:

- `ClienteDetalhe` como CRM 360: header + KPIs + tabs + rail contacto/RGPD.
- A lista deve continuar com inspector e filtros por etiqueta.

### Catalogo / Stock / Produtos

Bom:

- `Catalogo` ja e o melhor exemplo de layout Mender: tabela + inspector sticky.
- Separacao stock fisico/virtual/loja online esta conceptualmente certa.

Problemas:

- `Produtos.tsx` ainda carrega demasiado estado e modais longos.
- Modelo/variante precisa de heranca clara: conteudo, fotos e SEO no pai; preco, stock, cor,
  fornecedor e publicacao na variante.

Recomendacao:

- Unificar experiencia em `Catalogo & Stock`.
- Produto pai = conteudo comercial, galeria, SEO, familias/cores.
- Variante = SKU, stock fisico/virtual, preco, grade, fornecedor, toggle loja.
- Editor deve ser side panel, nao modal gigante.

### Balcao

Bom:

- Ja esta no padrao certo: POS + rail caixa.
- A regra "sem caixa aberta nao ha venda" e clara.

Problemas:

- `Vendas.tsx` ainda e grande e critico.

Recomendacao:

- Nao refactorar agressivamente. Extrair gradualmente: `ProductSearch`, `CartPanel`,
  `PaymentMethods`, `SalesHistory`.

### Compras e Operacao

Bom:

- Conceito certo: inbox de faturas + despesas/opex + recorrentes.

Problemas:

- Pode parecer uma area fiscal dispersa se voltar a cards em sequencia.

Recomendacao:

- Layout "operational finance": esquerda tabela/inbox, direita rail com export contabilista,
  alertas, totais do mes, recorrentes.

### Definicoes

Problema principal:

- `Definicoes.tsx` tem 2465 linhas. Isto e um produto dentro do produto.

Recomendacao:

- Dividir por rotas/subpaginas reais: Empresa, Fiscal, Faturacao, POS, Portal cliente,
  Comunicacoes, Utilizadores, Aparencia.
- Usar subnav lateral de definicoes e preview/contexto a direita.

## Roadmap recomendado

### UX-1: Layout primitives

Escopo:

- Criar `DetailWorkspace`, `InspectorRail`, `EntityHeader`, `SectionPanel`, `ViewTabs`.
- Sem mudar comportamento de negocio.

Validacao:

- `npm run build`.
- Smoke visual em desktop/tablet/mobile.

### UX-2: Reparacao detalhe premium

Escopo:

- Reorganizar `/reparacoes/:id` para duas colunas + tabs.
- Extrair rail de cliente/acoes/financeiro.
- Extrair tabs em componentes pequenos.

Risco:

- Alto, porque `ReparacaoDetalhe.tsx` esta modificado no working tree. Ler diff antes de tocar.

### UX-3: Reparacoes lista polish

Escopo:

- Consolidar toolbar, mover acoes secundarias para `Mais`.
- Melhorar Kanban responsive.
- Inspector mais acionavel: proxima acao, contacto, financeiro.

### UX-4: Cliente 360

Escopo:

- Ficha cliente com tabs: Resumo, Historico, Equipamentos, Comunicacoes, RGPD.
- Rail contacto/consentimento/KPIs.

### UX-5: Catalogo & Stock final

Escopo:

- Editor pai/variante.
- Conteudo/fotos/SEO no modelo pai.
- Variantes focadas em SKU/stock/preco/loja.

### UX-6: Definicoes split

Escopo:

- Quebrar `Definicoes.tsx` em rotas e componentes.
- Subnav persistente.
- Preview/contextual help.

### UX-7: Visual QA

Escopo:

- Playwright screenshots em 1440, 1024, 390.
- Checks de no horizontal overflow.
- Build verde.

## Ordem certa

1. Nao comecar por cores. Comecar por layout.
2. Nao comecar por Dashboard. Comecar por reparacao detalhe, porque e onde o valor operacional vive.
3. Nao refazer tudo ao mesmo tempo. Criar primitives e migrar pagina a pagina.
4. Nao tocar em ficheiros que o Claude alterou sem ler diff.

## Definicao de "premium" para Mender

Premium aqui nao e hero bonito, gradientes ou cards maiores. Premium e:

- Menos scroll para completar uma reparacao.
- Estado e proxima acao sempre visiveis.
- Menos cliques para contactar cliente.
- Menos largura desperdicada.
- Separacao clara entre trabalho diario, dados fiscais e historico.
- Paginas que parecem ferramentas de trabalho, nao landing pages internas.
