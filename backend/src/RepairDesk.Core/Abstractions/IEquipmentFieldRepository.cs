using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IEquipmentFieldRepository
{
    Task<IReadOnlyList<EquipmentFieldTemplate>> ListTemplatesAsync(bool includeInactive, CancellationToken ct = default);
    Task<IReadOnlyList<EquipmentFieldTemplate>> ListActiveTemplatesAsync(CancellationToken ct = default);
    Task<EquipmentFieldTemplate?> FindTemplateAsync(Guid id, bool includeFields = true, CancellationToken ct = default);
    Task<int> CountActiveTemplatesAsync(CancellationToken ct = default);
    Task<bool> AnyTemplateAsync(CancellationToken ct = default);
    Task<bool> HasActiveReparacoesUsingTemplateAsync(Guid templateId, CancellationToken ct = default);
    Task<bool> HasValuesForDefinitionAsync(Guid definitionId, CancellationToken ct = default);
    Task<IReadOnlyList<EquipmentFieldValue>> ListValuesByReparacaoAsync(Guid reparacaoId, bool includeDefinition = true, CancellationToken ct = default);
    Task<IReadOnlyList<EquipmentFieldValue>> ListVisiblePortalValuesByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    void AddTemplate(EquipmentFieldTemplate template);
    void AddValue(EquipmentFieldValue value);
    void RemoveTemplate(EquipmentFieldTemplate template);
    void RemoveDefinition(EquipmentFieldDefinition definition);
    void RemoveValue(EquipmentFieldValue value);
    Task SaveAsync(CancellationToken ct = default);
}
