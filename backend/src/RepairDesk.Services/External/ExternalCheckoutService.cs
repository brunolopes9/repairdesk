using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Vendas;

namespace RepairDesk.Services.External;

/// <summary>
/// Endpoint atómico para integrações externas (loja online, kiosks, importadores) que
/// querem fechar uma venda inteira num único POST: cria/encontra cliente, cria venda
/// e marca paga (decrementa stock, emite garantia, opcionalmente emite fatura).
/// </summary>
public interface IExternalCheckoutService
{
    Task<ExternalCheckoutResponse> CheckoutAsync(ExternalCheckoutRequest req, CancellationToken ct = default);
    Task<ExternalOrderStatusResponse> GetOrderAsync(Guid vendaId, CancellationToken ct = default);
    Task<ExternalOrderStatusResponse> CancelOrderAsync(Guid vendaId, string? motivo, CancellationToken ct = default);
    Task<Clientes.PagedResult<ExternalPartDto>> ListPartsAsync(string? search, PartCategoria? categoria, int page, int pageSize, bool? lojaOnline = null, CancellationToken ct = default);
    Task<ExternalClienteHistoricoResponse?> GetHistoricoByNifAsync(string nif, CancellationToken ct = default);
    Task<ExternalGarantiaDetalhe?> GetGarantiaBySlugAsync(string slug, CancellationToken ct = default);
}

/// <summary>Resposta do health check — usada por integradores para clock skew e confirmação do tenant.</summary>
public sealed record ExternalHealthResponse(
    string Status,
    DateTimeOffset ServerTime,
    string ApiVersion,
    Guid? TenantId);

/// <summary>Detalhe da garantia para integrações externas — espelha PublicGarantiaDto sem mascaramento.</summary>
public sealed record ExternalGarantiaDetalhe(
    string Slug,
    string Origem,
    DateTime DataInicio,
    DateTime DataFim,
    int DiasGarantia,
    int DiasRestantes,
    bool Activa,
    bool Anulada,
    string? MotivoAnulacao,
    string Equipamento,
    string? Cobertura,
    string? Exclusoes,
    string? DocumentoReferencia,
    string? NumeroFatura);

/// <summary>
/// Histórico agregado de um cliente — vendas, reparações e garantias activas.
/// Pensado para loja online mostrar "Os meus pedidos" sem replicar BD.
/// Null se NIF não corresponde a cliente do tenant.
/// </summary>
public sealed record ExternalClienteHistoricoResponse(
    Guid ClienteId,
    string Nome,
    string? Email,
    string? Telefone,
    IReadOnlyList<ExternalVendaResumo> Vendas,
    IReadOnlyList<ExternalReparacaoResumo> Reparacoes,
    IReadOnlyList<ExternalGarantiaResumo> GarantiasActivas);

public sealed record ExternalVendaResumo(
    Guid Id,
    int Numero,
    DateTime Data,
    int TotalCents,
    string Status,
    string Origem,
    string? FaturaNumero,
    string? FaturaPdfUrl);

public sealed record ExternalReparacaoResumo(
    Guid Id,
    int Numero,
    DateTime RecebidoEm,
    string Equipamento,
    int Estado,
    /// <summary>Slug público /p/{slug} para acompanhamento.</summary>
    string? PublicSlug);

public sealed record ExternalGarantiaResumo(
    string Slug,
    string Origem,           // "Reparacao" ou "Venda"
    DateTime DataFim,
    int DiasRestantes,
    string? Equipamento);

/// <summary>
/// Versão pública do catálogo — NÃO expõe custo, fornecedor, local armazenamento, notas internas.
/// Para uso por integrações externas (loja online) que precisam de listar acessórios disponíveis.
/// </summary>
public sealed record ExternalPartDto(
    Guid Id,
    string? Sku,
    string Nome,
    PartCategoria Categoria,
    string? Marca,
    string? Modelo,
    /// <summary>Sprint 121: true se Bruno marcou esta peça para aparecer na loja online.</summary>
    bool MostrarLojaOnline,
    /// <summary>Stock disponível atualmente.</summary>
    int QtdStock,
    bool Activo);

