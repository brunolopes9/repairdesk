using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Moloni;

[JsonConverter(typeof(JsonStringEnumConverter<MoloniTipoDocumento>))]
public enum MoloniTipoDocumento
{
    Fatura,
    FaturaRecibo,
    Recibo,
    Orcamento,
}

public sealed record MoloniDocumentoDto(
    int DocumentId,
    MoloniTipoDocumento Tipo,
    string Numero,
    DateTime? Data,
    string? Cliente,
    string? Nif,
    decimal Total,
    bool Rascunho,
    bool Anulado,
    /// <summary>Faturas: 0–100 % pago (recibos emitidos). Null nos outros tipos.</summary>
    decimal? PagoPercentagem,
    Guid? VendaId,
    int? VendaNumero);

public sealed record MoloniPagina<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record MoloniProdutoDto(
    int ProductId,
    string Referencia,
    string Nome,
    /// <summary>1 = produto, 2 = serviço, 3 = outro.</summary>
    int Tipo,
    decimal Preco,
    decimal PrecoComIva,
    decimal Taxa,
    string? MotivoIsencao,
    int? CategoriaId,
    string? Categoria,
    int? UnidadeId,
    string? Unidade,
    bool PodeApagar);

/// <summary>Preço com IVA (como se pensa ao balcão) — o Mender calcula o preço sem IVA que o Moloni guarda.</summary>
public sealed record MoloniProdutoRequest(
    string Nome,
    string Referencia,
    int Tipo,
    decimal PrecoComIva,
    decimal Taxa,
    string? MotivoIsencao = null,
    int? CategoriaId = null,
    int? UnidadeId = null);

public sealed record MoloniOpcao(int Id, string Nome);

public sealed record MoloniReferenciasDto(IReadOnlyList<MoloniOpcao> Categorias, IReadOnlyList<MoloniOpcao> Unidades, IReadOnlyList<decimal> Taxas);

public sealed record MoloniImportacaoClientesDto(int Ligados, int Criados, int JaLigados, int Total);

public sealed record MoloniReciboDocumentoRequest(PaymentMethod PaymentMethod);

public interface IMoloniGestaoService
{
    Task<MoloniPagina<MoloniDocumentoDto>> DocumentosAsync(MoloniTipoDocumento tipo, string? q, int page, int pageSize, CancellationToken ct = default);
    Task<(byte[] Pdf, string Nome)> DocumentoPdfAsync(MoloniTipoDocumento tipo, int documentId, CancellationToken ct = default);
    /// <summary>Recibo de uma fatura por pagar (mesmo as emitidas fora do Mender).</summary>
    Task ReciboDeFaturaAsync(int documentId, MoloniReciboDocumentoRequest req, CancellationToken ct = default);
    Task AnularDocumentoAsync(MoloniTipoDocumento tipo, int documentId, string motivo, CancellationToken ct = default);

    Task<MoloniPagina<MoloniProdutoDto>> ProdutosAsync(string? q, int page, int pageSize, CancellationToken ct = default);
    Task<MoloniReferenciasDto> ReferenciasAsync(CancellationToken ct = default);
    Task<MoloniProdutoDto> CriarProdutoAsync(MoloniProdutoRequest req, CancellationToken ct = default);
    Task<MoloniProdutoDto> AtualizarProdutoAsync(int productId, MoloniProdutoRequest req, CancellationToken ct = default);
    Task ApagarProdutoAsync(int productId, CancellationToken ct = default);

    /// <summary>Liga (pelo NIF) ou cria no Mender os clientes que já existem no Moloni ON.</summary>
    Task<MoloniImportacaoClientesDto> ImportarClientesAsync(CancellationToken ct = default);
    /// <summary>Nome/preço do artigo da loja → artigo ligado no Moloni ON (sem efeito se não estiver ligado).</summary>
    Task SincronizarArtigoAsync(Guid artigoId, CancellationToken ct = default);
}

