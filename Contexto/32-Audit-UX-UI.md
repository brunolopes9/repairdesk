# Audit UX/UI RepairDesk + Roadmap Visual

Data: 2026-05-16  
Contexto: RepairDesk SaaS PT, frontend React 19 + Vite + Tailwind v4, estado avancado pre-Beta.

## Veredicto curto

A UI actual nao esta "fraca" no sentido de produto vazio. O RepairDesk ja tem muito produto real: dashboard operacional, lista + kanban de reparacoes, detalhe rico, diagnostico guiado, portal cliente, garantia, avaliacoes, dark mode e definicoes completas.

O problema e outro: ainda parece demasiado "app feita por tecnico competente" e nao "SaaS B2B polido". A diferenca esta menos em inventar features e mais em criar um sistema visual consistente: icones, botoes, toasts, skeletons, empty states, modais acessiveis, hierarquia de pagina e layouts desktop melhores.

Score honesto hoje: **6.2/10 para SaaS B2B pre-Beta**.  
Depois dos quick wins deste documento: **7.2/10**.  
Depois dos refactors medios: **8/10 suficiente para vender em PT sem vergonha visual**.

## O que nao e problema

- O produto nao precisa de Figma gigante antes da Beta.
- O portal cliente publico ja tem uma direccao visual forte e vendavel.
- A sidebar colapsavel tipo Notion e uma boa base.
- A existencia de lista + kanban em reparacoes e uma vantagem real.
- A densidade de funcionalidades nao e o problema; o problema e falta de hierarquia e componentes reutilizaveis.
- O azul `#0EA5E9` nao bloqueia Beta. E generico, mas aceitavel se for usado com disciplina.
- Nao vale a pena trocar React, Tailwind ou stack visual agora.

---

## Score por criterio

| Criterio | Score | Diagnostico | Prioridade |
|---|---:|---|---|
| Hierarquia visual | 3/5 | Ha muita informacao util, mas varias secoes competem pela atencao. Dashboard e detalhe precisam de "o que faco agora?" mais forte. | Alta |
| Consistencia | 3/5 | Tailwind da coerencia base, mas botoes, cards, raios, icones, estados e modais variam pagina a pagina. | Alta |
| Densidade de informacao | 3/5 | Boa para oficina, mas algumas paginas estao longas e com cards a mais. Falta separacao entre accao, leitura e arquivo. | Media |
| Affordance | 3/5 | A maior parte e clicavel, mas emojis, hover-only actions e botoes com pesos semelhantes reduzem clareza. | Alta |
| Feedback | 2/5 | Loading ainda muito textual, sucesso/erro pouco padronizado, falta toast global. | Alta |
| Mobile responsiveness | 3/5 | Existe bottom nav e layouts responsivos. Risco: excesso de itens, modais longos e tabelas densas. | Media |
| Acessibilidade | 2/5 | Modal sem focus trap completo, emojis como icones, foco inconsistente, possivel dependencia de cor. | Alta |
| Performance percebida | 3/5 | React Query ajuda, mas falta skeleton, optimistic feedback e layouts estaveis para reduzir sensacao de espera. | Media |
| Onboarding / empty states | 2/5 | Existem estados vazios basicos, mas nao ensinam o proximo passo nem mostram exemplos. | Alta |
| Error recovery | 2/5 | Erros aparecem, mas nao ha linguagem padrao, retries, accoes claras ou explicacao para utilizador de loja. | Alta |

## Analise por ecra

### Dashboard `/`

| Pontos fortes | Pontos fracos | Propostas concretas |
|---|---|---|
| Conteudo muito alinhado com oficina: KPIs, em curso, alertas, top reparacoes, avaliacoes, financas. | Parece uma coleccao de widgets com pesos parecidos. O Bruno/lojista pode nao saber onde olhar primeiro. | Reorganizar em 3 zonas: `Precisa de atencao`, `Hoje na oficina`, `Saude do negocio`. |
| Period filters existem e sao uteis. | Loading textual "A carregar..." passa sensacao de app menos madura. | Skeletons para KPI cards, listas e grafico. |
| Alertas de itens por cobrar/despesas orfas sao valiosos. | Alertas competem visualmente com KPIs e secoes normais. | Alertas no topo, com severidade e accao directa. |
| Grafico SVG simples sem dependencia pesada. | Grafico pode parecer artesanal se nao tiver eixos/tooltip/legenda consistentes. | Manter simples, mas envolver num componente `TrendChart` com tooltip e estado vazio. |

