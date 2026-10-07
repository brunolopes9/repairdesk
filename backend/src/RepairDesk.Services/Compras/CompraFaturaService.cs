using System.Text.Json;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Fiscal;

namespace RepairDesk.Services.Compras;

/// <summary>
/// Sprint 560: liga uma fatura recebida (PDF lido em "Faturas recebidas") à compra que já existe,
/// em vez de criar uma compra nova — o caso típico é a compra registada pela encomenda, com a
/// fatura em falta, e a fatura a chegar mais tarde.
/// <para>
/// Regra de ouro: a associação automática só acontece quando há exatamente uma compra possível e
/// ela bate certo com a fatura ao cêntimo (linhas da compra + portes da fatura = total da fatura).
/// Tudo o resto fica "para rever", com a comparação à vista — nunca se mexe em linhas/stock a partir
/// de uma leitura de PDF.
/// </para>
/// </summary>
public interface ICompraFaturaService
{
    Task<CorrespondenciaFaturaDto> CorrespondenciaAsync(Guid importId, CancellationToken ct = default);
    Task<CompraDocumentoDto> AssociarAsync(Guid compraId, Guid importId, CancellationToken ct = default);
    Task<AssociacaoAutomaticaDto> AssociarAutomaticamenteAsync(CancellationToken ct = default);
}

public sealed record FaturaLinhaLidaDto(string Descricao, int Quantidade, decimal Total, bool Portes);

public sealed record FaturaLidaDto(
    Guid ImportId,
    Guid? FornecedorId,
    string? Fornecedor,
    string? Numero,
    DateTime? Data,
    decimal? Total,
    decimal Portes,
    IReadOnlyList<FaturaLinhaLidaDto> Linhas);

public sealed record CandidatoLinhaDto(int Numero, string Descricao, int Quantidade, decimal PrecoUnitarioPago, decimal TotalPago);

public sealed record CandidatoCompraDto(
    Guid CompraId,
    int Numero,
    string Referencia,
    DateTime Data,
    bool FaturaEmFalta,
    decimal PortesAtuais,
    decimal? TotalDocumentoAtual,
    decimal TotalLinhas,
    /// <summary>"mesma_fatura" | "encomenda" | "data".</summary>
    string Motivo,
    /// <summary>Linhas da compra + portes da fatura − total da fatura (0 = bate certo).</summary>
    decimal DiferencaComFatura,
    bool BateCerto,
    /// <summary>Já tem outra fatura (PDF) associada — não pode receber esta.</summary>
    bool JaDocumentada,
    IReadOnlyList<CandidatoLinhaDto> Linhas);

public sealed record CorrespondenciaFaturaDto(
    FaturaLidaDto Fatura,
    /// <summary>"associar" (1 compra, bate certo) | "rever" | "nova" (nenhuma compra) | "duplicada" | "ilegivel".</summary>
    string Sugestao,
    Guid? CompraSugeridaId,
    IReadOnlyList<CandidatoCompraDto> Candidatos);

public sealed record AssociacaoAutomaticaItemDto(Guid ImportId, string? Numero, Guid? CompraId, int? CompraNumero, string Resultado, string? Erro);

public sealed record AssociacaoAutomaticaDto(int Associadas, int ParaRever, int SemCompra, IReadOnlyList<AssociacaoAutomaticaItemDto> Itens);

public class CompraFaturaService : ICompraFaturaService
{
    /// <summary>Janela de datas para procurar a compra de uma fatura sem nº de encomenda.</summary>
    private const int JanelaDias = 10;
    private const int MaxPorLote = 200;

    private readonly ICompraRepository _compras;
    private readonly ISupplierInvoiceImportRepository _imports;
    private readonly ICompraService _compraService;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;

