using System.Globalization;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Vendas;

public interface IVendaService
{
    Task<PagedResult<VendaDto>> SearchAsync(DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct = default);
    Task<VendaDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<VendaDto> CreateAsync(CreateVendaRequest req, CancellationToken ct = default);
    Task<EmitVendaFaturaResponse> MarcarPagaAsync(Guid id, MarcarVendaPagaRequest req, CancellationToken ct = default);
    Task<InvoiceDto> EmitirFaturaAsync(Guid id, CancellationToken ct = default);
    Task<VendaDto> CancelarAsync(Guid id, CancellationToken ct = default);
    Task<VendaDto> AnularFaturaAsync(Guid id, CancellationToken ct = default);
    Task<VendaDto> LimparReferenciaFaturaAsync(Guid id, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);
}

public class VendaService : IVendaService
{
    private readonly IVendaRepository _vendas;
    private readonly IPartRepository _parts;
    private readonly IClienteRepository _clientes;
    private readonly ITenantContext _tenant;
    private readonly ITenantBillingSettingsRepository _billingSettings;
    private readonly IBillingProvider _billing;
    private readonly IMoloniClient _moloni;

    public VendaService(
        IVendaRepository vendas,
        IPartRepository parts,
        IClienteRepository clientes,
        ITenantContext tenant,
        ITenantBillingSettingsRepository billingSettings,
        IBillingProvider billing,
        IMoloniClient moloni)
    {
        _vendas = vendas;
        _parts = parts;
        _clientes = clientes;
        _tenant = tenant;
        _billingSettings = billingSettings;
        _billing = billing;
        _moloni = moloni;
    }

    public async Task<PagedResult<VendaDto>> SearchAsync(DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _vendas.SearchAsync(fromUtc, toUtc, page, pageSize, ct);
        return new PagedResult<VendaDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<VendaDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        return ToDto(venda);
    }

    public async Task<VendaDto> CreateAsync(CreateVendaRequest req, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");
        if (req.Items.Count == 0)
            throw new ValidationException("venda_sem_items", "Adiciona pelo menos uma linha ao carrinho.");

        Cliente? cliente = null;
        if (req.ClienteId is not null)
            cliente = await _clientes.FindByIdAsync(req.ClienteId.Value, ct)
                ?? throw new NotFoundException("Cliente", req.ClienteId.Value);

        var venda = new Venda
        {
            ClienteId = cliente?.Id,
            Cliente = cliente,
            Data = DateTime.UtcNow,
            Status = VendaStatus.Pendente,
            Notas = Clean(req.Notas),
        };

        foreach (var itemReq in req.Items)
        {
            ValidateItemRequest(itemReq);
            Part? part = null;

            if (itemReq.PartId is not null)
            {
                part = await _parts.FindByIdAsync(itemReq.PartId.Value, ct)
                    ?? throw new NotFoundException("Part", itemReq.PartId.Value);
                EnsurePartSellable(part, itemReq.Quantidade);
            }

            venda.Items.Add(new VendaItem
            {
                PartId = part?.Id,
                Part = part,
                Descricao = Clean(itemReq.Descricao) ?? part?.Nome ?? "Artigo",
                Quantidade = itemReq.Quantidade,
                PrecoUnitarioCents = itemReq.PrecoUnitarioCents,
                DescontoCents = itemReq.DescontoCents,
                IvaRate = itemReq.IvaRate,
            });
        }

        RecalculateTotals(venda);
        await _vendas.CreateWithNextNumeroAsync(venda, tenantId, ct);
        return ToDto(venda);
    }