Mockup proposto:

```text
+---------------------------------------------------------------------+
| Dashboard                         Hoje, 16 maio      [Actualizar]    |
+---------------------------------------------------------------------+
| PRECISA DE ATENCAO                                                   |
| [3 por cobrar  Ver] [2 despesas sem reparacao  Rever] [Backup OK]    |
+---------------------------------------------------------------------+
| HOJE NA OFICINA                                                      |
| [Recebidas 4] [Em diagnostico 7] [Prontas 3] [Entregues 2]           |
|                                                                     |
| Em curso                                                            |
| +------------+ +-------------+ +------------+                        |
| | iPhone 12  | | S21 Ultra   | | Redmi Note |                        |
| | Diagnostico| | Aguardar peca| | Pronto    |                        |
| +------------+ +-------------+ +------------+                        |
+---------------------------------------------------------------------+
| SAUDE DO NEGOCIO                                                     |
| [Receita 30d] [Margem] [Tempo medio] [Avaliacoes]                   |
| [grafico 6 meses]                         [top clientes/reparacoes] |
+---------------------------------------------------------------------+
```

### Lista reparacoes `/reparacoes`

| Pontos fortes | Pontos fracos | Propostas concretas |
|---|---|---|
| Ter lista e kanban e certo para oficina. | Toggle lista/kanban com caracteres/emoji parece pouco profissional. | Usar segmented control com icones `List` e `Columns3`. |
| Filtros por estado e pesquisa existem. | Barra de accoes mistura primario, import/export e view mode com pesos parecidos. | Criar `DataToolbar`: pesquisa, filtros, view mode, accoes secundarias num menu, CTA principal separado. |
| Kanban por estados encaixa no fluxo real. | Colunas horizontais podem ficar pesadas em mobile. | Mobile: lista agrupada por estado; desktop: kanban completo. |
| Import/export ja existe. | Import/export perto de "Nova" pode distrair no uso diario. | Mover para menu `Mais`. |

Mockup proposto:

```text
Reparacoes                                      [Nova reparacao]
Pesquisa por cliente, IMEI ou equipamento      [Lista | Kanban] [Mais]

[Todas] [Recebidas] [Diagnostico] [Aguardar peca] [Prontas] [Entregues]

+--------------------------------+----------------+-------+---------+-----+
| Cliente / Equipamento          | Estado         | Total | Tecnico | ... |
+--------------------------------+----------------+-------+---------+-----+
| Joao Silva - iPhone 12         | Diagnostico    | 89€   | Bruno   | ... |
| Maria Costa - Samsung A52      | Aguardar peca  | 129€  | Bruno   | ... |
+--------------------------------+----------------+-------+---------+-----+
```

### Detalhe reparacao `/reparacoes/:id`

| Pontos fortes | Pontos fracos | Propostas concretas |
|---|---|---|
| Muito completo: workflow, diagnostico, fotos, despesas, preco/lucro, timeline. | Demasiado vertical em desktop. O utilizador faz muito scroll para perceber estado, cliente e accoes. | Layout 2 colunas: conteudo principal + rail lateral sticky com cliente, estado, total, lucro e accoes. |
| Auto-save e indicador sao muito bons. | Indicador de guardado pode passar despercebido. | Sticky status pequeno no header: `Guardado ha 12s`. |
| Stepper de workflow e essencial. | Pode ocupar demasiado espaco se sempre visivel. | Versao compacta no header + detalhe expandivel. |
| Diagnostico guiado e diferenciador. | Misturado no scroll com blocos administrativos. | Separar em tabs internas: `Resumo`, `Diagnostico`, `Fotos`, `Financeiro`, `Timeline`. |

