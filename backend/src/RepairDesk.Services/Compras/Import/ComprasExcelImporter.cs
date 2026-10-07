using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Fiscal;

namespace RepairDesk.Services.Compras.Import;

public interface IComprasExcelImporter
{
    Task<ImportComprasResultado> ImportAsync(Stream xlsx, CancellationToken ct = default);
}

public sealed record ImportComprasResultado(
    int FornecedoresCriados,
    int FornecedoresAtualizados,
    int DocumentosCriados,
    int DocumentosJaExistentes,
    int Linhas,
    IReadOnlyList<string> Avisos);

/// <summary>
/// Importa o Excel de compras do formato LopesTech (folhas "Fornecedores", "Faturas", "Compras" —
/// o que o SPEC chama dataset de seed). Idempotente: documentos já existentes (mesmo fornecedor +
/// nº fatura ou encomenda) são ignorados, por isso pode ser corrido várias vezes.
///
/// "Faturado = Sim" no Excel NÃO marca vendas: as vendas passam a ser registadas nas Vendas do Mender
/// (com o preço real), por isso todas as linhas entram em stock — e isso fica nos avisos.
/// </summary>
public sealed class ComprasExcelImporter : IComprasExcelImporter
{
    private readonly ICompraRepository _compras;
    private readonly IFornecedorRepository _fornecedores;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;

    public ComprasExcelImporter(ICompraRepository compras, IFornecedorRepository fornecedores, ITenantContext tenant, IAuditLogger audit)
    {
        _compras = compras;
        _fornecedores = fornecedores;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<ImportComprasResultado> ImportAsync(Stream xlsx, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        XlsxReader excel;
        try { excel = XlsxReader.Read(xlsx); }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
        {
            throw new ValidationException("excel_invalido", "Não consegui ler o ficheiro. Confirma que é um .xlsx.");
        }

        var compras = excel.Sheet("Compras");
        if (compras.Count == 0)
            throw new ValidationException("excel_sem_compras", "O Excel não tem a folha \"Compras\" no formato esperado.");

        var avisos = new List<string>();
        int fornCriados = 0, fornAtualizados = 0, docsCriados = 0, docsExistentes = 0, linhasImportadas = 0, vendidosNoExcel = 0;

        // 1. Fornecedores (folha opcional): regime e país.
        var fornecedores = new Dictionary<string, Fornecedor>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in await _fornecedores.ListByTenantAsync(includeInactive: true, ct))
            fornecedores[f.Name] = await _fornecedores.FindByIdAsync(f.Id, ct) ?? f;

        foreach (var (_, row) in excel.Sheet("Fornecedores").Where(r => r.Key >= 2))
        {
            var nome = XlsxReader.Text(row, "A");
            if (nome is null) continue;
            var regime = ParseRegime(XlsxReader.Text(row, "C"), XlsxReader.Number(row, "D"));
            var pais = ParsePais(XlsxReader.Text(row, "B"));
            if (fornecedores.TryGetValue(nome, out var existente))
            {
                if (existente.RegimeIva != regime || (pais is not null && existente.Pais != pais))
                {
                    existente.RegimeIva = regime;
                    existente.Pais = pais ?? existente.Pais;
                    fornAtualizados++;
                }
            }
            else
            {
                fornecedores[nome] = await CriarFornecedorAsync(tenantId, nome, regime, pais, ct);
                fornCriados++;
            }
        }

        // 2. Cabeçalhos dos documentos (folha "Faturas"), chave = fornecedor + referência da coluna C.
        var cabecalhos = new Dictionary<(string, string), Dictionary<string, string>>();
        foreach (var (_, row) in excel.Sheet("Faturas").Where(r => r.Key >= 2))
        {
            if (XlsxReader.Text(row, "A") is { } forn && XlsxReader.Text(row, "C") is { } refDoc)
                cabecalhos[(forn, refDoc)] = row;
        }

        // 3. Linhas agrupadas por documento.
        var grupos = compras
            .Where(r => r.Key >= 3 && XlsxReader.Text(r.Value, "A") is not null && XlsxReader.Text(r.Value, "D") is not null)
            .GroupBy(r => (Forn: XlsxReader.Text(r.Value, "A")!, Ref: XlsxReader.Text(r.Value, "C") ?? string.Empty))
            .ToList();

        foreach (var grupo in grupos)
        {
            var (nomeForn, refDoc) = grupo.Key;
            if (!fornecedores.TryGetValue(nomeForn, out var fornecedor))
            {
                // Fornecedor sem linha na folha Fornecedores: deduz o regime pela taxa da 1.ª linha.
                var taxa = XlsxReader.Number(grupo.First().Value, "F") ?? FiscalDefaults.TaxaIvaNormal;
                fornecedor = await CriarFornecedorAsync(tenantId, nomeForn,
                    taxa == 0 ? RegimeIvaFornecedor.UeAutoliquidacao : RegimeIvaFornecedor.Nacional, null, ct);
                fornecedores[nomeForn] = fornecedor;
                fornCriados++;
            }

            cabecalhos.TryGetValue((nomeForn, refDoc), out var cab);
            var ehEncomenda = refDoc.StartsWith("Enc", StringComparison.OrdinalIgnoreCase);
            var numeroFatura = ehEncomenda || refDoc.Length == 0 ? null : refDoc;
            var encomendas = CompraService.NormalizarEncomendas(
                new[] { cab is null ? null : XlsxReader.Text(cab, "D"), ehEncomenda ? refDoc : null }.OfType<string>());

            var duplicados = await _compras.FindPossiveisDuplicadosAsync(fornecedor.Id, numeroFatura, encomendas, null, ct);
            if (duplicados.Count > 0)
            {
                docsExistentes++;
                continue;
            }

            var data = (cab is null ? null : XlsxReader.Date(cab, "B")) ?? XlsxReader.Date(grupo.First().Value, "B");
            if (data is null)
            {
                avisos.Add($"{nomeForn} {refDoc}: sem data — documento ignorado.");
                continue;
            }

            var notas = new[] { cab is null ? null : XlsxReader.Text(cab, "M"), cab is null ? null : XlsxReader.Text(cab, "N") }
                .OfType<string>().ToList();
            var doc = new CompraDocumento
            {
                TenantId = tenantId,
                FornecedorId = fornecedor.Id,
                Fornecedor = fornecedor,
                Data = data.Value,
                NumeroFatura = numeroFatura,
                NumerosEncomenda = encomendas.Count == 0 ? null : string.Join(',', encomendas),
                MetodoPagamento = cab is null ? null : Trunc(XlsxReader.Text(cab, "E"), 50),
                PortesPagos = cab is null ? 0m : XlsxReader.Number(cab, "H") ?? 0m,
                PortesIva = cab is null ? null : XlsxReader.Number(cab, "I"),
                TotalDocumento = cab is null ? null : XlsxReader.Number(cab, "K"),
                Notas = notas.Count == 0 ? "Importado do Excel." : Trunc("Importado do Excel. " + string.Join(" · ", notas), 2000),
            };

            foreach (var (numLinha, row) in grupo)
            {
                var descricao = XlsxReader.Text(row, "D")!;
                var qtd = (int)(XlsxReader.Number(row, "E") ?? 0);
                var preco = XlsxReader.Number(row, "G");
                if (qtd < 1 || preco is null or < 0)
                {
                    avisos.Add($"Compras linha {numLinha} (\"{descricao}\"): quantidade/preço inválidos — ignorada.");
                    continue;
                }
                if (string.Equals(XlsxReader.Text(row, "K"), "sim", StringComparison.OrdinalIgnoreCase)) vendidosNoExcel++;

                doc.Linhas.Add(new CompraLinha
                {
                    TenantId = tenantId,
                    Descricao = Trunc(descricao, 500)!,
                    Quantidade = qtd,
                    PrecoUnitarioPago = preco.Value,
                    TaxaIvaCompra = XlsxReader.Number(row, "F") ?? fornecedor.TaxaIvaCompraPorDefeito(FiscalDefaults.TaxaIvaNormal),
                    LucroUnitario = XlsxReader.Number(row, "M") ?? FiscalDefaults.LucroPorDefeito(descricao),
                });
            }

            if (doc.Linhas.Count == 0) continue;
            await _compras.AddAsync(doc, ct);
            docsCriados++;
            linhasImportadas += doc.Linhas.Count;
        }

        await _compras.SaveAsync(ct);

        if (vendidosNoExcel > 0)
            avisos.Add($"{vendidosNoExcel} linha(s) estavam marcadas \"Faturado = Sim\" no Excel e entraram em stock: as vendas registam-se nas Vendas do Mender, com o preço real.");

        await _audit.LogAsync(AuditAction.Create, "ImportacaoComprasExcel", null,
            new { fornCriados, docsCriados, docsExistentes, linhasImportadas }, ct: ct);

        return new ImportComprasResultado(fornCriados, fornAtualizados, docsCriados, docsExistentes, linhasImportadas, avisos);
    }

