using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Linha de um documento de compra = um lote de stock (SPEC compras §2.3). Guarda só o que foi
/// introduzido; custo sem IVA, preço final, IVA a pagar e lucro são calculados pelo motor de IVA.
/// Stock disponível = <see cref="Quantidade"/> − <see cref="QuantidadeVendida"/> − <see cref="QuantidadeAbatida"/>.
/// </summary>
public class CompraLinha : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    public Guid CompraDocumentoId { get; set; }
    public CompraDocumento? Documento { get; set; }

    /// <summary>Nº do lote, sequencial por tenant ("Lote 112") — como o nº de linha do Excel. Nunca reutilizado.</summary>
    public int Numero { get; set; }

    public required string Descricao { get; set; }

    public int Quantidade { get; set; }

    /// <summary>Preço unitário efetivamente pago (nacional: já com IVA; UE: sem IVA, veio a 0%).</summary>
    public decimal PrecoUnitarioPago { get; set; }

    /// <summary>Taxa de IVA incluída no preço pago (0,23 nacional; 0 UE). Vem do fornecedor, editável.</summary>
    public decimal TaxaIvaCompra { get; set; }

    /// <summary>Lucro desejado por unidade, sem IVA (ex.: 5 € peças, 1 € películas).</summary>
    public decimal LucroUnitario { get; set; }

    /// <summary>Unidades já vendidas (passa a ser gerido pelas Vendas).</summary>
    public int QuantidadeVendida { get; set; }

    /// <summary>Unidades retiradas sem venda (garantia, defeito, uso interno).</summary>
    public int QuantidadeAbatida { get; set; }

    /// <summary>Onde está guardado (ex.: "Gaveta A3").</summary>
    public string? Localizacao { get; set; }

    /// <summary>Sprint 566: artigo da loja a que este lote pertence (o stock do artigo é a soma dos lotes).</summary>
    public Guid? ArtigoId { get; set; }
    public Artigo? Artigo { get; set; }

    /// <summary>
    /// Sprint 581: código do produto no catálogo do fornecedor ("RIX03253", "20940"), tal como vem na fatura. Serve
    /// para reconhecer as compras seguintes do mesmo produto (ver <see cref="ArtigoCodigo"/>) — não é o código da loja.
    /// </summary>
    public string? SkuFornecedor { get; set; }

    /// <summary>
    /// Sprint 581: peça de reparação (ecrã, bateria…) — não se vende ao balcão nem leva etiqueta, por isso não fica
    /// à espera de artigo. Usa-se nas reparações a partir do stock.
    /// </summary>
    public bool PecaReparacao { get; set; }

    public int QuantidadeEmStock => Quantidade - QuantidadeVendida - QuantidadeAbatida;
}
