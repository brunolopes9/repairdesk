using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Documents;

/// <summary>Sprint 147: orquestra ingest de fatura de fornecedor via endpoint external (n8n).</summary>
public interface ISupplierInvoiceImportService
{
    Task<SupplierInvoiceImportResult> IngestAsync(
        byte[] pdfBytes,
        SupplierInvoiceEmailMeta? emailMeta,
        Guid? apiKeyId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<SupplierInvoiceImportDto>> ListPendingAsync(int take, CancellationToken ct = default);

    // Sprint 148: admin operations (JWT-auth).
    Task<byte[]> GetPdfAsync(Guid importId, CancellationToken ct = default);
    Task<SupplierInvoiceImportDto> ApproveAsync(Guid importId, ApproveSupplierInvoiceRequest req, CancellationToken ct = default);
    Task<SupplierInvoiceImportDto> RejectAsync(Guid importId, string? reason, CancellationToken ct = default);
    Task<(byte[] Zip, string Filename)> ExportZipAsync(DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Sprint 160: aprovar items como Stock (Part). Para cada item, Bruno escolhe acção:
    /// - "existing": liga a Part existente, cria PartMovimento Entrada, actualiza CustoUnitario.
    /// - "new": cria Part nova com SKU/Nome fornecidos, cria PartMovimento Entrada.
    /// - "skip": ignora o item.
    /// Em qualquer caso (excepto skip), SkuMapping é registado/incrementado para aprender.
    /// Marca import.Status = Approved.
    /// </summary>
    Task<SupplierInvoiceImportDto> ApproveAsStockAsync(Guid importId, ApproveAsStockRequest req, CancellationToken ct = default);
}

public sealed record ApproveAsStockRequest(IReadOnlyList<ApproveAsStockItem> Items);

public sealed record ApproveAsStockItem(
    string Description,
    int Quantity,
    int UnitCostCents,
    /// <summary>"existing" | "new" | "skip".</summary>
    string Action,
    Guid? ExistingPartId,
    string? NewSku,
    string? NewName,
    string? NewMarca,
    string? NewModelo,
    /// <summary>Opcional: SKU do fornecedor para aprender mapping.</summary>
    string? SupplierSku);

public sealed record ApproveSupplierInvoiceRequest(
    int ValorCents,
    string Descricao,
    Core.Enums.DespesaCategoria Categoria,
    DateTime? Data,
    string? Fornecedor,
    string? NumeroEncomenda,
    string? Notas);

public sealed record SupplierInvoiceEmailMeta(
    string? MessageId,
    string? Subject,
    string? From,
    DateTime? ReceivedAt);

public sealed record SupplierInvoiceImportResult(
    Guid ImportId,
    SupplierInvoiceImportStatus Status,
    string? FornecedorNameRaw,
    Guid? FornecedorId,
    int? TotalCents,
    string? DocumentNumber,
    string PdfRelativePath,
    bool WasDuplicate);

public sealed record SupplierInvoiceImportDto(
    Guid Id,
    Guid? FornecedorId,
    string? FornecedorName,
    string? DocumentNumber,
    DateTime? DocumentDate,
    int? TotalCents,
    string Status,
    string? ParseConfidence,
    DateTime CreatedAt,
    string PdfRelativePath,
    // Sprint 158: items parseados + fuzzy match candidates.
    IReadOnlyList<SupplierInvoiceItemDto>? Items);

public sealed record SupplierInvoiceItemDto(
    string Description,
    int Quantity,
    int LineTotalCents,
    string? Brand,
    string? Model,
    // Sprint 158: matches sugeridos (top 3 Part candidatos por fuzzy + 1 auto match se mapping existe).
    IReadOnlyList<SkuMatchSuggestion> Suggestions);

public sealed record SkuMatchSuggestion(
    Guid PartId,
    string PartName,
    string PartSku,
    /// <summary>0..1 — quanto maior, melhor match.</summary>
    double Score,
    /// <summary>"auto" (já mapeado), "fuzzy" (similaridade nome).</summary>
    string MatchType);

public sealed class SupplierInvoiceImportService : ISupplierInvoiceImportService
{
    private readonly ISupplierInvoiceImportRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IFornecedorRepository _fornecedores;
    private readonly ISupplierInvoiceStorage _storage;
    private readonly Despesas.IDespesaService _despesas;
    private readonly ISkuMappingRepository _skuMappings;
    private readonly IPartRepository _parts;
    private readonly ISupplierFingerprintingService _fingerprinting;
    private readonly IAuditLogger _audit;
    private readonly ILogger<SupplierInvoiceImportService> _logger;

    public SupplierInvoiceImportService(
        ISupplierInvoiceImportRepository repo,
        ITenantContext tenant,
        IFornecedorRepository fornecedores,
        ISupplierInvoiceStorage storage,
        Despesas.IDespesaService despesas,
        ISkuMappingRepository skuMappings,
        IPartRepository parts,
        ISupplierFingerprintingService fingerprinting,
        IAuditLogger audit,
        ILogger<SupplierInvoiceImportService> logger)
    {
        _repo = repo;
        _tenant = tenant;
        _fornecedores = fornecedores;
        _storage = storage;
        _despesas = despesas;
        _skuMappings = skuMappings;
        _parts = parts;
        _fingerprinting = fingerprinting;
        _audit = audit;
        _logger = logger;
    }

    public async Task<SupplierInvoiceImportResult> IngestAsync(
        byte[] pdfBytes,
        SupplierInvoiceEmailMeta? emailMeta,
        Guid? apiKeyId = null,
        CancellationToken ct = default)
    {
        if (pdfBytes is null || pdfBytes.Length == 0)
            throw new ValidationException("pdf_empty", "PDF vazio.");
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");

        // 1. Dedupe por hash.
        var sha256 = ComputeSha256(pdfBytes);
        var existing = await _repo.FindBySha256Async(tenantId, sha256, ct);
        if (existing is not null)
        {
            _logger.LogInformation("SupplierInvoice duplicate ignored: {Sha256}", sha256);
            return new SupplierInvoiceImportResult(
                existing.Id, existing.Status, existing.FornecedorNameRaw, existing.FornecedorId,
                existing.ParsedTotalCents, existing.ParsedDocumentNumber, existing.PdfRelativePath,
                WasDuplicate: true);
        }

        // 2. Extract text + parse.
        SupplierPdfParseResult? parsed = null;
        string? rawText = null;
        try
        {
            using var pdfStream = new MemoryStream(pdfBytes);
            var extracted = PdfTextExtractor.Extract(pdfStream, "supplier-invoice.pdf");
            rawText = extracted.Text;
            parsed = SupplierPdfParser.Parse(extracted.Text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parser failed on PDF (will store as Failed)");
        }

        // 3. Reconcile fornecedor — pipeline em ordem (mais específico → mais geral):
        //   a) Parser específico (Sprint 124+134 Tudo4Mobile etc) já detectou pelo formato do PDF.
        //   b) Sprint 162: supplier fingerprinting determinístico via emailMeta + first chars do PDF
        //      (cobre fornecedores conhecidos sem parser específico — Utopya, Molano, Mr.Phones, etc).
        //   c) Sprint 163 (futuro): LLM fallback se ainda nada detectado.
        Guid? fornecedorId = null;
        var fornecedorNameRaw = parsed?.SupplierName;
        if (string.IsNullOrWhiteSpace(fornecedorNameRaw))
        {
            // Fingerprinting (b): só tenta se o parser específico falhou.
            var pdfFirstChars = rawText is { Length: > 500 } ? rawText[..500] : rawText;
            var fp = await _fingerprinting.DetectAsync(emailMeta, pdfFirstChars, filename: null, ct);
            if (fp.Code is not null)
            {
                fornecedorNameRaw = fp.Name;
                fornecedorId = fp.FornecedorId;
                _logger.LogInformation("Fingerprinting detectou {Code} ({Source}) — fornecedorId={FId}",
                    fp.Code, fp.Source, fp.FornecedorId);
            }
        }
        if (fornecedorId is null && !string.IsNullOrWhiteSpace(fornecedorNameRaw))
        {
            var existingForn = await _fornecedores.FindByNameAsync(fornecedorNameRaw, ct);
            fornecedorId = existingForn?.Id;
        }

        // 4. Save to filesystem with organized layout.
        var docDate = parsed?.DateAdded ?? emailMeta?.ReceivedAt ?? DateTime.UtcNow;
        var supplierSlug = fornecedorNameRaw ?? "desconhecido";
        var filename = BuildFilename(docDate, parsed?.OrderId);
        var relativePath = await _storage.SaveAsync(tenantId, supplierSlug, docDate, filename, pdfBytes, ct);

        // 5. Persist.
        var status = parsed is null || parsed.Confidence == ParseConfidence.None
            ? SupplierInvoiceImportStatus.Failed
            : SupplierInvoiceImportStatus.Pending;

        var entity = new SupplierInvoiceImport
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FornecedorId = fornecedorId,
            FornecedorNameRaw = fornecedorNameRaw,
            PdfSha256 = sha256,
            PdfRelativePath = relativePath,
            PdfBytesSize = pdfBytes.Length,
            EmailMessageId = emailMeta?.MessageId,
            EmailSubject = emailMeta?.Subject,
            EmailFrom = emailMeta?.From,
            EmailReceivedAt = emailMeta?.ReceivedAt,
            ParsedTotalCents = parsed?.TotalCents,
            ParsedDocumentNumber = parsed?.OrderId,
            ParsedDocumentDate = parsed?.DateAdded,
            ParsedItemsJson = parsed?.Items is { Count: > 0 } items
                ? JsonSerializer.Serialize(items)
                : null,
            ParseConfidence = parsed?.Confidence.ToString(),
            Status = status,
            CreatedAt = DateTime.UtcNow,
            CreatedByApiKeyId = apiKeyId,
        };

        await _repo.AddAsync(entity, ct);
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Create, nameof(SupplierInvoiceImport), entity.Id, new
        {
            operation = "supplier_invoice_ingest",
            sha256,
            fornecedor = fornecedorNameRaw,
            totalCents = parsed?.TotalCents,
            documentNumber = parsed?.OrderId,
            confidence = parsed?.Confidence.ToString(),
            relativePath,
        }, ct: ct);

        _logger.LogInformation(
            "SupplierInvoice ingested: id={Id} sha={Sha} fornecedor={Fornecedor} total={TotalCents} status={Status}",
            entity.Id, sha256[..8], fornecedorNameRaw, parsed?.TotalCents, status);

        return new SupplierInvoiceImportResult(
            entity.Id, status, fornecedorNameRaw, fornecedorId,
            parsed?.TotalCents, parsed?.OrderId, relativePath, WasDuplicate: false);
    }

    public async Task<IReadOnlyList<SupplierInvoiceImportDto>> ListPendingAsync(int take, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");

        var entities = await _repo.ListPendingAsync(tenantId, take, ct);

        // Sprint 158: pré-carrega lista de Parts uma vez para fuzzy matching de todos os items.
        // Não usa AsQueryable() para evitar lazy load issues — Bruno tipicamente tem <500 Parts.
        var (allParts, _) = await _parts.SearchAsync(null, null, null, false, 1, 500, ct);
        var partHaystack = allParts.Select(p => (Id: p.Id, Name: $"{p.Nome} {p.Marca} {p.Modelo}".Trim())).ToList();

        var dtos = new List<SupplierInvoiceImportDto>(entities.Count);
        foreach (var x in entities)
        {
            var items = await BuildItemsWithMatchesAsync(x, tenantId, partHaystack, ct);
            dtos.Add(new SupplierInvoiceImportDto(
                x.Id,
                x.FornecedorId,
                x.Fornecedor != null ? x.Fornecedor.Name : x.FornecedorNameRaw,
                x.ParsedDocumentNumber,
                x.ParsedDocumentDate,
                x.ParsedTotalCents,
                x.Status.ToString(),
                x.ParseConfidence,
                x.CreatedAt,
                x.PdfRelativePath,
                items));
        }
        return dtos;
    }

    /// <summary>
    /// Sprint 158: para cada item parseado, sugere top 3 matches Part candidatos:
    /// 1) Auto match — se SkuMapping existe (já aprovado antes), score 1.0.
    /// 2) Fuzzy match — top 3 por Jaccard + Levenshtein no nome.
    /// </summary>
    private async Task<IReadOnlyList<SupplierInvoiceItemDto>?> BuildItemsWithMatchesAsync(
        SupplierInvoiceImport entity,
        Guid tenantId,
        List<(Guid Id, string Name)> partHaystack,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entity.ParsedItemsJson)) return null;
        SupplierPdfItem[]? items;
        try
        {
            items = System.Text.Json.JsonSerializer.Deserialize<SupplierPdfItem[]>(entity.ParsedItemsJson);
        }
        catch { return null; }
        if (items is null) return null;

