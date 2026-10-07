using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Vendas;

/// <summary>
/// Linha de venda. Com <see cref="CompraLinhaId"/> = unidade(s) de um lote de stock; sem = serviço /
/// mão de obra (IVA sobre o valor todo, sem dedução associada — SPEC §5.5).
/// </summary>
public sealed record VendaLinhaWriteRequest(
    Guid? Id,
    Guid? CompraLinhaId,
    string? Descricao,
    int Quantidade,
    /// <summary>Preço unitário cobrado, com IVA. Para lotes, a UI propõe o preço final do motor (SPEC §3.2).</summary>
    int PrecoUnitarioCents,
    int DescontoCents = 0,
    /// <summary>Taxa de IVA da venda em % (23, 13, 6, 0). Null → taxa normal.</summary>
    decimal? IvaRate = null,
    string? Imei = null);

public sealed record VendaWriteRequest(
    VendaTipo Tipo,
    Guid? ClienteId,
    string? Equipamento,
    string? Problema,
    string? Notas,
    IReadOnlyList<VendaLinhaWriteRequest> Linhas,
    /// <summary>Só na criação: estado inicial (por omissão Orçamento; venda ao balcão = Entregue).</summary>
    VendaEstado? Estado = null,
    PaymentMethod? PaymentMethod = null);

public sealed record MudarEstadoVendaRequest(VendaEstado Estado, PaymentMethod? PaymentMethod = null);

/// <summary>Nº da fatura emitida fora do Mender (Moloni). Vazio = limpar.</summary>
public sealed record RegistarFaturaRequest(string? InvoiceNumber, DateTime? InvoiceEmittedAt);

public sealed record VendaClienteResumo(Guid Id, string Nome, string Telefone);

public sealed record VendaItemDto(
    Guid Id,
    Guid? CompraLinhaId,
    string Descricao,
    int Quantidade,
    int PrecoUnitarioCents,
    int DescontoCents,
    decimal IvaRate,
    int TotalCents,
    int IvaCents,
    string? Imei,
    // Snapshot do lote e contas do motor de IVA (euros, sem arredondar — a UI arredonda).
    decimal? CustoUnitarioPago,
    decimal? TaxaIvaCompra,
    decimal CustoDasPecas,
    decimal IvaAPagarEstado,
    decimal Lucro);

public sealed record VendaDto(
    Guid Id,
    int Numero,
    VendaTipo Tipo,
    VendaEstado Estado,
    DateTime Data,
    DateTime CreatedAt,
    VendaClienteResumo? Cliente,
    string? Equipamento,
    string? Problema,
    int TotalCents,
    int IvaCents,
    decimal IvaAPagarEstado,
    decimal Lucro,
    PaymentMethod PaymentMethod,
    string? InvoiceNumber,
    DateTime? InvoiceEmittedAt,
    bool FaturaPorRegistar,
    string? Notas,
    IReadOnlyList<VendaItemDto> Items);

public sealed record VendaImeiLookupDto(
    Guid VendaId,
    int Numero,
    DateTime Data,
    string Descricao,
    string? ClienteNome);

public sealed record VendaReparacaoRelacionadaDto(
    Guid ReparacaoId,
    int ReparacaoNumero,
    DateTime RecebidoEm,
    string Equipamento,
    string Imei,
    int Estado,
    int DiasDesdeAVenda,
    int? OrcamentoCents);