Mockup proposto:

```text
+---------------------------------------------------------------------+
| Reparacao #1042 - iPhone 12                 Guardado ha 12s [Acao]  |
| Recebido > Diagnostico > Orcamento > Reparacao > Pronto > Entregue  |
+---------------------------------------------+-----------------------+
| [Resumo] [Diagnostico] [Fotos] [Financeiro] | CLIENTE               |
|                                             | Joao Silva            |
| Conteudo da tab                             | 912 345 678           |
|                                             |                       |
|                                             | ESTADO                |
|                                             | Diagnostico           |
|                                             |                       |
|                                             | FINANCEIRO            |
|                                             | PVP 89€ - Margem 42€  |
|                                             |                       |
|                                             | [Enviar WhatsApp]     |
|                                             | [Imprimir folha]      |
+---------------------------------------------+-----------------------+
```

### Definicoes `/definicoes`

| Pontos fortes | Pontos fracos | Propostas concretas |
|---|---|---|
| Tabs fazem sentido: Empresa, Fiscal, Pagamentos, Pos-venda, Aparencia. | Formularios longos sem preview deixam o utilizador inseguro. | Adicionar preview contextual: exemplo de recibo, mensagem, tema ou portal. |
| Auto-save reduz friccao. | Auto-save em definicoes pode assustar se nao houver undo/confirmacao. | Toast discreto `Definicoes guardadas` + historico visual `ultima alteracao`. |
| Copy e hints PT sao bons. | Falta separacao clara entre obrigatorio para Beta e opcional. | Badges: `Obrigatorio`, `Opcional`, `Mais tarde`. |

### Tabela de precos `/precos`

| Pontos fortes | Pontos fracos | Propostas concretas |
|---|---|---|
| Estrutura certa: categoria, marca, modelo, servico, custo, PVP, margem, tempo. | Tabela pode ficar densa e dificil de editar em massa. | Inline editing para PVP/tempo/margem; accoes em menu por linha. |
| Import CSV e filtros existem. | Falta confianca/estado da tabela base. | Mostrar origem: `Base PT 2026`, `Editado pela loja`, `Sem custo definido`. |
| Margem e tempo sao muito bons para onboarding. | Falta fluxo "preencher a minha loja em 10 min". | Wizard: escolher marcas que repara + importar tabela base + ajustar multiplicador Lisboa/interior. |

### Portal cliente publico `/r/:slug`

| Pontos fortes | Pontos fracos | Propostas concretas |
|---|---|---|
| E o ecra mais vendavel: Apple-like, timeline, garantia, avaliacao, fotos, health score. | Pode ficar demasiado decorativo se todos os cards tiverem o mesmo peso. | Manter visual premium, mas dar prioridade ao estado e proxima accao. |
| Mobile-first e claro para cliente final. | Emojis podem parecer menos profissionais dependendo da loja. | Trocar emojis principais por icones consistentes; manter tom humano no texto. |
| Fotos antes/depois ajudam a justificar valor. | Risco de imagens grandes afectarem LCP. | Usar thumbnails responsivas e lazy loading. |

### Portal garantia `/g/:slug`

| Pontos fortes | Pontos fracos | Propostas concretas |
|---|---|---|
| Simples, directo, facil de entender. | Falta talvez uma accao principal muito clara quando a garantia esta activa. | CTA: `Contactar loja sobre esta garantia`. |
| Bom para QR em folha/recibo. | Pode expor pouca confianca se nao mostrar dados da loja claramente. | Header com logo/nome/contacto da loja e data de emissao. |

---

## Quick wins: fazer esta semana

