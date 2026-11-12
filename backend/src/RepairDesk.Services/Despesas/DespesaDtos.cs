using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Despesas;

public sealed record CreateDespesaRequest(
    string Descricao,
    DespesaCategoria Categoria,
    int ValorCents,
    DateTime? Data,
    string? Fornecedor,
    string? NumeroEncomenda,
    string? Notas,
    bool IsCogs = false,
    bool IsRecorrente = false,
    int? PeriodicidadeMeses = null,
    // Sprint 525: compra intra-UE em autoliquidação — IVA não dedutível em PT.
    bool ReverseCharge = false,
    // Sprint 573: IVA da fatura e % dedutível (apuramento trimestral).
    int? IvaCents = null,
    int DeducaoPercent = 100,
    AfetacaoIva Afetacao = AfetacaoIva.LopesTech,
    DespesaAmbito Ambito = DespesaAmbito.Negocio,
    DespesaNatureza Natureza = DespesaNatureza.Gasto,
    /// <summary>Sprint 644: sem fatura com o NIF da empresa — só gestão, fora do fiscal.</summary>
    bool SemFaturaValida = false);

public sealed record UpdateDespesaRequest(
    string Descricao,
    DespesaCategoria Categoria,
    int ValorCents,
    DateTime Data,
    string? Fornecedor,
    string? NumeroEncomenda,
    string? Notas,
    bool IsCogs = false,
    bool IsRecorrente = false,
    int? PeriodicidadeMeses = null,
    int? IvaCents = null,
    int DeducaoPercent = 100,
    bool ReverseCharge = false,
    AfetacaoIva Afetacao = AfetacaoIva.LopesTech,
    DespesaAmbito Ambito = DespesaAmbito.Negocio,
    DespesaNatureza Natureza = DespesaNatureza.Gasto,
    /// <summary>Sprint 644: sem fatura com o NIF da empresa — só gestão, fora do fiscal.</summary>
    bool SemFaturaValida = false);

/// <summary>
/// Sprint 646: classificação em lote (livros fiscal/gestão) — por nº visível. Só muda o que vem preenchido;
/// «Nota» junta-se às notas existentes (ex.: «duplicado de FT MV-595155249»).
/// </summary>
public sealed record ClassificarDespesa(int Numero, bool? SoGestao = null, DespesaAmbito? Ambito = null, AfetacaoIva? Afetacao = null, string? Nota = null);

public sealed record ClassificarDespesasResultado(int Atualizadas, IReadOnlyList<int> NaoEncontradas);

/// <summary>Sprint 574: resumo do filtro inteiro (todas as páginas).</summary>
public sealed record DespesasResumoDto(int Registos, int TotalCents, int IvaCents, int SemIva, int Recorrentes,
    IReadOnlyList<Core.Abstractions.DespesaResumoCategoria> PorCategoria);

public sealed record DespesaDto(
    Guid Id,
    string Descricao,
    DespesaCategoria Categoria,
    int ValorCents,
    DateTime Data,
    string? Fornecedor,
    string? NumeroEncomenda,
    string? Notas,
    DateTime CreatedAt,
    // Sprint 176/177: COGS flag — peça consumida em reparação (não OpEx).
    bool IsCogs,
    bool IsRecorrente,
    int? PeriodicidadeMeses,
    int? IvaCents = null,
    int DeducaoPercent = 100,
    bool ReverseCharge = false,
    /// <summary>Sprint 584: nº visível (sequencial por empresa).</summary>
    int Numero = 0,
    /// <summary>Sprint 586: a fatura (PDF) desta despesa, para abrir e imprimir; null = sem PDF anexado.</summary>
    Guid? SupplierInvoiceImportId = null,
    AfetacaoIva Afetacao = AfetacaoIva.LopesTech,
    DespesaAmbito Ambito = DespesaAmbito.Negocio,
    DespesaNatureza Natureza = DespesaNatureza.Gasto,
    /// <summary>Sprint 644: sem fatura com o NIF da empresa — só gestão, fora do fiscal.</summary>
    bool SemFaturaValida = false);


