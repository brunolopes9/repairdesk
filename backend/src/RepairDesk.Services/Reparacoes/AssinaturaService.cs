using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Reparacoes;

public interface IAssinaturaService
{
    /// <summary>Guarda (ou substitui) a assinatura de entrada/entrega de uma reparação.</summary>
    Task<AssinaturaDto> SaveAsync(Guid reparacaoId, string tipo, string dataUrl, CancellationToken ct = default);

    /// <summary>Estado das assinaturas da reparação (sem os bytes — só tipo + quando).</summary>
    Task<IReadOnlyList<AssinaturaDto>> ListAsync(Guid reparacaoId, CancellationToken ct = default);
}

public sealed record AssinaturaDto(string Tipo, DateTime AssinadaEm);

/// <summary>
/// Sprint 551 (Doc 80 pilar assinaturas / Doc 93 #5): assinatura manuscrita recolhida no
/// canvas do balcão (telemóvel/tablet virado para o cliente). Valida o data-URL (PNG por
/// magic bytes, tamanho limitado) e faz upsert — recapturar substitui. Os PDFs de
/// entrada/entrega estampam a imagem quando existe.
/// </summary>
public sealed class AssinaturaService : IAssinaturaService
{
    // Canvas de telemóvel hi-dpi gera PNGs de 10-60 KB; 300 KB é folga generosa sem
    // permitir abusar do endpoint como storage de imagens arbitrárias.
    private const int MaxPngBytes = 300_000;
    private const string PngDataUrlPrefix = "data:image/png;base64,";
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private readonly IReparacaoAssinaturaRepository _assinaturas;
    private readonly IReparacaoRepository _reparacoes;

    public AssinaturaService(IReparacaoAssinaturaRepository assinaturas, IReparacaoRepository reparacoes)
    {
        _assinaturas = assinaturas;
        _reparacoes = reparacoes;
    }

    public async Task<AssinaturaDto> SaveAsync(Guid reparacaoId, string tipo, string dataUrl, CancellationToken ct = default)
    {
        var rep = await _reparacoes.FindByIdAsync(reparacaoId, ct)
            ?? throw new NotFoundException("Reparacao", reparacaoId);

        var tipoEnum = ParseTipo(tipo);
        var png = DecodePng(dataUrl);

        var existente = await _assinaturas.FindAsync(rep.Id, tipoEnum, ct);
        if (existente is not null)
        {
            existente.PngBytes = png;
            existente.AssinadaEm = DateTime.UtcNow;
            await _assinaturas.SaveAsync(ct);
            return new AssinaturaDto(tipoEnum.ToString().ToLowerInvariant(), existente.AssinadaEm);
        }

        var nova = new ReparacaoAssinatura
        {
            TenantId = rep.TenantId,
            ReparacaoId = rep.Id,
            Tipo = tipoEnum,
            PngBytes = png,
            AssinadaEm = DateTime.UtcNow,
        };
        await _assinaturas.AddAsync(nova, ct);
        await _assinaturas.SaveAsync(ct);
        return new AssinaturaDto(tipoEnum.ToString().ToLowerInvariant(), nova.AssinadaEm);
    }

    public async Task<IReadOnlyList<AssinaturaDto>> ListAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        _ = await _reparacoes.FindByIdAsync(reparacaoId, ct)
            ?? throw new NotFoundException("Reparacao", reparacaoId);
        var rows = await _assinaturas.ListByReparacaoAsync(reparacaoId, ct);
        return rows.Select(a => new AssinaturaDto(a.Tipo.ToString().ToLowerInvariant(), a.AssinadaEm)).ToList();
    }

    private static AssinaturaTipo ParseTipo(string tipo) => tipo?.Trim().ToLowerInvariant() switch
    {
        "entrada" => AssinaturaTipo.Entrada,
        "entrega" => AssinaturaTipo.Entrega,
        _ => throw new ValidationException("invalid_tipo", "Tipo de assinatura deve ser 'entrada' ou 'entrega'."),
    };

    private static byte[] DecodePng(string dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith(PngDataUrlPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("invalid_signature", "Assinatura tem de ser um data-URL PNG (data:image/png;base64,...).");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(dataUrl[PngDataUrlPrefix.Length..]);
        }
        catch (FormatException)
        {
            throw new ValidationException("invalid_signature", "Base64 da assinatura inválido.");
        }

        if (bytes.Length is 0 or > MaxPngBytes)
            throw new ValidationException("invalid_signature", $"Assinatura tem de ter entre 1 byte e {MaxPngBytes / 1000} KB.");
        if (bytes.Length < PngMagic.Length || !bytes.AsSpan(0, PngMagic.Length).SequenceEqual(PngMagic))
            throw new ValidationException("invalid_signature", "Conteúdo não é um PNG válido.");

        return bytes;
    }
}
