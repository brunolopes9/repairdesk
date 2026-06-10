using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 551: persistência das assinaturas de entrada/entrega por reparação.</summary>
public interface IReparacaoAssinaturaRepository
{
    Task<ReparacaoAssinatura?> FindAsync(Guid reparacaoId, AssinaturaTipo tipo, CancellationToken ct = default);
    Task<IReadOnlyList<ReparacaoAssinatura>> ListByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task AddAsync(ReparacaoAssinatura assinatura, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
