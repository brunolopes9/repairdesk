using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Billing;

public interface IBillingProvider
{
    Task<InvoiceDto> EmitReparacaoInvoiceAsync(Guid reparacaoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default);
    Task<InvoiceDto> EmitTrabalhoInvoiceAsync(Guid trabalhoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default);
    Task<InvoiceDto> EmitVendaInvoiceAsync(Guid vendaId, CancellationToken ct = default);
    Task<Stream> GetPdfStreamAsync(string invoiceId, CancellationToken ct = default);
}

public class MoloniBillingProvider : IBillingProvider
{
    private readonly IReparacaoRepository _reparacoes;
    private readonly ITrabalhoRepository _trabalhos;
    private readonly IVendaRepository _vendas;
    private readonly ITenantBillingSettingsRepository _settingsRepo;
    private readonly ITenantRepository _tenants;
    private readonly ITenantContext _tenant;
    private readonly IMoloniClient _moloni;
    private readonly IPartRepository _parts;

    public MoloniBillingProvider(
        IReparacaoRepository reparacoes,
        ITrabalhoRepository trabalhos,
        IVendaRepository vendas,
        ITenantBillingSettingsRepository settingsRepo,
        ITenantRepository tenants,
        ITenantContext tenant,
        IMoloniClient moloni,
        IPartRepository parts)
    {
        _reparacoes = reparacoes;
        _trabalhos = trabalhos;
        _vendas = vendas;
        _settingsRepo = settingsRepo;
        _tenants = tenants;
        _tenant = tenant;
        _moloni = moloni;
        _parts = parts;
    }

    public async Task<InvoiceDto> EmitReparacaoInvoiceAsync(Guid reparacaoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default)
    {
        var reparacao = await _reparacoes.FindByIdWithTimelineAsync(reparacaoId, ct)
            ?? throw new NotFoundException("Reparacao", reparacaoId);

        if (reparacao.InvoiceExternalId is not null)
            return ToDto(reparacao.InvoiceNumber, reparacao.InvoicePdfUrl, reparacao.InvoiceEmittedAt);

        EnsurePaid(reparacao.EstadoPagamento);
        var settings = await RequireSettingsAsync(ct);
        var tenant = await RequireTenantAsync(ct);
        var amount = RequireAmount(reparacao.PrecoFinalCents ?? reparacao.OrcamentoCents);
        var customerId = await ResolveCustomerIdAsync(settings, reparacao.Cliente, ct);
        var effectiveVat = ResolveVatPercent(tenant, vatPercent);

        // Sprint 136: discrimina peças do stock + mão-de-obra na fatura. Fallback null se não há peças.
        var lines = await BuildBillingItemsAsync(reparacao, amount, effectiveVat, ct);

        var result = await _moloni.InsertInvoiceAsync(settings, new MoloniInvoiceDraft(
            customerId,
            $"Reparacao #{reparacao.Numero}",
            $"Reparacao {reparacao.Equipamento}",
            reparacao.Avaria,
            amount,
            effectiveVat,
            paymentMethod,
            Items: lines),
            ct);

        reparacao.InvoiceProvider = BillingProvider.Moloni;
        reparacao.InvoiceExternalId = result.ExternalId;
        reparacao.InvoicePdfUrl = result.PdfUrl;
        reparacao.InvoiceNumber = result.Number;
        reparacao.InvoiceEmittedAt = result.EmittedAt;
        await _reparacoes.SaveAsync(ct);

        return new InvoiceDto(result.Number, result.PdfUrl, result.EmittedAt);
    }

