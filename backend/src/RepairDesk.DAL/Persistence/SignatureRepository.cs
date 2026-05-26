using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class SignatureRepository : ISignatureRepository
{
    private readonly AppDbContext _db;

    public SignatureRepository(AppDbContext db) => _db = db;

    public Task<SignatureCapture?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.SignatureCaptures.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SignatureCapture>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default) =>
        await _db.SignatureCaptures
            .Where(s => s.ReparacaoId == reparacaoId)
            .OrderByDescending(s => s.SignedAt)
            .ToListAsync(ct);

    public async Task AddAsync(SignatureCapture signature, CancellationToken ct = default)
    {
        await _db.SignatureCaptures.AddAsync(signature, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(SignatureCapture signature, CancellationToken ct = default)
    {
        _db.SignatureCaptures.Remove(signature);
        await _db.SaveChangesAsync(ct);
    }
}
