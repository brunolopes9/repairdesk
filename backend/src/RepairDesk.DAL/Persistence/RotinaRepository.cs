using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class RotinaRepository : IRotinaRepository
{
    private readonly AppDbContext _db;
    public RotinaRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<RotinaItem>> ItensDoUtilizadorAsync(Guid userId, CancellationToken ct = default)
        => await _db.RotinaItens.Where(i => i.UserId == userId).OrderBy(i => i.Hora).ThenBy(i => i.Titulo).ToListAsync(ct);

    public Task<RotinaItem?> FindItemAsync(Guid id, CancellationToken ct = default)
        => _db.RotinaItens.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task AddItemAsync(RotinaItem item, CancellationToken ct = default) => await _db.RotinaItens.AddAsync(item, ct);

    public async Task<IReadOnlyList<RotinaRegisto>> RegistosAsync(Guid userId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => await _db.RotinaRegistos.Where(r => r.UserId == userId && r.Dia >= de && r.Dia <= ate).ToListAsync(ct);

    public Task<RotinaRegisto?> FindRegistoAsync(Guid itemId, DateOnly dia, CancellationToken ct = default)
        => _db.RotinaRegistos.FirstOrDefaultAsync(r => r.RotinaItemId == itemId && r.Dia == dia, ct);

    public async Task AddRegistoAsync(RotinaRegisto registo, CancellationToken ct = default) => await _db.RotinaRegistos.AddAsync(registo, ct);

    public async Task<IReadOnlyList<RotinaRegisto>> ValoresAsync(Guid userId, Guid itemId, int max, CancellationToken ct = default)
        => await _db.RotinaRegistos.AsNoTracking()
            .Where(r => r.UserId == userId && r.RotinaItemId == itemId && r.Valor != null)
            .OrderByDescending(r => r.Dia).Take(max).ToListAsync(ct);

    public async Task<IReadOnlyList<RotinaItem>> TodosItensAtivosAsync(CancellationToken ct = default)
        => await _db.RotinaItens.IgnoreQueryFilters().AsNoTracking().Where(i => !i.IsDeleted && i.Ativo).ToListAsync(ct);

    public async Task<IReadOnlyList<RotinaRegisto>> TodosRegistosDoDiaAsync(DateOnly dia, CancellationToken ct = default)
        => await _db.RotinaRegistos.IgnoreQueryFilters().Where(r => !r.IsDeleted && r.Dia == dia).ToListAsync(ct);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
