using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

/// <summary>Sprint 422: implementação EF do <see cref="IInternalTaskRepository"/>.</summary>
public class InternalTaskRepository : IInternalTaskRepository
{
    private readonly AppDbContext _db;

    public InternalTaskRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<InternalTask>> ListAsync(
        InternalTaskStatus? status,
        Guid? assignedToUserId,
        Guid? reparacaoId,
        CancellationToken ct = default)
    {
        var q = _db.InternalTasks
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .Include(t => t.Reparacao)
            .AsQueryable();
        if (status is { } s) q = q.Where(t => t.Status == s);
        if (assignedToUserId is { } u) q = q.Where(t => t.AssignedToUserId == u);
        if (reparacaoId is { } r) q = q.Where(t => t.ReparacaoId == r);
        // Pendentes primeiro (por DueAt asc; sem DueAt no fim), depois resto por CreatedAt desc.
        return await q
            .OrderBy(t => t.Status == InternalTaskStatus.Pendente ? 0 : 1)
            .ThenBy(t => t.DueAt ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<InternalTask?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => _db.InternalTasks
            .Include(t => t.AssignedToUser)
            .Include(t => t.Reparacao)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(InternalTask task, CancellationToken ct = default)
        => await _db.InternalTasks.AddAsync(task, ct);

    public void Remove(InternalTask task) => _db.InternalTasks.Remove(task);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