    public async Task<InvoiceDto> EmitTrabalhoInvoiceAsync(Guid trabalhoId, decimal? vatPercent, string? paymentMethod, CancellationToken ct = default)
    {
        var trabalho = await _trabalhos.FindByIdAsync(trabalhoId, ct)
            ?? throw new NotFoundException("Trabalho", trabalhoId);

        if (trabalho.InvoiceExternalId is not null)
            return ToDto(trabalho.InvoiceNumber, trabalho.InvoicePdfUrl, trabalho.InvoiceEmittedAt);

        EnsurePaid(trabalho.EstadoPagamento);
        var settings = await RequireSettingsAsync(ct);
        var tenant = await RequireTenantAsync(ct);
        var amount = RequireAmount(trabalho.PrecoFinalCents ?? trabalho.OrcamentoCents);
        var customerId = await ResolveCustomerIdAsync(settings, trabalho.Cliente, ct);
        var effectiveVat = ResolveVatPercent(tenant, vatPercent);

        var result = await _moloni.InsertInvoiceAsync(settings, new MoloniInvoiceDraft(
            customerId,
            $"Trabalho #{trabalho.Numero}",
            trabalho.Titulo,
            trabalho.Descricao,
            amount,
            effectiveVat,
            paymentMethod),
            ct);

        trabalho.InvoiceProvider = BillingProvider.Moloni;
        trabalho.InvoiceExternalId = result.ExternalId;
        trabalho.InvoicePdfUrl = result.PdfUrl;
        trabalho.InvoiceNumber = result.Number;
        trabalho.InvoiceEmittedAt = result.EmittedAt;
        await _trabalhos.SaveAsync(ct);

        return new InvoiceDto(result.Number, result.PdfUrl, result.EmittedAt);
    }

    public async Task<InvoiceDto> EmitVendaInvoiceAsync(Guid vendaId, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(vendaId, ct)
            ?? throw new NotFoundException("Venda", vendaId);

        if (venda.InvoiceExternalId is not null)
            return ToDto(venda.InvoiceNumber, venda.InvoicePdfUrl, venda.InvoiceEmittedAt);
        if (venda.Status != VendaStatus.Paga)
            throw new ValidationException("invoice_requires_paid", "So podes emitir fatura quando a venda estiver paga.");
        if (venda.Items.Count == 0)
            throw new ValidationException("invoice_items_missing", "A venda nao tem linhas para faturar.");

        var settings = await RequireSettingsAsync(ct);
        var customerId = await ResolveCustomerIdAsync(settings, venda.Cliente, ct);
        // Sprint 113: Fatura Simplificada quando não há NIF (cliente anónimo OU criado com nome
        // interno como "Sérgio de Guimarães" sem NIF). Limite €1000 — abaixo desse valor o cliente
        // não precisa de NIF para a fatura ter validade fiscal.
        var documentType = string.IsNullOrWhiteSpace(venda.Cliente?.Nif)
            ? BillingDocumentType.FaturaSimplificada
            : BillingDocumentType.Fatura;

        var items = venda.Items.Select(i => new MoloniInvoiceDraftItem(
            i.Descricao,
            i.Part?.Sku,
            i.Quantidade,
            i.PrecoUnitarioCents,
            i.DescontoCents,
            i.IvaRate)).ToList();

        var result = await _moloni.InsertInvoiceAsync(settings, new MoloniInvoiceDraft(
            customerId,
            $"Venda #{venda.Numero}",
            $"Venda #{venda.Numero}",
            venda.Notas,
            venda.TotalCents,
            items.Max(i => i.VatPercent),
            venda.PaymentMethod.ToString(),
            documentType,
            items),
            ct);

        venda.InvoiceProvider = BillingProvider.Moloni;
        venda.InvoiceExternalId = result.ExternalId;
        venda.InvoicePdfUrl = result.PdfUrl;
        venda.InvoiceNumber = result.Number;
        venda.InvoiceEmittedAt = result.EmittedAt;
        await _vendas.SaveAsync(ct);

        return new InvoiceDto(result.Number, result.PdfUrl, result.EmittedAt);
    }

    public async Task<Stream> GetPdfStreamAsync(string invoiceId, CancellationToken ct = default)
    {
        var settings = await RequireSettingsAsync(ct);
        return await _moloni.GetPdfStreamAsync(settings, invoiceId, ct);
    }

