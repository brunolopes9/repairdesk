# 46 - Mobile Audit Results

Data: 2026-05-19
Branch: `codex/sprint-55-mobile-audit`

## Resumo

Objetivo: tornar o RepairDesk mais seguro para uso em tablet/telemovel no balcao, sem mexer na logica de negocio.

Resultado desta ronda:

- Touch targets normalizados: botoes de texto com `min-h-11`, botoes icon-only com `h-10 w-10`, inputs/selects com `min-h-11`.
- Tabelas densas protegidas com `overflow-x-auto` e `min-w-*` consistente.
- Modais com `max-h-[90vh] overflow-y-auto` e footer em stack no mobile.
- Skeletons consistentes em queries principais e componentes encaixados no detalhe.
- Grids convertidas para mobile-first: `grid-cols-1 sm:* lg:*`.
- Sem alteracao de regras de negocio, endpoints, models ou fluxo de dados.

## Mapa pagina-a-pagina

| Pagina | Antes | Depois |
|---|---|---|
| `/clientes` | Lista ja era card-based, mas botoes/import tinham alvos pequenos. | Search e paginacao com 44px, acoes em stack, import CSV com botao grande. |
| `/clientes/:id` | Loading textual e header apertado em mobile. | Skeleton de detalhe, header em stack, KPI grid `1/2/4`, links telefone/WhatsApp maiores. |
| `/reparacoes` | Tabs e paginacao pequenas, filtros e modais com alguns controlos densos. | Tabs scrollaveis, search 44px, paginacao 44px, modal batch com stack, checkboxes ampliadas. |
| `/reparacoes/:id` | Header de breadcrumb/acoes apertado e alguns inputs pequenos. | Header mobile-first, acoes wrap, skeleton inicial, inputs de workflow com 44px. |
| `/trabalhos` | Mesmo padrao antigo de tabs/filtros/paginacao. | Tabs scrollaveis, filtros em stack no mobile, cards e acoes com touch targets seguros. |
| `/trabalhos/:id` | Header/acoes e input de workflow pequenos. | Header em stack, botoes `Reabrir/Apagar` maiores, skeleton inicial. |
| `/despesas` | Filtros lado a lado e paginacao pequena. | Filtros stack mobile, delete icon 40px, cards responsivos, paginacao 44px. |
| `/stock` | Tabela grande dependia demasiado do viewport. | SkeletonTable horizontal, tabela `min-w-[920px]`, filtros stack, formularios 1 coluna em mobile. |
| `/precos` | Tabela densa e modal com grids fixas. | Tabela `min-w-[900px]`, skeleton com mesma largura, forms `1 -> 2/3 col`, import responsivo. |
| `/auditoria` | Layout filtros + tabela podia ficar comprimido. | Layout `1 coluna -> sidebar/tabela`, chips 40px, inputs 44px, tabela `min-w-[760px]`. |
| `/relatorios/iva` | Segmentos/inputs/documentos ainda apertados. | Inputs 44px, tabela `min-w-[760px]`, skeleton de cards + tabela. |
| `/definicoes` | Tabs e campos personalizados eram os mais frageis em mobile. | Tabs com scroll horizontal, skeletons, builder de campos com checkboxes maiores e footer em stack. |
| `/vendas` | POS ja estava parcialmente polido, mas historico e modal tinham botoes pequenos. | Historico com SkeletonTable, datas/export 44px, acoes do modal maiores, tabela `min-w-[820px]`. |
| Portal publico `/r/:slug` e `/g/:slug` | Loading textual. | Skeleton inicial para evitar texto solto. |

## Componentes partilhados alterados

- `Button`: tamanhos base agora respeitam touch targets.
- `Modal`: scroll interno seguro em mobile, footer responsivo.
- `PageHeader`: acoes fazem wrap e ocupam largura no mobile.
- `SkeletonTable`: aceita `minWidth` e ja vem dentro de `overflow-x-auto`.
- `Layout` e `HealthIndicator`: botoes do header com alvos maiores.
- `DespesasImputadas`, `PecasUsadas`, `FotosReparacao`, `DiagnosticoGuiado`: skeletons e controlos mobile-first no detalhe.

## Antes/depois ASCII

### Lista mobile

Antes:

```text
[Filtro][Pesquisa...............]
[Card com conteudo longo        ][x]
<- Ant   1/4   Seg ->
```

Depois:

```text
[Filtro                    ]
[Pesquisa.................]

[Card]
  titulo
  meta
  acoes / valor

[ Anterior ]   1/4   [ Seguinte ]
```

### Tabela densa

Antes:

```text
| SKU | Nome | Marca | Stock | Min | Custo | ... |
<- corta/encolhe em 390px ->
```

Depois:

```text
+------------------------------------ viewport ------------------------------------+
| scroll-x: | SKU | Nome | Marca | Stock | Min | Custo | Valor | Estado | Acoes |
+----------------------------------------------------------------------------------+
```

### Modal mobile

Antes:

```text
[ Modal ]
campo | campo
campo | campo
Cancelar Guardar
```

Depois:

```text
[ Modal - max 90vh, scroll interno ]
campo
campo
campo

[ Guardar          ]
[ Cancelar         ]
```

## Checklist de verificacao

- `npm.cmd run build`: passou.
- `dotnet test`: passou, 135 testes.
- Browser smoke em `390x844`: `http://127.0.0.1:5173/login` sem overflow horizontal.
- Vite dev server iniciado em `http://127.0.0.1:5173`.

## Pendentes recomendados

1. Fazer uma ronda visual autenticada com dados reais em `/clientes`, `/reparacoes`, `/stock`, `/definicoes` e `/vendas`.
2. Afinar paginas fora do scope desta ronda, especialmente `Dashboard`, `Login` e `OnboardingWizard`.
3. Adicionar teste visual simples no futuro: Playwright com viewport `390x844` para confirmar `documentElement.scrollWidth <= clientWidth` nas rotas principais.
