namespace RepairDesk.Core.Enums;

/// <summary>
/// Distingue de onde a venda foi originada — útil para relatórios e análise de canal.
/// </summary>
public enum VendaOrigem
{
    /// <summary>POS no balcão (canal default — todas as vendas até Sprint 70).</summary>
    Balcao = 0,
    /// <summary>Loja online (criada via API de integração, ex: shop.lopestech.pt).</summary>
    Online = 1,
    /// <summary>Importada de CSV ou migração de outro sistema.</summary>
    Importacao = 2,
}
