using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Documents;

/// <summary>
/// Sprint 491: label pt-PT da <see cref="DeviceCategory"/> (S475) para documentos PDF.
/// Centralizado para os recibos de entrada (S490) e entrega (S491) partilharem a mesma
/// tradução, evitando duplicação do switch.
/// </summary>
internal static class DeviceCategoryLabels
{
    public static string? PtLabel(DeviceCategory? c) => c switch
    {
        DeviceCategory.Smartphone => "Telemóvel",
        DeviceCategory.Tablet => "Tablet",
        DeviceCategory.Laptop => "Portátil",
        DeviceCategory.Desktop => "Desktop",
        DeviceCategory.Smartwatch => "Smartwatch",
        DeviceCategory.Consola => "Consola",
        DeviceCategory.Outro => "Outro",
        _ => null,
    };
}