| # | Mudanca | Impacto | Esforco | Onde |
|---:|---|---|---|---|
| 1 | Instalar `lucide-react` e trocar emojis de nav/accoes por icones consistentes. | Alto | 0.5-1 dia | Layout, Dashboard, Reparacoes |
| 2 | Criar componente `Button` com variantes: primary, secondary, ghost, danger, icon. | Alto | 0.5 dia | Global |
| 3 | Criar `PageHeader` reutilizavel: titulo, descricao curta, estado, accao principal. | Alto | 0.5 dia | Todas as paginas internas |
| 4 | Criar `EmptyState` padrao com titulo, texto, CTA e exemplo. | Alto | 0.5 dia | Listas, dashboard, precos |
| 5 | Adicionar toasts com `sonner`: sucesso, erro, aviso, loading promise. | Alto | 0.5 dia | Global |
| 6 | Substituir textos "A carregar..." por skeletons em KPIs/listas/cards. | Alto | 1 dia | Dashboard, Reparacoes, Precos |
| 7 | Uniformizar radius: app interna `rounded-lg`/8px; public portal pode manter mais arredondado. | Medio | 0.5 dia | Global CSS/components |
| 8 | Reordenar dashboard: alertas primeiro, operacao segundo, negocio terceiro. | Alto | 1 dia | Dashboard |
| 9 | Mover import/export para menu `Mais`; deixar `Nova reparacao` como CTA dominante. | Alto | 0.25 dia | Reparacoes |
| 10 | Adicionar `focus-visible` ring consistente em botoes, inputs, tabs e cards clicaveis. | Alto | 0.5 dia | Global |
| 11 | Adicionar `Ultima actualizacao` + botao refresh no dashboard. | Medio | 0.25 dia | Dashboard |
| 12 | Criar componente `StatusBadge` com cor + texto + icone, sem depender so da cor. | Alto | 0.5 dia | Reparacoes, detalhe, portal |
| 13 | Melhorar mensagens de erro: linguagem PT simples + accao (`Tentar novamente`). | Alto | 0.5 dia | Queries/forms |
| 14 | No mobile, limitar bottom nav a 5 itens e mover resto para `Mais`. | Medio | 0.5 dia | Layout |
| 15 | Importar Inter correctamente ou assumir system font sem fingir. | Medio | 0.25 dia | `index.css`/package |

Top 3 para os primeiros 2 dias:

1. `lucide-react` + `Button` + `StatusBadge`.
2. `sonner` + mensagens de erro/sucesso padronizadas.
3. Dashboard reorganizado em `Precisa de atencao` / `Hoje` / `Saude do negocio`.

---

## Refactors UX medios: 1-3 dias cada

| Refactor | Porque vale a pena | Entrega minima |
|---|---|---|
| Design system interno | Acaba com variacoes soltas de botoes, cards, badges, modais. | `components/ui/Button.tsx`, `Card.tsx`, `Badge.tsx`, `PageHeader.tsx`, `EmptyState.tsx`, `Skeleton.tsx`. |
| Dashboard cockpit | Transforma dashboard de "relatorio" em painel de gestao diaria. | Novo layout por zonas, alertas accionaveis, KPIs com trend, listas compactas. |
| Detalhe reparacao 2 colunas | Reduz scroll e torna estado/cliente/accoes sempre visiveis. | Sidebar sticky desktop + tabs internas; mobile mantem stack vertical. |
| DataToolbar + DataTable | Listas ficam consistentes e escalaveis. | Pesquisa, filtros, view mode, menu `Mais`, bulk actions futuras. |
| Modal/Dialog system | Melhora acessibilidade e evita modal dentro de modal sem regra. | Usar Radix Dialog ou melhorar Modal actual com focus trap, title/description e stacking rule. |
| Empty/onboarding states | Ajuda lojas novas a perceber o que fazer sem tutorial longo. | Estados vazios para dashboard, clientes, reparacoes, precos, despesas. |
| Public portal performance pass | Mantem o portal bonito sem prejudicar mobile. | Lazy loading fotos, skeletons, prioridade a estado + CTA. |

## Mudancas grandes: >3 dias

| Mudanca | Quando fazer | Resultado esperado |
|---|---|---|
| Global search + command palette | Sprint visual 2, depois dos quick wins | Bruno/loja pesquisa cliente, IMEI, reparacao, accoes e definicoes com `Ctrl+K`. |
| Redesign completo do dashboard operacional | Antes de 5-10 lojas externas | Dashboard vira cockpit diario, nao mosaico de widgets. |
| Onboarding guiado por dados reais | Depois da primeira loja beta | Nova loja cria empresa, importa clientes/precos e cria primeira reparacao em <15 min. |

