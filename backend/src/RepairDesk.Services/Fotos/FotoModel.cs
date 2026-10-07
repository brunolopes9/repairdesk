using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Fotos;

public sealed record FotoDto(
    Guid Id,
    Guid VendaId,
    string FileName,
    string ContentType,
    long Size,
    FotoTipo Tipo,
    int Ordem,
    string? Legenda,
    bool VisivelNoPortal,
    DateTime CriadaEm);

public sealed record UpdateFotoRequest(
    FotoTipo Tipo,
    int Ordem,
    string? Legenda,
    bool VisivelNoPortal);
