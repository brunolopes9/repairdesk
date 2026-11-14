using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class ClienteLojaRepository : IClienteLojaRepository
{
    private readonly AppDbContext _db;
    public ClienteLojaRepository(AppDbContext db) => _db = db;

    public Task<ClienteLoja?> FindByEmailAsync(string email, CancellationToken ct = default)
        => _db.ClientesLoja.FirstOrDefaultAsync(c => c.Email == email, ct);

    public Task<ClienteLoja?> FindByResetHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.ClientesLoja.FirstOrDefaultAsync(c => c.ResetTokenHash == tokenHash, ct);

    public Task<ClienteLojaSessao?> FindSessaoAsync(string tokenHash, DateTime agora, CancellationToken ct = default)
        => _db.ClientesLojaSessoes.Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash && s.ExpiraEm > agora, ct);

    public async Task<IReadOnlyList<EncomendaLoja>> EncomendasAsync(Guid clienteId, CancellationToken ct = default)
        => await _db.EncomendasLoja.Include(e => e.Linhas).Where(e => e.ClienteLojaId == clienteId)
            .OrderByDescending(e => e.CreatedAt).Take(100).ToListAsync(ct);

    public async Task AddAsync(ClienteLoja cliente, CancellationToken ct = default) => await _db.ClientesLoja.AddAsync(cliente, ct);
    public async Task AddSessaoAsync(ClienteLojaSessao sessao, CancellationToken ct = default) => await _db.ClientesLojaSessoes.AddAsync(sessao, ct);
    public void RemoveSessao(ClienteLojaSessao sessao) => _db.ClientesLojaSessoes.Remove(sessao);

    public async Task RemoverSessoesAsync(Guid clienteId, CancellationToken ct = default)
    {
        var sessoes = await _db.ClientesLojaSessoes.Where(s => s.ClienteId == clienteId).ToListAsync(ct);
        _db.ClientesLojaSessoes.RemoveRange(sessoes);
    }

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
