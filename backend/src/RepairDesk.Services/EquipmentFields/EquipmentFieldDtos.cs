using RepairDesk.Core.Enums;

namespace RepairDesk.Services.EquipmentFields;

public sealed record EquipmentFieldDefinitionDto(
    Guid Id,
    string Label,
    EquipmentFieldType Type,
    IReadOnlyList<string> Options,
    bool Required,
    int Ordem,
    bool VisibleInPortal);

public sealed record EquipmentFieldTemplateDto(
    Guid Id,
    string Nome,
    DeviceCategory Categoria,
    bool IsActive,
    int Ordem,
    IReadOnlyList<EquipmentFieldDefinitionDto> Fields);

public sealed record UpsertEquipmentFieldDefinitionRequest(
    Guid? Id,
    string Label,
    EquipmentFieldType Type,
    IReadOnlyList<string>? Options,
    bool Required,
    int Ordem,
    bool VisibleInPortal);

public sealed record CreateEquipmentFieldTemplateRequest(
    string Nome,
    DeviceCategory Categoria,
    bool IsActive,
    IReadOnlyList<UpsertEquipmentFieldDefinitionRequest> Fields);

public sealed record UpdateEquipmentFieldTemplateRequest(
    string Nome,
    DeviceCategory Categoria,
    bool IsActive,
    IReadOnlyList<UpsertEquipmentFieldDefinitionRequest> Fields);

public sealed record ReorderEquipmentFieldTemplatesRequest(IReadOnlyList<Guid> Ids);

public sealed record SetEquipmentFieldValueRequest(Guid FieldDefinitionId, string? Value);

public sealed record SetEquipmentFieldValuesRequest(
    Guid? TemplateId,
    IReadOnlyList<SetEquipmentFieldValueRequest> Values);

public sealed record EquipmentFieldValueDto(
    Guid FieldDefinitionId,
    string Label,
    EquipmentFieldType Type,
    string? Value,
    bool Required,
    bool VisibleInPortal,
    int Ordem);
