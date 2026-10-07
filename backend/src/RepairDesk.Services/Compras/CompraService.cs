using RepairDesk.Services.Clientes;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Fiscal;

namespace RepairDesk.Services.Compras;

public interface ICompraService
{
    Task<PagedResult<CompraDocumentoDto>> SearchAsync(CompraFiltro filtro, int page, int pageSize, CancellationToken ct = default);
    Task<CompraDocumentoDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CompraDocumentoDto> CreateAsync(CompraDocumentoWriteRequest req, CancellationToken ct = default);
    Task<CompraDocumentoDto> UpdateAsync(Guid id, CompraDocumentoWriteRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<InventarioLinhaDto>> InventarioAsync(CancellationToken ct = default);
    Task<ResumoComprasDto> ResumoAsync(CancellationToken ct = default);
    SimuladorResponse Simular(SimuladorRequest req);
}

/// <summary>
/// Compras a fornecedores = stock por lote (Doc 94 Fase 3, SPEC compras §2–§5).
/// O que se guarda é só o introduzido; tudo o resto vem do <see cref="IvaEngine"/>.
/// </summary>
public class CompraService : ICompraService
{
    private const int MaxLinhas = 500;

    private readonly ICompraRepository _repo;
    private readonly IFornecedorRepository _fornecedores;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;

    public CompraService(ICompraRepository repo, IFornecedorRepository fornecedores, ITenantContext tenant, IAuditLogger audit)
    {
        _repo = repo;
        _fornecedores = fornecedores;
        _tenant = tenant;
        _audit = audit;
    }

    private static decimal TaxaVenda => FiscalDefaults.TaxaIvaNormal;

    public async Task<PagedResult<CompraDocumentoDto>> SearchAsync(CompraFiltro filtro, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var (items, total) = await _repo.SearchAsync(filtro, page, pageSize, ct);
        return new PagedResult<CompraDocumentoDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<CompraDocumentoDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("CompraDocumento", id));

    public async Task<CompraDocumentoDto> CreateAsync(CompraDocumentoWriteRequest req, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var fornecedor = await RequireFornecedorAsync(req.FornecedorId, ct);
        var (numeroFatura, encomendas) = Validate(req);
        await EnsureNotDuplicateAsync(req, fornecedor.Id, numeroFatura, encomendas, null, ct);

        var doc = new CompraDocumento { TenantId = tenantId, FornecedorId = fornecedor.Id, Fornecedor = fornecedor };
        ApplyHeader(doc, req, numeroFatura, encomendas);
        foreach (var l in req.Linhas)
            doc.Linhas.Add(NewLinha(tenantId, l, fornecedor));

        await _repo.AddAsync(doc, ct);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Create, nameof(CompraDocumento), doc.Id,
            new { fornecedor = fornecedor.Name, doc.NumeroFatura, linhas = doc.Linhas.Count }, ct: ct);
        return ToDto(doc);
    }

