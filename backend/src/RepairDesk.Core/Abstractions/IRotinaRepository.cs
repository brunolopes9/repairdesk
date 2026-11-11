using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 615 (Doc 98): rotina pessoal. Os métodos «Todos…» correm no trabalho de fundo (sem empresa ativa).</summary>
public interface IRotinaRepository
{
    Task<IReadOnlyList<RotinaItem>> ItensDoUtilizadorAsync(Guid userId, CancellationToken ct = default);
    Task<RotinaItem?> FindItemAsync(Guid id, CancellationToken ct = default);
    Task AddItemAsync(RotinaItem item, CancellationToken ct = default);
    Task<IReadOnlyList<RotinaRegisto>> RegistosAsync(Guid userId, DateOnly de, DateOnly ate, CancellationToken ct = default);
    Task<RotinaRegisto?> FindRegistoAsync(Guid itemId, DateOnly dia, CancellationToken ct = default);
    Task AddRegistoAsync(RotinaRegisto registo, CancellationToken ct = default);
    Task<IReadOnlyList<RotinaRegisto>> ValoresAsync(Guid userId, Guid itemId, int max, CancellationToken ct = default);

    /// <summary>Todos os itens ativos de todas as empresas (para os avisos).</summary>
    Task<IReadOnlyList<RotinaItem>> TodosItensAtivosAsync(CancellationToken ct = default);
    /// <summary>Registos de um dia de todas as empresas, com tracking (os avisos atualizam-nos).</summary>
    Task<IReadOnlyList<RotinaRegisto>> TodosRegistosDoDiaAsync(DateOnly dia, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
