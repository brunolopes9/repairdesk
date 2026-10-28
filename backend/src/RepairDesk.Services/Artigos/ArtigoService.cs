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
    /// <summary>Sprint 570: de onde vem o preço — "artigo" (próprio), "categoria" ou "calculado" (do lote).</summary>
    string? OrigemPreco,
    int Stock,
    int Lotes,
    string? Localizacao,
    bool Ativo,
    string? Notas,
    /// <summary>Sprint 581: artigo agrupado (vários modelos, um código) — o balcão pergunta o modelo.</summary>
    bool Agrupado = false);

public sealed record ArtigoLoteDto(
    Guid LinhaId,
    int Numero,
    string Descricao,
    string Fornecedor,
    DateTime Data,
    int Quantidade,
    int QuantidadeEmStock,
    decimal CustoUnitarioPago,
    decimal PrecoCalculadoComIva,
    string? SkuFornecedor = null);

/// <summary>Sprint 581: código que leva ao artigo — SKU de um fornecedor, ou código de barras alternativo (Fornecedor null).</summary>
public sealed record ArtigoCodigoDto(Guid Id, string Codigo, Guid? FornecedorId, string? Fornecedor);

/// <summary>Sprint 581: artigo que foi junto neste (pode desfazer-se).</summary>
public sealed record ArtigoFusaoDto(Guid Id, string OrigemSku, string OrigemNome, int Lotes, DateTime Em);

public sealed record ArtigoDetalheDto(ArtigoDto Artigo, IReadOnlyList<ArtigoLoteDto> Lotes, IReadOnlyList<ArtigoCodigoDto>? Codigos = null,
    IReadOnlyList<ArtigoFusaoDto>? Fusoes = null,
    /// <summary>Sprint 581: avisos da gravação (ex.: um SKU de fornecedor passou de outro artigo para este).</summary>
    IReadOnlyList<string>? Avisos = null);

/// <summary>Sprint 581: juntar artigos repetidos neste (lotes, SKUs de fornecedor; as etiquetas antigas continuam a ler).</summary>
public sealed record FundirArtigosRequest(IReadOnlyList<Guid> OrigemIds);

/// <summary>Sprint 581: marcar/desmarcar lotes como peça de reparação (não ficam à espera de artigo).</summary>
public sealed record PecaReparacaoRequest(IReadOnlyList<Guid> LoteIds, bool PecaReparacao = true);

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
    IReadOnlyList<Guid>? LoteIds = null,
    /// <summary>Sprint 581: null = mantém.</summary>
    bool? Agrupado = null);

/// <summary>Sprint 581: <c>LoteId</c> = o modelo escolhido num artigo agrupado (sai só desse lote).</summary>
public sealed record BalcaoLinhaRequest(Guid ArtigoId, int Quantidade, int? PrecoUnitarioCents = null, Guid? LoteId = null);

/// <summary>Sprint 571: <c>ComNif</c> vazio = fatura-recibo sem NIF (consumidor final), mesmo com cliente escolhido.</summary>
public sealed record BalcaoVendaRequest(IReadOnlyList<BalcaoLinhaRequest> Linhas, PaymentMethod PaymentMethod, Guid? ClienteId = null,
    Moloni.MoloniDadosFiscais? ComNif = null);

/// <summary>Sprint 569: lotes em stock sem artigo, agrupados pela mesma descrição — um artigo por grupo.</summary>
public sealed record ArtigoSugestaoDto(string Nome, IReadOnlyList<Guid> LoteIds, IReadOnlyList<int> Lotes, int Stock, int? PrecoCalculadoCents, string? Localizacao,
    /// <summary>Sprint 570: categoria sugerida (ex.: películas → "Películas").</summary>
    string? Categoria = null,
    /// <summary>Sprint 581: artigo de um lote anterior com a mesma descrição — sugestão para confirmar, nunca automática.</summary>
    Guid? ArtigoSugeridoId = null,
    string? ArtigoSugeridoNome = null);

public sealed record CategoriaArtigoDto(Guid Id, string Nome, int? PrecoVendaCents, int Artigos);

public sealed record CategoriaArtigoRequest(string Nome, int? PrecoVendaCents);

public sealed record ArtigoEmLoteItem(string Nome, IReadOnlyList<Guid> LoteIds, int? PrecoVendaCents = null, string? Categoria = null, bool Agrupado = false);

