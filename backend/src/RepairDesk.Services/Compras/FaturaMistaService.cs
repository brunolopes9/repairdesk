using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Despesas;

namespace RepairDesk.Services.Compras;

/// <summary>Sprint 688: linhas de uma compra que afinal são despesa (consumíveis, equipamento da loja…).</summary>
public sealed record PassarParaDespesaRequest(
    IReadOnlyList<Guid> LinhaIds,
    string? Descricao = null,
    DespesaCategoria Categoria = DespesaCategoria.Material,
    DespesaNatureza Natureza = DespesaNatureza.Gasto);

/// <summary><c>PdfImportId</c>: a fatura anexada à própria despesa (quando o PDF ficou do lado da despesa e não da compra).</summary>
public sealed record DespesaLigadaDto(Guid Id, int Numero, string Descricao, decimal Valor, decimal? Iva, DespesaNatureza Natureza, DateTime Data,
    Guid? PdfImportId = null);

public sealed record PassarParaDespesaResultado(DespesaLigadaDto Despesa, decimal ValorLinhas, decimal PortesDaDespesa, decimal? TotalQueFicaNaCompra);

public interface IFaturaMistaService
{
    Task<IReadOnlyList<DespesaLigadaDto>> DespesasDaCompraAsync(Guid compraId, CancellationToken ct = default);
    Task<PassarParaDespesaResultado> PassarParaDespesaAsync(Guid compraId, PassarParaDespesaRequest req, CancellationToken ct = default);
    Task<DespesaDto> LigarDespesaAsync(Guid despesaId, LigarCompraRequest req, CancellationToken ct = default);
}

/// <summary>
/// Sprint 688 (fatura mista — regra do Bruno, 16/11/2026): o fornecedor não separa faturas; quem separa é o Mender.
/// As linhas que são despesa saem da compra (e do stock) e passam a uma despesa ligada à compra, com a parte
/// proporcional dos portes. A fatura (PDF) continua a ser uma só — a da compra; a despesa abre essa mesma, e a
/// exportação para a contabilidade leva-a uma vez, marcada «Mista».
/// </summary>
public sealed class FaturaMistaService : IFaturaMistaService
{
    private readonly ICompraRepository _compras;
    private readonly IDespesaRepository _despesas;
    private readonly IAuditLogger _audit;
    private readonly ISupplierInvoiceImportRepository? _imports;

    public FaturaMistaService(ICompraRepository compras, IDespesaRepository despesas, IAuditLogger audit, ISupplierInvoiceImportRepository? imports = null)
    {
        _imports = imports;
        _compras = compras;
        _despesas = despesas;
        _audit = audit;
    }

    public async Task<IReadOnlyList<DespesaLigadaDto>> DespesasDaCompraAsync(Guid compraId, CancellationToken ct = default)
    {
        var lista = await _despesas.ListByCompraAsync(compraId, ct);
        var pdfs = _imports is null ? null : await _imports.ImportIdsPorDespesaAsync(lista.Select(d => d.Id).ToList(), ct);
        return lista.Select(d => Dto(d) with { PdfImportId = pdfs?.GetValueOrDefault(d.Id) is { } g && g != Guid.Empty ? g : null }).ToList();
    }

