using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

public interface ICompraRepository
{
    Task<(IReadOnlyList<CompraDocumento> Items, int Total)> SearchAsync(CompraFiltro filtro, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Documento com fornecedor e linhas (tracked, para edição).</summary>
    Task<CompraDocumento?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Documentos do fornecedor com o mesmo nº de fatura ou alguma encomenda em comum (anti-duplicados).</summary>
    Task<IReadOnlyList<CompraDocumento>> FindPossiveisDuplicadosAsync(Guid fornecedorId, string? numeroFatura, IReadOnlyCollection<string> encomendas, Guid? excluirId, CancellationToken ct = default);

    /// <summary>Todas as linhas do tenant com documento e fornecedor (para inventário e resumo).</summary>
    Task<IReadOnlyList<CompraLinha>> ListLinhasAsync(bool soComStock, CancellationToken ct = default);

    /// <summary>Todos os documentos do tenant (sem linhas) — portes para a posição de IVA.</summary>
    Task<IReadOnlyList<CompraDocumento>> ListDocumentosAsync(CancellationToken ct = default);

    Task AddAsync(CompraDocumento doc, CancellationToken ct = default);
    void Remove(CompraDocumento doc);
    void AddLinha(CompraLinha linha);
    void RemoveLinha(CompraLinha linha);
    Task SaveAsync(CancellationToken ct = default);
}

public sealed record CompraFiltro(
    string? Query = null,
    Guid? FornecedorId = null,
    bool SoFaturaEmFalta = false,
    DateTime? DeUtc = null,
    DateTime? AteUtc = null);