        var supplierCode = entity.Fornecedor?.Code ?? entity.FornecedorNameRaw ?? "unknown";
        var result = new List<SupplierInvoiceItemDto>(items.Length);
        foreach (var item in items)
        {
            var suggestions = new List<SkuMatchSuggestion>();

            // 1. Auto match: o parser não tem supplierSku per-item ainda (Sprint futuro adiciona).
            //    Por ora só fazemos fuzzy.

            // 2. Fuzzy match.
            var candidates = Products.PartFuzzyMatcher.Find(item.Description, partHaystack, topN: 3, minScore: 0.35);
            foreach (var c in candidates)
            {
                // Procurar Part para preencher SKU.
                var part = await _parts.FindByIdAsync(c.TargetId, ct);
                if (part is null) continue;
                suggestions.Add(new SkuMatchSuggestion(c.TargetId, c.TargetName, part.Sku ?? "", c.Score, "fuzzy"));
            }

            result.Add(new SupplierInvoiceItemDto(
                item.Description,
                item.Quantity,
                item.LineTotalCents,
                item.Brand,
                item.Model,
                suggestions));
        }
        return result;
    }

    public async Task<byte[]> GetPdfAsync(Guid importId, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        var entity = await _repo.FindByIdAsync(importId, ct) ?? throw new NotFoundException("SupplierInvoiceImport", importId);
        if (entity.TenantId != tenantId) throw new ForbiddenException("cross_tenant", "Não autorizado.");
        return await _storage.ReadAsync(entity.PdfRelativePath, ct);
    }

    public async Task<SupplierInvoiceImportDto> ApproveAsync(Guid importId, ApproveSupplierInvoiceRequest req, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        var entity = await _repo.FindByIdAsync(importId, ct) ?? throw new NotFoundException("SupplierInvoiceImport", importId);
        if (entity.TenantId != tenantId) throw new ForbiddenException("cross_tenant", "Não autorizado.");
        if (entity.Status == SupplierInvoiceImportStatus.Approved)
            throw new ConflictException("already_approved", "Esta importação já foi aprovada.");

        // Cria despesa real. Bruno pode ter editado o total/categoria no UI antes de aprovar.
        var despesa = await _despesas.CreateAsync(new Despesas.CreateDespesaRequest(
            Descricao: req.Descricao,
            Categoria: req.Categoria,
            ValorCents: req.ValorCents,
            Data: req.Data ?? entity.ParsedDocumentDate ?? entity.CreatedAt,
            Fornecedor: req.Fornecedor ?? entity.FornecedorNameRaw,
            NumeroEncomenda: req.NumeroEncomenda ?? entity.ParsedDocumentNumber,
            Notas: req.Notas,
            TrabalhoId: null,
            ReparacaoId: null), ct);

        entity.Status = SupplierInvoiceImportStatus.Approved;
        entity.DespesaId = despesa.Id;
        entity.ProcessedAt = DateTime.UtcNow;
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Update, nameof(SupplierInvoiceImport), entity.Id, new
        {
            operation = "supplier_invoice_approve",
            despesaId = despesa.Id,
            valorCents = req.ValorCents,
        }, ct: ct);

        return ToDto(entity);
    }

    /// <summary>
    /// Sprint 160: aprovar items como Stock. Crie/incrementa Parts + PartMovimento Entrada.
    /// SkuMapping aprende para que futuras importações do mesmo fornecedor sugiram automaticamente.
    /// </summary>
    public async Task<SupplierInvoiceImportDto> ApproveAsStockAsync(Guid importId, ApproveAsStockRequest req, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        if (req.Items is null || req.Items.Count == 0)
            throw new ValidationException("no_items", "Sem items para aprovar.");

        var entity = await _repo.FindByIdAsync(importId, ct) ?? throw new NotFoundException("SupplierInvoiceImport", importId);
        if (entity.TenantId != tenantId) throw new ForbiddenException("cross_tenant", "Não autorizado.");
        if (entity.Status == SupplierInvoiceImportStatus.Approved)
            throw new ConflictException("already_approved", "Esta importação já foi aprovada.");

        var supplierCode = (entity.Fornecedor?.Code ?? entity.FornecedorNameRaw ?? "unknown")
            .Trim().ToLowerInvariant();
        var partsAffected = 0;
        var newParts = 0;
        var notas = $"Compra fornecedor {entity.Fornecedor?.Name ?? entity.FornecedorNameRaw ?? "?"} doc {entity.ParsedDocumentNumber ?? "?"}";

        foreach (var item in req.Items)
        {
            if (string.Equals(item.Action, "skip", StringComparison.OrdinalIgnoreCase)) continue;

            Part part;
            if (string.Equals(item.Action, "existing", StringComparison.OrdinalIgnoreCase))
            {
                if (item.ExistingPartId is not { } pid)
                    throw new ValidationException("missing_part_id", $"Item '{item.Description}': falta ExistingPartId.");
                part = await _parts.FindByIdAsync(pid, ct) ?? throw new NotFoundException("Part", pid);
                // Última compra prevalece — sobrescreve CustoUnitarioCents.
                part.CustoUnitarioCents = item.UnitCostCents;
            }
            else if (string.Equals(item.Action, "new", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(item.NewSku) || string.IsNullOrWhiteSpace(item.NewName))
                    throw new ValidationException("missing_new_fields", $"Item '{item.Description}': falta NewSku/NewName para criar Part.");
                var sku = item.NewSku.Trim().ToUpperInvariant();
                if (await _parts.SkuExistsAsync(sku, null, ct))
                    throw new ConflictException("part_sku_exists", $"Já existe uma Part com SKU '{sku}'.");
                part = new Part
                {
                    TenantId = tenantId,
                    Sku = sku,
                    Nome = item.NewName.Trim(),
                    Marca = item.NewMarca?.Trim(),
                    Modelo = item.NewModelo?.Trim(),
                    CustoUnitarioCents = item.UnitCostCents,
                    QtdStock = 0,
                };
                await _parts.AddAsync(part, ct);
                newParts++;
            }
            else
            {
                throw new ValidationException("invalid_action", $"Action '{item.Action}' inválida — usa existing/new/skip.");
            }

            // PartMovimento Entrada (compra a fornecedor).
            var stockAntes = part.QtdStock;
            _parts.AddMovimento(new PartMovimento
            {
                TenantId = tenantId,
                PartId = part.Id,
                Quantidade = item.Quantity,
                StockAntes = stockAntes,
                StockDepois = stockAntes + item.Quantity,
                Motivo = PartMovimentoMotivo.Entrada,
                Notas = notas,
            });
            part.QtdStock += item.Quantity;
            partsAffected++;

            // Aprende SkuMapping (UPSERT por (tenant, supplierCode, supplierSku)).
            if (!string.IsNullOrWhiteSpace(item.SupplierSku))
            {
                var supplierSku = item.SupplierSku.Trim();
                var existing = await _skuMappings.FindAsync(tenantId, supplierCode, supplierSku, ct);
                if (existing is null)
                {
                    await _skuMappings.AddAsync(new SkuMapping
                    {
                        TenantId = tenantId,
                        SupplierCode = supplierCode,
                        SupplierSku = supplierSku,
                        SupplierProductName = item.Description,
                        TargetType = SkuMappingTargetType.Part,
                        TargetId = part.Id,
                        Confidence = SkuMappingConfidence.Manual,
                        UseCount = 1,
                        CreatedFromImportId = importId,
                    }, ct);
                }
                else
                {
                    existing.TargetId = part.Id;
                    existing.UseCount++;
                }
            }
        }

        entity.Status = SupplierInvoiceImportStatus.Approved;
        entity.ProcessedAt = DateTime.UtcNow;
        await _parts.SaveAsync(ct);
        await _skuMappings.SaveAsync(ct);
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Update, nameof(SupplierInvoiceImport), entity.Id, new
        {
            operation = "supplier_invoice_approve_as_stock",
            partsAffected,
            newParts,
        }, ct: ct);

        return ToDto(entity);
    }

    public async Task<SupplierInvoiceImportDto> RejectAsync(Guid importId, string? reason, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        var entity = await _repo.FindByIdAsync(importId, ct) ?? throw new NotFoundException("SupplierInvoiceImport", importId);
        if (entity.TenantId != tenantId) throw new ForbiddenException("cross_tenant", "Não autorizado.");
        if (entity.Status == SupplierInvoiceImportStatus.Approved)
            throw new ConflictException("already_approved", "Não podes rejeitar uma importação já aprovada — anula a Despesa primeiro.");

        entity.Status = SupplierInvoiceImportStatus.Rejected;
        entity.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "(sem motivo)" : reason.Trim();
        entity.ProcessedAt = DateTime.UtcNow;
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Update, nameof(SupplierInvoiceImport), entity.Id, new
        {
            operation = "supplier_invoice_reject",
            reason = entity.RejectionReason,
        }, ct: ct);

        return ToDto(entity);
    }

    public async Task<(byte[] Zip, string Filename)> ExportZipAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        // Para o contabilista: queremos só as aprovadas (rejeitadas/pending não interessam fiscalmente).
        var entities = await _repo.ListByDateRangeAsync(tenantId, from, to, SupplierInvoiceImportStatus.Approved, ct);
        var paths = entities.Select(e => e.PdfRelativePath).ToList();
        var zip = await _storage.CreateZipAsync(tenantId, paths, ct);
        var filename = $"Faturas-fornecedor_{from:yyyy-MM-dd}_a_{to:yyyy-MM-dd}.zip";
        return (zip, filename);
    }

    private static SupplierInvoiceImportDto ToDto(SupplierInvoiceImport x) => new(
        x.Id,
        x.FornecedorId,
        x.Fornecedor != null ? x.Fornecedor.Name : x.FornecedorNameRaw,
        x.ParsedDocumentNumber,
        x.ParsedDocumentDate,
        x.ParsedTotalCents,
        x.Status.ToString(),
        x.ParseConfidence,
        x.CreatedAt,
        x.PdfRelativePath,
        // ToDto interno (Approve/Reject) — não inclui matches; UI usa pending list para isso.
        Items: null);

    private static string ComputeSha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildFilename(DateTime docDate, string? documentNumber)
    {
        var datePart = docDate.ToString("yyyy-MM-dd");
        var docPart = string.IsNullOrWhiteSpace(documentNumber)
            ? Guid.NewGuid().ToString("N")[..8]
            : documentNumber.Replace('/', '-').Replace('\\', '-');
        return $"{datePart}_{docPart}.pdf";
    }
}