    public async Task<PassarParaDespesaResultado> PassarParaDespesaAsync(Guid compraId, PassarParaDespesaRequest req, CancellationToken ct = default)
    {
        var doc = await _compras.FindByIdAsync(compraId, ct) ?? throw new NotFoundException("CompraDocumento", compraId);
        var ids = (req.LinhaIds ?? []).Distinct().ToHashSet();
        if (ids.Count == 0) throw new ValidationException("sem_linhas", "Escolhe as linhas que são despesa.");
        var linhas = doc.Linhas.Where(l => ids.Contains(l.Id)).ToList();
        if (linhas.Count != ids.Count) throw new ValidationException("linha_inexistente", "Há linhas que não são desta compra.");
        if (linhas.Count == doc.Linhas.Count)
            throw new ConflictException("todas_as_linhas",
                "Escolheste todas as linhas — então a fatura é toda despesa. Apaga a compra e lança a fatura como despesa.");
        if (linhas.FirstOrDefault(l => l.QuantidadeVendida > 0) is { } vendida)
            throw new ConflictException("linha_vendida",
                $"\"{vendida.Descricao}\" (lote {vendida.Numero}) já tem unidades vendidas — não pode passar a despesa.");

        var totalLinhas = doc.Linhas.Sum(l => l.Quantidade * l.PrecoUnitarioPago);
        var valorLinhas = linhas.Sum(l => l.Quantidade * l.PrecoUnitarioPago);
        var proporcao = totalLinhas > 0 ? valorLinhas / totalLinhas : 0m;
        var portes = Math.Round(doc.PortesPagos * proporcao, 2, MidpointRounding.AwayFromZero);
        var portesIva = doc.PortesIva is { } pi ? Math.Round(pi * proporcao, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
        var intraUe = doc.Fornecedor?.IntraUe == true;
        var ivaLinhas = linhas.Sum(l => l.TaxaIvaCompra > 0 ? l.Quantidade * l.PrecoUnitarioPago * l.TaxaIvaCompra / (1 + l.TaxaIvaCompra) : 0m);
        var iva = intraUe ? 0m : Math.Round(ivaLinhas + (portesIva ?? 0m), 2, MidpointRounding.AwayFromZero);
        var valor = Math.Round(valorLinhas + portes, 2, MidpointRounding.AwayFromZero);

        var descricao = string.IsNullOrWhiteSpace(req.Descricao)
            ? string.Join(", ", linhas.Select(l => l.Quantidade > 1 ? $"{l.Quantidade} × {l.Descricao}" : l.Descricao))
            : req.Descricao.Trim();
        if (descricao.Length > 300) descricao = descricao[..297] + "…";
        var fatura = doc.NumeroFatura ?? doc.NumerosEncomenda;
        var despesa = new Despesa
        {
            TenantId = doc.TenantId,
            Descricao = descricao,
            Categoria = req.Categoria,
            Natureza = req.Natureza,
            ValorCents = (int)Math.Round(valor * 100, MidpointRounding.AwayFromZero),
            IvaCents = intraUe ? 0 : (int)Math.Round(iva * 100, MidpointRounding.AwayFromZero),
            ReverseCharge = intraUe,
            Data = doc.Data,
            Fornecedor = doc.Fornecedor?.Name,
            NumeroEncomenda = fatura,
            Notas = $"Fatura mista — vem da compra C#{doc.Numero} (o PDF é o dessa compra). Inclui {portes:0.00} € dos portes (proporcional).",
            CompraDocumentoId = doc.Id,
        };
        await _despesas.AddAsync(despesa, ct);

        // A compra fica só com o que é stock: sem estas linhas e sem a parte dos portes.
        foreach (var l in linhas)
        {
            doc.Linhas.Remove(l);
            _compras.RemoveLinha(l);
        }
        doc.PortesPagos -= portes;
        if (doc.PortesIva is not null) doc.PortesIva -= portesIva ?? 0m;
        if (doc.TotalDocumento is not null) doc.TotalDocumento -= valor;
        var nota = $"Fatura mista: {string.Join(", ", linhas.Select(l => $"lote {l.Numero}"))} passou a despesa ({valor:0.00} € com {portes:0.00} € de portes).";
        doc.Notas = string.IsNullOrWhiteSpace(doc.Notas) ? nota : $"{doc.Notas} · {nota}";
        if (doc.Notas.Length > 2000) doc.Notas = doc.Notas[..2000];

        await _compras.SaveAsync(ct); // mesmo DbContext: compra e despesa gravam juntas
        await _audit.LogAsync(AuditAction.Update, nameof(CompraDocumento), doc.Id,
            new { operation = "fatura_mista", despesa = despesa.Numero, lotes = linhas.Select(l => l.Numero).ToList(), valor, portes }, ct: ct);
        return new PassarParaDespesaResultado(Dto(despesa), Math.Round(valorLinhas, 2), portes, doc.TotalDocumento);
    }

    public async Task<DespesaDto> LigarDespesaAsync(Guid despesaId, LigarCompraRequest req, CancellationToken ct = default)
    {
        var d = await _despesas.FindByIdAsync(despesaId, ct) ?? throw new NotFoundException("Despesa", despesaId);
        CompraLigada? ligada = null;
        if (req.CompraNumero is { } numero)
        {
            var doc = await _compras.FindByNumeroAsync(numero, ct)
                ?? throw new ValidationException("compra_inexistente", $"Não há nenhuma compra C#{numero}.");
            d.CompraDocumentoId = doc.Id;
            ligada = new CompraLigada(doc.Id, doc.Numero, doc.SupplierInvoiceImportId);
        }
        else d.CompraDocumentoId = null;
        await _despesas.SaveAsync(ct);
        return DespesaService.ToDto(d, null, ligada);
    }

    private static DespesaLigadaDto Dto(Despesa d)
        => new(d.Id, d.Numero, d.Descricao, d.ValorCents / 100m, d.IvaCents / 100m, d.Natureza, d.Data);
}