    public async Task<EmitVendaFaturaResponse> MarcarPagaAsync(Guid id, MarcarVendaPagaRequest req, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        if (venda.Status == VendaStatus.Cancelada)
            throw new ConflictException("venda_cancelada", "Venda cancelada nao pode ser marcada como paga.");

        if (venda.Status != VendaStatus.Paga)
        {
            foreach (var item in venda.Items.Where(i => i.PartId is not null))
            {
                var part = await _parts.FindByIdAsync(item.PartId!.Value, ct)
                    ?? throw new NotFoundException("Part", item.PartId.Value);
                EnsurePartSellable(part, item.Quantidade);

                var before = part.QtdStock;
                var after = before - item.Quantidade;
                part.QtdStock = after;
                _parts.AddMovimento(new PartMovimento
                {
                    PartId = part.Id,
                    Quantidade = -item.Quantidade,
                    StockAntes = before,
                    StockDepois = after,
                    Motivo = PartMovimentoMotivo.VendaCliente,
                    VendaId = venda.Id,
                    Notas = $"Venda #{venda.Numero:D5}",
                });
            }

            venda.Status = VendaStatus.Paga;
            venda.PaymentMethod = req.PaymentMethod;
            venda.Data = DateTime.UtcNow;
            RecalculateTotals(venda);
            await _vendas.SaveAsync(ct);
        }

        InvoiceDto? invoice = null;
        if (req.EmitirFatura && await HasBillingProviderAsync(ct))
            invoice = await EmitirFaturaAsync(venda.Id, ct);

        venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? venda;
        return new EmitVendaFaturaResponse(ToDto(venda), invoice);
    }

    public async Task<InvoiceDto> EmitirFaturaAsync(Guid id, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        if (venda.InvoiceExternalId is not null)
            return new InvoiceDto(venda.InvoiceNumber ?? "Fatura emitida", venda.InvoicePdfUrl, venda.InvoiceEmittedAt ?? DateTime.UtcNow);
        if (venda.Status != VendaStatus.Paga)
            throw new ValidationException("venda_nao_paga", "So podes emitir fatura depois de marcar a venda como paga.");

        return await _billing.EmitVendaInvoiceAsync(id, ct);
    }

    /// <summary>Limpa apenas referencias locais da fatura. Util quando o utilizador ja anulou a
    /// fatura manualmente no painel Moloni (status 'Anulado') e so quer sincronizar com o RepairDesk.</summary>
    public async Task<byte[]> ExportCsvAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        // SearchAsync com pageSize generoso. 1000 vendas chega para vários meses.
        var (rows, _) = await _vendas.SearchAsync(fromUtc, toUtc, 1, 1000, ct);

        var csv = new CsvBuilder();
        csv.Row(
            "numero", "data", "cliente_nome", "cliente_nif",
            "total_eur", "iva_eur", "metodo_pagamento", "status",
            "invoice_provider", "invoice_numero", "invoice_emitida_em",
            "notas");

        foreach (var v in rows)
        {
            // Calcular IVA total da venda a partir dos items
            var totalCents = v.Items.Sum(i => Math.Max(0, i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents));
            var ivaCents = v.Items.Sum(i =>
            {
                if (i.IvaRate <= 0) return 0;
                var gross = Math.Max(0, i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents);
                return (int)Math.Round(gross - gross / (1m + i.IvaRate / 100m));
            });

            var statusLabel = v.Status switch
            {
                VendaStatus.Pendente => "Pendente",
                VendaStatus.Paga => "Paga",
                VendaStatus.Cancelada => "Cancelada",
                _ => v.Status.ToString(),
            };
            var paymentLabel = v.PaymentMethod switch
            {
                PaymentMethod.Dinheiro => "Numerario",
                PaymentMethod.Multibanco => "Multibanco",
                PaymentMethod.MBWay => "MBWay",
                PaymentMethod.TransferenciaBancaria => "Transferencia",
                PaymentMethod.Cartao => "Cartao",
                _ => "Outro",
            };

            csv.Row(
                v.Numero,
                v.Data.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                v.Cliente?.Nome ?? "",
                v.Cliente?.Nif ?? "",
                (totalCents / 100m).ToString("0.00", CultureInfo.InvariantCulture),
                (ivaCents / 100m).ToString("0.00", CultureInfo.InvariantCulture),
                paymentLabel,
                statusLabel,
                v.InvoiceProvider == BillingProvider.Moloni ? "Moloni" : "",
                v.InvoiceNumber ?? "",
                v.InvoiceEmittedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                v.Notas ?? "");
        }