    private async Task<Fornecedor> CriarFornecedorAsync(Guid tenantId, string nome, RegimeIvaFornecedor regime, string? pais, CancellationToken ct)
    {
        var f = new Fornecedor { TenantId = tenantId, Name = Trunc(nome, 200)!, RegimeIva = regime, Pais = pais, Active = true };
        await _fornecedores.AddAsync(f, ct);
        return f;
    }

    private static RegimeIvaFornecedor ParseRegime(string? regime, decimal? taxa)
    {
        var r = (regime ?? string.Empty).ToLowerInvariant();
        if (r.Contains("ue") || r.Contains("intra") || r.Contains("autoliq")) return RegimeIvaFornecedor.UeAutoliquidacao;
        if (r.Contains("fora") || r.Contains("import")) return RegimeIvaFornecedor.ForaUe;
        if (r.Contains("nacional")) return RegimeIvaFornecedor.Nacional;
        return taxa == 0 ? RegimeIvaFornecedor.UeAutoliquidacao : RegimeIvaFornecedor.Nacional;
    }

    private static readonly Dictionary<string, string> Paises = new(StringComparer.OrdinalIgnoreCase)
    {
        ["portugal"] = "PT", ["países baixos"] = "NL", ["paises baixos"] = "NL", ["holanda"] = "NL",
        ["frança"] = "FR", ["franca"] = "FR", ["espanha"] = "ES", ["alemanha"] = "DE", ["itália"] = "IT",
        ["italia"] = "IT", ["bélgica"] = "BE", ["belgica"] = "BE", ["polónia"] = "PL", ["china"] = "CN",
    };

    private static string? ParsePais(string? pais)
    {
        if (pais is null) return null;
        if (pais.Length == 2 && pais.All(char.IsAsciiLetter)) return pais.ToUpperInvariant();
        return Paises.GetValueOrDefault(pais.Trim());
    }

    private static string? Trunc(string? v, int max) => v is null ? null : v.Length > max ? v[..max] : v;
}
