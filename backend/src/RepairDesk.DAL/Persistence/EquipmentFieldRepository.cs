using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class EquipmentFieldRepository : IEquipmentFieldRepository
{
    private readonly AppDbContext _db;

    public EquipmentFieldRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<EquipmentFieldTemplate>> ListTemplatesAsync(bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.EquipmentFieldTemplates
            .Include(t => t.Fields.OrderBy(f => f.Ordem))
            .AsQueryable();
        if (!includeInactive) q = q.Where(t => t.IsActive);
        return await q.OrderBy(t => t.Ordem).ThenBy(t => t.Nome).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EquipmentFieldTemplate>> ListActiveTemplatesAsync(CancellationToken ct = default)
        => await _db.EquipmentFieldTemplates
            .AsNoTracking()
            .Include(t => t.Fields.OrderBy(f => f.Ordem))
            .Where(t => t.IsActive)
            .OrderBy(t => t.Ordem)
            .ThenBy(t => t.Nome)
            .ToListAsync(ct);

    public Task<EquipmentFieldTemplate?> FindTemplateAsync(Guid id, bool includeFields = true, CancellationToken ct = default)
    {
        var q = _db.EquipmentFieldTemplates.AsQueryable();
        if (includeFields) q = q.Include(t => t.Fields.OrderBy(f => f.Ordem));
        return q.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public Task<int> CountActiveTemplatesAsync(CancellationToken ct = default)
        => _db.EquipmentFieldTemplates.CountAsync(t => t.IsActive, ct);

    public Task<bool> AnyTemplateAsync(CancellationToken ct = default)
        => _db.EquipmentFieldTemplates.AnyAsync(ct);

    public Task<bool> HasActiveReparacoesUsingTemplateAsync(Guid templateId, CancellationToken ct = default)
        => _db.Reparacoes.AnyAsync(r =>
            r.EquipmentFieldTemplateId == templateId &&
            r.Estado != RepairStatus.Entregue &&
            r.Estado != RepairStatus.Cancelado, ct);

    public Task<bool> HasValuesForDefinitionAsync(Guid definitionId, CancellationToken ct = default)
        => _db.EquipmentFieldValues.AnyAsync(v => v.FieldDefinitionId == definitionId, ct);

    public async Task<IReadOnlyList<EquipmentFieldValue>> ListValuesByReparacaoAsync(Guid reparacaoId, bool includeDefinition = true, CancellationToken ct = default)
    {
        var q = _db.EquipmentFieldValues.AsNoTracking().Where(v => v.ReparacaoId == reparacaoId);
        if (includeDefinition) q = q.Include(v => v.FieldDefinition);
        return await q
            .OrderBy(v => v.FieldDefinition!.Ordem)
            .ThenBy(v => v.FieldDefinition!.Label)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EquipmentFieldValue>> ListVisiblePortalValuesByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
        => await _db.EquipmentFieldValues
            .AsNoTracking()
            .Include(v => v.FieldDefinition)
            .Where(v => v.ReparacaoId == reparacaoId && v.FieldDefinition != null && v.FieldDefinition.VisibleInPortal)
            .OrderBy(v => v.FieldDefinition!.Ordem)
            .ThenBy(v => v.FieldDefinition!.Label)
            .ToListAsync(ct);

    public void AddTemplate(EquipmentFieldTemplate template) => _db.EquipmentFieldTemplates.Add(template);
    public void AddValue(EquipmentFieldValue value) => _db.EquipmentFieldValues.Add(value);
    public void RemoveTemplate(EquipmentFieldTemplate template) => _db.EquipmentFieldTemplates.Remove(template);
    public void RemoveDefinition(EquipmentFieldDefinition definition) => _db.EquipmentFieldDefinitions.Remove(definition);
    public void RemoveValue(EquipmentFieldValue value) => _db.EquipmentFieldValues.Remove(value);
    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
