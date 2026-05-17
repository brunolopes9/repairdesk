using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Diagnostico;

public sealed record DiagnosticoTemplateDto(
    Guid Id,
    string Nome,
    DeviceCategory Categoria,
    bool IsDefault,
    bool Activo,
    IReadOnlyList<DiagnosticoTemplateItemDto> Items);

public sealed record DiagnosticoTemplateItemDto(
    Guid Id,
    string Label,
    string? Descricao,
    string? Grupo,
    int Ordem,
    int Peso);

public sealed record CreateTemplateRequest(
    string Nome,
    DeviceCategory Categoria,
    bool IsDefault,
    IReadOnlyList<CreateTemplateItemRequest> Items);

public sealed record CreateTemplateItemRequest(
    string Label,
    string? Descricao,
    string? Grupo,
    int Ordem,
    int Peso);

public sealed record DiagnosticoExecucaoDto(
    Guid Id,
    Guid ReparacaoId,
    Guid? TemplateId,
    string? TemplateNomeSnapshot,
    DeviceCategory Categoria,
    DateTime? CompletadoEm,
    string? NotasGerais,
    int? Score,
    IReadOnlyList<DiagnosticoExecucaoItemDto> Items);

public sealed record DiagnosticoExecucaoItemDto(
    Guid Id,
    string Label,
    string? Descricao,
    string? Grupo,
    int Ordem,
    int Peso,
    DiagnosticoResultado Resultado,
    string? Notas);

public sealed record StartExecucaoRequest(Guid? TemplateId, DeviceCategory? Categoria);

public sealed record UpdateExecucaoItemRequest(
    Guid ItemId,
    DiagnosticoResultado Resultado,
    string? Notas);

public sealed record UpdateExecucaoRequest(
    string? NotasGerais,
    bool MarcarCompletado,
    IReadOnlyList<UpdateExecucaoItemRequest> Items);
