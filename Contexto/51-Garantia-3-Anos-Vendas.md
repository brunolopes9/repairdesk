# 51 — Garantia 3 anos para Vendas (DL 84/2021)

## Problema

A entidade `Garantia` no RepairDesk só liga a `Reparacao`:

```csharp
public class Garantia : BaseEntity, ITenantEntity
{
    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }
    // ...
}
```

Quando se emite uma fatura de **Venda** (telemóvel novo ou refurbished), **não há garantia digital gerada**. Isto é um gap operacional e legal.

## Enquadramento legal PT

**DL 84/2021** (Decreto-Lei n.º 84/2021, de 18 de outubro) — substituiu o DL 67/2003 e transpõe a Diretiva (UE) 2019/771. Em vigor desde **1 de janeiro de 2022**.

Pontos chave para a LopesTech:
- **Bens móveis novos**: garantia de **3 anos** (até 31/12/2021 eram 2). Art. 12.º n.º 1.
- **Bens móveis em segunda mão (refurbished)**: garantia mínima de **3 anos**, sendo possível acordar prazo inferior **nunca menos de 18 meses** (Art. 12.º n.º 4) — tem de constar no contrato/fatura **expressamente**.
- **Ónus da prova** (presunção de não conformidade) durante **2 anos** desde a entrega (vs 6 meses no regime anterior). Art. 13.º n.º 4.
- **Conteúdos/serviços digitais** associados (ex: telemóveis com firmware/apps): aplica-se também (Art. 2.º al. d) e Capítulo III).
- **Soluções do consumidor**: reposição da conformidade (reparação ou substituição), redução do preço, resolução do contrato. Art. 15.º.
- **Direito de regresso** do vendedor sobre o fornecedor (Art. 21.º) — Bruno pode reclamar à Molano se um equipamento falhar em garantia.

## Impacto para a LopesTech

O Bruno vai vender:
1. **Telemóveis refurbished** (Molano dropshipping) — 3 anos por defeito; pode reduzir até 18 meses **se contratualizado expressamente**
2. **Acessórios novos** (capas, carregadores) — 3 anos sempre
3. **Equipamentos reparados** (já cobertos pelo sistema actual via `Garantia` ↔ `Reparacao`)

Sem emissão automática de garantia digital nas Vendas:
- Cliente reclama em 2026 → Bruno não tem prova fácil da data de entrega/cobertura
- Cliente perde a fatura → não sabe o que está coberto
- Quando há litígio, ónus da prova está com Bruno durante 2 anos
- Disputas via DECO/portais → sem documentação digital = posição fraca

## Soluções de design

### Opção A — Refactor `Garantia` para poliforme

```csharp
public class Garantia : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? ReparacaoId { get; set; }   // nullable agora
    public Reparacao? Reparacao { get; set; }
    public Guid? VendaId { get; set; }       // novo, nullable
    public Venda? Venda { get; set; }
    // resto igual
}
```

**Constraint**: exatamente um de `ReparacaoId` ou `VendaId` preenchido.

**Vantagens**: portal público `/g/{slug}` único, dashboards/filtros unificados, reutilização de tudo o que já existe (cobertura, exclusões, anulação, slug, PDF render).

**Custos**: migration (alterar coluna `ReparacaoId` para nullable + adicionar `VendaId` + check constraint), tocar em RGPD service (carregar garantias por venda), tocar no public portal para suportar venda.

### Opção B — Nova entity `VendaGarantia`

```csharp
public class VendaGarantia : BaseEntity, ITenantEntity { ... }
```

**Vantagens**: zero impacto no que já existe.

**Custos**: duplicação de portal/PDF/lifecycle. Cliente final vê duas URLs diferentes ("/g/X" para reparação, "/gv/Y" para venda) — má UX.

**Recomendação:** **Opção A**. A entidade é conceptualmente a mesma garantia legal; só varia o documento origem.

## Auto-emissão

Trigger: quando `VendaService.MarcarPagaAsync` completa com `Status = Paga`, criar garantia automática.

```csharp
// Em VendaService, depois de venda.Status = Paga e antes de retornar:
var diasGarantia = settings.GarantiaVendaDias ?? 1095;   // 3 anos default
var garantia = new Garantia
{
    TenantId = tenantId,
    VendaId = venda.Id,
    Slug = GarantiaSlugGenerator.Next(),
    DataInicio = venda.Data,
    DataFim = venda.Data.AddDays(diasGarantia),
    DiasGarantia = diasGarantia,
    Cobertura = settings.GarantiaVendaCoberturaPadrao
        ?? "Conformidade do bem com o descrito na fatura (DL 84/2021).",
    Exclusoes = settings.GarantiaVendaExclusoesPadrao
        ?? "Danos por uso indevido, líquidos, quedas, abertura do equipamento, desgaste normal.",
};
```

Bruno pode parametrizar nas Definições:
- Dias garantia padrão para Vendas (default 1095 = 3 anos)
- Cobertura textual padrão (já com referência ao DL 84/2021)
- Exclusões padrão

## Portal público

`/g/{slug}` continua a funcionar. Adaptar `PortalGarantia.tsx` para mostrar:
- Se `garantia.venda != null`: mostrar dados da venda (artigos, IMEI, fatura)
- Se `garantia.reparacao != null`: mostrar dados da reparação (já existe)

## Sequência sugerida

1. **Esperar** Codex sprints (#C17-C22) mergidas em main
2. **Sprint 58.1**: migration + entity poliforme + EF config + portal adaptado
3. **Sprint 58.2**: auto-emit em `MarcarPagaAsync` + settings na Definições
4. **Sprint 58.3**: campo nas Vendas para "Período garantia" sobrescrever default (caso refurbished com 18 meses contratualizado)
5. **Sprint 58.4**: PDF garantia com texto legal DL 84/2021 + link Direito de Regresso

## KPIs depois de lançar

Dashboard widget:
- Garantias activas / a expirar nos próximos 30 dias
- Reclamações em garantia (taxa) — alimenta decisão de continuar com Molano ou mudar fornecedor

## Riscos

- **Migration em zona crítica**: Codex acabou de criar `Sprint60EstimateFields` em 2026-05-19. Adicionar outra migration imediatamente sem ter as sprints dele mergidas pode causar conflitos. Esperar.
- **Reduzir abaixo de 3 anos**: requer **acordo expresso** ao consumidor, escrito na fatura. Pôr campo `GarantiaMesesAcordados` na Venda + render no PDF da fatura/recibo.
- **IMEI tracking**: idealmente o IMEI fica registado no `VendaItem` quando peça é Telemóvel — facilita registo na garantia. Cruza com [[project-imei-autoridades]].

## Referências

- [DL 84/2021 — Diário da República](https://diariodarepublica.pt/dr/detalhe/decreto-lei/84-2021-172938301)
- Diretiva (UE) 2019/771 (transposição)
- Anexo do DL 84/2021 — Modelo informativo a entregar ao consumidor
