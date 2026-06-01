namespace RepairDesk.Services.Clientes;

public sealed record CreateClienteRequest(
    string Nome,
    string? Telefone,
    string? Email,
    string? Nif,
    string? Notas,
    string? NotaImportante = null,
    string? ContactoPreferido = null,
    bool AceitaMarketing = false,
    bool NaoContactar = false);

public sealed record UpdateClienteRequest(
    string Nome,
    string? Telefone,
    string? Email,
    string? Nif,
    string? Notas,
    string? NotaImportante = null,
    string? ContactoPreferido = null,
    bool AceitaMarketing = false,
    bool NaoContactar = false);

public sealed record ClienteDto(
    Guid Id,
    string Nome,
    string? Telefone,
    string? Email,
    string? Nif,
    string? Notas,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    /// <summary>Sprint 355: alerta destacado.</summary>
    string? NotaImportante = null,
    /// <summary>Sprint 479: canal preferido para contacto.</summary>
    string? ContactoPreferido = null,
    bool AceitaMarketing = false,
    bool NaoContactar = false,
    IReadOnlyList<ClienteTagSummaryDto>? Tags = null);

/// <summary>Sprint 480: customer segment tag embedded in ClienteDto.</summary>
public sealed record ClienteTagSummaryDto(Guid Id, string Nome, string CorHex);

public sealed record SetClienteTagsRequest(Guid[]? TagIds);

public sealed record ClienteEquipamentoDto(
    string Nome,
    string? Imei,
    DateTime PrimeiroRegistoEm,
    DateTime UltimoRegistoEm,
    int ReparacoesCount,
    int VendasCount,
    Guid? UltimaReparacaoId,
    int? UltimaReparacaoNumero,
    Guid? UltimaVendaId,
    int? UltimaVendaNumero);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record ImportClientesRequest(string Csv);

public sealed record ImportError(int Linha, string Campo, string Mensagem, string? ValorOriginal);

public sealed record ImportClientesResponse(
    int TotalLinhas,
    int Criados,
    int Ignorados,
    int ComErro,
    IReadOnlyList<ClienteDto> ClientesCriados,
    IReadOnlyList<ImportError> Erros);
