namespace RepairDesk.API.Cash;

/// <summary>
/// Sprint 302 (Doc 80 Pillar A.1): geração do PDF Z-report ao fechar caixa.
/// On-demand a partir do snapshot do <see cref="DailyClosingDto"/> — depois de
/// Closed o registo é imutável, logo o PDF é determinístico.
/// </summary>
public interface IZReportPdfService
{
    Task<(byte[] Pdf, string Filename)> ForClosingAsync(Guid dailyClosingId, CancellationToken ct = default);
}