    public async Task<CompraDocumentoDto> UpdateAsync(Guid id, CompraDocumentoWriteRequest req, CancellationToken ct = default)
    {
        var doc = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("CompraDocumento", id);
        var fornecedor = doc.FornecedorId == req.FornecedorId ? doc.Fornecedor! : await RequireFornecedorAsync(req.FornecedorId, ct);
        var (numeroFatura, encomendas) = Validate(req);
        await EnsureNotDuplicateAsync(req, fornecedor.Id, numeroFatura, encomendas, doc.Id, ct);

        doc.FornecedorId = fornecedor.Id;
        doc.Fornecedor = fornecedor;
        ApplyHeader(doc, req, numeroFatura, encomendas);

        // Linhas: com Id = atualiza; sem Id = nova; ausentes = removidas (só se ainda não venderam nada).
        var pedidas = req.Linhas.Where(l => l.Id is not null).Select(l => l.Id!.Value).ToHashSet();
        foreach (var existente in doc.Linhas.Where(l => !pedidas.Contains(l.Id)).ToList())
        {
            if (existente.QuantidadeVendida > 0 || existente.QuantidadeAbatida > 0)
                throw new ConflictException("linha_com_movimentos",
                    $"\"{existente.Descricao}\" já tem unidades vendidas ou abatidas — não pode ser removida.");
            doc.Linhas.Remove(existente);
            _repo.RemoveLinha(existente);
        }
        foreach (var l in req.Linhas)
        {
            if (l.Id is null)
            {
                var nova = NewLinha(doc.TenantId, l, fornecedor);
                nova.CompraDocumentoId = doc.Id;
                _repo.AddLinha(nova); // o fixup do EF junta-a a doc.Linhas
                continue;
            }
            var linha = doc.Linhas.FirstOrDefault(x => x.Id == l.Id)
                ?? throw new ValidationException("linha_inexistente", "Linha não pertence a este documento.");
            if (l.Quantidade < linha.QuantidadeVendida + linha.QuantidadeAbatida)
                throw new ConflictException("quantidade_abaixo_vendido",
                    $"\"{linha.Descricao}\": já saíram {linha.QuantidadeVendida + linha.QuantidadeAbatida} unidades.");
            linha.Descricao = l.Descricao.Trim();
            linha.Quantidade = l.Quantidade;
            linha.PrecoUnitarioPago = l.PrecoUnitarioPago;
            linha.TaxaIvaCompra = l.TaxaIvaCompra ?? fornecedor.TaxaIvaCompraPorDefeito(TaxaVenda);
            linha.LucroUnitario = l.LucroUnitario ?? FiscalDefaults.LucroPorDefeito(l.Descricao);
            linha.Localizacao = Clean(l.Localizacao, 100);
        }

        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(CompraDocumento), doc.Id,
            new { fornecedor = fornecedor.Name, doc.NumeroFatura, linhas = doc.Linhas.Count }, ct: ct);
        return ToDto(doc);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("CompraDocumento", id);
        if (doc.Linhas.Any(l => l.QuantidadeVendida > 0 || l.QuantidadeAbatida > 0))
            throw new ConflictException("documento_com_movimentos", "Há unidades deste documento já vendidas — não pode ser apagado.");
        _repo.Remove(doc);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, nameof(CompraDocumento), id, new { doc.NumeroFatura }, ct: ct);
    }

    public async Task<IReadOnlyList<InventarioLinhaDto>> InventarioAsync(CancellationToken ct = default)
    {
        var linhas = await _repo.ListLinhasAsync(soComStock: true, ct);
        return linhas.Select(l =>
        {
            var u = Calc(l);
            var q = l.QuantidadeEmStock;
            var doc = l.Documento!;
            return new InventarioLinhaDto(
                l.Id, doc.Id, doc.Fornecedor?.Name ?? "—", doc.Data, Referencia(doc), doc.FaturaEmFalta,
                l.Descricao, l.Localizacao, q, l.PrecoUnitarioPago,
                q * l.PrecoUnitarioPago, q * u.CustoSemIva, u.PrecoFinalComIva, q * u.LucroQueSobra, q * u.IvaAPagarEstado);
        }).ToList();
    }

    public async Task<ResumoComprasDto> ResumoAsync(CancellationToken ct = default)
    {
        var linhas = await _repo.ListLinhasAsync(soComStock: false, ct);
        var documentos = await _repo.ListDocumentosAsync(ct);

        var entradas = linhas.Select(ToResumoLinha).ToList();
        var posicao = ResumoIva.Posicao(entradas,
            documentos.Select(d => new ResumoIva.Documento(d.PortesPagos, d.PortesIva)),
            ivaLiquidadoVendas: 0m, ivaDespesasDedutiveis: 0m, TaxaVenda);

        var linhasPorDoc = linhas.GroupBy(l => l.CompraDocumentoId).ToDictionary(g => g.Key, g => g.ToList());
        var comDiferenca = documentos.Count(d =>
            IvaEngine.Reconciliar(linhasPorDoc.GetValueOrDefault(d.Id)?.Sum(l => l.Quantidade * l.PrecoUnitarioPago) ?? 0m,
                d.PortesPagos, d.TotalDocumento).TemDiferenca);

        var porFornecedor = documentos
            .GroupBy(d => d.FornecedorId)
            .Select(g =>
            {
                var ls = g.SelectMany(d => linhasPorDoc.GetValueOrDefault(d.Id) ?? []).ToList();
                var totalPecas = ls.Sum(l => l.Quantidade * l.PrecoUnitarioPago);
                var ivaPecas = ls.Sum(l => l.Quantidade * Calc(l).IvaPagoNaCompra);
                var portes = g.Sum(d => d.PortesPagos);
                var f = g.First().Fornecedor;
                return new ResumoFornecedorDto(g.Key, f?.Name ?? "—", f?.RegimeIva ?? RegimeIvaFornecedor.Nacional,
                    g.Count(), totalPecas, ivaPecas, portes, totalPecas + portes, g.Count(d => d.FaturaEmFalta));
            })
            .OrderByDescending(x => x.TotalGasto)
            .ToList();

        return new ResumoComprasDto(
            TaxaVenda,
            ToColuna(ResumoIva.JaVendido(entradas, TaxaVenda)),
            ToColuna(ResumoIva.EmStock(entradas, TaxaVenda)),
            posicao.AutoliquidacaoUe,
            posicao.IvaComprasNacionais,
            posicao.IvaPortesNacionais,
            documentos.Sum(d => d.PortesPagos),
            documentos.Count(d => d.FaturaEmFalta),
            comDiferenca,
            porFornecedor);
    }

    public SimuladorResponse Simular(SimuladorRequest req)
    {
        if (req.PrecoPago < 0 || req.Lucro < 0)
            throw new ValidationException("valores_invalidos", "Preço e lucro não podem ser negativos.");
        var taxaCompra = req.Regime == RegimeIvaFornecedor.Nacional ? TaxaVenda : 0m;
        var u = IvaEngine.Unidade(req.PrecoPago, taxaCompra, req.Lucro, TaxaVenda, req.Regime == RegimeIvaFornecedor.UeAutoliquidacao);
        CalculoVenda? venda = req.PrecoVendaComIva is { } p and >= 0
            ? IvaEngine.VendaAPreco(1, p, req.PrecoPago, taxaCompra, TaxaVenda)
            : null;
        return new SimuladorResponse(u.CustoSemIva, u.IvaPagoNaCompra, u.AutoliquidacaoUe, u.PrecoVendaSemIva,
            u.IvaDaVenda, u.PrecoFinalComIva, u.IvaAPagarEstado, u.LucroQueSobra, venda?.IvaAPagarEstado, venda?.Lucro);
    }

    // ---------- helpers ----------

    private async Task<Fornecedor> RequireFornecedorAsync(Guid id, CancellationToken ct)
        => await _fornecedores.FindByIdAsync(id, ct)
           ?? throw new ValidationException("fornecedor_inexistente", "Escolhe um fornecedor válido.");

    private static (string? NumeroFatura, IReadOnlyList<string> Encomendas) Validate(CompraDocumentoWriteRequest req)
    {
        if (req.Data == default)
            throw new ValidationException("data_obrigatoria", "Data do documento obrigatória.");
        if (req.Linhas is null || req.Linhas.Count == 0)
            throw new ValidationException("sem_linhas", "Adiciona pelo menos uma linha.");
        if (req.Linhas.Count > MaxLinhas)
            throw new ValidationException("demasiadas_linhas", $"Máximo {MaxLinhas} linhas por documento.");
        if (req.PortesPagos < 0 || req.PortesIva < 0 || req.TotalDocumento < 0)
            throw new ValidationException("valores_invalidos", "Portes e total não podem ser negativos.");
        if (req.PortesIva > req.PortesPagos)
            throw new ValidationException("iva_portes_invalido", "O IVA dos portes não pode ser maior que os portes.");

        foreach (var l in req.Linhas)
        {
            if (string.IsNullOrWhiteSpace(l.Descricao))
                throw new ValidationException("linha_sem_descricao", "Todas as linhas precisam de descrição.");
            if (l.Quantidade is < 1 or > 100_000)
                throw new ValidationException("quantidade_invalida", $"\"{l.Descricao}\": quantidade inválida.");
            if (l.PrecoUnitarioPago < 0)
                throw new ValidationException("preco_invalido", $"\"{l.Descricao}\": preço não pode ser negativo.");
            if (l.TaxaIvaCompra is < 0 or > 1)
                throw new ValidationException("taxa_invalida", $"\"{l.Descricao}\": taxa de IVA entre 0 e 1 (ex.: 0,23).");
            if (l.LucroUnitario < 0)
                throw new ValidationException("lucro_invalido", $"\"{l.Descricao}\": lucro não pode ser negativo.");
        }

        var numero = Clean(req.NumeroFatura, 100);
        var encomendas = NormalizarEncomendas(req.NumerosEncomenda);
        return (numero, encomendas);
    }

    private async Task EnsureNotDuplicateAsync(CompraDocumentoWriteRequest req, Guid fornecedorId, string? numeroFatura,
        IReadOnlyList<string> encomendas, Guid? excluirId, CancellationToken ct)
    {
        if (req.IgnorarDuplicado || (numeroFatura is null && encomendas.Count == 0)) return;
        var dups = await _repo.FindPossiveisDuplicadosAsync(fornecedorId, numeroFatura, encomendas, excluirId, ct);
        if (dups.Count == 0) return;
        var d = dups[0];
        var data = d.Data.ToString("dd/MM/yyyy");
        throw new ConflictException("compra_duplicada",
            d.NumeroFatura is not null && string.Equals(d.NumeroFatura, numeroFatura, StringComparison.OrdinalIgnoreCase)
                ? $"Já existe a fatura {d.NumeroFatura} deste fornecedor (registada a {data})."
                : $"Já existe um documento deste fornecedor com a mesma encomenda ({d.NumerosEncomenda}, {data}).");
    }

    /// <summary>"#165048, 165227" → ["165048","165227"] (sem '#', sem prefixo "Enc.", sem repetidos).</summary>
    public static IReadOnlyList<string> NormalizarEncomendas(IEnumerable<string>? encomendas)
        => (encomendas ?? [])
            .SelectMany(e => (e ?? string.Empty).Split([',', ';', '+', ' '], StringSplitOptions.RemoveEmptyEntries))
            .Select(e => e.Trim().TrimStart('#'))
            .Where(e => e.Length > 0 && !e.Equals("Enc.", StringComparison.OrdinalIgnoreCase) && !e.Equals("Enc", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Length > 60 ? e[..60] : e)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void ApplyHeader(CompraDocumento doc, CompraDocumentoWriteRequest req, string? numeroFatura, IReadOnlyList<string> encomendas)
    {
        doc.Data = DateTime.SpecifyKind(req.Data.Date, DateTimeKind.Utc);
        doc.NumeroFatura = numeroFatura;
        doc.NumerosEncomenda = encomendas.Count == 0 ? null : string.Join(',', encomendas);
        doc.MetodoPagamento = Clean(req.MetodoPagamento, 50);
        doc.PortesPagos = req.PortesPagos;
        doc.PortesIva = req.PortesIva;
        doc.TotalDocumento = req.TotalDocumento;
        doc.Notas = Clean(req.Notas, 2000);
    }

    private static CompraLinha NewLinha(Guid tenantId, CompraLinhaWriteRequest l, Fornecedor fornecedor) => new()
    {
        TenantId = tenantId,
        Descricao = l.Descricao.Trim().Length > 500 ? l.Descricao.Trim()[..500] : l.Descricao.Trim(),
        Quantidade = l.Quantidade,
        PrecoUnitarioPago = l.PrecoUnitarioPago,
        TaxaIvaCompra = l.TaxaIvaCompra ?? fornecedor.TaxaIvaCompraPorDefeito(TaxaVenda),
        LucroUnitario = l.LucroUnitario ?? FiscalDefaults.LucroPorDefeito(l.Descricao),
        Localizacao = Clean(l.Localizacao, 100),
    };

    private static CalculoUnidade Calc(CompraLinha l)
        => IvaEngine.Unidade(l.PrecoUnitarioPago, l.TaxaIvaCompra, l.LucroUnitario, TaxaVenda,
            l.Documento?.Fornecedor?.RegimeIva == RegimeIvaFornecedor.UeAutoliquidacao);

    private static ResumoIva.Linha ToResumoLinha(CompraLinha l)
        => new(l.Quantidade, l.QuantidadeVendida, l.QuantidadeAbatida, l.PrecoUnitarioPago, l.TaxaIvaCompra, l.LucroUnitario,
            l.Documento?.Fornecedor?.RegimeIva == RegimeIvaFornecedor.UeAutoliquidacao);

    private static ResumoColunaDto ToColuna(ResumoIva.Coluna c)
        => new(c.Unidades, c.ValorCobradoComIva, c.ValorPecasPago, c.IvaDaVenda, c.IvaJaPagoFornecedores,
            c.IvaAPagarEstado, c.IvaAPagarNacionais, c.IvaAPagarUe, c.Lucro);

    private static string Referencia(CompraDocumento d)
        => d.NumeroFatura ?? (d.NumerosEncomenda is { } e ? $"Enc. {e.Replace(",", ", ")}" : "—");

    private static string? Clean(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var t = v.Trim();
        return t.Length > max ? t[..max] : t;
    }

    private static CompraDocumentoDto ToDto(CompraDocumento d)
    {
        var fornecedor = d.Fornecedor;
        var ue = fornecedor?.RegimeIva == RegimeIvaFornecedor.UeAutoliquidacao;
        var linhas = d.Linhas.OrderBy(l => l.CreatedAt).Select(l =>
        {
            var u = IvaEngine.Unidade(l.PrecoUnitarioPago, l.TaxaIvaCompra, l.LucroUnitario, TaxaVenda, ue);
            return new CompraLinhaDto(l.Id, l.Descricao, l.Quantidade, l.QuantidadeVendida, l.QuantidadeAbatida,
                l.QuantidadeEmStock, l.PrecoUnitarioPago, l.TaxaIvaCompra, l.LucroUnitario, l.Localizacao,
                u.CustoSemIva, u.IvaPagoNaCompra, u.AutoliquidacaoUe, u.LucroComIva, u.PrecoVendaSemIva,
                u.IvaDaVenda, u.PrecoFinalComIva, u.IvaAPagarEstado,
                l.Quantidade * l.PrecoUnitarioPago, l.Quantidade * u.CustoSemIva);
        }).ToList();

        var totalLinhas = linhas.Sum(l => l.TotalPago);
        var rec = IvaEngine.Reconciliar(totalLinhas, d.PortesPagos, d.TotalDocumento);
        return new CompraDocumentoDto(
            d.Id, d.FornecedorId, fornecedor?.Name ?? "—", fornecedor?.RegimeIva ?? RegimeIvaFornecedor.Nacional,
            d.Data, d.NumeroFatura,
            (d.NumerosEncomenda ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries),
            d.MetodoPagamento, d.PortesPagos, d.PortesIva, d.TotalDocumento, d.Notas, d.FaturaEmFalta,
            d.SupplierInvoiceImportId,
            linhas.Sum(l => l.Quantidade), linhas.Sum(l => l.QuantidadeEmStock),
            totalLinhas, rec.TotalCalculado, rec.Diferenca, rec.TemDiferenca, linhas);
    }
}
