using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IApuramentoFechoRepository
{
    Task<ApuramentoFecho?> FindAsync(int ano, int trimestre, CancellationToken ct = default);
    Task AddAsync(ApuramentoFecho fecho, CancellationToken ct = default);
    void Remove(ApuramentoFecho fecho);
    Task SaveAsync(CancellationToken ct = default);
}
