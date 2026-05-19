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
            "repairdesk-client-export-v2",
            ToCliente(data.Cliente),
            data.Reparacoes.Select(r => ToReparacao(r, data.Timeline.Where(t => t.ReparacaoId == r.Id))).ToList(),
            data.Trabalhos.Select(ToTrabalho).ToList(),
            data.Despesas.Select(ToDespesa).ToList(),
            data.Fotos.Select(f => ToFoto(f, baseUrl, expiresAt)).ToList(),
            data.Garantias.Select(ToGarantia).ToList(),
            data.Avaliacoes.Select(ToAvaliacao).ToList(),
            data.PartMovimentos.Select(ToPartMovimento).ToList(),
            data.Vendas.Select(ToVenda).ToList(),
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
            data.Reparacoes.Count,
            data.Trabalhos.Count,
            data.Despesas.Count,
            data.Fotos.Count,
            data.Vendas.Count);

        await _audit.LogAsync(AuditAction.HardDelete, "Cliente", clienteId, new
        {
            cliente = data.Cliente.Nome,
            motivo = req.Motivo,
            response.Reparacoes,
            response.Trabalhos,
            response.Despesas,
            response.Fotos,
            response.Vendas,
        }, data.Cliente.TenantId, ct: ct);

        return response;
    }

    private static ClienteExportDto ToCliente(Cliente c) =>
        new(c.Id, c.Nome, c.Telefone, c.Email, c.Nif, c.Notas, c.CreatedAt, c.UpdatedAt);

    private static ReparacaoExportDto ToReparacao(Reparacao r, IEnumerable<ReparacaoEstadoLog> timeline) =>
        new(r.Id, r.Numero, r.Equipamento, r.Imei, r.Avaria, r.Diagnostico, r.Estado, r.EstadoSince, r.CreatedAt, r.EntregueEm,
            r.OrcamentoCents, r.OrcamentoAprovado, r.PrecoFinalCents, r.CustoPecasCents, r.HorasGastas, r.Notas, r.EstadoPagamento,
            r.PublicSlug, timeline.Select(t => new EstadoLogExportDto(t.Id, t.EstadoFrom, t.EstadoTo, t.MudouEm, t.UserId, t.Notas)).ToList());

    private static TrabalhoExportDto ToTrabalho(Trabalho t) =>
        new(t.Id, t.Numero, t.Titulo, t.Descricao, t.Categoria, t.Status, t.DataInicio, t.DataConclusao, t.OrcamentoCents,
            t.PrecoFinalCents, t.HorasGastas, t.Notas, t.EstadoPagamento, t.CreatedAt);

    private static DespesaExportDto ToDespesa(Despesa d) =>
        new(d.Id, d.Descricao, d.Categoria, d.ValorCents, d.Data, d.Fornecedor, d.NumeroEncomenda, d.Notas, d.TrabalhoId, d.ReparacaoId, d.CreatedAt);

    private FotoExportDto ToFoto(ReparacaoFoto f, string baseUrl, DateTimeOffset expiresAt)
    {
        var path = _photoLinks.CreatePath(f.Id, expiresAt);
        return new FotoExportDto(f.Id, f.ReparacaoId, f.FileName, f.ContentType, f.Size, f.Tipo, f.Ordem, f.Legenda, f.VisivelNoPortal,
            baseUrl.TrimEnd('/') + path, expiresAt, f.CreatedAt);
    }

    private static GarantiaExportDto ToGarantia(Garantia g) =>
        new(g.Id, g.ReparacaoId, g.VendaId, g.SourceType, g.Slug, g.DataInicio, g.DataFim, g.DiasGarantia, g.Cobertura, g.Exclusoes, g.Anulada, g.MotivoAnulacao);

    private static AvaliacaoExportDto ToAvaliacao(Avaliacao a) =>
        new(a.Id, a.ReparacaoId, a.Score, a.Comentario, a.PublicarTestemunho, a.PedidoGoogleReview, a.CreatedAt);

    private static PartMovimentoExportDto ToPartMovimento(PartMovimento m) =>
        new(m.Id, m.PartId, m.Part?.Nome, m.Part?.Sku, m.Quantidade, m.StockAntes, m.StockDepois, m.Motivo, m.ReparacaoId, m.Notas, m.CreatedAt);

    private static VendaExportDto ToVenda(Venda v) =>
        new(v.Id, v.Numero, v.Data, v.TotalCents, v.IvaCents, v.PaymentMethod, v.Status,
            v.InvoiceProvider, v.InvoiceExternalId, v.InvoiceNumber, v.InvoicePdfUrl, v.InvoiceEmittedAt, v.Notas,
            v.Items.Select(i => new VendaItemExportDto(
                i.Id, i.PartId, i.Part?.Sku, i.Descricao,
                i.Quantidade, i.PrecoUnitarioCents, i.DescontoCents, i.IvaRate, i.TotalCents)).ToList());

    private static AuditEntryDto ToAudit(AuditEntry a) =>
        new(a.Id, a.TenantId, a.AppUserId, null, null, a.Action, a.EntityType, a.EntityId, a.ChangesJson, a.IpAddress, a.UserAgent, a.CreatedAt);
}
