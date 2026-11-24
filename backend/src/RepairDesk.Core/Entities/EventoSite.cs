using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// 23/11 (Bruno: «quero saber tudo o que as pessoas fazem na loja»): um evento do site lopestech.pt — página vista,
/// pesquisa, produto visto, carrinho, checkout, pagamento iniciado, compra, clique no WhatsApp/telefone ou saída
/// (tempo e scroll). Medição própria (1.ª parte): sem IP guardado; o visitante só é reconhecido entre visitas se
/// aceitou as métricas (lt_cookies=sim) — sem isso cada visita tem um id só dessa visita.
/// </summary>
public class EventoSite : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Id aleatório do navegador (persistente só com consentimento).</summary>
    public required string Visitante { get; set; }
    /// <summary>Id da visita (30 min sem atividade = nova visita).</summary>
    public required string Sessao { get; set; }
    /// <summary>pagina · saida · pesquisa · produto · carrinho · remover · checkout · pagamento · compra · contacto.</summary>
    public required string Tipo { get; set; }
    public string? Caminho { get; set; }
    public string? Titulo { get; set; }
    public string? Referrer { get; set; }
    /// <summary>google, facebook, instagram, whatsapp, direto, outro site… (lida do referrer e dos utm/fbclid/gclid).</summary>
    public string? Origem { get; set; }
    public string? Campanha { get; set; }
    /// <summary>Termo pesquisado (pesquisa) ou canal do contacto (whatsapp/telefone/email).</summary>
    public string? Termo { get; set; }
    /// <summary>Nº de resultados da pesquisa (0 = procurou e não encontrou — oportunidade).</summary>
    public int? Resultados { get; set; }
    public string? Produto { get; set; }
    public string? ProdutoSlug { get; set; }
    public decimal? Valor { get; set; }
    /// <summary>Segundos na página (evento saida).</summary>
    public int? Duracao { get; set; }
    /// <summary>% da página que viu (evento saida).</summary>
    public int? Scroll { get; set; }
    /// <summary>Referência da encomenda (checkout/pagamento/compra).</summary>
    public string? Encomenda { get; set; }
    /// <summary>Nome do cliente da loja com sessão iniciada (ou da encomenda).</summary>
    public string? Cliente { get; set; }
    public string? Dispositivo { get; set; }
    public string? Sistema { get; set; }
    public string? Navegador { get; set; }
    public string? Ecra { get; set; }
    public string? Idioma { get; set; }
    public bool Consentimento { get; set; }
}