    public CompraFaturaService(ICompraRepository compras, ISupplierInvoiceImportRepository imports,
        ICompraService compraService, ITenantContext tenant, IAuditLogger audit)
    {
        _compras = compras;
        _imports = imports;
        _compraService = compraService;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<CorrespondenciaFaturaDto> CorrespondenciaAsync(Guid importId, CancellationToken ct = default)
        => await AnalisarAsync(await RequireImportAsync(importId, ct), ct);

    public async Task<CompraDocumentoDto> AssociarAsync(Guid compraId, Guid importId, CancellationToken ct = default)
    {
        var import = await RequireImportAsync(importId, ct);
        var analise = await AnalisarAsync(import, ct);
        var candidato = analise.Candidatos.FirstOrDefault(c => c.CompraId == compraId);

        // Idempotente: repetir a associação já feita devolve o resultado, sem mexer em nada.
        if (import.Status == SupplierInvoiceImportStatus.Approved)
        {
            var doc = await _compras.FindByIdAsync(compraId, ct);
            if (doc?.SupplierInvoiceImportId == import.Id) return await _compraService.GetAsync(compraId, ct);
            throw new ConflictException("importacao_fechada", "Esta fatura já foi tratada (aprovada ou associada a outra compra).");
        }
        if (import.Status != SupplierInvoiceImportStatus.Pending)
            throw new ConflictException("importacao_fechada", "Esta fatura já foi rejeitada ou não foi lida — reprocessa-a primeiro.");
        if (candidato is null)
            throw new ValidationException("compra_nao_corresponde",
                "Esta compra não é do mesmo fornecedor ou está fora das datas da fatura.");
        if (candidato.JaDocumentada)
            throw new ConflictException("compra_ja_tem_fatura", "Esta compra já tem outra fatura associada.");

        return await AplicarAsync(import, analise.Fatura, candidato, ct);
    }

    public async Task<AssociacaoAutomaticaDto> AssociarAutomaticamenteAsync(CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId ?? throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        var pendentes = (await _imports.ListPendingAsync(tenantId, MaxPorLote, ct))
            .Where(i => i.Status == SupplierInvoiceImportStatus.Pending)
            .OrderBy(i => i.ParsedDocumentDate ?? i.CreatedAt)
            .ToList();

        var itens = new List<AssociacaoAutomaticaItemDto>();
        foreach (var import in pendentes)
        {
            // Reavaliado a cada fatura: uma associação anterior tira a compra da lista das seguintes.
            var analise = await AnalisarAsync(import, ct);
            if (analise.Sugestao != "associar")
            {
                itens.Add(new(import.Id, import.ParsedDocumentNumber, analise.CompraSugeridaId, null,
                    analise.Sugestao switch { "nova" => "sem_compra", "duplicada" => "duplicada", _ => "rever" }, null));
                continue;
            }
            var candidato = analise.Candidatos.First(c => c.CompraId == analise.CompraSugeridaId);
            try
            {
                await AplicarAsync(import, analise.Fatura, candidato, ct);
                itens.Add(new(import.Id, import.ParsedDocumentNumber, candidato.CompraId, candidato.Numero, "associada", null));
            }
            catch (DomainException ex)
            {
                // Uma fatura com conflito não trava as outras; o erro vai na resposta, não é engolido.
                itens.Add(new(import.Id, import.ParsedDocumentNumber, candidato.CompraId, candidato.Numero, "rever", ex.Message));
            }
        }

        return new AssociacaoAutomaticaDto(
            itens.Count(i => i.Resultado == "associada"),
            itens.Count(i => i.Resultado == "rever"),
            itens.Count(i => i.Resultado == "sem_compra"),
            itens);
    }

    // ---------- núcleo ----------

    private async Task<CorrespondenciaFaturaDto> AnalisarAsync(SupplierInvoiceImport import, CancellationToken ct)
    {
        var fatura = LerFatura(import);
        if (import.Status != SupplierInvoiceImportStatus.Pending && import.Status != SupplierInvoiceImportStatus.Approved
            || fatura.FornecedorId is null || fatura.Total is null || fatura.Data is null)
            return new CorrespondenciaFaturaDto(fatura, "ilegivel", null, []);

        var data = fatura.Data.Value.Date;
        var docs = await _compras.FindCandidatosFaturaAsync(fatura.FornecedorId.Value, fatura.Numero,
            data.AddDays(-JanelaDias), data.AddDays(JanelaDias + 1).AddTicks(-1), ct);

        var candidatos = docs
            .Select(d => Candidato(d, fatura, import.Id))
            .OrderBy(c => c.Motivo == "data" ? 1 : 0)
            .ThenByDescending(c => c.BateCerto)
            .ThenBy(c => Math.Abs((c.Data - data).TotalDays))
            .ToList();

        var livres = candidatos.Where(c => !c.JaDocumentada).ToList();
        var fortes = livres.Where(c => c.Motivo != "data").ToList();
        string sugestao;
        Guid? sugerida = null;
        if (fortes.Count == 0 && candidatos.Any(c => c.JaDocumentada && c.Motivo == "mesma_fatura"))
        {
            // Outra cópia (ex.: 2ª via) de uma fatura que já está registada e documentada.
            sugestao = "duplicada";
            sugerida = candidatos.First(c => c.Motivo == "mesma_fatura").CompraId;
        }
        else if (fortes.Count == 1)
        {
            sugerida = fortes[0].CompraId;
            sugestao = fortes[0].BateCerto ? "associar" : "rever";
        }
        else if (fortes.Count > 1)
        {
            sugestao = "rever";
        }
        else
        {
            var batem = livres.Where(c => c.BateCerto).ToList();
            if (batem.Count == 1) { sugerida = batem[0].CompraId; sugestao = "associar"; }
            else if (livres.Count > 0 || candidatos.Count > 0) { sugerida = livres.FirstOrDefault()?.CompraId; sugestao = "rever"; }
            else sugestao = "nova";
        }
        return new CorrespondenciaFaturaDto(fatura, sugestao, sugerida, candidatos);
    }

    private async Task<CompraDocumentoDto> AplicarAsync(SupplierInvoiceImport import, FaturaLidaDto fatura, CandidatoCompraDto candidato, CancellationToken ct)
    {
        var doc = await _compras.FindByIdAsync(candidato.CompraId, ct) ?? throw new NotFoundException("CompraDocumento", candidato.CompraId);
        var antes = new { doc.NumeroFatura, doc.Data, doc.PortesPagos, doc.PortesIva, doc.TotalDocumento, doc.SupplierInvoiceImportId };

        // Um resumo de encomenda (o nº lido é o da encomenda) documenta a compra mas não é a fatura:
        // a compra continua com "fatura em falta" até chegar o documento fiscal.
        var eFatura = candidato.Motivo != "encomenda" && !string.IsNullOrWhiteSpace(fatura.Numero);
        if (eFatura)
        {
            if (!string.IsNullOrWhiteSpace(doc.NumeroFatura)
                && !string.Equals(doc.NumeroFatura, fatura.Numero, StringComparison.OrdinalIgnoreCase))
                throw new ConflictException("numero_diferente",
                    $"A compra já tem a fatura {doc.NumeroFatura} — não pode receber a {fatura.Numero}.");
            if (string.IsNullOrWhiteSpace(doc.NumeroFatura))
            {
                var dups = await _compras.FindPossiveisDuplicadosAsync(doc.FornecedorId, fatura.Numero, [], doc.Id, ct);
                if (dups.Any(d => string.Equals(d.NumeroFatura, fatura.Numero, StringComparison.OrdinalIgnoreCase)))
                    throw new ConflictException("compra_duplicada", $"A fatura {fatura.Numero} já está registada noutra compra.");
            }
            doc.NumeroFatura = fatura.Numero;
            // A data que conta para o IVA é a da fatura.
            doc.Data = DateTime.SpecifyKind(fatura.Data!.Value.Date, DateTimeKind.Utc);
        }

        // O documento fiscal manda nos totais: total impresso e portes passam a ser os da fatura.
        var taxa = doc.Fornecedor?.TaxaIvaCompraPorDefeito(FiscalDefaults.TaxaIvaNormal) ?? 0m;
        doc.TotalDocumento = fatura.Total;
        doc.PortesPagos = fatura.Portes;
        doc.PortesIva = taxa > 0 ? Math.Round(fatura.Portes - fatura.Portes / (1 + taxa), 4) : 0m;
        doc.SupplierInvoiceImportId = import.Id;

        import.Status = SupplierInvoiceImportStatus.Approved;
        import.ProcessedAt = DateTime.UtcNow;
        import.FornecedorId ??= doc.FornecedorId;

        // Mesmo DbContext (scoped): compra e importação gravam juntas — ou as duas, ou nenhuma.
        await _compras.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(CompraDocumento), doc.Id, new
        {
            operation = "associar_fatura",
            importId = import.Id,
            motivo = candidato.Motivo,
            antes,
            depois = new { doc.NumeroFatura, doc.Data, doc.PortesPagos, doc.PortesIva, doc.TotalDocumento, doc.SupplierInvoiceImportId },
        }, ct: ct);
        return await _compraService.GetAsync(doc.Id, ct);
    }

