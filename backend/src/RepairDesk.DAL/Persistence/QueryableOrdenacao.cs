using System.Linq.Expressions;

namespace RepairDesk.DAL.Persistence;

/// <summary>Sprint 561: OrderBy / OrderByDescending numa só chamada, para as listas ordenáveis.</summary>
internal static class QueryableOrdenacao
{
    public static IOrderedQueryable<T> OrdenarPor<T, TKey>(this IQueryable<T> q, Expression<Func<T, TKey>> chave, bool desc)
        => desc ? q.OrderByDescending(chave) : q.OrderBy(chave);

    public static IOrderedQueryable<T> DepoisPor<T, TKey>(this IOrderedQueryable<T> q, Expression<Func<T, TKey>> chave, bool desc)
        => desc ? q.ThenByDescending(chave) : q.ThenBy(chave);
}
