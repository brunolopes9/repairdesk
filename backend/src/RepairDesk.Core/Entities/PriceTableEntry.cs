using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Entrada da tabela de preços por tenant.
/// Combinação (Categoria + Marca + Modelo + Servico) deve ser única por tenant.
/// </summary>
public class PriceTableEntry : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public DeviceCategory Categoria { get; set; } = DeviceCategory.Smartphone;

    public required string Marca { get; set; }       // "Apple", "Samsung", "Xiaomi"
    public required string Modelo { get; set; }      // "iPhone 13", "Galaxy A50"
    public required string Servico { get; set; }     // "Substituição de ecrã", "Bateria"

    public int? CustoPecaCents { get; set; }         // Custo aproximado da peça (interno)
    public int PvpCents { get; set; }                // Preço para o cliente
    public int? TempoEstimadoMin { get; set; }       // Tempo médio de execução
    public string? Notas { get; set; }               // "Original Apple, garantia 1 ano"
    public bool Activo { get; set; } = true;
}
