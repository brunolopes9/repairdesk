using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _db;

    public PaymentRepository(AppDbContext db) => _db = db;

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Payment?> GetByProviderRefAsync(string providerRef, CancellationToken ct = default) =>
        _db.Payments.FirstOrDefaultAsync(p => p.ProviderRef == providerRef, ct);

    public async Task<IReadOnlyList<Payment>> GetByVendaAsync(Guid vendaId, CancellationToken ct = default) =>
        await _db.Payments
            .Where(p => p.VendaId == vendaId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        await _db.Payments.AddAsync(payment, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Payment payment, CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);
}
