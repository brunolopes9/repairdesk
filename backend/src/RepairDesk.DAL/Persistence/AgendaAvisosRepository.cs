using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

/// <summary>Sprint 611: corre fora de qualquer pedido (sem empresa ativa) — por isso ignora os filtros de tenant e filtra à mão.</summary>
public class AgendaAvisosRepository : IAgendaAvisosRepository
{
    private readonly AppDbContext _db;
    public AgendaAvisosRepository(AppDbContext db) => _db = db;

    private IQueryable<Appointment> Ativos() => _db.Appointments.IgnoreQueryFilters()
        .Where(a => !a.IsDeleted && (a.Status == AppointmentStatus.Agendado || a.Status == AppointmentStatus.Confirmado));

    public async Task<IReadOnlyList<Appointment>> SemLembreteEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken ct = default)
        => await Ativos().Where(a => a.LembreteEnviadoEm == null && a.ScheduledAt >= deUtc && a.ScheduledAt < ateUtc)
            .OrderBy(a => a.ScheduledAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> AtivosEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken ct = default)
        => await Ativos().AsNoTracking().Where(a => a.ScheduledAt >= deUtc && a.ScheduledAt < ateUtc)
            .OrderBy(a => a.ScheduledAt).ToListAsync(ct);

    public async Task<IReadOnlyList<PecaPorEncomendar>> PecasPorEncomendarAsync(CancellationToken ct = default)
        => await _db.Vendas.IgnoreQueryFilters().AsNoTracking()
            .Where(v => !v.IsDeleted && v.Estado != VendaEstado.Entregue && v.Estado != VendaEstado.Cancelada)
            .SelectMany(v => v.Items.Where(i => i.Encomenda == PecaEncomendaEstado.PorEncomendar),
                (v, i) => new PecaPorEncomendar(v.TenantId, v.Numero, v.Equipamento, i.Descricao, i.Quantidade))
            .ToListAsync(ct);

    public Task<bool> AvisoJaEnviadoAsync(Guid tenantId, string chave, CancellationToken ct = default)
        => _db.AgendaAvisos.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.Chave == chave, ct);

    public async Task AddAvisoAsync(AgendaAviso aviso, CancellationToken ct = default) => await _db.AgendaAvisos.AddAsync(aviso, ct);

    public Task SaveAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
