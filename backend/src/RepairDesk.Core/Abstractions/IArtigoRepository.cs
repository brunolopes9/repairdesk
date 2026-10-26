using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IArtigoRepository
{
    /// <summary>Artigos com os lotes (e documento/fornecedor, para o preço calculado). Pesquisa por palavras.</summary>
    Task<IReadOnlyList<Artigo>> ListAsync(string? q, bool incluirInativos, CancellationToken ct = default);
    Task<Artigo?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Pelo que a pistola lê: código de barras, código do fabricante ou SKU (sem distinguir maiúsculas).</summary>
    Task<Artigo?> FindByCodigoAsync(string codigo, CancellationToken ct = default);
    /// <summary>Outro artigo (≠ <paramref name="exceptoId"/>) já usa este código como barras, fabricante ou SKU?</summary>
    Task<bool> CodigoEmUsoAsync(string codigo, Guid? exceptoId, CancellationToken ct = default);
    Task AddAsync(Artigo artigo, CancellationToken ct = default);
    /// <summary>Grava atribuindo Numero/SKU/código interno aos artigos novos (com nova tentativa se colidir).</summary>
    Task SaveAsync(CancellationToken ct = default);
}