public sealed record ExternalOrderStatusResponse(
    Guid VendaId,
    int VendaNumero,
    DateTime Data,
    Guid? ClienteId,
    int TotalCents,
    int IvaCents,
    /// <summary>"Pendente" / "Paga" / "Cancelada".</summary>
    string Status,
    /// <summary>"Balcao" / "Online" / "Importacao".</summary>
    string Origem,
    string? FaturaNumero,
    string? FaturaPdfUrl,
    DateTime? FaturaEmitidaEm,
    string? GarantiaSlug,
    bool GarantiaActiva,
    bool GarantiaAnulada,
    DateTime? CanceladaEm);

public sealed record CancelOrderRequest(string? Motivo);

public sealed record ExternalCheckoutRequest(
    /// <summary>Dados do cliente final. NIF é opcional mas recomendado para emissão de fatura PT.</summary>
    ExternalCheckoutCliente Cliente,
    IReadOnlyList<CreateVendaItemRequest> Items,
    PaymentMethod PaymentMethod,
    /// <summary>Quando true, emite fatura via provider de billing (Moloni/InvoiceXpress) após marcar paga.</summary>
    bool EmitirFatura = true,
    string? Notas = null,
    /// <summary>Default Online — para integrações externas. Override apenas se for outro canal.</summary>
    VendaOrigem? Origem = null);

public sealed record ExternalCheckoutCliente(
    string Nome,
    string? Telefone,
    string? Email,
    string? Nif,
    string? Notas);

public sealed record ExternalCheckoutResponse(
    Guid VendaId,
    int VendaNumero,
    Guid ClienteId,
    bool ClienteCreated,
    int TotalCents,
    int IvaCents,
    string? FaturaNumero,
    string? FaturaPdfUrl,
    /// <summary>Slug da garantia digital (URL pública: /g/{slug}).</summary>
    string? GarantiaSlug);

public class ExternalCheckoutService : IExternalCheckoutService
{
    private readonly IClienteService _clientes;
    private readonly IVendaService _vendas;
    private readonly IGarantiaRepository _garantias;
    private readonly IAuditLogger _audit;
    private readonly ITenantContext _tenant;
    private readonly IPartRepository _parts;
    private readonly IClienteRepository _clienteRepo;
    private readonly IVendaRepository _vendaRepo;
    private readonly IReparacaoRepository _reparacaoRepo;

    public ExternalCheckoutService(
        IClienteService clientes,
        IVendaService vendas,
        IGarantiaRepository garantias,
        IAuditLogger audit,
        ITenantContext tenant,
        IPartRepository parts,
        IClienteRepository clienteRepo,
        IVendaRepository vendaRepo,
        IReparacaoRepository reparacaoRepo)
    {
        _clientes = clientes;
        _vendas = vendas;
        _garantias = garantias;
        _audit = audit;
        _tenant = tenant;
        _parts = parts;
        _clienteRepo = clienteRepo;
        _vendaRepo = vendaRepo;
        _reparacaoRepo = reparacaoRepo;
    }