    private async Task<TenantBillingSettings> RequireSettingsAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var settings = await _settingsRepo.FindByTenantIdAsync(tenantId, ct);
        if (settings is null || settings.Provider == BillingProvider.None)
            throw new ValidationException("billing_not_configured", "Configura a faturacao em Definicoes > Faturacao.");
        if (settings.Provider != BillingProvider.Moloni)
            throw new ValidationException("billing_provider_not_supported", "Este provider de faturacao ainda nao esta implementado.");
        return settings;
    }

    private async Task<Tenant> RequireTenantAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");
        return await _tenants.FindByIdAsync(tenantId, ct)
            ?? throw new NotFoundException("Tenant", tenantId);
    }

    private async Task<int> ResolveCustomerIdAsync(TenantBillingSettings settings, Cliente? cliente, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(cliente?.Nif))
        {
            var id = await _moloni.FindCustomerIdByVatAsync(settings, cliente.Nif, ct);
            if (id is > 0) return id.Value;

            // Sprint 66: cliente novo — cria na Moloni se tem NIF + nome.
            if (!string.IsNullOrWhiteSpace(cliente.Nome))
            {
                var created = await _moloni.InsertCustomerAsync(settings, cliente.Nome.Trim(), cliente.Nif.Trim(), ct);
                if (created.Id > 0) return created.Id;
            }
        }

        if (settings.FallbackCustomerId is > 0)
            return settings.FallbackCustomerId.Value;

        // Sprint 113: fallback hardcoded — Consumidor Final PT (NIF 999999990).
        var consumidorFinalId = await _moloni.FindCustomerIdByVatAsync(settings, "999999990", ct);
        if (consumidorFinalId is > 0) return consumidorFinalId.Value;

        throw new ValidationException(
            "moloni_customer_missing",
            "Cliente sem NIF e não foi possível encontrar Consumidor Final no Moloni. "
            + "Liga Moloni nas Definições (auto-discovery cria 'Consumidor Final') ou adiciona NIF ao cliente.");
    }

    private static decimal ResolveVatPercent(Tenant tenant, decimal? explicitVat)
    {
        if (explicitVat is not null) return explicitVat.Value;
        return tenant.RegimeFiscal == RegimeFiscal.IsentoArt53 ? 0m : 23m;
    }

    /// <summary>Sprint 136: helper partilhado com ReparacaoService (estimate). Devolve null = fallback.</summary>
    private async Task<IReadOnlyList<MoloniInvoiceDraftItem>?> BuildBillingItemsAsync(
        Reparacao rep, int totalCents, decimal vatPercent, CancellationToken ct)
    {
        var movimentos = await _parts.MovimentosAsync(partId: null, reparacaoId: rep.Id, ct);
        if (movimentos.Count == 0) return null;
        var usedParts = movimentos
            .GroupBy(m => m.PartId)
            .Select(g =>
            {
                var first = g.First();
                var netQty = -g.Sum(m => m.Quantidade);
                var name = first.Part?.Nome ?? "Peça";
                var unitCost = first.Part?.CustoUnitarioCents ?? 0;
                return new ReparacaoBillingItemsBuilder.UsedPart(name, netQty, unitCost);
            })
            .Where(p => p.Quantity > 0)
            .ToList();
        if (usedParts.Count == 0) return null;
        return ReparacaoBillingItemsBuilder.Build(rep.Equipamento, usedParts, totalCents, vatPercent);
    }

    private static void EnsurePaid(PaymentStatus paymentStatus)
    {
        if (paymentStatus != PaymentStatus.Pago)
            throw new ValidationException("invoice_requires_paid", "So podes emitir fatura quando estiver marcado como pago.");
    }

    private static int RequireAmount(int? amountCents)
    {
        if (amountCents is null or <= 0)
            throw new ValidationException("invoice_amount_missing", "Define um valor final antes de emitir fatura.");
        return amountCents.Value;
    }

    private static InvoiceDto ToDto(string? number, string? pdfUrl, DateTime? emittedAt)
        => new(number ?? "Fatura emitida", pdfUrl, emittedAt ?? DateTime.UtcNow);
}
