using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public sealed record ClienteRgpdData(
    Cliente Cliente,
    IReadOnlyList<Reparacao> Reparacoes,
    IReadOnlyList<ReparacaoEstadoLog> Timeline,
    IReadOnlyList<Trabalho> Trabalhos,
    IReadOnlyList<Despesa> Despesas,
    IReadOnlyList<ReparacaoFoto> Fotos,
    IReadOnlyList<Garantia> Garantias,
    IReadOnlyList<Avaliacao> Avaliacoes,
    IReadOnlyList<PartMovimento> PartMovimentos,
    IReadOnlyList<AuditEntry> AuditEntries);

public interface IClienteRgpdRepository
{
    Task<ClienteRgpdData?> LoadClienteDataAsync(Guid clienteId, CancellationToken ct = default);
    Task HardDeleteAsync(ClienteRgpdData data, CancellationToken ct = default);
}
