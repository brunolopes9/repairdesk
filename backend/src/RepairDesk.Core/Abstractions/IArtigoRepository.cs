using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface IArtigoRepository
{
    /// <summary>Artigos com os lotes (e documento/fornecedor, para o preço calculado). Pesquisa por palavras.</summary>
    Task<IReadOnlyList<Artigo>> ListAsync(string? q, bool incluirInativos, CancellationToken ct = default);
    Task<Artigo?> FindByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Pelo que a pistola lê: código de barras, do fabricante, SKU ou código alternativo (sem distinguir maiúsculas).</summary>
    Task<Artigo?> FindByCodigoAsync(string codigo, CancellationToken ct = default);
    /// <summary>Outro artigo (≠ <paramref name="exceptoId"/>) já usa este código como barras, fabricante ou SKU?</summary>
    Task<bool> CodigoEmUsoAsync(string codigo, Guid? exceptoId, CancellationToken ct = default);
    Task AddAsync(Artigo artigo, CancellationToken ct = default);
    void Remove(Artigo artigo);
    /// <summary>Sprint 581: o artigo que este fornecedor vende com este SKU (aprendido nas compras anteriores).</summary>
    Task<Guid?> ArtigoPorSkuFornecedorAsync(Guid fornecedorId, string sku, CancellationToken ct = default);
    /// <summary>Sprint 581: guarda "fornecedor + SKU → artigo" (se já apontava para outro artigo, passa a apontar para este).</summary>
    Task AprenderSkuFornecedorAsync(Guid artigoId, Guid fornecedorId, string sku, CancellationToken ct = default);
    Task AddCodigoAsync(ArtigoCodigo codigo, CancellationToken ct = default);
    /// <summary>Sprint 581: devolve true quando o SKU já apontava para OUTRO artigo (mudou — o chamador avisa).</summary>
    Task<bool> SkuApontaParaOutroAsync(Guid artigoId, Guid fornecedorId, string sku, CancellationToken ct = default);
    Task AddFusaoAsync(ArtigoFusao fusao, CancellationToken ct = default);
    Task<ArtigoFusao?> FindFusaoAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ArtigoFusao>> ListFusoesAsync(Guid destinoId, CancellationToken ct = default);
    /// <summary>Artigo apagado (soft delete) do tenant atual — para desfazer uma junção.</summary>
    Task<Artigo?> FindApagadoAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ArtigoCodigo>> FindCodigosAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    void RemoveCodigo(ArtigoCodigo codigo);
    /// <summary>Grava sem a lógica de numeração (passo intermédio de operações em dois tempos).</summary>
    Task SaveIntermedioAsync(CancellationToken ct = default);
    /// <summary>Sprint 570: categorias com preço por defeito.</summary>
    Task<IReadOnlyList<CategoriaArtigo>> ListCategoriasAsync(CancellationToken ct = default);
    Task<CategoriaArtigo?> FindCategoriaAsync(string nome, CancellationToken ct = default);
    Task AddCategoriaAsync(CategoriaArtigo categoria, CancellationToken ct = default);
    /// <summary>Grava atribuindo Numero/SKU/código interno aos artigos novos (com nova tentativa se colidir).</summary>
    Task SaveAsync(CancellationToken ct = default);
}
