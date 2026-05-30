using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 461 (Doc 90 Tier 2 #6 — Asset registry): equipamento persistente do cliente.
///
/// Diferenciação vs ClienteEquipamentoDto (que é DERIVED de reparações+vendas):
///   - Device é uma ENTITY persistente, vive entre reparações
///   - Pode existir SEM reparação (cliente regista o seu telemóvel mesmo sem avaria)
///   - Permite guardar campos próprios: Apelido (ex: "iPhone do João"), DataAquisicao,
///     GarantiaUntil (do fabricante), Notas internas
///   - Quando se cria uma reparação com IMEI conhecido, faz auto-link ao Device existente
///   - Histórico completo: todas as reparações deste Device numa só vista
///
/// Nota: por agora coexiste com ClienteEquipamentoDto (derived). O migration path
/// futuro será unificar — qualquer reparação com IMEI cria/liga ao Device.
/// </summary>
public class Device : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    /// <summary>Tipo livre: "Telemóvel", "Tablet", "Portátil", "Smartwatch", etc.</summary>
    public required string Tipo { get; set; }
    /// <summary>Marca: Apple, Samsung, Xiaomi, etc.</summary>
    public string? Marca { get; set; }
    /// <summary>Modelo: iPhone 13, Galaxy S22, etc.</summary>
    public string? Modelo { get; set; }
    /// <summary>Alcunha opcional dada pelo cliente (ex: "iPhone do João", "tablet escola").</summary>
    public string? Apelido { get; set; }

    /// <summary>IMEI principal (telemóveis). Único por tenant quando preenchido.</summary>
    public string? Imei { get; set; }
    /// <summary>Serial number (portáteis, tablets, smartwatches).</summary>
    public string? Serial { get; set; }
    /// <summary>Cor (descrição livre).</summary>
    public string? Cor { get; set; }

    /// <summary>Data de aquisição declarada pelo cliente (não necessariamente nesta loja).</summary>
    public DateOnly? DataAquisicao { get; set; }
    /// <summary>Fim da garantia do fabricante (separado da garantia da loja, S127).</summary>
    public DateOnly? GarantiaFabricanteUntil { get; set; }
    /// <summary>Notas internas do staff (não visíveis ao cliente).</summary>
    public string? Notas { get; set; }

    /// <summary>Soft-archive — Device antigo que cliente já não tem. Não apaga histórico.</summary>
    public bool Arquivado { get; set; }
}