Mockup command palette:

```text
+----------------------------------------------+
| Procurar cliente, IMEI, reparacao ou accao   |
+----------------------------------------------+
| Reparacoes                                   |
|  #1042 iPhone 12 - Joao Silva - Diagnostico  |
|  #1039 Samsung A52 - Maria Costa - Pronto    |
|                                              |
| Accoes                                       |
|  Nova reparacao                              |
|  Criar cliente                               |
|  Exportar tabela de precos                   |
+----------------------------------------------+
```

## Componentes a criar/destacar

| Componente | Estado actual | Recomendacao |
|---|---|---|
| Toast notifications | Falta sistema global. | Usar `sonner` para feedback rapido: guardado, erro, import concluido, WhatsApp enviado. |
| Modal/Dialog | Modal proprio existe, mas sem focus trap completo/stacking claro. | Migrar para Radix Dialog ou reforcar Modal actual. Regra: evitar modal dentro de modal; usar sub-step ou drawer. |
| Empty states | Basicos/inconsistentes. | Template reutilizavel com titulo, explicacao curta, CTA e exemplo. |
| Skeleton loaders | Falta. | Skeleton por tipo: KPI, table row, card, chart. Menos spinners/texto. |
| StatusBadge | Parcial/inconsistente. | Estado sempre com label, cor, icon opcional e tooltip quando preciso. |
| DataToolbar | Falta. | Padronizar pesquisa/filtros/view/accoes em reparacoes, clientes, despesas, precos. |
| PageHeader | Falta. | Titulo, subtitulo, breadcrumbs opcional, accao primaria, metadata. |
| Tooltip | Falta/irregular. | Para icon buttons e campos tecnicos. |

Empty state padrao:

```text
+----------------------------------------------+
| Ainda nao ha reparacoes                      |
| Cria a primeira reparacao para testar o      |
| fluxo completo: cliente, equipamento, estado |
| e portal publico.                            |
|                                              |
| [Criar reparacao]   [Importar CSV]           |
+----------------------------------------------+
```

## Paleta, tipografia e spacing

### Brand color

O `brand-500 #0EA5E9` e uma escolha aceitavel para Beta: moderno, limpo, associado a tecnologia/servico. O risco e parecer Tailwind default se for usado em todo o lado.

Recomendacao: **manter para ja**, mas usar `brand-600 #0284C7` como cor principal de botoes quando contraste for importante. Criar tokens semanticos:

| Token | Uso | Cor sugerida |
|---|---|---|
| `brand` | Accao primaria, links activos | `#0284C7` |
| `success` | Pago, concluido, entregue | `#16A34A` |
| `warning` | Aguardar peca, orcamento pendente | `#D97706` |
| `danger` | Erro, apagado, falha pagamento | `#DC2626` |
| `info` | Diagnostico, notas, sugestoes | `#2563EB` |
| `neutral` | App chrome, texto, bordas | zinc/slate |

Alternativas defensaveis se quiseres rebrand depois:

- `#2563EB` blue: mais B2B/enterprise, menos "Tailwind sky".
- `#0F766E` teal: mais assistencia/saude do dispositivo, confianca, menos comum.
- `#0284C7`: manter familia sky, mas com mais presenca e contraste.

### Tipografia

O CSS referencia `"Inter"`, mas se nao houver import/package a app cai para system font. As duas opcoes sao boas:

- **Opcao A recomendada:** instalar `@fontsource/inter` e carregar pesos 400/500/600/700.
- **Opcao B simples:** remover Inter e assumir `system-ui`, que e rapido e nativo.

Para SaaS B2B polido, Inter e boa escolha. So nao deixar "Inter" fantasma sem carregar.

### Spacing

Manter escala Tailwind, mas com regras:

