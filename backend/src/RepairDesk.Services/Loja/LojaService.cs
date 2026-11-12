using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Loja;

public sealed record SpecLoja(string Nome, string Valor);

public sealed record GuardarProdutoLojaRequest(
    string Titulo,
    string Marca,
    string? Modelo,
    LojaCategoria Categoria,
    LojaEstado Estado,
    LojaGrau? Grau,
    int GarantiaMeses,
    LojaRegimeIva RegimeIva,
    LojaOrigem Origem,
    Guid? FornecedorId,
    LojaDisponibilidade? Disponibilidade,
    decimal Preco,
    decimal? Custo,
    string? Cor,
    int? RamGb,
    int? ArmazenamentoGb,
    string? Descricao,
    IReadOnlyList<SpecLoja>? Specs,
    string? Slug,
    string? SeoTitulo,
    string? SeoDescricao);

public sealed record ImportarProdutosResultado(int Criados, int JaExistiam, IReadOnlyList<ProdutoLojaDto> Produtos);

public sealed record ProdutoLojaFotoDto(Guid Id, int Ordem, string? Alt, string? Url, bool Enviada);

/// <summary>Vista do dono — inclui custo, IVA estimado e lucro.</summary>
public sealed record ProdutoLojaDto(
    Guid Id, int Numero, string Slug, string Titulo, string Marca, string? Modelo, LojaCategoria Categoria,
    LojaEstado Estado, LojaGrau? Grau, int GarantiaMeses, LojaRegimeIva RegimeIva, LojaOrigem Origem, Guid? FornecedorId,
    LojaDisponibilidade Disponibilidade, decimal Preco, decimal? Custo, decimal IvaEstimado, decimal? LucroEstimado,
    string? Cor, int? RamGb, int? ArmazenamentoGb, string? Descricao, IReadOnlyList<SpecLoja> Specs,
    string? SeoTitulo, string? SeoDescricao, bool Publicado, DateTime? PublicadoEm, string UrlSite,
    IReadOnlyList<ProdutoLojaFotoDto> Fotos, DateTime CreatedAt, DateTime? UpdatedAt);

/// <summary>Vista pública (gerador do site) — SEM custo nem margem.</summary>
public sealed record CatalogoProdutoDto(
    string Slug, string Categoria, string Titulo, string Marca, string? Modelo, string Estado, string? Grau,
    string CondicaoSchema, int GarantiaMeses, string Disponibilidade, decimal Preco, string? Cor, int? RamGb,
    int? ArmazenamentoGb, string? Descricao, IReadOnlyList<SpecLoja> Specs, string SeoTitulo, string SeoDescricao,
    IReadOnlyList<CatalogoFotoDto> Fotos, DateTime AtualizadoEm);

public sealed record CatalogoFotoDto(string Url, string? Alt);

/// <summary>`Versao` muda sempre que algo publicado muda — o gerador só reconstrói quando muda.</summary>
public sealed record CatalogoDto(string Versao, IReadOnlyList<CatalogoProdutoDto> Produtos);

public sealed record PedirUploadFotoRequest(string NomeOriginal, string ContentType, long Bytes, string? Alt);
public sealed record UploadFotoTicketDto(Guid FotoId, string UploadUrl, string ContentType, DateTime ExpiraEm);

