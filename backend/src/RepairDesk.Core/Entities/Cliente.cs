using System.ComponentModel.DataAnnotations;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

public class Cliente : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Nome { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Nif { get; set; }
    public string? Notas { get; set; }

    /// <summary>Sprint 510: morada fiscal do cliente — usada na fatura Moloni (com NIF).</summary>
    [MaxLength(200)]
    public string? Morada { get; set; }

    /// <summary>Código postal PT (formato 0000-000). Validado antes de enviar ao Moloni.</summary>
    [MaxLength(20)]
    public string? CodigoPostal { get; set; }

    /// <summary>Localidade/cidade do cliente.</summary>
    [MaxLength(100)]
    public string? Localidade { get; set; }

    /// <summary>
    /// Sprint 355 (Doc 83 Pillar 10): alerta curto destacado em todo o lado onde o
    /// cliente aparece (ex: "Paga sempre em dinheiro", "Junta — fatura com NIF",
    /// "Cliente difícil"). Diferente de Notas (texto longo de contexto).
    /// </summary>
    public string? NotaImportante { get; set; }

    /// <summary>
    /// Canal que o cliente prefere para contactos operacionais.
    /// Valores aceites pela API: Telefone, WhatsApp, Email, Sms.
    /// </summary>
    [MaxLength(20)]
    public string? ContactoPreferido { get; set; }

    /// <summary>Consentimento/opt-in para campanhas e comunicacoes comerciais.</summary>
    public bool AceitaMarketing { get; set; }

    /// <summary>Bloqueia contactos não essenciais quando o cliente pede para não ser contactado.</summary>
    public bool NaoContactar { get; set; }

    public List<ClienteTagAssignment> TagAssignments { get; set; } = new();
}