public sealed record ArtigosEmLoteRequest(IReadOnlyList<ArtigoEmLoteItem> Itens, string? Categoria = null);

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
    /// <summary>Sprint 569: lotes em stock ainda sem artigo, agrupados pela descrição (sugestões de artigos).</summary>
    Task<IReadOnlyList<ArtigoSugestaoDto>> SugestoesAsync(CancellationToken ct = default);
    /// <summary>Sprint 569: cria vários artigos de uma vez, cada um já com os seus lotes.</summary>
    Task<IReadOnlyList<ArtigoDto>> CriarEmLoteAsync(ArtigosEmLoteRequest req, CancellationToken ct = default);
    /// <summary>Sprint 570: categorias com preço por defeito (ex.: Películas 15 €).</summary>
    Task<IReadOnlyList<CategoriaArtigoDto>> CategoriasAsync(CancellationToken ct = default);
    Task<CategoriaArtigoDto> GuardarCategoriaAsync(CategoriaArtigoRequest req, CancellationToken ct = default);
    /// <summary>Sprint 581: junta artigos repetidos no destino (lotes e códigos passam; os códigos antigos continuam a ler).</summary>
    Task<ArtigoDetalheDto> FundirAsync(Guid destinoId, FundirArtigosRequest req, CancellationToken ct = default);
    /// <summary>Sprint 581: lotes que são peças de reparação (não aparecem como "por ligar").</summary>
    Task<int> MarcarPecaReparacaoAsync(PecaReparacaoRequest req, CancellationToken ct = default);
    /// <summary>Sprint 581: desfaz uma junção — a origem volta com os seus lotes e códigos.</summary>
    Task<ArtigoDetalheDto> DesfazerFusaoAsync(Guid fusaoId, CancellationToken ct = default);
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
    {
        await PrecosCategoriaAsync(ct);
        return (await _repo.ListAsync(q, incluirInativos, ct)).Select(ToDto).ToList();
    }

    public async Task<ArtigoDetalheDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await PrecosCategoriaAsync(ct);
        var d = Detalhe(await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Artigo", id));
        var fusoes = await _repo.ListFusoesAsync(id, ct);
        return d with
        {
            Fusoes = fusoes.Select(f => new ArtigoFusaoDto(f.Id, f.OrigemSku, f.OrigemNome, Ids(f.LoteIdsJson).Count, f.CreatedAt)).ToList(),
            Avisos = _avisos.Count > 0 ? _avisos.ToList() : null,
        };
    }

    /// <summary>Avisos desta operação (ex.: SKU que mudou de artigo) — vão no detalhe devolvido.</summary>
    private readonly List<string> _avisos = new();

    private static List<Guid> Ids(string json)
        => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json) ?? new();

    public async Task<ArtigoDto> PorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var c = Codigo(codigo) ?? throw new ValidationException("codigo_vazio", "Código vazio.");
        await PrecosCategoriaAsync(ct);
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

    public async Task<IReadOnlyList<ArtigoSugestaoDto>> SugestoesAsync(CancellationToken ct = default)
    {
        var todas = await _compras.ListLinhasAsync(soComStock: false, ct);
        var lotes = todas.Where(l => l.ArtigoId is null && !l.PecaReparacao && l.QuantidadeEmStock > 0).ToList();
        // Sprint 581: lote já ligado com a mesma descrição → sugere esse artigo (confirma-se num clique).
        var ligados = todas.Where(l => l.Artigo is { Ativo: true })
            .GroupBy(l => ChaveDescricao(l.Descricao)).ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.CreatedAt).First().Artigo!);
        return lotes
            .GroupBy(l => ChaveDescricao(l.Descricao))
            .Select(g =>
            {
                var ordenados = g.OrderBy(l => l.Documento?.Data ?? l.CreatedAt).ThenBy(l => l.Numero).ToList();
                var nome = g.GroupBy(l => l.Descricao.Trim()).OrderByDescending(x => x.Count()).ThenBy(x => x.Key.Length).First().Key;
                var maisAntigo = ordenados.First();
                return new ArtigoSugestaoDto(nome.Length > 200 ? nome[..200] : nome, ordenados.Select(l => l.Id).ToList(), ordenados.Select(l => l.Numero).ToList(),
                    g.Sum(l => l.QuantidadeEmStock), (int)Math.Round(PrecoCalculado(maisAntigo) * 100m, MidpointRounding.AwayFromZero),
                    ordenados.Select(l => l.Localizacao).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                    EPelicula(nome) ? "Películas" : null,
                    ligados.TryGetValue(g.Key, out var sugerido) ? sugerido.Id : null, sugerido?.Nome);
            })
            .OrderBy(s => s.Nome)
            .ToList();
    }

    public async Task<IReadOnlyList<ArtigoDto>> CriarEmLoteAsync(ArtigosEmLoteRequest req, CancellationToken ct = default)
    {
        if (req.Itens is null || req.Itens.Count == 0) throw new ValidationException("sem_itens", "Escolhe pelo menos um artigo a criar.");
        if (req.Itens.Count > 300) throw new ValidationException("demasiados", "Máximo 300 artigos de cada vez.");
        var repetidos = req.Itens.SelectMany(i => i.LoteIds).GroupBy(x => x).Where(g => g.Count() > 1).ToList();
        if (repetidos.Count > 0) throw new ValidationException("lote_repetido", "O mesmo lote está em dois artigos.");
        var criados = new List<ArtigoDto>();
        foreach (var item in req.Itens)
        {
            var d = await CreateAsync(new ArtigoWriteRequest(item.Nome, item.Categoria ?? req.Categoria, item.PrecoVendaCents, null, null, null, null, true, item.LoteIds, item.Agrupado), ct);
            criados.Add(d.Artigo);
        }
        return criados;
    }

    /// <summary>"Película  Vidro  A34 " e "película vidro a34" são o mesmo artigo.</summary>
    private static string ChaveDescricao(string s)
        => System.Text.RegularExpressions.Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");

    public async Task<ArtigoDetalheDto> DesligarLoteAsync(Guid id, Guid loteId, CancellationToken ct = default)
    {
        var artigo = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Artigo", id);
        var lote = artigo.Lotes.FirstOrDefault(l => l.Id == loteId) ?? throw new NotFoundException("Lote", loteId);
        lote.ArtigoId = null;
        artigo.Lotes.Remove(lote);
        await _repo.SaveAsync(ct);
        await PrecosCategoriaAsync(ct);
        return Detalhe(artigo);
    }

    public async Task<VendaDto> VenderAsync(BalcaoVendaRequest req, CancellationToken ct = default)
    {
        if (req.Linhas is null || req.Linhas.Count == 0)
            throw new ValidationException("venda_sem_linhas", "Passa pelo menos um artigo.");
        if (req.Linhas.Count > MaxLinhasBalcao)
            throw new ValidationException("linhas_invalidas", $"Máximo {MaxLinhasBalcao} linhas.");

        // Dados da fatura validados antes de dar saída ao stock (um NIF errado não deixa a venda sem fatura).
        if (req.ComNif is { } fiscais) Moloni.MoloniService.ValidarDadosFiscais(fiscais);

        await PrecosCategoriaAsync(ct);
        var linhas = new List<VendaLinhaWriteRequest>();
        foreach (var grupo in req.Linhas.GroupBy(l => (l.ArtigoId, l.PrecoUnitarioCents, l.LoteId)))
        {
            var qtd = grupo.Sum(l => l.Quantidade);
            if (qtd <= 0) throw new ValidationException("quantidade_invalida", "Quantidade tem de ser positiva.");
            var artigo = await _repo.FindByIdAsync(grupo.Key.ArtigoId, ct) ?? throw new NotFoundException("Artigo", grupo.Key.ArtigoId);
            var preco = grupo.Key.PrecoUnitarioCents ?? PrecoEfetivo(artigo)
                ?? throw new ValidationException("sem_preco", $"\"{artigo.Nome}\" não tem preço de venda — define-o no artigo.");
            if (preco <= 0) throw new ValidationException("preco_invalido", "Preço inválido.");

            // FIFO: do lote mais antigo para o mais recente (as unidades já reservadas nesta venda contam).
            // Sprint 581: num artigo agrupado, o modelo escolhido no balcão — sai só desse lote.
            var falta = qtd;
            var fonte = LotesFifo(artigo);
            if (grupo.Key.LoteId is { } loteId)
                fonte = fonte.Where(l => l.Id == loteId);
            foreach (var lote in fonte)
            {
                var usadas = linhas.Where(l => l.CompraLinhaId == lote.Id).Sum(l => l.Quantidade);
                var disponivel = lote.QuantidadeEmStock - usadas;
                if (disponivel <= 0) continue;
                var tira = Math.Min(disponivel, falta);
                // Agrupado: a fatura diz o modelo (art. 36.º n.º 5 b) CIVA, denominação usual) — "Película vidro – <descrição do lote>".
                linhas.Add(new VendaLinhaWriteRequest(null, lote.Id, artigo.Agrupado ? $"{artigo.Nome} – {lote.Descricao}" : artigo.Nome, tira, preco));
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
            try { return await _moloni.EmitirAsync(venda.Id, new Moloni.MoloniEmitirRequest(Moloni.MoloniDocumento.FaturaRecibo,
                PaymentMethod: req.PaymentMethod, SemNif: req.ComNif is null, ComNif: req.ComNif), ct); }
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
        if (req.Agrupado is { } agrupado) artigo.Agrupado = agrupado;
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
            lote.PecaReparacao = false;
            if (!artigo.Lotes.Contains(lote)) artigo.Lotes.Add(lote);
            // Sprint 581: ligar um lote ensina o Mender — a próxima compra deste SKU deste fornecedor vem já ligada.
            if (lote.SkuFornecedor is { } sku && lote.Documento?.FornecedorId is { } fornecedorId)
            {
                if (await _repo.SkuApontaParaOutroAsync(artigo.Id, fornecedorId, sku, ct))
                    _avisos.Add($"O SKU {sku} de {lote.Documento.Fornecedor?.Name ?? "fornecedor"} estava ligado a outro artigo — as próximas compras com este SKU passam a entrar em «{artigo.Nome}».");
                await _repo.AprenderSkuFornecedorAsync(artigo.Id, fornecedorId, sku, ct);
            }
        }
    }

    public async Task<ArtigoDetalheDto> FundirAsync(Guid destinoId, FundirArtigosRequest req, CancellationToken ct = default)
    {
        if (req.OrigemIds is null || req.OrigemIds.Count == 0) throw new ValidationException("sem_artigos", "Escolhe os artigos a juntar.");
        if (req.OrigemIds.Count > 300) throw new ValidationException("demasiados", "Máximo 300 artigos de cada vez.");
        var destino = await _repo.FindByIdAsync(destinoId, ct) ?? throw new NotFoundException("Artigo", destinoId);
        foreach (var origemId in req.OrigemIds.Distinct().Where(id => id != destinoId))
        {
            var origem = await _repo.FindByIdAsync(origemId, ct) ?? throw new NotFoundException("Artigo", origemId);
            var lotesMovidos = origem.Lotes.Select(l => l.Id).ToList();
            foreach (var lote in origem.Lotes.ToList())
            {
                lote.ArtigoId = destino.Id;
                destino.Lotes.Add(lote);
            }
            // As etiquetas já coladas continuam a ler: o código de barras e o do fabricante passam a alternativos.
            var antigos = new[] { origem.CodigoBarras, origem.CodigoFabricante }.Where(c => !string.IsNullOrEmpty(c)).Select(c => c!);
            var codigosMovidos = origem.Codigos.Select(c => c.Id).ToList();
            foreach (var codigo in origem.Codigos.ToList())
            {
                codigo.ArtigoId = destino.Id;
                destino.Codigos.Add(codigo);
            }
            origem.Ativo = false;
            _repo.Remove(origem); // soft delete — liberta o código de barras (índice só sobre os não apagados)
            var criados = new List<Guid>();
            foreach (var c in antigos.Where(c => !destino.Codigos.Any(x => x.FornecedorId == null && x.Codigo == c)))
            {
                var alias = new ArtigoCodigo { Id = Guid.NewGuid(), TenantId = destino.TenantId, ArtigoId = destino.Id, Codigo = c };
                await _repo.AddCodigoAsync(alias, ct);
                criados.Add(alias.Id);
            }
            // Registo para desfazer (Sprint 581): o que passou e o que se criou.
            await _repo.AddFusaoAsync(new ArtigoFusao
            {
                TenantId = destino.TenantId, DestinoId = destino.Id, OrigemId = origem.Id, OrigemSku = origem.Sku, OrigemNome = origem.Nome,
                LoteIdsJson = System.Text.Json.JsonSerializer.Serialize(lotesMovidos),
                CodigosMovidosJson = System.Text.Json.JsonSerializer.Serialize(codigosMovidos),
                CodigosCriadosJson = System.Text.Json.JsonSerializer.Serialize(criados),
            }, ct);
            await _audit.LogAsync(AuditAction.Update, nameof(Artigo), destino.Id,
                new { operation = "artigo_fundir", origem = origem.Sku, origemNome = origem.Nome }, ct: ct);
        }
        destino.UpdatedAt = DateTime.UtcNow;
        await _repo.SaveAsync(ct);
        return await GetAsync(destino.Id, ct);
    }

    public async Task<ArtigoDetalheDto> DesfazerFusaoAsync(Guid fusaoId, CancellationToken ct = default)
    {
        var fusao = await _repo.FindFusaoAsync(fusaoId, ct) ?? throw new NotFoundException("Junção", fusaoId);
        if (fusao.DesfeitaEm is not null) throw new ConflictException("ja_desfeita", "Esta junção já foi desfeita.");
        var destino = await _repo.FindByIdAsync(fusao.DestinoId, ct) ?? throw new NotFoundException("Artigo", fusao.DestinoId);
        var origem = await _repo.FindApagadoAsync(fusao.OrigemId, ct) ?? throw new NotFoundException("Artigo", fusao.OrigemId);

        // 1.º tira as etiquetas alternativas criadas no destino (libertam o código de barras da origem)…
        foreach (var c in await _repo.FindCodigosAsync(Ids(fusao.CodigosCriadosJson), ct)) _repo.RemoveCodigo(c);
        await _repo.SaveIntermedioAsync(ct);

        // …2.º a origem volta, com os lotes que ainda estão no destino e os seus códigos.
        origem.IsDeleted = false;
        origem.Ativo = true;
        var lotes = Ids(fusao.LoteIdsJson).ToHashSet();
        foreach (var lote in destino.Lotes.Where(l => lotes.Contains(l.Id)).ToList())
        {
            lote.ArtigoId = origem.Id;
            destino.Lotes.Remove(lote);
        }
        foreach (var c in await _repo.FindCodigosAsync(Ids(fusao.CodigosMovidosJson), ct))
            if (c.ArtigoId == destino.Id) c.ArtigoId = origem.Id;
        fusao.DesfeitaEm = DateTime.UtcNow;
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Artigo), destino.Id, new { operation = "artigo_fundir_desfazer", origem = origem.Sku }, ct: ct);
        return await GetAsync(origem.Id, ct);
    }

    public async Task<int> MarcarPecaReparacaoAsync(PecaReparacaoRequest req, CancellationToken ct = default)
    {
        if (req.LoteIds is null || req.LoteIds.Count == 0) return 0;
        var lotes = await _compras.FindLinhasAsync(req.LoteIds.Distinct().ToList(), ct);
        foreach (var l in lotes)
        {
            l.PecaReparacao = req.PecaReparacao;
            if (req.PecaReparacao) l.ArtigoId = null;
        }
        await _compras.SaveAsync(ct);
        return lotes.Count;
    }

    private static IEnumerable<CompraLinha> LotesFifo(Artigo a)
        => a.Lotes.Where(l => !l.IsDeleted && l.QuantidadeEmStock > 0)
            .OrderBy(l => l.Documento?.Data ?? l.CreatedAt).ThenBy(l => l.Numero);

    private static int Stock(Artigo a) => a.Lotes.Where(l => !l.IsDeleted).Sum(l => Math.Max(0, l.QuantidadeEmStock));

    private static decimal PrecoCalculado(CompraLinha l)
        => IvaEngine.Unidade(l.PrecoUnitarioPago, l.TaxaIvaCompra, l.LucroUnitario, FiscalDefaults.TaxaIvaNormal,
            l.Documento?.Fornecedor?.RegimeIva == RegimeIvaFornecedor.UeAutoliquidacao).PrecoFinalComIva;

    public async Task<IReadOnlyList<CategoriaArtigoDto>> CategoriasAsync(CancellationToken ct = default)
    {
        var categorias = (await _repo.ListCategoriasAsync(ct)).ToList();
        var artigos = await _repo.ListAsync(null, incluirInativos: false, ct);
        int Conta(string nome) => artigos.Count(a => string.Equals(a.Categoria?.Trim(), nome, StringComparison.OrdinalIgnoreCase));
        // Categorias escritas nos artigos mas ainda sem registo aparecem também (sem preço).
        var soNosArtigos = artigos.Select(a => a.Categoria?.Trim()).Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(c => !categorias.Any(x => x.Nome.Equals(c, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new CategoriaArtigoDto(Guid.Empty, c!, null, Conta(c!)));
        return categorias.Select(c => new CategoriaArtigoDto(c.Id, c.Nome, c.PrecoVendaCents, Conta(c.Nome))).Concat(soNosArtigos)
            .OrderBy(c => c.Nome).ToList();
    }

    public async Task<CategoriaArtigoDto> GuardarCategoriaAsync(CategoriaArtigoRequest req, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId) throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        var nome = Clean(req.Nome, 80) ?? throw new ValidationException("nome_obrigatorio", "A categoria precisa de nome.");
        if (req.PrecoVendaCents is < 0 or > 100_000_000) throw new ValidationException("preco_invalido", "Preço inválido.");
        var cat = await _repo.FindCategoriaAsync(nome, ct);
        if (cat is null)
        {
            cat = new CategoriaArtigo { TenantId = tenantId, Nome = nome };
            await _repo.AddCategoriaAsync(cat, ct);
        }
        var antes = cat.PrecoVendaCents;
        cat.PrecoVendaCents = req.PrecoVendaCents is > 0 ? req.PrecoVendaCents : null;
        await _repo.SaveAsync(ct);
        _precos = null;
        await _audit.LogAsync(AuditAction.Update, nameof(CategoriaArtigo), cat.Id, new { cat.Nome, antes, depois = cat.PrecoVendaCents }, ct: ct);
        return (await CategoriasAsync(ct)).First(c => c.Nome.Equals(cat.Nome, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Preço por categoria (nome em minúsculas → cêntimos), carregado uma vez por pedido.</summary>
    private Dictionary<string, int>? _precos;

    private async Task PrecosCategoriaAsync(CancellationToken ct)
        => _precos ??= (await _repo.ListCategoriasAsync(ct)).Where(c => c.PrecoVendaCents is not null)
            .GroupBy(c => c.Nome.Trim().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().PrecoVendaCents!.Value);

    private int? PrecoCategoria(Artigo a)
        => a.Categoria is { } c && _precos is not null && _precos.TryGetValue(c.Trim().ToLowerInvariant(), out var p) ? p : null;

    /// <summary>Películas e vidros temperados (PT/ES/EN) — vão para a categoria "Películas".</summary>
    private static bool EPelicula(string nome)
        => System.Text.RegularExpressions.Regex.IsMatch(nome, "pel[ií]cula|vidro temperado|templado|tempered|hydrogel|hidrogel",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>O do artigo; senão o da categoria; senão o calculado do lote mais antigo com stock (ou do mais recente).</summary>
    private int? PrecoEfetivo(Artigo a)
    {
        if (a.PrecoVendaCents is { } p) return p;
        if (PrecoCategoria(a) is { } pc) return pc;
        var lote = LotesFifo(a).FirstOrDefault()
            ?? a.Lotes.Where(l => !l.IsDeleted).OrderByDescending(l => l.Documento?.Data ?? l.CreatedAt).FirstOrDefault();
        return lote is null ? null : (int)Math.Round(PrecoCalculado(lote) * 100m, MidpointRounding.AwayFromZero);
    }

    private ArtigoDto ToDto(Artigo a) => new(
        a.Id, a.Numero, a.Sku, a.CodigoBarras, a.CodigoFabricante, a.Nome, a.Categoria, a.PrecoVendaCents, PrecoEfetivo(a),
        a.PrecoVendaCents is not null ? "artigo" : PrecoCategoria(a) is not null ? "categoria" : "calculado",
        Stock(a), a.Lotes.Count(l => !l.IsDeleted), a.Localizacao, a.Ativo, a.Notas, a.Agrupado);

    private ArtigoDetalheDto Detalhe(Artigo a) => new(ToDto(a), a.Lotes
        .Where(l => !l.IsDeleted)
        .OrderBy(l => l.Documento?.Data ?? l.CreatedAt).ThenBy(l => l.Numero)
        .Select(l => new ArtigoLoteDto(l.Id, l.Numero, l.Descricao, l.Documento?.Fornecedor?.Name ?? "—",
            l.Documento?.Data ?? l.CreatedAt, l.Quantidade, l.QuantidadeEmStock, l.PrecoUnitarioPago, IvaEngine.Euros(PrecoCalculado(l)), l.SkuFornecedor))
        .ToList(),
        a.Codigos.Where(c => !c.IsDeleted).OrderBy(c => c.FornecedorId is null ? 0 : 1).ThenBy(c => c.Codigo)
            .Select(c => new ArtigoCodigoDto(c.Id, c.Codigo, c.FornecedorId,
                c.FornecedorId is { } fid ? a.Lotes.FirstOrDefault(l => l.Documento?.FornecedorId == fid)?.Documento?.Fornecedor?.Name : null))
            .ToList());

    private static string? Codigo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();

    private static string? Clean(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length > max ? t[..max] : t;
    }
}
