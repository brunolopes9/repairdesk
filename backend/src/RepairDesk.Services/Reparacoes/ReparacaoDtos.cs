using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Reparacoes;

public sealed record CreateReparacaoRequest(
    Guid ClienteId,
    string Equipamento,
    string Avaria,
    string? Imei,
    int? OrcamentoCents,
    string? Notas,
    RepairStatus? EstadoInicial = null);

public sealed record UpdateReparacaoRequest(
    string Equipamento,
    string Avaria,
    string? Imei,
    string? Diagnostico,
    int? OrcamentoCents,
    bool OrcamentoAprovado,
    int? PrecoFinalCents,
    int CustoPecasCents,
    decimal HorasGastas,
    string? Notas,
    PaymentStatus EstadoPagamento,
    Guid? ClienteId = null);

public sealed record ChangeEstadoRequest(RepairStatus Estado, string? Notas);

public sealed record ClienteResumo(Guid Id, string Nome, string Telefone);

public sealed record EstadoLogDto(
    Guid Id,
    RepairStatus? EstadoFrom,
    RepairStatus EstadoTo,
    DateTime MudouEm,
    string? Notas);

public sealed record ReparacaoDto(
    Guid Id,
    int Numero,
    ClienteResumo Cliente,
    string Equipamento,
    string Avaria,
    string? Imei,
    string? Diagnostico,
    RepairStatus Estado,
    DateTime EstadoSince,
    DateTime RecebidoEm,
    DateTime? EntregueEm,
    int? OrcamentoCents,
    bool OrcamentoAprovado,
    int? PrecoFinalCents,
    int CustoPecasCents,
    decimal HorasGastas,
    int LucroCents,
    int CustoDespesasCents,
    string? Notas,
    PaymentStatus EstadoPagamento,
    string? PublicSlug);

public sealed record ReparacaoDetalhadaDto(
    ReparacaoDto Reparacao,
    IReadOnlyList<EstadoLogDto> Timeline);

public sealed record ReparacaoHistoricoItem(
    Guid Id,
    int Numero,
    string Equipamento,
    string? Imei,
    ClienteResumo Cliente,
    RepairStatus Estado,
    DateTime RecebidoEm,
    DateTime? EntregueEm,
    int? PrecoFinalCents,
    string? Diagnostico);

public sealed record ReparacaoHistoricoResponse(
    string Imei,
    bool LuhnValido,
    int Total,
    IReadOnlyList<ReparacaoHistoricoItem> Items);

public sealed record ImportReparacoesRequest(string Csv);

public sealed record ImportReparacaoError(int Linha, string Campo, string Mensagem, string? ValorOriginal);

public sealed record ImportReparacoesResponse(
    int TotalLinhas,
    int Criadas,
    int ClientesCriados,
    int ClientesReutilizados,
    int ComErro,
    IReadOnlyList<ImportReparacaoError> Erros);
