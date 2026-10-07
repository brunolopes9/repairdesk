using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Tudo o que existe sobre um cliente — exportação RGPD (portabilidade) e apagamento.</summary>
public sealed record ClienteRgpdData(
    Cliente Cliente,
    IReadOnlyList<Venda> Vendas,
    IReadOnlyList<VendaEstadoLog> Timeline,
    IReadOnlyList<VendaFoto> Fotos,
    IReadOnlyList<VendaComunicacao> Comunicacoes,
    IReadOnlyList<Garantia> Garantias,
    IReadOnlyList<Avaliacao> Avaliacoes,
    IReadOnlyList<AuditEntry> AuditEntries);

public interface IClienteRgpdRepository
{
    Task<ClienteRgpdData?> LoadClienteDataAsync(Guid clienteId, CancellationToken ct = default);
    Task HardDeleteAsync(ClienteRgpdData data, CancellationToken ct = default);
}