public interface ILojaService
{
    Task<IReadOnlyList<ProdutoLojaDto>> ListAsync(CancellationToken ct = default);
    Task<ProdutoLojaDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ProdutoLojaDto> CreateAsync(GuardarProdutoLojaRequest req, CancellationToken ct = default);
    /// <summary>Sprint 636: cria vários produtos como rascunho (não publica). Salta títulos que já existem.</summary>
    Task<ImportarProdutosResultado> ImportarAsync(IReadOnlyList<GuardarProdutoLojaRequest> produtos, CancellationToken ct = default);
    Task<ProdutoLojaDto> UpdateAsync(Guid id, GuardarProdutoLojaRequest req, CancellationToken ct = default);
    Task<ProdutoLojaDto> PublicarAsync(Guid id, bool publicar, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<UploadFotoTicketDto> PedirUploadFotoAsync(Guid produtoId, PedirUploadFotoRequest req, CancellationToken ct = default);
    Task<ProdutoLojaDto> ConfirmarFotoAsync(Guid produtoId, Guid fotoId, CancellationToken ct = default);
    Task<ProdutoLojaDto> OrdenarFotosAsync(Guid produtoId, IReadOnlyList<Guid> ordem, CancellationToken ct = default);
    Task<ProdutoLojaDto> ApagarFotoAsync(Guid produtoId, Guid fotoId, CancellationToken ct = default);

    /// <summary>Catálogo publicado da empresa da chave (gerador do lopestech.pt).</summary>
    Task<CatalogoDto> CatalogoAsync(CancellationToken ct = default);
}

/// <summary>
/// Sprint 613 (Doc 97): produtos da loja do lopestech.pt. Regras legais no próprio modelo:
/// garantia mínima (DL 84/2021) e regime da margem só para bens em segunda mão (DL 199/96).
/// </summary>
public sealed class LojaService : ILojaService
{
    public const string SiteBase = "https://lopestech.pt";
    private const decimal TaxaIva = 0.23m;
    private const long MaxBytesFoto = 15 * 1024 * 1024;
    private static readonly TimeSpan ValidadeUpload = TimeSpan.FromHours(1);
    private static readonly TimeSpan ValidadeLeitura = TimeSpan.FromHours(6);
    private static readonly Dictionary<string, string> TiposFoto = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp",
    };

    private readonly IProdutoLojaRepository _repo;
    private readonly IDirectAccessStorage? _storage;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly IAuditLogger _audit;

    public LojaService(IProdutoLojaRepository repo, IEnumerable<IDirectAccessStorage> storage, ITenantContext tenant, ICurrentUser user, IAuditLogger audit)
    {
        _repo = repo;
        _storage = storage.FirstOrDefault();
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ProdutoLojaDto>> ListAsync(CancellationToken ct = default)
        => (await _repo.ListAsync(false, ct)).Select(ToDto).ToList();

    public async Task<ProdutoLojaDto> GetAsync(Guid id, CancellationToken ct = default) => ToDto(await Load(id, ct));

    public const int MaxImportar = 100;

    public async Task<ImportarProdutosResultado> ImportarAsync(IReadOnlyList<GuardarProdutoLojaRequest> produtos, CancellationToken ct = default)
    {
        if (produtos is null || produtos.Count == 0) throw new ValidationException("sem_produtos", "Não há produtos para importar.");
        if (produtos.Count > MaxImportar) throw new ValidationException("demasiados_produtos", $"No máximo {MaxImportar} de cada vez.");
        var existentes = (await _repo.ListAsync(false, ct)).Select(p => p.Titulo.Trim().ToLowerInvariant()).ToHashSet();
        var criados = new List<ProdutoLojaDto>();
        var saltados = 0;
        foreach (var req in produtos)
        {
            if (existentes.Contains(req.Titulo?.Trim().ToLowerInvariant() ?? string.Empty)) { saltados++; continue; }
            criados.Add(await CreateAsync(req, ct));
            existentes.Add(req.Titulo!.Trim().ToLowerInvariant());
        }
        return new ImportarProdutosResultado(criados.Count, saltados, criados);
    }

    public async Task<ProdutoLojaDto> CreateAsync(GuardarProdutoLojaRequest req, CancellationToken ct = default)
    {
        var p = new ProdutoLoja { Slug = "-", Titulo = "-", Marca = "-", TenantId = _tenant.TenantId ?? Guid.Empty };
        await Aplicar(p, req, ct);
        await _repo.AddAsync(p, ct);
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Create, p, new { p.Titulo, preco = p.Preco() }, ct);
        return ToDto(p);
    }