    private static CandidatoCompraDto Candidato(CompraDocumento d, FaturaLidaDto f, Guid importId)
    {
        var motivo = !string.IsNullOrWhiteSpace(f.Numero) && string.Equals(d.NumeroFatura, f.Numero, StringComparison.OrdinalIgnoreCase)
            ? "mesma_fatura"
            : !string.IsNullOrWhiteSpace(f.Numero) && (d.NumerosEncomenda ?? string.Empty).Split(',')
                .Any(e => string.Equals(e, f.Numero, StringComparison.OrdinalIgnoreCase))
                ? "encomenda"
                : "data";
        var totalLinhas = d.Linhas.Sum(l => l.Quantidade * l.PrecoUnitarioPago);
        // A mesma regra de reconciliação (tolerância 0,01 €) que mostra a "Dif." na lista de compras.
        var rec = IvaEngine.Reconciliar(totalLinhas, f.Portes, f.Total);
        // Já documentada = tem outro PDF e já não está em falta (um resumo de encomenda pode ser substituído pela fatura).
        var jaDocumentada = d.SupplierInvoiceImportId is { } outro && outro != importId && !d.FaturaEmFalta;

        return new CandidatoCompraDto(
            d.Id, d.Numero,
            d.NumeroFatura ?? (d.NumerosEncomenda is { } e ? $"Enc. {e.Replace(",", ", ")}" : "—"),
            d.Data, d.FaturaEmFalta, d.PortesPagos, d.TotalDocumento, totalLinhas, motivo,
            -(rec.Diferenca ?? 0m), !rec.TemDiferenca, jaDocumentada,
            d.Linhas.OrderBy(l => l.Numero)
                .Select(l => new CandidatoLinhaDto(l.Numero, l.Descricao, l.Quantidade, l.PrecoUnitarioPago, l.Quantidade * l.PrecoUnitarioPago))
                .ToList());
    }

