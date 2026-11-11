using RepairDesk.Core.Entities;

namespace RepairDesk.Core.Abstractions;

/// <summary>Sprint 613 (Doc 97): produtos da loja (com fotos).</summary>
public interface IProdutoLojaRepository
{
    Task<IReadOnlyList<ProdutoLoja>> ListAsync(bool soPublicados, CancellationToken ct = default);
    Task<ProdutoLoja?> FindAsync(Guid id, CancellationToken ct = default);
    Task<bool> SlugExisteAsync(string slug, Guid? exceptoId, CancellationToken ct = default);
    Task AddAsync(ProdutoLoja produto, CancellationToken ct = default);
    Task AddFotoAsync(ProdutoLojaFoto foto, CancellationToken ct = default);
    void Remove(ProdutoLojaFoto foto);
    void Remove(ProdutoLoja produto);
    /// <summary>Grava e atribui o nº visível aos produtos novos (com nova tentativa se houver colisão).</summary>
    Task SaveAsync(CancellationToken ct = default);
}