    public async Task<ProdutoLojaDto> UpdateAsync(Guid id, GuardarProdutoLojaRequest req, CancellationToken ct = default)
    {
        var p = await Load(id, ct);
        await Aplicar(p, req, ct);
        if (p.Publicado) ValidarParaPublicar(p);
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Update, p, new { p.Titulo, preco = p.Preco(), p.Publicado }, ct);
        return ToDto(p);
    }

    public async Task<ProdutoLojaDto> PublicarAsync(Guid id, bool publicar, CancellationToken ct = default)
    {
        var p = await Load(id, ct);
        if (publicar) ValidarParaPublicar(p);
        p.Publicado = publicar;
        if (publicar) p.PublicadoEm ??= DateTime.UtcNow;
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Update, p, new { publicado = publicar }, ct);
        return ToDto(p);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var p = await Load(id, ct);
        if (p.Publicado)
            throw new ConflictException("produto_publicado", "Tira primeiro o produto do site (despublicar) antes de o apagar.");
        _repo.Remove(p);
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Delete, p, new { p.Titulo }, ct);
    }

    // ---- Fotos --------------------------------------------------------------------------------

    public async Task<UploadFotoTicketDto> PedirUploadFotoAsync(Guid produtoId, PedirUploadFotoRequest req, CancellationToken ct = default)
    {
        var storage = _storage ?? throw new ValidationException("storage_indisponivel", "O armazenamento de fotos não está configurado.");
        var p = await Load(produtoId, ct);
        if (!TiposFoto.TryGetValue(req.ContentType ?? "", out var ext))
            throw new ValidationException("tipo_foto", "Só fotos JPG, PNG ou WebP (as HEIC do iPhone: exporta como JPG).");
        if (req.Bytes <= 0 || req.Bytes > MaxBytesFoto)
            throw new ValidationException("foto_grande", "Cada foto tem de ter até 15 MB.");
        if (p.Fotos.Count >= 20) throw new ValidationException("fotos_a_mais", "Máximo 20 fotos por produto.");

        var foto = new ProdutoLojaFoto
        {
            TenantId = p.TenantId,
            ProdutoId = p.Id,
            Ordem = p.Fotos.Count == 0 ? 0 : p.Fotos.Max(f => f.Ordem) + 1,
            StorageKey = $"loja/{p.TenantId:N}/{p.Id:N}/{Guid.NewGuid():N}{ext}",
            ContentType = req.ContentType!.ToLowerInvariant(),
            Bytes = req.Bytes,
            Alt = Corta(TrimOrNull(req.Alt), 200),
        };
        await _repo.AddFotoAsync(foto, ct);
        await _repo.SaveAsync(ct);
        return new UploadFotoTicketDto(foto.Id, storage.CreateUploadUrl(foto.StorageKey, foto.ContentType, ValidadeUpload),
            foto.ContentType, DateTime.UtcNow.Add(ValidadeUpload));
    }

    public async Task<ProdutoLojaDto> ConfirmarFotoAsync(Guid produtoId, Guid fotoId, CancellationToken ct = default)
    {
        var storage = _storage ?? throw new ValidationException("storage_indisponivel", "O armazenamento de fotos não está configurado.");
        var p = await Load(produtoId, ct);
        var f = p.Fotos.FirstOrDefault(x => x.Id == fotoId) ?? throw new NotFoundException("ProdutoLojaFoto", fotoId);
        if (!f.Enviada)
        {
            var tamanho = await storage.GetSizeAsync(f.StorageKey, ct);
            if (tamanho is null or 0)
                throw new ValidationException("foto_em_falta", "A foto ainda não chegou — tenta enviar outra vez.");
            f.Bytes = tamanho.Value;
            f.Enviada = true;
            await _repo.SaveAsync(ct);
        }
        return ToDto(p);
    }

    public async Task<ProdutoLojaDto> OrdenarFotosAsync(Guid produtoId, IReadOnlyList<Guid> ordem, CancellationToken ct = default)
    {
        var p = await Load(produtoId, ct);
        if (ordem.Count != p.Fotos.Count || p.Fotos.Any(f => !ordem.Contains(f.Id)))
            throw new ValidationException("ordem_invalida", "A ordem tem de incluir todas as fotos do produto.");
        for (var i = 0; i < ordem.Count; i++) p.Fotos.First(f => f.Id == ordem[i]).Ordem = i;
        await _repo.SaveAsync(ct);
        return ToDto(p);
    }

    public async Task<ProdutoLojaDto> ApagarFotoAsync(Guid produtoId, Guid fotoId, CancellationToken ct = default)
    {
        var p = await Load(produtoId, ct);
        var f = p.Fotos.FirstOrDefault(x => x.Id == fotoId) ?? throw new NotFoundException("ProdutoLojaFoto", fotoId);
        if (p.Publicado && p.Fotos.Count(x => x.Enviada) == 1 && f.Enviada)
            throw new ConflictException("ultima_foto", "Um produto publicado precisa de pelo menos uma foto.");
        p.Fotos.Remove(f);
        _repo.Remove(f);
        await _repo.SaveAsync(ct);
        return ToDto(p);
    }

    // ---- Catálogo público ---------------------------------------------------------------------

    public async Task<CatalogoDto> CatalogoAsync(CancellationToken ct = default)
    {
        var produtos = await _repo.ListAsync(true, ct);
        var itens = produtos.OrderBy(p => p.Categoria).ThenByDescending(p => p.PublicadoEm).Select(ToCatalogo).ToList();
        // A versão vem do conteúdo (não dos URLs assinados, que mudam a cada pedido).
        var assinatura = string.Join("|", produtos.OrderBy(p => p.Id)
            .Select(p => $"{p.Id}:{(p.UpdatedAt ?? p.CreatedAt).Ticks}:{string.Join(",", p.Fotos.Where(f => f.Enviada).OrderBy(f => f.Ordem).Select(f => f.Id))}"));
        var versao = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(assinatura)))[..16].ToLowerInvariant();
        return new CatalogoDto(versao, itens);
    }

    // ---- Regras -------------------------------------------------------------------------------

    private async Task Aplicar(ProdutoLoja p, GuardarProdutoLojaRequest req, CancellationToken ct)
    {
        var titulo = (req.Titulo ?? "").Trim();
        if (titulo.Length is < 3 or > 160) throw new ValidationException("titulo_invalido", "Título obrigatório (3 a 160 caracteres).");
        var marca = (req.Marca ?? "").Trim();
        if (marca.Length is < 1 or > 60) throw new ValidationException("marca_invalida", "Marca obrigatória (até 60 caracteres).");
        if (req.Preco <= 0 || req.Preco > 100_000) throw new ValidationException("preco_invalido", "Preço de venda obrigatório.");
        if (req.Custo is < 0) throw new ValidationException("custo_invalido", "O custo não pode ser negativo.");

        // Garantia mínima legal ao consumidor (DL 84/2021, art. 12.º): bens novos e recondicionados 3 anos;
        // usados (não recondicionados) podem ir a 18 meses por acordo expresso.
        var minimo = req.Estado == LojaEstado.Usado ? 18 : 36;
        if (req.GarantiaMeses < minimo || req.GarantiaMeses > 120)
            throw new ValidationException("garantia_abaixo_lei", req.Estado == LojaEstado.Usado
                ? "Bens usados: a garantia mínima ao consumidor é de 18 meses (com acordo expresso)."
                : "Bens novos (mesmo de caixa aberta) e recondicionados: a garantia mínima ao consumidor é de 3 anos (36 meses).");
        if (req.RegimeIva == LojaRegimeIva.Margem && req.Estado is not (LojaEstado.Recondicionado or LojaEstado.Usado))
            throw new ValidationException("margem_so_usados", "O regime da margem só se aplica a bens em segunda mão (recondicionados ou usados).");
        if (req.Grau is not null && req.Estado is not (LojaEstado.Recondicionado or LojaEstado.Usado))
            throw new ValidationException("grau_so_usados", "O grau (Premium, Excelente…) só se aplica a recondicionados e usados.");
        if (req.Origem == LojaOrigem.Fornecedor && req.Disponibilidade == LojaDisponibilidade.EmStock)
            throw new ValidationException("fornecedor_sem_stock", "Produtos do fornecedor não estão na loja: usa «Por encomenda».");
        if (req.RamGb is < 0 or > 1024 || req.ArmazenamentoGb is < 0 or > 65536)
            throw new ValidationException("atributo_invalido", "Memória ou armazenamento com valor impossível.");

        var specs = (req.Specs ?? Array.Empty<SpecLoja>())
            .Select(s => new SpecLoja((s.Nome ?? "").Trim(), (s.Valor ?? "").Trim()))
            .Where(s => s.Nome.Length > 0 && s.Valor.Length > 0).ToList();
        if (specs.Count > 40 || specs.Any(s => s.Nome.Length > 60 || s.Valor.Length > 200))
            throw new ValidationException("specs_invalidas", "Até 40 especificações (nome até 60 e valor até 200 caracteres).");

        var slug = Slugify(string.IsNullOrWhiteSpace(req.Slug) ? titulo : req.Slug!);
        if (slug.Length < 3) throw new ValidationException("slug_invalido", "O endereço da página precisa de pelo menos 3 letras ou números.");
        if (p.Publicado && p.Slug != slug)
            throw new ConflictException("slug_publicado", "O endereço de um produto publicado não muda (o Google já o conhece).");
        if (p.Slug != slug)
        {
            var baseSlug = slug;
            for (var n = 2; await _repo.SlugExisteAsync(slug, p.Id == Guid.Empty ? null : p.Id, ct); n++) slug = $"{baseSlug}-{n}";
        }

        p.Titulo = titulo;
        p.Marca = marca;
        p.Modelo = Corta(TrimOrNull(req.Modelo), 80);
        p.Categoria = req.Categoria;
        p.Estado = req.Estado;
        p.Grau = req.Grau;
        p.GarantiaMeses = req.GarantiaMeses;
        p.RegimeIva = req.RegimeIva;
        p.Origem = req.Origem;
        p.FornecedorId = req.Origem == LojaOrigem.Fornecedor ? req.FornecedorId : null;
        p.Disponibilidade = req.Disponibilidade ?? (req.Origem == LojaOrigem.Fornecedor ? LojaDisponibilidade.PorEncomenda : LojaDisponibilidade.EmStock);
        p.PrecoCents = (long)Math.Round(req.Preco * 100, MidpointRounding.AwayFromZero);
        p.CustoCents = req.Custo is { } c ? (long)Math.Round(c * 100, MidpointRounding.AwayFromZero) : null;
        p.Cor = Corta(TrimOrNull(req.Cor), 40);
        p.RamGb = req.RamGb;
        p.ArmazenamentoGb = req.ArmazenamentoGb;
        p.Descricao = Corta(TrimOrNull(req.Descricao), 8000);
        p.SpecsJson = specs.Count == 0 ? null : JsonSerializer.Serialize(specs);
        p.Slug = slug;
        p.SeoTitulo = Corta(TrimOrNull(req.SeoTitulo), 70);
        p.SeoDescricao = Corta(TrimOrNull(req.SeoDescricao), 170);
    }

    private static void ValidarParaPublicar(ProdutoLoja p)
    {
        if (!p.Fotos.Any(f => f.Enviada))
            throw new ValidationException("sem_fotos", "Põe pelo menos uma foto antes de publicar.");
    }

    /// <summary>
    /// IVA e lucro estimados: regime normal = IVA incluído no preço (23/123); regime da margem =
    /// IVA só sobre (venda − compra), também incluído (DL 199/96). Peças de reparação ficam de fora.
    /// </summary>
    public static (decimal Iva, decimal? Lucro) Estimar(long precoCents, long? custoCents, LojaRegimeIva regime)
    {
        var preco = precoCents / 100m;
        var custo = custoCents / 100m;
        if (regime == LojaRegimeIva.Margem)
        {
            if (custo is null) return (0, null);
            var margem = preco - custo.Value;
            var ivaM = margem > 0 ? Math.Round(margem * TaxaIva / (1 + TaxaIva), 2) : 0;
            return (ivaM, margem - ivaM);
        }
        var iva = Math.Round(preco * TaxaIva / (1 + TaxaIva), 2);
        return (iva, custo is null ? null : preco - iva - custo.Value);
    }

    public static string Slugify(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            var c = char.ToLowerInvariant(ch);
            sb.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-');
        }
        var slug = string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length > 80 ? slug[..80].TrimEnd('-') : slug;
    }

    public static string CategoriaSlug(LojaCategoria c) => c switch
    {
        LojaCategoria.Smartphones => "smartphones",
        LojaCategoria.Computadores => "computadores",
        LojaCategoria.Tablets => "tablets",
        LojaCategoria.Consolas => "consolas",
        LojaCategoria.Acessorios => "acessorios",
        _ => "outros",
    };

    private static string CondicaoSchema(LojaEstado e) => e switch
    {
        LojaEstado.Novo or LojaEstado.NovoCaixaAberta => "https://schema.org/NewCondition",
        LojaEstado.Recondicionado => "https://schema.org/RefurbishedCondition",
        _ => "https://schema.org/UsedCondition",
    };

    // ---- Mapeamento ---------------------------------------------------------------------------

    private ProdutoLojaDto ToDto(ProdutoLoja p)
    {
        var (iva, lucro) = Estimar(p.PrecoCents, p.CustoCents, p.RegimeIva);
        return new ProdutoLojaDto(p.Id, p.Numero, p.Slug, p.Titulo, p.Marca, p.Modelo, p.Categoria, p.Estado, p.Grau,
            p.GarantiaMeses, p.RegimeIva, p.Origem, p.FornecedorId, p.Disponibilidade, p.PrecoCents / 100m, p.CustoCents / 100m,
            iva, lucro, p.Cor, p.RamGb, p.ArmazenamentoGb, p.Descricao, LerSpecs(p.SpecsJson), p.SeoTitulo, p.SeoDescricao,
            p.Publicado, p.PublicadoEm, $"{SiteBase}/loja/{CategoriaSlug(p.Categoria)}/{p.Slug}",
            p.Fotos.OrderBy(f => f.Ordem).Select(f => new ProdutoLojaFotoDto(f.Id, f.Ordem, f.Alt,
                f.Enviada ? _storage?.CreateDownloadUrl(f.StorageKey, ValidadeLeitura) : null, f.Enviada)).ToList(),
            p.CreatedAt, p.UpdatedAt);
    }

    private CatalogoProdutoDto ToCatalogo(ProdutoLoja p) => new(
        p.Slug, CategoriaSlug(p.Categoria), p.Titulo, p.Marca, p.Modelo, p.Estado.ToString(), p.Grau?.ToString(),
        CondicaoSchema(p.Estado), p.GarantiaMeses, p.Disponibilidade.ToString(), p.PrecoCents / 100m, p.Cor, p.RamGb,
        p.ArmazenamentoGb, p.Descricao, LerSpecs(p.SpecsJson),
        p.SeoTitulo ?? Corta($"{p.Titulo} | LopesTech", 70)!,
        p.SeoDescricao ?? Corta($"{p.Titulo} por {p.PrecoCents / 100m:0.00} € — {EstadoTexto(p.Estado)}, garantia de {p.GarantiaMeses} meses. LopesTech, Viseu.", 170)!,
        p.Fotos.Where(f => f.Enviada).OrderBy(f => f.Ordem)
            .Select(f => new CatalogoFotoDto(_storage?.CreateDownloadUrl(f.StorageKey, ValidadeLeitura) ?? "", f.Alt ?? p.Titulo)).ToList(),
        p.UpdatedAt ?? p.CreatedAt);

    private static string EstadoTexto(LojaEstado e) => e switch
    {
        LojaEstado.Novo => "novo",
        LojaEstado.NovoCaixaAberta => "novo, caixa aberta",
        LojaEstado.Recondicionado => "recondicionado",
        _ => "usado",
    };

    private static IReadOnlyList<SpecLoja> LerSpecs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<SpecLoja>();
        try { return JsonSerializer.Deserialize<List<SpecLoja>>(json) ?? new List<SpecLoja>(); }
        catch (JsonException) { return Array.Empty<SpecLoja>(); }
    }

    private async Task<ProdutoLoja> Load(Guid id, CancellationToken ct)
        => await _repo.FindAsync(id, ct) ?? throw new NotFoundException("ProdutoLoja", id);

    private Task Auditar(AuditAction acao, ProdutoLoja p, object dados, CancellationToken ct)
        => _audit.LogAsync(acao, "ProdutoLoja", p.Id, dados, p.TenantId, _user.UserId, ct);

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? Corta(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}

internal static class ProdutoLojaExtensions
{
    public static decimal Preco(this ProdutoLoja p) => p.PrecoCents / 100m;
}