/// <summary>
/// Sprint 568: tudo o que se fazia no site do Moloni ON passa a fazer-se no Mender — listar e gerir documentos
/// (incluindo os emitidos antes do Mender), o catálogo de artigos/serviços e os clientes.
/// </summary>
public partial class MoloniService : IMoloniGestaoService
{
    private const string CamposDocumento = "documentId number documentSetName date totalValue status nullified reconciliationPercentage entityName entityVat";
    private const string CamposProduto = "productId reference name type price priceWithTaxes exemptionReason taxes { tax { taxId value } } productCategory { productCategoryId name } measurementUnit { measurementUnitId abbreviation } deletionBlockers";

    private static (string ApiCode, string Lista, string Prefixo) Api(MoloniTipoDocumento tipo) => tipo switch
    {
        MoloniTipoDocumento.Fatura => ("invoice", "invoices", "FT"),
        MoloniTipoDocumento.FaturaRecibo => ("invoiceReceipt", "invoiceReceipts", "FR"),
        MoloniTipoDocumento.Recibo => ("receipt", "receipts", "RC"),
        _ => ("estimate", "estimates", "OR"),
    };

    public async Task<MoloniPagina<MoloniDocumentoDto>> DocumentosAsync(MoloniTipoDocumento tipo, string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var (_, key, companyId, _) = await ContextoAsync(ct);
        var (apiCode, lista, prefixo) = Api(tipo);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var json = await _client.ExecutarEscalarAsync(key,
            $"query($c:Int!,$o:{Pascal(apiCode)}Options){{ {lista}(companyId:$c, options:$o) {{ errors {{ field msg }} data {{ {CamposDocumento} }} options {{ pagination {{ count }} }} }} }}",
            new
            {
                c = companyId,
                o = new { order = new[] { new { field = "date", sort = "DESC" } }, search = new { field = "ALL", value = q?.Trim() ?? string.Empty }, pagination = new { page, qty = pageSize } },
            }, lista, ct);
        Erros(json);
        var docs = json?["data"]?.AsArray() ?? [];
        var ids = docs.Select(d => d!["documentId"]!.GetValue<int>()).ToList();
        var vendas = await _vendas.ListByMoloniIdsAsync(ids, ct);
        var items = docs.Select(d =>
        {
            var id = d!["documentId"]!.GetValue<int>();
            var venda = vendas.FirstOrDefault(v => v.MoloniDocumentoId == id || v.MoloniReciboId == id || v.MoloniOrcamentoId == id);
            var numero = d["number"] is JsonValue n && n.TryGetValue<int>(out var num) ? $"{prefixo} {d["documentSetName"]?.GetValue<string>()}/{num}" : $"{prefixo} (rascunho)";
            return new MoloniDocumentoDto(id, tipo, numero,
                d["date"]?.GetValue<DateTime>(), d["entityName"]?.GetValue<string>(), d["entityVat"]?.GetValue<string>(),
                d["totalValue"]?.GetValue<decimal>() ?? 0m, d["status"]?.GetValue<int>() == 0, d["nullified"]?.GetValue<bool>() ?? false,
                tipo == MoloniTipoDocumento.Fatura && d["reconciliationPercentage"] is JsonValue p && p.TryGetValue<decimal>(out var pago) ? Math.Round(pago * 100m, 0) : null,
                venda?.Id, venda?.Numero);
        }).ToList();
        var total = json?["options"]?["pagination"]?["count"]?.GetValue<int>() ?? items.Count;
        return new MoloniPagina<MoloniDocumentoDto>(items, total, page, pageSize);
    }

    public async Task<(byte[] Pdf, string Nome)> DocumentoPdfAsync(MoloniTipoDocumento tipo, int documentId, CancellationToken ct = default)
    {
        var (_, key, companyId, _) = await ContextoAsync(ct);
        var (apiCode, _, prefixo) = Api(tipo);
        return (await ObterPdfAsync(key, companyId, apiCode, documentId, ct), $"{prefixo}-{documentId}.pdf");
    }

