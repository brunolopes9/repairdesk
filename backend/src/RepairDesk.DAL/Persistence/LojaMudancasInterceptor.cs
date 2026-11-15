using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

/// <summary>
/// Sprint 674: depois de gravar, se mudou alguma coisa que a loja do site mostra (produto, foto, artigo, lote de
/// stock — inclui unidades vendidas —, categoria com preço), avisa o <see cref="ILojaMudancas"/>.
/// </summary>
public sealed class LojaMudancasInterceptor : SaveChangesInterceptor
{
    private readonly ILojaMudancas _mudancas;
    private bool _relevante;

    public LojaMudancasInterceptor(ILojaMudancas mudancas) => _mudancas = mudancas;

    private static bool Interessa(object e) => e is ProdutoLoja or ProdutoLojaFoto or Artigo or CompraLinha or CategoriaArtigo;

    private void Ver(DbContext? db)
    {
        _relevante = db is not null && db.ChangeTracker.Entries()
            .Any(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && Interessa(x.Entity));
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Ver(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Ver(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (_relevante) { _relevante = false; _mudancas.Sinalizar(); }
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (_relevante) { _relevante = false; _mudancas.Sinalizar(); }
        return ValueTask.FromResult(result);
    }
}
