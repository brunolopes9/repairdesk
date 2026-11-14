using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 656: encomenda feita na loja do lopestech.pt. O preço de cada linha é o do servidor no momento do checkout
/// (nunca o que o browser mandou). Paga → cria a venda no Mender (o stock sai) e fica por faturar para o dono.
/// </summary>
public class EncomendaLoja : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Nº visível por empresa.</summary>
    public int Numero { get; set; }
    /// <summary>Código público mostrado ao cliente (ex.: «LT-7K3QX9»).</summary>
    public required string Referencia { get; set; }
    /// <summary>SHA-256 do token secreto do link de seguimento (o token em claro só existe no link do cliente).</summary>
    public required string TokenHash { get; set; }

    public EncomendaLojaEstado Estado { get; set; }

    public required string Nome { get; set; }
    public required string Email { get; set; }
    public required string Telefone { get; set; }
    public string? Nif { get; set; }
    public required string Morada { get; set; }
    public required string CodigoPostal { get; set; }
    public required string Localidade { get; set; }
    public string? Notas { get; set; }

    public required string Transportadora { get; set; }
    public long PortesCents { get; set; }
    public long SubtotalCents { get; set; }
    public long TotalCents { get; set; }

    public string? StripeSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public DateTime? PagaEm { get; set; }
    public DateTime? EnviadaEm { get; set; }
    public DateTime? EntregueEm { get; set; }
    public DateTime? CanceladaEm { get; set; }
    public string? NumeroSeguimento { get; set; }

    /// <summary>Sprint 659: conta de cliente que fez a encomenda (null = compra sem conta).</summary>
    public Guid? ClienteLojaId { get; set; }

    /// <summary>Venda criada quando ficou paga (stock e faturação seguem por aí).</summary>
    public Guid? VendaId { get; set; }
    /// <summary>Pago mas a venda não foi criada (ex.: vendido ao balcão entretanto) — o dono tem de resolver/reembolsar.</summary>
    public string? Problema { get; set; }

    public List<EncomendaLojaLinha> Linhas { get; set; } = new();
}

public class EncomendaLojaLinha : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid EncomendaId { get; set; }
    public EncomendaLoja? Encomenda { get; set; }
    public Guid ProdutoLojaId { get; set; }
    public Guid? ArtigoId { get; set; }
    public required string Slug { get; set; }
    public required string Titulo { get; set; }
    public int Quantidade { get; set; }
    public long PrecoUnitarioCents { get; set; }
}
