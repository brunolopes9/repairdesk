namespace RepairDesk.Core.Enums;

/// <summary>
/// Sprint 622 (Doc 98 F6, §4.1): a que atividade serve um gasto — define quanto IVA se deduz num sujeito passivo misto.
/// LopesTech = atividade tributada (deduz a % da despesa); Trading = atividade isenta sem dedução (0%);
/// Mista = as duas (deduz × pro rata, a partir de <c>Tenant.ProRataInicio</c>).
/// </summary>
public enum AfetacaoIva
{
    LopesTech = 0,
    Trading = 1,
    Mista = 2,
}
