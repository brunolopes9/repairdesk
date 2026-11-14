using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 659: contas de cliente da loja online.</summary>
public interface IClienteLojaRepository
{
    Task<ClienteLoja?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<ClienteLoja?> FindByResetHashAsync(string tokenHash, CancellationToken ct = default);
    /// <summary>A sessão válida (não expirada) com este hash, com o cliente.</summary>
    Task<ClienteLojaSessao?> FindSessaoAsync(string tokenHash, DateTime agora, CancellationToken ct = default);
    Task<IReadOnlyList<EncomendaLoja>> EncomendasAsync(Guid clienteId, CancellationToken ct = default);
    Task AddAsync(ClienteLoja cliente, CancellationToken ct = default);
    Task AddSessaoAsync(ClienteLojaSessao sessao, CancellationToken ct = default);
    void RemoveSessao(ClienteLojaSessao sessao);
    /// <summary>Termina todas as sessões do cliente (ex.: depois de mudar a palavra-passe).</summary>
    Task RemoverSessoesAsync(Guid clienteId, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
