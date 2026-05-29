# 87 — "Catálogo & Stock": unificar Stock (Part) + Produtos (Product) (Bruno 2026-05-27)

Ref no disco: `RepairDesk/catalogo+stock.png`. Substitui as secções **Stock** e **Produtos** por
**uma só**: "Catálogo & Stock" com árvore **produto pai → variantes**.

## Decisões do Bruno (2026-05-27)
1. **Unificação UI/leitura, NÃO fundir entidades na BD.** Mantém `Part` (stock operacional) e
   `Product`/`ProductModel` (retail) separados. Uma página + endpoint de leitura junta tudo. As
   ESCRITAS continuam a ir para a entidade certa. Motivo: `Part` está acoplado a vendas (VendaItem),
   reparações, kits, dashboard, faturas-fornecedor, relatórios IVA, POS → fundir = semanas + regressão.
2. **v1 inclui retail + peças técnicas** (como o mockup): telemóveis (Product) E peças/acessórios
   (Part) na mesma lista, agrupados por modelo.

## O que JÁ existe (não construir)
- **`ProductModel`** (S359) = produto pai/template: Marca+Modelo, descrição, SpecsJson, imagens
  marketing, preço bateria. Chave única (Tenant, Brand, Model). Tem `Units` (variantes).
- **`Product`** = variante/unidade: cor, storage, grade, fornecedor, preço, stock, SKU,
  MostrarLojaOnline, SupplyType (Stock=físico / Dropship=virtual), estado técnico. Liga via `ModelId`,
  **herda** conteúdo do modelo com override. Inheritance testada (ProductModelInheritanceTests).
- **`Part`** = peça/equipamento técnico: Sku, Nome, Categoria, Marca, Modelo, QtdStock, QtdMinima,
  CustoUnitarioCents, Fornecedor, MostrarLojaOnline. Sempre stock físico.

## Mapeamento mockup → dados
- **Linha pai** = `ProductModel` (retail agrupado) · OU grupo de `Part` por (Marca, Modelo) · OU
  `Product`/`Part` standalone (sem modelo) como pai de 1 variante.
- **Variante** = `Product` (retail) ou `Part` (técnico).
- **Tipo stock** Físico/Virtual = `Product.SupplyType` (Stock/Dropship); `Part` = sempre Físico.
- **Loja online** toggle = `MostrarLojaOnline` (existe em ambos).
- **Conteúdo** Completo/Incompleto = tem descrição+imagens+SEO? (Product/Model). Part = N/A.
- **Tabs**: Todos · Stock físico · Stock virtual · Loja online · Sem conteúdo · Stock crítico.
- **KPIs**: Stock físico (un + custo) · Stock virtual (un) · Publicados na loja (% catálogo) ·
  Stock crítico · Sem conteúdo.
- **Painel direito** (pai selecionado): Visão geral (conteúdo herdado) · Variantes · Histórico ·
  Preços + Ações rápidas (Nova variante, Importar CSV, Sincronizar loja, Corrigir conteúdo).

## Plano por fases
- **Fase 1 (S385) — Backend read model.** `CatalogService` + DTOs (CatalogParentDto, CatalogVariantDto,
  CatalogKpisDto) + `CatalogController` GET /api/catalog (filtros: q, categoria, marca, fornecedor,
  canal, estado, tab) + GET /api/catalog/kpis. Junta Product/ProductModel/Part. Read-only. Tests +
  RolesMatrix hash. **← começar aqui.**
- **Fase 2 (S386) — Frontend shell.** Página /catalogo: header + KPI row + tabs + filtros + tabela de
  linhas-pai (colapsadas). Primitivas KpiCard/SectionCard. Read-only.
- **Fase 3 (S387) — Expandir variantes + painel direito** (detalhe do pai, herança, tab Variantes).
- **Fase 4 (S388) — Ações + nav.** Toggles loja online, nova variante, sincronizar, importar CSV.
  Substituir entradas de nav Stock + Produtos por "Catálogo & Stock" (manter /stock e /produtos como
  rotas vivas/deep-links durante transição).

## Notas
- Não partir a POS/Stock existentes — são fontes de escrita. A página nova é uma LENTE de leitura +
  atalhos para as ações que já existem.
- Sessão atual muito longa (24+ sprints) — cada fase é sprint própria, completa, com build+deploy.
