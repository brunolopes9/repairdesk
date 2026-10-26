using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Fiscal;
using RepairDesk.Services.Vendas;

namespace RepairDesk.Services.Artigos;

public sealed record ArtigoDto(
    Guid Id,
    int Numero,
    string Sku,
    string CodigoBarras,
    string? CodigoFabricante,
    string Nome,
    string? Categoria,
    /// <summary>Preço definido no artigo (c/ IVA), ou null.</summary>
    int? PrecoVendaCents,
    /// <summary>O preço que o balcão usa: o definido, ou o calculado do lote mais antigo com stock.</summary>
    int? PrecoEfetivoCents,
    int Stock,
    int Lotes,
    string? Localizacao,
    bool Ativo,
    string? Notas);

public sealed record ArtigoLoteDto(
    Guid LinhaId,
    int Numero,
    string Descricao,
    string Fornecedor,
    DateTime Data,
    int Quantidade,
    int QuantidadeEmStock,
    decimal CustoUnitarioPago,
    decimal PrecoCalculadoComIva);

public sealed record ArtigoDetalheDto(ArtigoDto Artigo, IReadOnlyList<ArtigoLoteDto> Lotes);

public sealed record ArtigoWriteRequest(
    string Nome,
    string? Categoria,
    int? PrecoVendaCents,
    /// <summary>Vazio = código interno da loja (EAN-13 "20…"). Se preenchido, tem de ser um EAN-13 válido.</summary>
    string? CodigoBarras,
    string? CodigoFabricante,
    string? Localizacao,
    string? Notas,
    bool Ativo = true,
    /// <summary>Lotes de compra a ligar a este artigo (os já ligados mantêm-se).</summary>
    IReadOnlyList<Guid>? LoteIds = null);

public sealed record BalcaoLinhaRequest(Guid ArtigoId, int Quantidade, int? PrecoUnitarioCents = null);

public sealed record BalcaoVendaRequest(IReadOnlyList<BalcaoLinhaRequest> Linhas, PaymentMethod PaymentMethod, Guid? ClienteId = null);

public interface IArtigoService
{
    Task<IReadOnlyList<ArtigoDto>> ListAsync(string? q, bool incluirInativos, CancellationToken ct = default);
    Task<ArtigoDetalheDto> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>O que a pistola leu (código de barras, do fabricante ou SKU) → artigo. 404 se não existir.</summary>
    Task<ArtigoDto> PorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<ArtigoDetalheDto> CreateAsync(ArtigoWriteRequest req, CancellationToken ct = default);
    Task<ArtigoDetalheDto> UpdateAsync(Guid id, ArtigoWriteRequest req, CancellationToken ct = default);
    Task<ArtigoDetalheDto> DesligarLoteAsync(Guid id, Guid loteId, CancellationToken ct = default);
    /// <summary>Venda ao balcão: tira cada artigo dos lotes mais antigos e cria a venda entregue.</summary>
    Task<VendaDto> VenderAsync(BalcaoVendaRequest req, CancellationToken ct = default);
}

/// <summary>
/// Sprint 566: catálogo de artigos da loja. Um artigo junta os lotes de compra do mesmo produto (stock =
/// soma), tem SKU e código de barras próprios para a etiqueta, e preço de venda — definido, ou calculado
/// pelo motor de IVA a partir do lote mais antigo com stock (custo + lucro + IVA), como no resto do Mender.
/// </summary>
public class ArtigoService : IArtigoService
{
    private const int MaxLinhasBalcao = 100;

    private readonly IArtigoRepository _repo;
    private readonly ICompraRepository _compras;
    private readonly IVendaService _vendas;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;
    private readonly Moloni.IMoloniService? _moloni;
    private readonly Moloni.IMoloniGestaoService? _moloniGestao;

