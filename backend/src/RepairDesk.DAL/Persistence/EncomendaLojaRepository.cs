using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class EncomendaLojaRepository : IEncomendaLojaRepository
{
    private readonly AppDbContext _db;
    public EncomendaLojaRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<EncomendaLoja>> ListAsync(CancellationToken ct = default)
        => await _db.EncomendasLoja.Include(e => e.Linhas).OrderByDescending(e => e.Numero).Take(500).ToListAsync(ct);

    public Task<EncomendaLoja?> FindAsync(Guid id, CancellationToken ct = default)
        => _db.EncomendasLoja.Include(e => e.Linhas).FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<EncomendaLoja?> FindByReferenciaAsync(string referencia, CancellationToken ct = default)
        => _db.EncomendasLoja.Include(e => e.Linhas).FirstOrDefaultAsync(e => e.Referencia == referencia, ct);

    public Task<EncomendaLoja?> FindByStripeSessionGlobalAsync(string sessionId, CancellationToken ct = default)
        => _db.EncomendasLoja.IgnoreQueryFilters().Include(e => e.Linhas)
            .FirstOrDefaultAsync(e => e.StripeSessionId == sessionId && !e.IsDeleted, ct);

    public Task<bool> ReferenciaExisteAsync(string referencia, CancellationToken ct = default)
        => _db.EncomendasLoja.IgnoreQueryFilters().AnyAsync(e => e.Referencia == referencia, ct);

    public async Task AddAsync(EncomendaLoja encomenda, CancellationToken ct = default) => await _db.EncomendasLoja.AddAsync(encomenda, ct);

    public async Task SaveAsync(CancellationToken ct = default)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            var novas = _db.ChangeTracker.Entries<EncomendaLoja>()
                .Where(e => e.State == EntityState.Added && e.Entity.Numero == 0).Select(e => e.Entity).ToList();
            foreach (var grupo in novas.GroupBy(e => e.TenantId))
            {
                var max = await _db.EncomendasLoja.IgnoreQueryFilters().Where(e => e.TenantId == grupo.Key)
                    .Select(e => (int?)e.Numero).MaxAsync(ct) ?? 0;
                foreach (var e in grupo) e.Numero = ++max;
            }
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < maxAttempts && novas.Count > 0)
            {
                foreach (var e in novas) e.Numero = 0;
            }
        }
    }
}
