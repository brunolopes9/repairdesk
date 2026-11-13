using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 613 (Doc 97): produto à venda na /loja do lopestech.pt. O Mender é a fonte única —
/// o site só gera páginas a partir do catálogo publicado. Custo e margem nunca saem do Mender.
/// </summary>
public class ProdutoLoja : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Nº visível por empresa (como nas outras tabelas).</summary>
    public int Numero { get; set; }

    /// <summary>Segmento do URL (/loja/{categoria}/{slug}) — único por empresa e estável depois de publicado.</summary>
    public required string Slug { get; set; }
    public required string Titulo { get; set; }
    public required string Marca { get; set; }
    public string? Modelo { get; set; }
    public LojaCategoria Categoria { get; set; }

    public LojaEstado Estado { get; set; }
    public LojaGrau? Grau { get; set; }
    /// <summary>Obrigatória e mostrada no selo. Novos e recondicionados: mínimo legal de 36 meses (DL 84/2021).</summary>
    public int GarantiaMeses { get; set; }
    public LojaRegimeIva RegimeIva { get; set; }

    public LojaOrigem Origem { get; set; }
    /// <summary>
    /// Sprint 655: artigo do stock que este produto vende. Quando está ligado, o preço, o custo e as unidades vêm SEMPRE
    /// do artigo (fonte única) — vender ao balcão tira da loja e mudar o preço do artigo muda o site.
    /// </summary>
    public Guid? ArtigoId { get; set; }
    public Artigo? Artigo { get; set; }
    public Guid? FornecedorId { get; set; }
    public LojaDisponibilidade Disponibilidade { get; set; }

    /// <summary>Preço final ao cliente (IVA incluído), em cêntimos.</summary>
    public long PrecoCents { get; set; }
    /// <summary>Custo (privado — nunca vai para o site).</summary>
    public long? CustoCents { get; set; }
    /// <summary>Sprint 654: preço riscado (promoção). Tem de ser o preço mais baixo dos últimos 30 dias (DL 109/2019, Omnibus).</summary>
    public long? PrecoAnteriorCents { get; set; }
    /// <summary>Sprint 654: unidades em stock (próprios). Mostra «últimas unidades» na loja quando ≤ 2. Null = não controla.</summary>
    public int? Unidades { get; set; }

    // Atributos filtráveis na loja.
    public string? Cor { get; set; }
    public int? RamGb { get; set; }
    public int? ArmazenamentoGb { get; set; }

    /// <summary>Descrição em Markdown simples.</summary>
    public string? Descricao { get; set; }
    /// <summary>Tabela de especificações: JSON [{"nome":"Processador","valor":"Ryzen 3 4300U"}].</summary>
    public string? SpecsJson { get; set; }

    public string? SeoTitulo { get; set; }
    public string? SeoDescricao { get; set; }

    public bool Publicado { get; set; }
    public DateTime? PublicadoEm { get; set; }

    public List<ProdutoLojaFoto> Fotos { get; set; } = new();
}

/// <summary>Foto do produto no storage (R2). A ordem é a da galeria; a 1.ª é a principal.</summary>
public class ProdutoLojaFoto : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ProdutoId { get; set; }
    public ProdutoLoja? Produto { get; set; }
    public int Ordem { get; set; }
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public long Bytes { get; set; }
    /// <summary>Texto alternativo (acessibilidade e SEO).</summary>
    public string? Alt { get; set; }
    /// <summary>False até o browser confirmar que o upload chegou ao storage.</summary>
    public bool Enviada { get; set; }
}
