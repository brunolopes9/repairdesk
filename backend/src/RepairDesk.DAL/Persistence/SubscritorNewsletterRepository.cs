using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class SubscritorNewsletterRepository : ISubscritorNewsletterRepository
{
    private readonly AppDbContext _db;
    public SubscritorNewsletterRepository(AppDbContext db) => _db = db;

    public Task<SubscritorNewsletter?> FindByEmailAsync(string email, CancellationToken ct = default)
        => _db.SubscritoresNewsletter.FirstOrDefaultAsync(s => s.Email == email, ct);

    public Task<SubscritorNewsletter?> FindByConfirmacaoHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.SubscritoresNewsletter.FirstOrDefaultAsync(s => s.TokenConfirmacaoHash == tokenHash, ct);

    public Task<SubscritorNewsletter?> FindByCancelamentoAsync(string token, CancellationToken ct = default)
        => _db.SubscritoresNewsletter.FirstOrDefaultAsync(s => s.TokenCancelamento == token, ct);

    public async Task<IReadOnlyList<SubscritorNewsletter>> ListAsync(CancellationToken ct = default)
        => await _db.SubscritoresNewsletter.AsNoTracking().OrderByDescending(s => s.CreatedAt).Take(5000).ToListAsync(ct);

    public async Task AddAsync(SubscritorNewsletter subscritor, CancellationToken ct = default)
        => await _db.SubscritoresNewsletter.AddAsync(subscritor, ct);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
