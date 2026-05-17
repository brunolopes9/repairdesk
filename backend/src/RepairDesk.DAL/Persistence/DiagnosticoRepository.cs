using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class DiagnosticoRepository : IDiagnosticoRepository
{
    private readonly AppDbContext _db;
    public DiagnosticoRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DiagnosticoTemplate>> ListTemplatesAsync(CancellationToken ct = default)
        => await _db.DiagnosticoTemplates
            .Include(t => t.Items.OrderBy(i => i.Ordem))
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Categoria)
            .ThenBy(t => t.Nome)
            .ToListAsync(ct);

    public Task<DiagnosticoTemplate?> FindTemplateAsync(Guid id, CancellationToken ct = default)
        => _db.DiagnosticoTemplates
            .Include(t => t.Items.OrderBy(i => i.Ordem))
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<DiagnosticoTemplate?> FindDefaultTemplateAsync(DeviceCategory cat, CancellationToken ct = default)
        => _db.DiagnosticoTemplates
            .Include(t => t.Items.OrderBy(i => i.Ordem))
            .Where(t => t.Categoria == cat && t.Activo)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Nome)
            .FirstOrDefaultAsync(ct);

    public Task AddTemplateAsync(DiagnosticoTemplate template, CancellationToken ct = default)
        => _db.DiagnosticoTemplates.AddAsync(template, ct).AsTask();

    public void RemoveTemplate(DiagnosticoTemplate template) => _db.DiagnosticoTemplates.Remove(template);

    public Task<DiagnosticoExecucao?> FindExecucaoByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
        => _db.DiagnosticoExecucoes
            .Include(e => e.Items.OrderBy(i => i.Ordem))
            .FirstOrDefaultAsync(e => e.ReparacaoId == reparacaoId, ct);

    public Task AddExecucaoAsync(DiagnosticoExecucao execucao, CancellationToken ct = default)
        => _db.DiagnosticoExecucoes.AddAsync(execucao, ct).AsTask();

    public void RemoveExecucao(DiagnosticoExecucao execucao) => _db.DiagnosticoExecucoes.Remove(execucao);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
