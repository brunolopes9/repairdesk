using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Audit;
using RepairDesk.Services.Fotos;

namespace RepairDesk.Services.Clientes;

public interface IClienteRgpdService
{
    Task<ClientePortableExportDto> ExportAsync(Guid clienteId, string baseUrl, CancellationToken ct = default);
    Task<HardDeleteClienteResponse> HardDeleteAsync(Guid clienteId, HardDeleteClienteRequest req, CancellationToken ct = default);
}

public class ClienteRgpdService : IClienteRgpdService
{
    private readonly IClienteRgpdRepository _repo;
    private readonly IPhotoStorage _storage;
    private readonly IPhotoExportLinkService _photoLinks;
    private readonly IAuditLogger _audit;

    public ClienteRgpdService(
        IClienteRgpdRepository repo,
        IPhotoStorage storage,
        IPhotoExportLinkService photoLinks,
        IAuditLogger audit)
    {
        _repo = repo;
        _storage = storage;
        _photoLinks = photoLinks;
        _audit = audit;
    }

    public async Task<ClientePortableExportDto> ExportAsync(Guid clienteId, string baseUrl, CancellationToken ct = default)
    {
        var data = await _repo.LoadClienteDataAsync(clienteId, ct) ?? throw new NotFoundException("Cliente", clienteId);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        await _audit.LogAsync(AuditAction.Export, "Cliente", clienteId, new { tipo = "rgpd_portabilidade" }, data.Cliente.TenantId, ct: ct);

        return new ClientePortableExportDto(
            DateTime.UtcNow,
            "mender-client-export-v3",
            ToCliente(data.Cliente),
            data.Vendas.Select(v => ToVenda(v, data.Timeline.Where(t => t.VendaId == v.Id))).ToList(),
            data.Fotos.Select(f => ToFoto(f, baseUrl, expiresAt)).ToList(),
            data.Comunicacoes.Select(c => new ComunicacaoExportDto(c.Id, c.VendaId, c.Tipo, c.Direcao, c.Texto, c.CreatedAt)).ToList(),
            data.Garantias.Select(ToGarantia).ToList(),
            data.Avaliacoes.Select(ToAvaliacao).ToList(),
            data.AuditEntries.Select(ToAudit).ToList());
    }

    public async Task<HardDeleteClienteResponse> HardDeleteAsync(Guid clienteId, HardDeleteClienteRequest req, CancellationToken ct = default)
    {
        var data = await _repo.LoadClienteDataAsync(clienteId, ct) ?? throw new NotFoundException("Cliente", clienteId);
        var expected = $"APAGAR {data.Cliente.Nome}";
        if (!string.Equals(req.Confirm, expected, StringComparison.Ordinal))
            throw new ValidationException("confirmacao_invalida", $"Confirmação inválida. Escreve exactamente: {expected}");

        foreach (var foto in data.Fotos)
        {
            await _storage.DeleteAsync(foto.StorageKey, ct);
        }

        await _repo.HardDeleteAsync(data, ct);

        var response = new HardDeleteClienteResponse(
            data.Cliente.Id,
            data.Cliente.Nome,
            DateTime.UtcNow,
            data.Vendas.Count(v => v.Tipo == VendaTipo.Reparacao),
            data.Fotos.Count,
            data.Vendas.Count);

        await _audit.LogAsync(AuditAction.HardDelete, "Cliente", clienteId, new
        {
            cliente = data.Cliente.Nome,
            motivo = req.Motivo,
            response.Reparacoes,
            response.Fotos,
            response.Vendas,
        }, data.Cliente.TenantId, ct: ct);

        return response;
    }

    private static ClienteExportDto ToCliente(Cliente c) =>
        new(c.Id, c.Nome, c.Telefone, c.Email, c.Nif, c.Notas, c.CreatedAt, c.UpdatedAt);

    private FotoExportDto ToFoto(VendaFoto f, string baseUrl, DateTimeOffset expiresAt)
    {
        var path = _photoLinks.CreatePath(f.Id, expiresAt);
        return new FotoExportDto(f.Id, f.VendaId, f.FileName, f.ContentType, f.Size, f.Tipo, f.Ordem, f.Legenda, f.VisivelNoPortal,
            baseUrl.TrimEnd('/') + path, expiresAt, f.CreatedAt);
    }

    private static GarantiaExportDto ToGarantia(Garantia g) =>
        new(g.Id, g.VendaId, g.SourceType, g.Slug, g.DataInicio, g.DataFim, g.DiasGarantia, g.Cobertura, g.Exclusoes, g.Anulada, g.MotivoAnulacao);

    private static AvaliacaoExportDto ToAvaliacao(Avaliacao a) =>
        new(a.Id, a.VendaId, a.Score, a.Comentario, a.PublicarTestemunho, a.PedidoGoogleReview, a.CreatedAt);

    private static VendaExportDto ToVenda(Venda v, IEnumerable<VendaEstadoLog> timeline) =>
        new(v.Id, v.Numero, v.Tipo, v.Estado, v.Data, v.CreatedAt, v.Equipamento, v.Problema, v.TotalCents, v.IvaCents,
            v.PaymentMethod, v.InvoiceNumber, v.InvoiceEmittedAt, v.Notas,
            v.Items.Select(i => new VendaItemExportDto(
                i.Id, i.Descricao, i.Quantidade, i.PrecoUnitarioCents, i.DescontoCents, i.IvaRate, i.TotalCents, i.Imei)).ToList(),
            timeline.Select(t => new EstadoLogExportDto(t.Id, t.EstadoFrom, t.EstadoTo, t.MudouEm)).ToList());

    private static AuditEntryDto ToAudit(AuditEntry a) =>
        new(a.Id, a.TenantId, a.AppUserId, null, null, a.Action, a.EntityType, a.EntityId, a.ChangesJson, a.IpAddress, a.UserAgent, a.CreatedAt,
            a.ServiceApiKeyId, a.ServiceApiKey?.Name, a.ServiceApiKey?.KeyPrefix);
}