    public async Task<ExternalClienteHistoricoResponse?> GetHistoricoByNifAsync(string nif, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nif)) return null;
        var cliente = await _clienteRepo.FindByNifAsync(nif.Trim(), ct);
        if (cliente is null) return null;

        var (vendas, _) = await _vendaRepo.SearchAsync(null, null, cliente.Id, 1, 100, ct);
        var (reparacoes, _) = await _reparacaoRepo.SearchAsync(null, null, cliente.Id, 1, 100, ct);

        var vendaResumos = vendas.Select(v => new ExternalVendaResumo(
            v.Id, v.Numero, v.Data, v.TotalCents,
            v.Status.ToString(), v.Origem.ToString(),
            v.InvoiceNumber, v.InvoicePdfUrl)).ToList();

        var reparacaoResumos = reparacoes.Select(r => new ExternalReparacaoResumo(
            r.Id, r.Numero, r.CreatedAt, r.Equipamento, (int)r.Estado, r.PublicSlug)).ToList();

        // Garantias activas — uma por venda/reparação, agregadas
        var agora = DateTime.UtcNow;
        var garantiasActivas = new List<ExternalGarantiaResumo>();
        foreach (var v in vendas)
        {
            var g = await _garantias.FindByVendaAsync(v.Id, ct);
            if (g is not null && !g.Anulada && agora >= g.DataInicio && agora <= g.DataFim)
            {
                garantiasActivas.Add(new ExternalGarantiaResumo(
                    g.Slug, "Venda", g.DataFim,
                    (int)Math.Max(0, (g.DataFim - agora).TotalDays),
                    v.Items.FirstOrDefault()?.Descricao));
            }
        }
        foreach (var r in reparacoes)
        {
            var g = await _garantias.FindByReparacaoAsync(r.Id, ct);
            if (g is not null && !g.Anulada && agora >= g.DataInicio && agora <= g.DataFim)
            {
                garantiasActivas.Add(new ExternalGarantiaResumo(
                    g.Slug, "Reparacao", g.DataFim,
                    (int)Math.Max(0, (g.DataFim - agora).TotalDays),
                    r.Equipamento));
            }
        }

        return new ExternalClienteHistoricoResponse(
            cliente.Id, cliente.Nome, cliente.Email, cliente.Telefone,
            vendaResumos, reparacaoResumos, garantiasActivas);
    }

    public async Task<ExternalGarantiaDetalhe?> GetGarantiaBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var g = await _garantias.FindBySlugAsync(slug.Trim(), ct);
        if (g is null) return null;

        var agora = DateTime.UtcNow;
        var activa = !g.Anulada && agora >= g.DataInicio && agora <= g.DataFim;
        var diasRestantes = (int)Math.Max(0, (g.DataFim - agora).TotalDays);

        string equipamento;
        string origem;
        string? docRef;
        string? numFatura;
        if (g.SourceType == GarantiaSourceType.Venda && g.Venda is not null)
        {
            equipamento = g.Venda.Items.FirstOrDefault()?.Descricao ?? "Artigos vendidos";
            origem = "Venda";
            docRef = $"Venda #{g.Venda.Numero:D5}";
            numFatura = g.Venda.InvoiceNumber;
        }
        else
        {
            equipamento = g.Reparacao?.Equipamento ?? "Equipamento";
            origem = "Reparacao";
            docRef = g.Reparacao is not null ? $"Reparação #{g.Reparacao.Numero:D5}" : null;
            numFatura = null;
        }

        return new ExternalGarantiaDetalhe(
            g.Slug, origem, g.DataInicio, g.DataFim, g.DiasGarantia, diasRestantes,
            activa, g.Anulada, g.MotivoAnulacao,
            equipamento, g.Cobertura, g.Exclusoes, docRef, numFatura);
    }

    public async Task<Clientes.PagedResult<ExternalPartDto>> ListPartsAsync(
        string? search, PartCategoria? categoria, int page, int pageSize, bool? lojaOnline = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _parts.SearchAsync(search, categoria, marca: null, lowStockOnly: false, page, pageSize, ct);
        var query = items.Where(p => p.Activo);
        if (lojaOnline.HasValue)
            query = query.Where(p => p.MostrarLojaOnline == lojaOnline.Value);
        var dtos = query
            .Select(p => new ExternalPartDto(
                p.Id, p.Sku, p.Nome, p.Categoria, p.Marca, p.Modelo, p.MostrarLojaOnline, p.QtdStock, p.Activo))
            .ToList();
        return new Clientes.PagedResult<ExternalPartDto>(dtos, page, pageSize, total);
    }

    public async Task<ExternalCheckoutResponse> CheckoutAsync(ExternalCheckoutRequest req, CancellationToken ct = default)
    {
        if (req.Items is null || req.Items.Count == 0)
            throw new ValidationException("items_required", "Pelo menos uma linha é obrigatória.");
        if (string.IsNullOrWhiteSpace(req.Cliente.Nome))
            throw new ValidationException("cliente_nome_required", "Nome do cliente é obrigatório.");

        // 1. Cliente — lookup por NIF ou cria. Idempotente.
        var clienteResp = await _clientes.LookupOrCreateAsync(new CreateClienteRequest(
            req.Cliente.Nome,
            req.Cliente.Telefone,
            req.Cliente.Email,
            req.Cliente.Nif,
            req.Cliente.Notas), ct);

        // 2. Venda — criar com origem default Online (integração externa).
        var venda = await _vendas.CreateAsync(new CreateVendaRequest(
            clienteResp.Cliente.Id,
            req.Items,
            req.Notas,
            req.Origem ?? VendaOrigem.Online), ct);

        // 3. Marcar paga — decrementa stock, emite garantia, opcionalmente fatura.
        var paga = await _vendas.MarcarPagaAsync(venda.Id, new MarcarVendaPagaRequest(
            req.PaymentMethod,
            EmitirFatura: req.EmitirFatura), ct);

        // 4. Garantia (Sprint 58 auto-emit) — fetch slug para incluir na response.
        var garantia = await _garantias.FindByVendaAsync(venda.Id, ct);

        await _audit.LogAsync(
            AuditAction.Create,
            "ExternalCheckout",
            paga.Venda.Id,
            new
            {
                origem = paga.Venda.Origem.ToString(),
                clienteCreated = clienteResp.Created,
                total = paga.Venda.TotalCents,
                faturaEmitida = paga.Invoice is not null,
                garantiaSlug = garantia?.Slug,
            },
            _tenant.TenantId,
            ct: ct);

        return new ExternalCheckoutResponse(
            VendaId: paga.Venda.Id,
            VendaNumero: paga.Venda.Numero,
            ClienteId: clienteResp.Cliente.Id,
            ClienteCreated: clienteResp.Created,
            TotalCents: paga.Venda.TotalCents,
            IvaCents: paga.Venda.IvaCents,
            FaturaNumero: paga.Invoice?.Number,
            FaturaPdfUrl: paga.Invoice?.PdfUrl,
            GarantiaSlug: garantia?.Slug);
    }

    public async Task<ExternalOrderStatusResponse> GetOrderAsync(Guid vendaId, CancellationToken ct = default)
    {
        var venda = await _vendas.GetAsync(vendaId, ct);
        var garantia = await _garantias.FindByVendaAsync(vendaId, ct);
        return BuildOrderStatus(venda, garantia);
    }

    public async Task<ExternalOrderStatusResponse> CancelOrderAsync(Guid vendaId, string? motivo, CancellationToken ct = default)
    {
        var venda = await _vendas.GetAsync(vendaId, ct);

        // Idempotente: se já cancelada, devolve estado actual sem repetir trabalho.
        if (venda.Status == VendaStatus.Cancelada)
        {
            var garantiaExistente = await _garantias.FindByVendaAsync(vendaId, ct);
            return BuildOrderStatus(venda, garantiaExistente);
        }

        // CancelarAsync já cascateia: anula fatura Moloni/InvoiceXpress + revert stock (Sprint 54).
        var cancelada = await _vendas.CancelarAsync(vendaId, ct);

        // Sprint 74: também anula a garantia (não há produto a cobrir — venda cancelada).
        var garantia = await _garantias.FindByVendaAsync(vendaId, ct);
        if (garantia is not null && !garantia.Anulada)
        {
            garantia.Anulada = true;
            garantia.MotivoAnulacao = string.IsNullOrWhiteSpace(motivo)
                ? "Venda cancelada via integração externa."
                : $"Venda cancelada: {motivo.Trim()}";
            await _garantias.SaveAsync(ct);
        }

        await _audit.LogAsync(
            AuditAction.Update,
            "ExternalCheckout",
            vendaId,
            new
            {
                operation = "cancel",
                motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(),
                garantiaAnulada = garantia is not null,
            },
            _tenant.TenantId,
            ct: ct);

        return BuildOrderStatus(cancelada, garantia);
    }

    private static ExternalOrderStatusResponse BuildOrderStatus(VendaDto venda, Core.Entities.Garantia? garantia)
    {
        var agora = DateTime.UtcNow;
        var garantiaActiva = garantia is not null
            && !garantia.Anulada
            && agora >= garantia.DataInicio
            && agora <= garantia.DataFim;
        return new ExternalOrderStatusResponse(
            VendaId: venda.Id,
            VendaNumero: venda.Numero,
            Data: venda.Data,
            ClienteId: venda.Cliente?.Id,
            TotalCents: venda.TotalCents,
            IvaCents: venda.IvaCents,
            Status: venda.Status.ToString(),
            Origem: venda.Origem.ToString(),
            FaturaNumero: venda.InvoiceNumber,
            FaturaPdfUrl: venda.InvoicePdfUrl,
            FaturaEmitidaEm: venda.InvoiceEmittedAt,
            GarantiaSlug: garantia?.Slug,
            GarantiaActiva: garantiaActiva,
            GarantiaAnulada: garantia?.Anulada ?? false,
            CanceladaEm: venda.Status == VendaStatus.Cancelada ? venda.Data : null);
    }
}