    public async Task ReciboDeFaturaAsync(int documentId, MoloniReciboDocumentoRequest req, CancellationToken ct = default)
    {
        // Ligada a uma venda: segue pelo fluxo da venda (fica marcada como paga no Mender).
        if ((await _vendas.ListByMoloniIdsAsync([documentId], ct)).FirstOrDefault(v => v.MoloniDocumentoId == documentId) is { } venda)
        {
            await ReciboAsync(venda.Id, new MoloniReciboRequest(req.PaymentMethod), ct);
            return;
        }
        var (t, key, companyId, cfg) = await ContextoAsync(ct);
        var inv = await _client.ExecutarAsync(key,
            "query($c:Int!,$d:Int!){ invoice(companyId:$c, documentId:$d) { errors { field msg } data { totalValue documentSetId customerId remainingReconciledValue nullified status } } }",
            new { c = companyId, d = documentId }, "invoice", ct);
        if (inv?["nullified"]?.GetValue<bool>() == true) throw new ConflictException("fatura_anulada", "A fatura está anulada.");
        if (inv?["status"]?.GetValue<int>() != 1) throw new ConflictException("fatura_rascunho", "A fatura ainda é rascunho no Moloni ON.");
        var resto = inv!["remainingReconciledValue"] is JsonValue rv && rv.TryGetValue<decimal>(out var r) ? r : inv["totalValue"]!.GetValue<decimal>();
        if (resto <= 0) throw new ConflictException("ja_paga", "Esta fatura já está paga.");
        var metodo = await MetodoPagamentoAsync(key, companyId, cfg, t, req.PaymentMethod, ct);
        var agora = DateTime.UtcNow;
        var recibo = await _client.ExecutarAsync(key,
            "mutation($c:Int!,$d:ReceiptInsert!){ receiptCreate(companyId:$c, data:$d) { errors { field msg } data { documentId number documentSetName } } }",
            new
            {
                c = companyId,
                d = new
                {
                    documentSetId = inv["documentSetId"]!.GetValue<int>(),
                    customerId = inv["customerId"]!.GetValue<int>(),
                    date = agora.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                    totalValue = resto,
                    relatedWith = new[] { new { relatedDocumentId = documentId, value = resto } },
                    payments = new[] { Pagamento(metodo, resto, agora) },
                    status = 1,
                },
            }, "receiptCreate", ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Tenant), t.Id,
            new { operation = "moloni_recibo_fatura", documentId, recibo = recibo?["documentId"]?.GetValue<int>(), resto }, ct: ct);
    }

    public async Task AnularDocumentoAsync(MoloniTipoDocumento tipo, int documentId, string motivo, CancellationToken ct = default)
    {
        var venda = (await _vendas.ListByMoloniIdsAsync([documentId], ct)).FirstOrDefault();
        // Fatura/fatura-recibo de uma venda: anula pelo fluxo da venda (recibo primeiro e a venda volta a "por faturar").
        if (venda is not null && venda.MoloniDocumentoId == documentId)
        {
            await AnularAsync(venda.Id, new MoloniAnularRequest(motivo), ct);
            return;
        }
        var (t, key, companyId, _) = await ContextoAsync(ct);
        var m = Limpar(motivo, 200);
        if (m is null || m.Length < 5) throw new ValidationException("motivo_obrigatorio", "Escreve o motivo da anulação (fica registado na AT).");
        var (apiCode, _, _) = Api(tipo);
        await NullifyAsync(key, companyId, apiCode, documentId, m, ct);

        if (venda is not null)
        {
            var v = await _vendas.FindByIdAsync(venda.Id, ct);
            if (v is not null)
            {
                if (v.MoloniReciboId == documentId) { v.MoloniReciboId = null; v.MoloniReciboNumero = null; }
                if (v.MoloniOrcamentoId == documentId) { v.MoloniOrcamentoId = null; v.MoloniOrcamentoNumero = null; }
                await _vendas.SaveAsync(ct);
            }
        }
        await _audit.LogAsync(AuditAction.Update, nameof(Tenant), t.Id, new { operation = "moloni_anular_documento", apiCode, documentId, motivo = m }, ct: ct);
    }

    // ---------- artigos / serviços ----------

    public async Task<MoloniPagina<MoloniProdutoDto>> ProdutosAsync(string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var (_, key, companyId, _) = await ContextoAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var json = await _client.ExecutarEscalarAsync(key,
            $"query($c:Int!,$o:ProductOptions){{ products(companyId:$c, options:$o) {{ errors {{ field msg }} data {{ {CamposProduto} }} options {{ pagination {{ count }} }} }} }}",
            new
            {
                c = companyId,
                o = new { order = new[] { new { field = "name", sort = "ASC" } }, search = new { field = "ALL", value = q?.Trim() ?? string.Empty }, pagination = new { page, qty = pageSize } },
            }, "products", ct);
        Erros(json);
        var items = (json?["data"]?.AsArray() ?? []).Select(p => Produto(p!)).ToList();
        return new MoloniPagina<MoloniProdutoDto>(items, json?["options"]?["pagination"]?["count"]?.GetValue<int>() ?? items.Count, page, pageSize);
    }

    public async Task<MoloniReferenciasDto> ReferenciasAsync(CancellationToken ct = default)
    {
        var (_, key, companyId, cfg) = await ContextoAsync(ct);
        var cats = await _client.ExecutarAsync(key, "query($c:Int!){ productCategories(companyId:$c) { errors { field msg } data { productCategoryId name } } }", new { c = companyId }, "productCategories", ct);
        var uns = await _client.ExecutarAsync(key, "query($c:Int!){ measurementUnits(companyId:$c) { errors { field msg } data { measurementUnitId name abbreviation } } }", new { c = companyId }, "measurementUnits", ct);
        return new MoloniReferenciasDto(
            cats!.AsArray().Select(c => new MoloniOpcao(c!["productCategoryId"]!.GetValue<int>(), c["name"]!.GetValue<string>())).ToList(),
            uns!.AsArray().Select(u => new MoloniOpcao(u!["measurementUnitId"]!.GetValue<int>(), $"{u["name"]!.GetValue<string>()} ({u["abbreviation"]!.GetValue<string>()})")).ToList(),
            [0m, .. cfg.Impostos.Keys.Select(k => decimal.Parse(k, CultureInfo.InvariantCulture)).OrderByDescending(x => x)]);
    }

    public async Task<MoloniProdutoDto> CriarProdutoAsync(MoloniProdutoRequest req, CancellationToken ct = default)
    {
        var (_, key, companyId, cfg) = await ContextoAsync(ct);
        var dados = DadosProduto(req, cfg);
        dados["productCategoryId"] = req.CategoriaId ?? cfg.CategoriaId;
        dados["measurementUnitId"] = req.UnidadeId ?? cfg.UnidadeId;
        var d = await _client.ExecutarAsync(key,
            $"mutation($c:Int!,$d:ProductInsert!){{ productCreate(companyId:$c, data:$d) {{ errors {{ field msg }} data {{ {CamposProduto} }} }} }}",
            new { c = companyId, d = dados }, "productCreate", ct);
        return Produto(d!);
    }

    public async Task<MoloniProdutoDto> AtualizarProdutoAsync(int productId, MoloniProdutoRequest req, CancellationToken ct = default)
    {
        var (_, key, companyId, cfg) = await ContextoAsync(ct);
        var dados = DadosProduto(req, cfg);
        dados["productId"] = productId;
        if (req.CategoriaId is { } cat) dados["productCategoryId"] = cat;
        if (req.UnidadeId is { } un) dados["measurementUnitId"] = un;
        var d = await _client.ExecutarAsync(key,
            $"mutation($c:Int!,$d:ProductUpdate!){{ productUpdate(companyId:$c, data:$d) {{ errors {{ field msg }} data {{ {CamposProduto} }} }} }}",
            new { c = companyId, d = dados }, "productUpdate", ct);
        return Produto(d!);
    }

    public async Task ApagarProdutoAsync(int productId, CancellationToken ct = default)
    {
        var (t, key, companyId, cfg) = await ContextoAsync(ct);
        if (productId == cfg.ProdutoServicoId || productId == cfg.ProdutoArtigoId)
            throw new ConflictException("produto_do_mender", "Este é o artigo genérico que o Mender usa nas faturas — não pode ser apagado.");
        var r = await _client.ExecutarEscalarAsync(key,
            "mutation($c:Int!,$d:[Int]!){ productDelete(companyId:$c, productId:$d) { status deletedCount errors { field msg } } }",
            new { c = companyId, d = new[] { productId } }, "productDelete", ct);
        Erros(r);
        if ((r?["deletedCount"]?.GetValue<int>() ?? 0) == 0)
            throw new ConflictException("produto_em_uso", "O Moloni ON não deixa apagar um artigo que já foi usado em documentos.");
        // Artigos da loja ligados a este artigo voltam a criar o seu na próxima fatura.
        foreach (var a in await _artigos.ListAsync(null, incluirInativos: true, ct))
            if (a.MoloniProductId == productId && await _artigos.FindByIdAsync(a.Id, ct) is { } artigo) artigo.MoloniProductId = null;
        await _artigos.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, nameof(Tenant), t.Id, new { operation = "moloni_apagar_artigo", productId }, ct: ct);
    }

    // ---------- clientes ----------

    public async Task<MoloniImportacaoClientesDto> ImportarClientesAsync(CancellationToken ct = default)
    {
        var (t, key, companyId, _) = await ContextoAsync(ct);
        int ligados = 0, criados = 0, jaLigados = 0, total = 0;
        for (var page = 1; page <= 100; page++)
        {
            var json = await _client.ExecutarEscalarAsync(key,
                "query($c:Int!,$o:CustomerOptions){ customers(companyId:$c, options:$o) { errors { field msg } data { customerId name vat email phone address city zipCode isDefault } options { pagination { count } } } }",
                new { c = companyId, o = new { order = new[] { new { field = "name", sort = "ASC" } }, pagination = new { page, qty = 50 } } }, "customers", ct);
            Erros(json);
            var lote = json?["data"]?.AsArray() ?? [];
            foreach (var c in lote)
            {
                if (c!["isDefault"]?.GetValue<bool>() == true) continue; // consumidor final
                total++;
                var id = c["customerId"]!.GetValue<int>();
                if (await _clientes.FindByMoloniCustomerIdAsync(id, ct) is not null) { jaLigados++; continue; }
                var vat = c["vat"]?.GetValue<string>();
                var existente = string.IsNullOrWhiteSpace(vat) ? null : await _clientes.FindByNifAsync(vat, ct);
                if (existente is not null)
                {
                    existente.MoloniCustomerId = id;
                    ligados++;
                    continue;
                }
                var nome = c["name"]?.GetValue<string>() ?? $"Cliente Moloni {id}";
                await _clientes.AddAsync(new Cliente
                {
                    TenantId = t.Id,
                    Nome = nome.Length > 200 ? nome[..200] : nome,
                    Nif = string.IsNullOrWhiteSpace(vat) || vat.Length > 20 ? null : vat,
                    Email = c["email"]?.GetValue<string>(),
                    Telefone = c["phone"]?.GetValue<string>(),
                    Morada = c["address"]?.GetValue<string>(),
                    CodigoPostal = c["zipCode"]?.GetValue<string>(),
                    Localidade = c["city"]?.GetValue<string>(),
                    MoloniCustomerId = id,
                    Notas = "Importado do Moloni ON.",
                }, ct);
                criados++;
            }
            await _clientes.SaveAsync(ct);
            if (lote.Count < 50) break;
        }
        await _audit.LogAsync(AuditAction.Update, nameof(Tenant), t.Id, new { operation = "moloni_importar_clientes", ligados, criados }, ct: ct);
        return new MoloniImportacaoClientesDto(ligados, criados, jaLigados, total);
    }

    public async Task SincronizarArtigoAsync(Guid artigoId, CancellationToken ct = default)
    {
        var t = await TenantAsync(ct);
        if (t?.MoloniApiKeyCipherText is null || t.MoloniCompanyId is not { } companyId || t.MoloniConfigJson is null) return;
        var artigo = await _artigos.FindByIdAsync(artigoId, ct);
        if (artigo?.MoloniProductId is not { } productId) return;
        var dados = new Dictionary<string, object?> { ["productId"] = productId, ["name"] = artigo.Nome };
        if (artigo.PrecoVendaCents is { } p) dados["price"] = Math.Round(p / 100m / 1.23m, 4, MidpointRounding.AwayFromZero);
        await _client.ExecutarAsync(_secrets.Unprotect(t.MoloniApiKeyCipherText),
            "mutation($c:Int!,$d:ProductUpdate!){ productUpdate(companyId:$c, data:$d) { errors { field msg } data { productId } } }",
            new { c = companyId, d = dados }, "productUpdate", ct);
    }

    // ---------- ajudas ----------

    private static Dictionary<string, object?> DadosProduto(MoloniProdutoRequest req, MoloniConfig cfg)
    {
        var nome = Limpar(req.Nome, 200);
        if (nome is null || nome.Length < 2) throw new ValidationException("nome_obrigatorio", "O nome tem de ter pelo menos 2 letras.");
        var referencia = Limpar(req.Referencia, 50) ?? throw new ValidationException("referencia_obrigatoria", "A referência é obrigatória (única).");
        if (req.Tipo is < 1 or > 3) throw new ValidationException("tipo_invalido", "Tipo: produto, serviço ou outro.");
        if (req.PrecoComIva is < 0 or > 1_000_000) throw new ValidationException("preco_invalido", "Preço inválido.");
        var dados = new Dictionary<string, object?>
        {
            ["name"] = nome,
            ["reference"] = referencia,
            ["type"] = req.Tipo,
            ["price"] = Math.Round(req.PrecoComIva / (1m + req.Taxa / 100m), 4, MidpointRounding.AwayFromZero),
        };
        if (req.Taxa > 0)
        {
            var chave = ((int)req.Taxa).ToString(CultureInfo.InvariantCulture);
            if (!cfg.Impostos.TryGetValue(chave, out var taxId)) throw new ValidationException("taxa_sem_imposto", $"O Moloni ON não tem o IVA {chave}% configurado.");
            dados["taxes"] = new[] { new { taxId, value = req.Taxa, ordering = 1, cumulative = false } };
            dados["exemptionReason"] = null;
        }
        else
        {
            dados["taxes"] = Array.Empty<object>();
            dados["exemptionReason"] = Limpar(req.MotivoIsencao, 10)?.ToUpperInvariant()
                ?? throw new ValidationException("motivo_isencao_obrigatorio", "Sem IVA tem de ter motivo de isenção (ex.: M40).");
        }
        return dados;
    }

    private static MoloniProdutoDto Produto(JsonNode p)
    {
        var taxa = p["taxes"]?.AsArray().FirstOrDefault()?["tax"]?["value"]?.GetValue<decimal>() ?? 0m;
        return new MoloniProdutoDto(
            p["productId"]!.GetValue<int>(), p["reference"]?.GetValue<string>() ?? "", p["name"]?.GetValue<string>() ?? "",
            p["type"]?.GetValue<int>() ?? 1, p["price"]?.GetValue<decimal>() ?? 0m, p["priceWithTaxes"]?.GetValue<decimal>() ?? 0m, taxa,
            p["exemptionReason"]?.GetValue<string>(),
            p["productCategory"]?["productCategoryId"]?.GetValue<int>(), p["productCategory"]?["name"]?.GetValue<string>(),
            p["measurementUnit"]?["measurementUnitId"]?.GetValue<int>(), p["measurementUnit"]?["abbreviation"]?.GetValue<string>(),
            p["deletionBlockers"] is not JsonArray b || b.Count == 0);
    }

    /// <summary>Respostas lidas com <see cref="IMoloniClient.ExecutarEscalarAsync"/> (para ter também o <c>options</c>).</summary>
    private static void Erros(JsonNode? resposta)
    {
        if (resposta?["errors"] is JsonArray erros && erros.Count > 0)
            throw new MoloniException(string.Join(" | ", erros.Select(e =>
                (e?["field"]?.GetValue<string>() is { } f && f != "*" ? f + ": " : "") + e?["msg"]?.GetValue<string>())));
    }

    private static string Pascal(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}