    private static FaturaLidaDto LerFatura(SupplierInvoiceImport x)
    {
        SupplierPdfItem[] items = [];
        if (!string.IsNullOrWhiteSpace(x.ParsedItemsJson))
        {
            try { items = JsonSerializer.Deserialize<SupplierPdfItem[]>(x.ParsedItemsJson) ?? []; }
            catch (JsonException) { items = []; }
        }
        var linhas = items.Select(i => new FaturaLinhaLidaDto(i.Description, i.Quantity, i.LineTotalCents / 100m,
                SupplierInvoiceImportService.ClassifyItemDescription(i.Description) == SupplierItemKind.Shipping))
            .ToList();
        return new FaturaLidaDto(
            x.Id, x.FornecedorId, x.Fornecedor?.Name ?? x.FornecedorNameRaw,
            string.IsNullOrWhiteSpace(x.ParsedDocumentNumber) ? null : x.ParsedDocumentNumber.Trim(),
            x.ParsedDocumentDate, x.ParsedTotalCents / 100m,
            linhas.Where(l => l.Portes).Sum(l => l.Total),
            linhas);
    }

    private async Task<SupplierInvoiceImport> RequireImportAsync(Guid importId, CancellationToken ct)
    {
        var import = await _imports.FindByIdAsync(importId, ct);
        if (import is null || import.TenantId != _tenant.TenantId)
            throw new NotFoundException("SupplierInvoiceImport", importId);
        return import;
    }
}
