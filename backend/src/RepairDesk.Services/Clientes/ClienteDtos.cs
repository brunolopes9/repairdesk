namespace RepairDesk.Services.Clientes;

public sealed record CreateClienteRequest(
    string Nome,
    string? Telefone,
    string? Email,
    string? Nif,
    string? Notas,
    string? NotaImportante = null);

public sealed record UpdateClienteRequest(
    string Nome,
    string? Telefone,
    string? Email,
    string? Nif,
    string? Notas,
    string? NotaImportante = null);

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
    string? NotaImportante = null);

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