        return csv.ToUtf8WithBom();
    }

    public async Task<VendaDto> LimparReferenciaFaturaAsync(Guid id, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        if (string.IsNullOrEmpty(venda.InvoiceExternalId))
            throw new ConflictException("venda_sem_fatura", "Esta venda nao tem fatura para limpar.");

        venda.InvoiceProvider = BillingProvider.None;
        venda.InvoiceExternalId = null;
        venda.InvoiceNumber = null;
        venda.InvoicePdfUrl = null;
        venda.InvoiceEmittedAt = null;
        await _vendas.SaveAsync(ct);
        return ToDto(venda);
    }

    public async Task<VendaDto> AnularFaturaAsync(Guid id, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        if (string.IsNullOrEmpty(venda.InvoiceExternalId))
            throw new ConflictException("venda_sem_fatura", "Esta venda nao tem fatura emitida para anular.");

        // Estratégia: tentar documentCancel primeiro (1 documento, mais limpo). Se Moloni rejeitar
        // (porque ja foi processado pela AT, etc), fallback para Nota de Credito.
        if (_tenant.TenantId is { } tenantId)
        {
            var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
            if (settings?.Provider == BillingProvider.Moloni && int.TryParse(venda.InvoiceExternalId, out var originalDocId))
            {
                var cancelled = await _moloni.CancelDocumentAsync(
                    settings,
                    originalDocId,
                    $"Anulado via RepairDesk — venda #{venda.Numero}",
                    ct);

                if (!cancelled)
                {
                    // Fallback: emite Nota de Credito (caso documentCancel nao seja aplicavel)
                    var items = venda.Items.Select(i => new MoloniInvoiceDraftItem(
                        i.Descricao,
                        null,
                        i.Quantidade,
                        i.PrecoUnitarioCents,
                        i.DescontoCents,
                        i.IvaRate)).ToList();

                    var customerId = settings.FallbackCustomerId ?? 0;
                    if (customerId <= 0)
                        throw new ValidationException("moloni_customer_fallback_missing", "Cliente fallback Moloni nao configurado.");

                    await _moloni.InsertCreditNoteAsync(settings, new MoloniCreditNoteDraft(
                        originalDocId,
                        customerId,
                        $"Venda #{venda.Numero}",
                        items,
                        $"Anulacao da Fatura {venda.InvoiceNumber} via RepairDesk"
                    ), ct);
                }
            }
        }

        // Limpa referencias locais para a venda sair do Relatorio IVA do RepairDesk
        venda.InvoiceProvider = BillingProvider.None;
        venda.InvoiceExternalId = null;
        venda.InvoiceNumber = null;
        venda.InvoicePdfUrl = null;
        venda.InvoiceEmittedAt = null;

        await _vendas.SaveAsync(ct);
        return ToDto(venda);
    }

    public async Task<VendaDto> CancelarAsync(Guid id, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(id, ct) ?? throw new NotFoundException("Venda", id);
        if (venda.Status == VendaStatus.Cancelada) return ToDto(venda);
        if (venda.InvoiceExternalId is not null)
            throw new ConflictException("venda_faturada", "Venda com fatura emitida nao pode ser cancelada sem nota de credito.");

        if (venda.Status == VendaStatus.Paga)
        {
            foreach (var item in venda.Items.Where(i => i.PartId is not null))
            {
                var part = await _parts.FindByIdAsync(item.PartId!.Value, ct)
                    ?? throw new NotFoundException("Part", item.PartId.Value);
                var before = part.QtdStock;
                var after = before + item.Quantidade;
                part.QtdStock = after;
                _parts.AddMovimento(new PartMovimento
                {
                    PartId = part.Id,
                    Quantidade = item.Quantidade,
                    StockAntes = before,
                    StockDepois = after,
                    Motivo = PartMovimentoMotivo.Devolucao,
                    VendaId = venda.Id,
                    Notas = $"Cancelamento venda #{venda.Numero:D5}",
                });
            }
        }

        venda.Status = VendaStatus.Cancelada;
        await _vendas.SaveAsync(ct);
        return ToDto(venda);
    }

    private async Task<bool> HasBillingProviderAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return false;
        var settings = await _billingSettings.FindByTenantIdAsync(tenantId, ct);
        return settings?.Provider == BillingProvider.Moloni;
    }

    private static void ValidateItemRequest(CreateVendaItemRequest item)
    {
        if (item.Quantidade <= 0)
            throw new ValidationException("quantidade_invalida", "Quantidade deve ser superior a zero.");
        if (item.PrecoUnitarioCents < 0)
            throw new ValidationException("preco_invalido", "Preco unitario nao pode ser negativo.");
        if (item.DescontoCents < 0)
            throw new ValidationException("desconto_invalido", "Desconto nao pode ser negativo.");
        if (item.DescontoCents > item.Quantidade * item.PrecoUnitarioCents)
            throw new ValidationException("desconto_excessivo", "Desconto nao pode exceder o total da linha.");
        if (item.IvaRate < 0 || item.IvaRate > 100)
            throw new ValidationException("iva_invalido", "IVA deve estar entre 0 e 100.");
        if (item.PartId is null && string.IsNullOrWhiteSpace(item.Descricao))
            throw new ValidationException("descricao_obrigatoria", "Linha sem stock precisa de descricao.");
    }

    private static void EnsurePartSellable(Part part, int quantity)
    {
        if (!part.Activo)
            throw new ConflictException("part_inactive", "Peca inactiva nao pode ser vendida.");
        if (part.QtdStock <= 0)
            throw new ValidationException("stock_zero", $"{part.Nome} esta sem stock.");
        if (part.QtdStock < quantity)
            throw new ValidationException("stock_insuficiente", $"{part.Nome} so tem {part.QtdStock} unidade(s) em stock.");
    }

    private static void RecalculateTotals(Venda venda)
    {
        venda.TotalCents = venda.Items.Sum(i => i.TotalCents);
        venda.IvaCents = venda.Items.Sum(CalculateIvaCents);
    }

    private static int CalculateIvaCents(VendaItem item)
    {
        if (item.IvaRate <= 0) return 0;
        var total = item.TotalCents;
        var net = total / (1 + item.IvaRate / 100m);
        return (int)Math.Round(total - net, MidpointRounding.AwayFromZero);
    }

    private static VendaDto ToDto(Venda venda)
    {
        var cliente = venda.Cliente is null || venda.ClienteId is null
            ? null
            : new VendaClienteResumo(venda.Cliente.Id, venda.Cliente.Nome, venda.Cliente.Telefone ?? string.Empty);

        return new VendaDto(
            venda.Id,
            venda.Numero,
            venda.Data,
            cliente,
            venda.TotalCents,
            venda.IvaCents,
            venda.PaymentMethod,
            venda.Status,
            venda.InvoiceProvider,
            venda.InvoiceExternalId,
            venda.InvoicePdfUrl,
            venda.InvoiceNumber,
            venda.InvoiceEmittedAt,
            venda.Notas,
            venda.Items.Select(i => new VendaItemDto(
                i.Id,
                i.PartId,
                i.Part?.Sku,
                i.Descricao,
                i.Quantidade,
                i.PrecoUnitarioCents,
                i.DescontoCents,
                i.IvaRate,
                i.TotalCents,
                CalculateIvaCents(i))).ToList());
    }

    private static string? Clean(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