- Page container: `max-w-7xl` para dashboard/listas; `max-w-5xl` para definicoes; `max-w-2xl` para portal publico.
- Page gap: `gap-6`.
- Card padding: `p-4` mobile, `p-5` desktop.
- App card radius: `rounded-lg` (8px). Reservar `rounded-2xl/3xl` para portal publico/marketing.
- Table rows: altura previsivel, sem mudanca de layout em hover.
- Icon buttons: tamanho fixo `h-9 w-9`.

## Inspiracao concreta

| Produto | Padrao a copiar | Como aplicar no RepairDesk | Link |
|---|---|---|---|
| Linear | Command menu e keyboard-first para accoes rapidas. | `Ctrl+K`: procurar IMEI/cliente/reparacao, criar reparacao, mudar tema, ir para definicoes. | [Linear Peek/command pattern](https://linear.app/docs/peek) |
| Pipedrive | Pipeline visual por fases, com filtros e drag/drop. | Melhorar kanban de reparacoes: contadores por estado, ageing, cards com proxima accao. | [Pipedrive deal detail](https://support.pipedrive.com/en/article/deal-detail-view) |
| Stripe Dashboard | Detalhe com resumo, actividade e accoes claras; tabelas fortes. | Reparacao detalhe com header/summary sticky e timeline/eventos mais limpa. | [Stripe Dashboard docs](https://docs.stripe.com/dashboard/basics?locale=en-GB) |
| Vercel | Project list simples, filtros, status badges e empty states limpos. | Clientes/reparacoes/precos com lista densa mas calma, badges consistentes e search acima da lista. | [Vercel managing projects](https://vercel.com/docs/projects/managing-projects) |
| Radix UI | Primitivos acessiveis para dialog, dropdown, tooltip. | Trocar ou reforcar Modal/menus/tooltips sem desenhar acessibilidade de raiz. | [Radix Dialog](https://www.radix-ui.com/primitives/docs/components/dialog) |

## Packages NPM uteis

Instalar poucos, por ordem:

| Package | Usar para | Prioridade |
|---|---|---|
| `lucide-react` | Iconografia consistente, tree-shakeable, TS. | Alta |
| `sonner` | Toasts rapidos e bonitos. | Alta |
| `class-variance-authority` + `clsx` + `tailwind-merge` | Variantes de componentes (`Button`, `Badge`, etc.). | Alta |
| `@radix-ui/react-dialog` | Modal acessivel, focus management. | Media |
| `@radix-ui/react-dropdown-menu` | Menus `Mais`, accoes por linha. | Media |
| `@radix-ui/react-tooltip` | Tooltips em icon buttons. | Media |
| `cmdk` | Command palette global. | Media/Grande |
| `@tanstack/react-table` | Tabelas com sorting/filter/selection quando listas crescerem. | Media |
| `@fontsource/inter` | Carregar Inter correctamente. | Baixa/rapida |

Notas:

- Nao instalar Radix Toast se escolheres `sonner`.
- Nao instalar TanStack Table antes de consolidar `DataToolbar`; pode ser overkill para listas pequenas.
- `cmdk` compoe com Dialog e permite menu global sem criar motor de pesquisa complexo logo no dia 1.

## Anti-padroes a remover

1. **Emojis como icones de produto**: bons em notas pessoais, fracos em SaaS B2B.
2. **Todos os botoes com peso parecido**: o CTA principal deve ganhar; import/export/config ficam secundarios.
3. **Loading textual**: "A carregar..." deve ser fallback, nao estado principal.
4. **Cards demasiado arredondados na app interna**: `rounded-xl/2xl/3xl` em excesso parece mais landing page do que ferramenta operacional.
5. **Modal dentro de modal sem regra**: cria confusao e problemas de foco, sobretudo em mobile.
6. **Dashboard como mosaico de widgets**: precisa de narrativa operacional.
7. **Hover-only actions**: em touch e acessibilidade podem desaparecer.
8. **Erros genericos**: cada erro deve dizer o que aconteceu e o que o utilizador pode fazer.
9. **Componentes inline repetidos por pagina**: aumenta inconsistencias e cansa o Bruno.
10. **Status dependente so de cor**: usar texto, icon e, quando preciso, tooltip.

## Roadmap de implementacao

### Sprint UX-1: Polimento essencial (3-5 dias)

Objectivo: produto deixar de parecer prototipo.

- Instalar `lucide-react`, `sonner`, `class-variance-authority`, `clsx`, `tailwind-merge`.
- Criar `Button`, `Badge`, `StatusBadge`, `PageHeader`, `EmptyState`, `Skeleton`.
- Trocar emojis da sidebar, bottom nav, botoes principais e headings.
- Adicionar Toaster global.
- Skeletons no Dashboard/Reparacoes/Precos.
- Ajustar radius/padding/focus ring global.
- Reordenar dashboard em zonas.

Resultado esperado: **+30% polimento percebido** em menos de uma semana.

### Sprint UX-2: Reparacoes como cockpit (4-7 dias)

Objectivo: melhorar o ecra mais usado.

- `DataToolbar` para reparacoes.
- Kanban com contadores, ageing e cards mais compactos.
- Lista desktop mais tabular, mobile por cards agrupados.
- Detalhe reparacao com header sticky e sidebar summary desktop.
- Modais principais com Radix Dialog ou Modal reforcado.

Resultado esperado: lojas pequenas percebem a app em 5 minutos.

### Sprint UX-3: Onboarding e tabelas (4-6 dias)

Objectivo: primeira loja conseguir configurar-se sem Bruno ao lado.

- Empty states accionaveis.
- Wizard simples para tabela de precos base.
- Settings com preview contextual.
- Erros e sucesso padronizados em forms.
- Melhorar import CSV com preview/validacao.

Resultado esperado: reduzir suporte manual no setup.

### Sprint UX-4: Power features visuais (5-10 dias)

Objectivo: vantagem de produto para 10+ lojas.

- Command palette `Ctrl+K`.
- Pesquisa global por cliente/IMEI/reparacao.
- Melhor dashboard com drill-downs.
- Melhor timeline/event log no detalhe.

Resultado esperado: RepairDesk com sensacao de SaaS maduro, nao so CRUD bonito.

## Metricas a seguir

| Metrica | Porque importa | Alvo Beta |
|---|---|---|
| Tempo ate criar primeira reparacao | Onboarding real. | <5 min com Bruno ao lado; <10 min sozinho. |
| Cliques ate mudar estado e avisar cliente | Core workflow. | <=3 cliques. |
| Tempo para encontrar reparacao por IMEI/cliente | Uso diario. | <10 segundos. |
| Erros reportados como "nao percebi" | UX copy/recovery. | 0-1 por loja/semana. |
| Reparacoes criadas sem ajuda | Produto compreensivel. | >80% depois da demo. |
| Utilizacao de kanban vs lista | Descobrir preferencia real. | Medir, nao assumir. |
| Abandono em modais longos | Friccao de forms. | Rever se >20%. |

## Plano para validar em lojas amigas

1. Antes da demo, aplicar Sprint UX-1.
2. Na demo, observar sem explicar durante 5 minutos:
   - encontra "Nova reparacao"?
   - percebe estados?
   - sabe onde ver cliente/contacto?
   - percebe portal cliente?
3. Pedir a loja para executar 3 tarefas:
   - criar reparacao;
   - mudar estado para "Aguardar peca";
   - consultar garantia/portal.
4. Anotar cada hesitacao como bug UX, nao como "o utilizador e burro".
5. So depois implementar Sprint UX-2.

## Decisao final

Nao atrasar Beta por tentar fazer um redesign perfeito. A UI actual esta suficientemente perto para vender a 2-3 lojas amigas, mas eu faria **Sprint UX-1 antes da primeira demo paga**.

O maior retorno vem de:

1. Sistema visual minimo.
2. Dashboard mais operacional.
3. Reparacao detalhe menos vertical.
4. Feedback/toasts/skeletons.
5. Iconografia profissional.

Isto e o suficiente para sair da zona "produto feito em casa" e entrar na zona "ferramenta seria para oficinas".
