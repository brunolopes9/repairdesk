using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.PriceTable;

public sealed record PriceTableEntryDto(
    Guid Id,
    DeviceCategory Categoria,
    string Marca,
    string Modelo,
    string Servico,
    int? CustoPecaCents,
    int PvpCents,
    int? TempoEstimadoMin,
    string? Notas,
    bool Activo,
    int? MargemPct);

public sealed record CreatePriceEntryRequest(
    DeviceCategory Categoria,
    string Marca,
    string Modelo,
    string Servico,
    int? CustoPecaCents,
    int PvpCents,
    int? TempoEstimadoMin,
    string? Notas);

public sealed record UpdatePriceEntryRequest(
    DeviceCategory Categoria,
    string Marca,
    string Modelo,
    string Servico,
    int? CustoPecaCents,
    int PvpCents,
    int? TempoEstimadoMin,
    string? Notas,
    bool Activo);

public sealed record ImportPriceTableRequest(string Csv);

public sealed record PriceImportError(int Linha, string Campo, string Mensagem, string? ValorOriginal);

public sealed record ImportPriceTableResponse(
    int TotalLinhas,
    int Criadas,
    int Ignoradas,
    int ComErro,
    IReadOnlyList<PriceImportError> Erros);