    public ArtigoService(IArtigoRepository repo, ICompraRepository compras, IVendaService vendas, ITenantContext tenant, IAuditLogger audit,
        Moloni.IMoloniService? moloni = null, Moloni.IMoloniGestaoService? moloniGestao = null)
    {
        _repo = repo;
        _compras = compras;
        _vendas = vendas;
        _tenant = tenant;
        _audit = audit;
        _moloni = moloni;
        _moloniGestao = moloniGestao;
    }

    public async Task<IReadOnlyList<ArtigoDto>> ListAsync(string? q, bool incluirInativos, CancellationToken ct = default)
        => (await _repo.ListAsync(q, incluirInativos, ct)).Select(ToDto).ToList();

    public async Task<ArtigoDetalheDto> GetAsync(Guid id, CancellationToken ct = default)
        => Detalhe(await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Artigo", id));

    public async Task<ArtigoDto> PorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var c = Codigo(codigo) ?? throw new ValidationException("codigo_vazio", "Código vazio.");
        return ToDto(await _repo.FindByCodigoAsync(c, ct) ?? throw new NotFoundException("Artigo", c));
    }

    public async Task<ArtigoDetalheDto> CreateAsync(ArtigoWriteRequest req, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        var artigo = new Artigo { TenantId = tenantId, Nome = string.Empty, Sku = string.Empty, CodigoBarras = string.Empty };
        await ApplyAsync(artigo, req, ct);
        await _repo.AddAsync(artigo, ct);
        await LigarLotesAsync(artigo, req.LoteIds, ct);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Create, nameof(Artigo), artigo.Id, new { artigo.Sku, artigo.Nome, artigo.CodigoBarras }, ct: ct);
        return await GetAsync(artigo.Id, ct);
    }

    public async Task<ArtigoDetalheDto> UpdateAsync(Guid id, ArtigoWriteRequest req, CancellationToken ct = default)
    {
        var artigo = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Artigo", id);
        await ApplyAsync(artigo, req, ct);
        await LigarLotesAsync(artigo, req.LoteIds, ct);
        artigo.UpdatedAt = DateTime.UtcNow;
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Artigo), artigo.Id, new { artigo.Sku, artigo.Nome, artigo.PrecoVendaCents }, ct: ct);
        // Sprint 568: nome/preço passam para o artigo ligado no Moloni ON (falha não bloqueia a gravação).
        if (_moloniGestao is not null)
        {
            try { await _moloniGestao.SincronizarArtigoAsync(artigo.Id, ct); }
            catch (DomainException) { }
            catch (HttpRequestException) { }
        }
        return await GetAsync(artigo.Id, ct);
    }

    public async Task<ArtigoDetalheDto> DesligarLoteAsync(Guid id, Guid loteId, CancellationToken ct = default)
    {
        var artigo = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Artigo", id);
        var lote = artigo.Lotes.FirstOrDefault(l => l.Id == loteId) ?? throw new NotFoundException("Lote", loteId);
        lote.ArtigoId = null;
        artigo.Lotes.Remove(lote);
        await _repo.SaveAsync(ct);
        return Detalhe(artigo);
    }

    public async Task<VendaDto> VenderAsync(BalcaoVendaRequest req, CancellationToken ct = default)
    {
        if (req.Linhas is null || req.Linhas.Count == 0)
            throw new ValidationException("venda_sem_linhas", "Passa pelo menos um artigo.");
        if (req.Linhas.Count > MaxLinhasBalcao)
            throw new ValidationException("linhas_invalidas", $"Máximo {MaxLinhasBalcao} linhas.");

        var linhas = new List<VendaLinhaWriteRequest>();
        foreach (var grupo in req.Linhas.GroupBy(l => (l.ArtigoId, l.PrecoUnitarioCents)))
        {
            var qtd = grupo.Sum(l => l.Quantidade);
            if (qtd <= 0) throw new ValidationException("quantidade_invalida", "Quantidade tem de ser positiva.");
            var artigo = await _repo.FindByIdAsync(grupo.Key.ArtigoId, ct) ?? throw new NotFoundException("Artigo", grupo.Key.ArtigoId);
            var preco = grupo.Key.PrecoUnitarioCents ?? PrecoEfetivo(artigo)
                ?? throw new ValidationException("sem_preco", $"\"{artigo.Nome}\" não tem preço de venda — define-o no artigo.");
            if (preco <= 0) throw new ValidationException("preco_invalido", "Preço inválido.");

            // FIFO: do lote mais antigo para o mais recente (as unidades já reservadas nesta venda contam).
            var falta = qtd;
            foreach (var lote in LotesFifo(artigo))
            {
                var usadas = linhas.Where(l => l.CompraLinhaId == lote.Id).Sum(l => l.Quantidade);
                var disponivel = lote.QuantidadeEmStock - usadas;
                if (disponivel <= 0) continue;
                var tira = Math.Min(disponivel, falta);
                linhas.Add(new VendaLinhaWriteRequest(null, lote.Id, artigo.Nome, tira, preco));
                falta -= tira;
                if (falta == 0) break;
            }
            if (falta > 0)
                throw new ValidationException("stock_insuficiente", $"\"{artigo.Nome}\": só há {Stock(artigo)} em stock (pedido {qtd}).");
        }

        var venda = await _vendas.CreateAsync(new VendaWriteRequest(
            VendaTipo.Produto, req.ClienteId, null, null, "Venda ao balcão.", linhas, VendaEstado.Entregue, req.PaymentMethod), ct);

        // Sprint 567: no balcão sai sempre fatura-recibo (pago na hora). Se o Moloni falhar, a venda fica
        // registada em "Fatura por registar" e emite-se depois na própria venda — o stock já saiu.
        if (_moloni is not null && await _moloni.ConfiguradoAsync(ct))
        {
            try { return await _moloni.EmitirAsync(venda.Id, new Moloni.MoloniEmitirRequest(Moloni.MoloniDocumento.FaturaRecibo, PaymentMethod: req.PaymentMethod), ct); }
            catch (DomainException) { return await _vendas.GetAsync(venda.Id, ct); }
            catch (HttpRequestException) { return await _vendas.GetAsync(venda.Id, ct); }
        }
        return venda;
    }

    // ---------- regras ----------

    private async Task ApplyAsync(Artigo artigo, ArtigoWriteRequest req, CancellationToken ct)
    {
        artigo.Nome = Clean(req.Nome, 200) ?? throw new ValidationException("nome_obrigatorio", "O artigo precisa de nome.");
        artigo.Categoria = Clean(req.Categoria, 80);
        artigo.Localizacao = Clean(req.Localizacao, 100);
        artigo.Notas = Clean(req.Notas, 1000);
        artigo.Ativo = req.Ativo;
        if (req.PrecoVendaCents is < 0 or > 100_000_000)
            throw new ValidationException("preco_invalido", "Preço de venda inválido.");
        artigo.PrecoVendaCents = req.PrecoVendaCents is > 0 ? req.PrecoVendaCents : null;

        var barras = Codigo(req.CodigoBarras);
        if (barras is not null)
        {
            if (!Ean13.Valido(barras))
                throw new ValidationException("codigo_barras_invalido", "O código de barras tem de ser um EAN-13 válido (13 dígitos). Deixa vazio para gerar o da loja.");
            if (await _repo.CodigoEmUsoAsync(barras, artigo.Id, ct))
                throw new ConflictException("codigo_em_uso", $"O código {barras} já está noutro artigo.");
            artigo.CodigoBarras = barras;
        }
        else if (artigo.Numero > 0 && !string.IsNullOrEmpty(artigo.CodigoBarras) && artigo.CodigoBarras != Ean13.Interno(artigo.Numero))
        {
            // Limpar um EAN do fabricante volta ao código interno.
            artigo.CodigoBarras = Ean13.Interno(artigo.Numero);
        }

        var fabricante = Codigo(req.CodigoFabricante);
        if (fabricante is not null)
        {
            if (fabricante.Length is < 4 or > 32 || !fabricante.All(char.IsAsciiLetterOrDigit))
                throw new ValidationException("codigo_fabricante_invalido", "Código do fabricante: 4 a 32 letras/dígitos.");
            if (await _repo.CodigoEmUsoAsync(fabricante, artigo.Id, ct))
                throw new ConflictException("codigo_em_uso", $"O código {fabricante} já está noutro artigo.");
        }
        artigo.CodigoFabricante = fabricante;
    }

    private async Task LigarLotesAsync(Artigo artigo, IReadOnlyList<Guid>? loteIds, CancellationToken ct)
    {
        if (loteIds is null || loteIds.Count == 0) return;
        var lotes = await _compras.FindLinhasAsync(loteIds.Distinct().ToList(), ct);
        if (lotes.Count != loteIds.Distinct().Count())
            throw new ValidationException("lote_inexistente", "Algum dos lotes não existe.");
        foreach (var lote in lotes)
        {
            lote.ArtigoId = artigo.Id;
            if (!artigo.Lotes.Contains(lote)) artigo.Lotes.Add(lote);
        }
    }

    private static IEnumerable<CompraLinha> LotesFifo(Artigo a)
        => a.Lotes.Where(l => !l.IsDeleted && l.QuantidadeEmStock > 0)
            .OrderBy(l => l.Documento?.Data ?? l.CreatedAt).ThenBy(l => l.Numero);

    private static int Stock(Artigo a) => a.Lotes.Where(l => !l.IsDeleted).Sum(l => Math.Max(0, l.QuantidadeEmStock));

    private static decimal PrecoCalculado(CompraLinha l)
        => IvaEngine.Unidade(l.PrecoUnitarioPago, l.TaxaIvaCompra, l.LucroUnitario, FiscalDefaults.TaxaIvaNormal,
            l.Documento?.Fornecedor?.RegimeIva == RegimeIvaFornecedor.UeAutoliquidacao).PrecoFinalComIva;

    /// <summary>O definido; senão o calculado do lote mais antigo com stock; senão o do lote mais recente.</summary>
    private static int? PrecoEfetivo(Artigo a)
    {
        if (a.PrecoVendaCents is { } p) return p;
        var lote = LotesFifo(a).FirstOrDefault()
            ?? a.Lotes.Where(l => !l.IsDeleted).OrderByDescending(l => l.Documento?.Data ?? l.CreatedAt).FirstOrDefault();
        return lote is null ? null : (int)Math.Round(PrecoCalculado(lote) * 100m, MidpointRounding.AwayFromZero);
    }

    private static ArtigoDto ToDto(Artigo a) => new(
        a.Id, a.Numero, a.Sku, a.CodigoBarras, a.CodigoFabricante, a.Nome, a.Categoria, a.PrecoVendaCents, PrecoEfetivo(a),
        Stock(a), a.Lotes.Count(l => !l.IsDeleted), a.Localizacao, a.Ativo, a.Notas);

    private static ArtigoDetalheDto Detalhe(Artigo a) => new(ToDto(a), a.Lotes
        .Where(l => !l.IsDeleted)
        .OrderBy(l => l.Documento?.Data ?? l.CreatedAt).ThenBy(l => l.Numero)
        .Select(l => new ArtigoLoteDto(l.Id, l.Numero, l.Descricao, l.Documento?.Fornecedor?.Name ?? "—",
            l.Documento?.Data ?? l.CreatedAt, l.Quantidade, l.QuantidadeEmStock, l.PrecoUnitarioPago, IvaEngine.Euros(PrecoCalculado(l))))
        .ToList());

    private static string? Codigo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();

    private static string? Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length > max ? t[..max] : t;
    }
}
