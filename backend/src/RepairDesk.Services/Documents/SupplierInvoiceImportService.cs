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

    /// <summary>Sprint 173: variante para webhook anonymous (passar tenant explícito).</summary>
    Task<SupplierInvoiceImportResult> IngestAsExternalAsync(
        Guid tenantId,
        byte[] pdfBytes,
        string originalFilename,
        SupplierInvoiceEmailMeta? emailMeta,
        Guid? apiKeyId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Sprint 164: upload de foto papel via Claude Vision OCR. Aceita JPG/PNG/WebP.
    /// Pipeline: validate mime + size → guarda imagem em storage → chama Claude Vision →
    /// cria SupplierInvoiceImport com items extraídos. Sem PDF text extraction.
    /// </summary>
    Task<SupplierInvoiceImportResult> IngestPhotoAsync(
        byte[] imageBytes,
        string fileName,
        string contentType,
        string? fornecedorHint,
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

    /// <summary>Sprint 163b: re-corre pipeline parser → fingerprint → LLM (Bruno auth required).</summary>
    Task<SupplierInvoiceImportDto> ReprocessAsync(Guid importId, CancellationToken ct = default);

    /// <summary>Sprint 163b: lista importações histórico (Approved/Rejected/Failed).</summary>
    Task<IReadOnlyList<SupplierInvoiceImportDto>> ListHistoryAsync(int take, CancellationToken ct = default);
}

public sealed record ApproveAsStockRequest(IReadOnlyList<ApproveAsStockItem> Items);

public sealed record ApproveAsStockItem(
    string Description,
    int Quantity,
    int UnitCostCents,
    /// <summary>Sprint 181: "existing" | "new" | "despesa" | "skip".
    /// "despesa" = não entra em stock, cria Despesa Categoria=Pecas (compra avulsa).</summary>
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
    private readonly IAnthropicSupplierParser _llmParser;
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
        IAnthropicSupplierParser llmParser,
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
        _llmParser = llmParser;
        _audit = audit;
        _logger = logger;
    }

    public Task<SupplierInvoiceImportResult> IngestAsync(
        byte[] pdfBytes,
        SupplierInvoiceEmailMeta? emailMeta,
        Guid? apiKeyId = null,
        CancellationToken ct = default)
        => IngestInternalAsync(pdfBytes, emailMeta, apiKeyId, tenantIdOverride: null, ct);

    /// <summary>
    /// Sprint 173: variante para webhooks anonymous (Cloudflare Email Routing).
    /// Caller já resolveu o tenant pelo TO header — passamos explícito porque não há
    /// HttpContext.User claim.
    /// </summary>
    public Task<SupplierInvoiceImportResult> IngestAsExternalAsync(
        Guid tenantId,
        byte[] pdfBytes,
        string originalFilename,
        SupplierInvoiceEmailMeta? emailMeta,
        Guid? apiKeyId = null,
        CancellationToken ct = default)
        => IngestInternalAsync(pdfBytes, emailMeta, apiKeyId, tenantIdOverride: tenantId, ct);

    private async Task<SupplierInvoiceImportResult> IngestInternalAsync(
        byte[] pdfBytes,
        SupplierInvoiceEmailMeta? emailMeta,
        Guid? apiKeyId,
        Guid? tenantIdOverride,
        CancellationToken ct)
    {
        if (pdfBytes is null || pdfBytes.Length == 0)
            throw new ValidationException("pdf_empty", "PDF vazio.");
        Guid tenantId;
        if (tenantIdOverride is { } overrideId) tenantId = overrideId;
        else if (_tenant.TenantId is { } ctxId) tenantId = ctxId;
        else throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");

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

        // Sprint 163b: LLM fallback se temos texto + key E:
        //   - Items vazios OU
        //   - Confidence < High (parser genérico apanha lixo como items — Utopya, etc).
        // Tudo4Mobile específico retorna High confidence → skip LLM (fast-path).
        var llmShouldFire = !string.IsNullOrWhiteSpace(rawText)
            && _llmParser.IsConfigured
            && (parsed?.Items is null || parsed.Items.Count == 0 || parsed.Confidence != ParseConfidence.High);
        if (llmShouldFire)
        {
            var llm = await _llmParser.ParseAsync(rawText, ct);
            if (llm is not null && llm.Items.Count > 0)
            {
                _logger.LogInformation("LLM parser extraiu {Count} items confidence={Conf:F2}",
                    llm.Items.Count, llm.Confidence);
                // Promove resultado LLM para parsed se ainda vazio.
                parsed = new SupplierPdfParseResult(
                    SupplierName: parsed?.SupplierName ?? llm.SupplierName ?? fornecedorNameRaw,
                    OrderId: parsed?.OrderId ?? llm.OrderId,
                    TotalCents: parsed?.TotalCents ?? llm.TotalCents,
                    DateAdded: parsed?.DateAdded ?? llm.DocumentDate,
                    Confidence: llm.Confidence >= 0.7 ? ParseConfidence.High : ParseConfidence.Low,
                    Items: llm.Items.Select(i => new SupplierPdfItem(
                        Description: i.Description,
                        Quantity: i.Quantity,
                        LineTotalCents: i.LineTotalCents)).ToList());
                if (string.IsNullOrWhiteSpace(fornecedorNameRaw))
                    fornecedorNameRaw = llm.SupplierName;
                if (fornecedorId is null && !string.IsNullOrWhiteSpace(fornecedorNameRaw))
                {
                    var existingForn = await _fornecedores.FindByNameAsync(fornecedorNameRaw, ct);
                    fornecedorId = existingForn?.Id;
                }
            }
        }

        // 4. Save to filesystem with organized layout.
        // Sprint 171: validation rules pós-parse — rebaixa confidence se totais não batem etc.
        IReadOnlyList<string> parseWarnings;
        (parsed, parseWarnings) = ParseValidator.Apply(parsed);
        if (parseWarnings.Count > 0)
            _logger.LogWarning("Parse validation warnings: {Warnings}", string.Join(" | ", parseWarnings));

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
            ParseWarningsJson = parseWarnings.Count > 0 ? JsonSerializer.Serialize(parseWarnings) : null,
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

    /// <summary>
    /// Sprint 164: upload foto papel + Claude Vision OCR. Bypassa parser tradicional
    /// porque imagens não têm texto extraível directo. Tudo passa pelo LLM Vision.
    /// </summary>
    public async Task<SupplierInvoiceImportResult> IngestPhotoAsync(
        byte[] imageBytes,
        string fileName,
        string contentType,
        string? fornecedorHint,
        CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        if (imageBytes is null || imageBytes.Length == 0)
            throw new ValidationException("no_file", "Anexa uma imagem.");
        if (!_llmParser.IsConfigured)
            throw new ValidationException("vision_not_configured",
                "Para processar fotos papel, é necessário ANTHROPIC_API_KEY configurada.");

        // Valida mime type — imagens suportadas pelo Claude Vision.
        var allowedMimes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif" };
        if (!allowedMimes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw new ValidationException("invalid_mime",
                $"Formato {contentType} não suportado. Aceita JPG/PNG/WebP/GIF.");

        // SHA256 dedup tal como PDFs — sintetiza ".jpg" ou ".png" para storage path.
        var sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(imageBytes)).ToLowerInvariant();
        var existing = await _repo.FindBySha256Async(tenantId, sha256, ct);
        if (existing is not null)
        {
            _logger.LogInformation("SupplierInvoice (photo) duplicate ignored: {Sha}", sha256);
            return new SupplierInvoiceImportResult(
                existing.Id, existing.Status, existing.FornecedorNameRaw, existing.FornecedorId,
                existing.ParsedTotalCents, existing.ParsedDocumentNumber, existing.PdfRelativePath, WasDuplicate: true);
        }

        // Chama Claude Vision.
        var llm = await _llmParser.ParseImageAsync(imageBytes, contentType, ct);

        // Tenta também fingerprinting a partir de filename (provavelmente useless mas barato).
        var fp = await _fingerprinting.DetectAsync(emailMeta: null, pdfText: llm?.SupplierName, filename: fileName, ct);
        var fornecedorNameRaw = llm?.SupplierName ?? fp.Name ?? fornecedorHint;
        Guid? fornecedorId = fp.FornecedorId;
        if (fornecedorId is null && !string.IsNullOrWhiteSpace(fornecedorNameRaw))
        {
            var existingForn = await _fornecedores.FindByNameAsync(fornecedorNameRaw, ct);
            fornecedorId = existingForn?.Id;
        }

        var supplierSlug = fornecedorNameRaw ?? "foto-papel";
        var docDate = llm?.DocumentDate ?? DateTime.UtcNow;
        var ext = contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png"
            : contentType.Contains("webp", StringComparison.OrdinalIgnoreCase) ? ".webp"
            : contentType.Contains("gif", StringComparison.OrdinalIgnoreCase) ? ".gif"
            : ".jpg";
        var filename = $"foto-{docDate:yyyy-MM-dd}_{sha256[..8]}{ext}";
        var relativePath = await _storage.SaveAsync(tenantId, supplierSlug, docDate, filename, imageBytes, ct);

        var entity = new SupplierInvoiceImport
        {
            TenantId = tenantId,
            FornecedorId = fornecedorId,
            FornecedorNameRaw = fornecedorNameRaw,
            PdfSha256 = sha256,
            PdfRelativePath = relativePath,
            ParsedTotalCents = llm?.TotalCents,
            ParsedDocumentNumber = llm?.OrderId,
            ParsedDocumentDate = llm?.DocumentDate,
            ParsedItemsJson = llm?.Items is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(
                llm.Items.Select(i => new SupplierPdfItem(i.Description, i.Quantity, i.LineTotalCents)).ToList()) : null,
            ParseConfidence = llm is null ? "None"
                : llm.Confidence >= 0.7 ? "High"
                : "Low",
            Status = llm is null || llm.Items.Count == 0
                ? SupplierInvoiceImportStatus.Failed
                : SupplierInvoiceImportStatus.Pending,
            EmailSubject = $"Foto papel: {fileName}",
            EmailFrom = "manual-upload-photo@repairdesk",
            EmailReceivedAt = DateTime.UtcNow,
        };

        await _repo.AddAsync(entity, ct);
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Create, nameof(SupplierInvoiceImport), entity.Id, new
        {
            operation = "supplier_invoice_photo_ingest",
            fornecedor = fornecedorNameRaw,
            items = llm?.Items.Count ?? 0,
            confidence = entity.ParseConfidence,
        }, ct: ct);

        _logger.LogInformation("SupplierInvoice photo ingested: id={Id} sha={Sha} fornecedor={F} items={C} confidence={Conf}",
            entity.Id, sha256[..8], fornecedorNameRaw, llm?.Items.Count ?? 0, entity.ParseConfidence);

        return new SupplierInvoiceImportResult(
            entity.Id, entity.Status, fornecedorNameRaw, fornecedorId,
            llm?.TotalCents, llm?.OrderId, relativePath, WasDuplicate: false);
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
    /// Sprint 163d: gera SKU descritivo único para Part nova criada via aprovação de fatura.
    /// Padrão: [ItemType3]-[Brand3]-[Model]-[NNNN]. Ex:
    /// "Battery iPhone 12/12 Pro" → "BAT-APL-12-0001"
    /// "Touch+Display Huawei P20 Lite" → "LCD-HUA-P20L-0001"
    /// "PELICULA DE VIDRO HUAWEI P20 LITE" → "FILM-HUA-P20L-0001"
    /// "Capa Samsung A15" → "CASE-SAM-A15-0001"
    /// Se não detectar nada → "PART-XXXX-NNNN" com chars da description.
    ///
    /// Adapta-se ao guia https://blog.skuvault.com/sku-generator/ adaptado para tech parts:
    /// general-to-specific, < 16 chars typical, sem caracteres especiais (só hífen).
    /// </summary>
    private async Task<string> GenerateAutoSkuAsync(string description, CancellationToken ct)
    {
        var desc = description.ToUpperInvariant();
        var itemType = DetectItemType(desc);
        var brand = DetectBrand(desc);
        var model = DetectModel(desc);

        var parts = new List<string> { itemType };
        if (!string.IsNullOrEmpty(brand)) parts.Add(brand);
        if (!string.IsNullOrEmpty(model)) parts.Add(model);
        var prefix = string.Join("-", parts);
        if (prefix.Length < 3 || prefix == "PART")
        {
            // Fallback se não detectou — 4 primeiras letras da descrição.
            var letters = new string(description.Where(char.IsLetterOrDigit).Take(4).ToArray()).ToUpperInvariant();
            prefix = letters.Length >= 3 ? letters : "PART";
        }

        for (var i = 1; i <= 9999; i++)
        {
            var candidate = $"{prefix}-{i:D4}";
            if (!await _parts.SkuExistsAsync(candidate, null, ct)) return candidate;
        }
        throw new InvalidOperationException($"Esgotou {prefix}-NNNN — escolhe SKU manualmente.");
    }

    private static string DetectItemType(string descUpper)
    {
        if (descUpper.Contains("BATTERY") || descUpper.Contains("BATERIA")) return "BAT";
        if (descUpper.Contains("TOUCH") || descUpper.Contains("DISPLAY") || descUpper.Contains("LCD") || descUpper.Contains("ECRA") || descUpper.Contains("ECRÃ") || descUpper.Contains("ECRAN")) return "LCD";
        if (descUpper.Contains("CAMERA") || descUpper.Contains("CÂMARA") || descUpper.Contains("CAMARA")) return "CAM";
        if (descUpper.Contains("CABLE") || descUpper.Contains("CABO")) return "CAB";
        if (descUpper.Contains("CASE") || descUpper.Contains("CAPA") || descUpper.Contains("COVER")) return "CASE";
        if (descUpper.Contains("FILM") || descUpper.Contains("PELICULA") || descUpper.Contains("PELÍCULA") || descUpper.Contains("VIDRO") || descUpper.Contains("GLASS")) return "FILM";
        if (descUpper.Contains("CHARGER") || descUpper.Contains("CARREGADOR")) return "CHRG";
        if (descUpper.Contains("SPEAKER") || descUpper.Contains("ALTIFALANTE") || descUpper.Contains("BUZZER")) return "SPK";
        if (descUpper.Contains("CONNECTOR") || descUpper.Contains("CONECTOR") || descUpper.Contains("PORT")) return "PORT";
        if (descUpper.Contains("FRAME") || descUpper.Contains("HOUSING") || descUpper.Contains("CHASSI")) return "HSG";
        if (descUpper.Contains("BUTTON") || descUpper.Contains("BOTAO") || descUpper.Contains("BOTÃO")) return "BTN";
        if (descUpper.Contains("TOOL") || descUpper.Contains("FERRAMENTA")) return "TOOL";
        return "PART";
    }

    private static string DetectBrand(string descUpper)
    {
        if (descUpper.Contains("IPHONE") || descUpper.Contains("APPLE") || descUpper.Contains("IPAD") || descUpper.Contains("MACBOOK") || descUpper.Contains("AIRPOD")) return "APL";
        if (descUpper.Contains("SAMSUNG") || descUpper.Contains("GALAXY")) return "SAM";
        if (descUpper.Contains("XIAOMI") || descUpper.Contains("REDMI") || descUpper.Contains("POCO")) return "XIA";
        if (descUpper.Contains("HUAWEI") || descUpper.Contains("HONOR")) return "HUA";
        if (descUpper.Contains("OPPO")) return "OPP";
        if (descUpper.Contains("ONEPLUS")) return "ONE";
        if (descUpper.Contains("MOTOROLA") || System.Text.RegularExpressions.Regex.IsMatch(descUpper, @"\bMOTO\b")) return "MOT";
        if (descUpper.Contains("NOKIA")) return "NOK";
        if (descUpper.Contains("REALME")) return "RLM";
        if (descUpper.Contains("ASUS")) return "ASU";
        if (descUpper.Contains("LG ") || descUpper.Contains(" LG") || descUpper.StartsWith("LG")) return "LG";
        return "";
    }

    private static string DetectModel(string descUpper)
    {
        // 1) Modelo claro tipo "iPhone 12 Pro Max", "Galaxy S24 Ultra", "Redmi Note 12"
        var m = System.Text.RegularExpressions.Regex.Match(
            descUpper,
            @"(?:IPHONE|GALAXY|REDMI|NOTE|POCO|HONOR|HUAWEI|OPPO|ONEPLUS|MOTO|MACBOOK|IPAD)\s+([A-Z]?\d{1,3}[A-Z]*(?:\s+(?:PRO|LITE|ULTRA|MAX|PLUS|MINI|FE|TE|S|SE))*)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && m.Groups.Count > 1)
        {
            var raw = m.Groups[1].Value;
            // Compress: "12 Pro Max" → "12PM", "S24 Ultra" → "S24U", "Note 12" → "N12"
            var compressed = System.Text.RegularExpressions.Regex.Replace(raw, @"\s+(PRO|LITE|ULTRA|MAX|PLUS|MINI|FE|TE|SE)", m => m.Value.Substring(1, 1));
            compressed = System.Text.RegularExpressions.Regex.Replace(compressed, @"\s+", "");
            return compressed.Length > 8 ? compressed[..8] : compressed;
        }
        // 2) Fallback: token tipo "P20", "S24", "A15" directamente sem brand prefix
        var m2 = System.Text.RegularExpressions.Regex.Match(descUpper, @"\b([A-Z]{1,3}\d{1,3}[A-Z]?)\b");
        if (m2.Success) return m2.Groups[1].Value;
        return "";
    }

    /// <summary>
    /// Sprint 163b: re-corre o pipeline parser → fingerprint → LLM numa importação existente.
    /// Útil quando LLM agora está configurado mas o ingest original tinha items lixo (parser
    /// genérico), ou quando Bruno actualizou Fornecedor.MatchPatternsJson.
    ///
    /// NÃO duplica row — actualiza in-place: fornecedor + items + totals + confidence.
    /// Reseta Status para Pending para Bruno rever de novo. Audit log da operação.
    /// </summary>
    public async Task<SupplierInvoiceImportDto> ReprocessAsync(Guid importId, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        var entity = await _repo.FindByIdAsync(importId, ct) ?? throw new NotFoundException("SupplierInvoiceImport", importId);
        if (entity.TenantId != tenantId) throw new ForbiddenException("cross_tenant", "Não autorizado.");

        var pdfBytes = await _storage.ReadAsync(entity.PdfRelativePath, ct);

        // Re-run parser + fingerprint + LLM (mesma lógica do IngestAsync sem o storage save).
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
            _logger.LogWarning(ex, "Reprocess: parser failed");
        }

        Guid? fornecedorId = null;
        var fornecedorNameRaw = parsed?.SupplierName;
        if (string.IsNullOrWhiteSpace(fornecedorNameRaw))
        {
            var pdfFirstChars = rawText is { Length: > 500 } ? rawText[..500] : rawText;
            var fp = await _fingerprinting.DetectAsync(emailMeta: null, pdfFirstChars, filename: null, ct);
            if (fp.Code is not null)
            {
                fornecedorNameRaw = fp.Name;
                fornecedorId = fp.FornecedorId;
            }
        }
        if (fornecedorId is null && !string.IsNullOrWhiteSpace(fornecedorNameRaw))
        {
            var existingForn = await _fornecedores.FindByNameAsync(fornecedorNameRaw, ct);
            fornecedorId = existingForn?.Id;
        }

        var llmShouldFire = !string.IsNullOrWhiteSpace(rawText)
            && _llmParser.IsConfigured
            && (parsed?.Items is null || parsed.Items.Count == 0 || parsed.Confidence != ParseConfidence.High);
        if (llmShouldFire)
        {
            var llm = await _llmParser.ParseAsync(rawText!, ct);
            if (llm is not null && llm.Items.Count > 0)
            {
                parsed = new SupplierPdfParseResult(
                    SupplierName: parsed?.SupplierName ?? llm.SupplierName ?? fornecedorNameRaw,
                    OrderId: parsed?.OrderId ?? llm.OrderId,
                    TotalCents: parsed?.TotalCents ?? llm.TotalCents,
                    DateAdded: parsed?.DateAdded ?? llm.DocumentDate,
                    Confidence: llm.Confidence >= 0.7 ? ParseConfidence.High : ParseConfidence.Low,
                    Items: llm.Items.Select(i => new SupplierPdfItem(
                        Description: i.Description,
                        Quantity: i.Quantity,
                        LineTotalCents: i.LineTotalCents)).ToList());
                if (string.IsNullOrWhiteSpace(fornecedorNameRaw)) fornecedorNameRaw = llm.SupplierName;
            }
        }

        // Sprint 171: validation pós-parse.
        IReadOnlyList<string> reprocessWarnings;
        (parsed, reprocessWarnings) = ParseValidator.Apply(parsed);

        // Update in-place.
        entity.FornecedorNameRaw = fornecedorNameRaw;
        entity.FornecedorId = fornecedorId;
        entity.ParsedTotalCents = parsed?.TotalCents;
        entity.ParsedDocumentNumber = parsed?.OrderId;
        entity.ParsedDocumentDate = parsed?.DateAdded;
        entity.ParsedItemsJson = parsed?.Items is { Count: > 0 } items
            ? System.Text.Json.JsonSerializer.Serialize(items)
            : null;
        entity.ParseConfidence = parsed?.Confidence.ToString();
        entity.ParseWarningsJson = reprocessWarnings.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(reprocessWarnings) : null;
        entity.Status = SupplierInvoiceImportStatus.Pending;
        entity.ProcessedAt = null;
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Update, nameof(SupplierInvoiceImport), entity.Id, new
        {
            operation = "supplier_invoice_reprocess",
            fornecedor = fornecedorNameRaw,
            items = parsed?.Items.Count ?? 0,
            confidence = parsed?.Confidence.ToString(),
        }, ct: ct);

        _logger.LogInformation("Reprocess: id={Id} fornecedor={F} items={C} confidence={Conf}",
            entity.Id, fornecedorNameRaw, parsed?.Items.Count ?? 0, parsed?.Confidence);
        return ToDto(entity);
    }

    /// <summary>
    /// Sprint 163b: lista importações HISTÓRICAS (Approved + Rejected + Failed) para tab
    /// "Histórico" na UI. Bruno vê o que já processou + pode chamar Reprocess.
    /// </summary>
    public async Task<IReadOnlyList<SupplierInvoiceImportDto>> ListHistoryAsync(int take, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        var entities = await _repo.ListHistoryAsync(tenantId, take, ct);
        return entities.Select(ToDto).ToList();
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

        var despesasCriadas = 0;
        foreach (var item in req.Items)
        {
            if (string.Equals(item.Action, "skip", StringComparison.OrdinalIgnoreCase)) continue;

            // Sprint 181: action="despesa" cria Despesa avulsa (sem PartMovimento).
            // Para itens que NÃO entram em inventário (ferramentas, serviços, portes pagos
            // ou peças usadas directamente sem passar por stock).
            if (string.Equals(item.Action, "despesa", StringComparison.OrdinalIgnoreCase))
            {
                await _despesas.CreateAsync(new Despesas.CreateDespesaRequest(
                    Descricao: item.Description.Length > 200 ? item.Description[..200] : item.Description,
                    Categoria: DespesaCategoria.Pecas,
                    ValorCents: item.Quantity * item.UnitCostCents,
                    Data: entity.ParsedDocumentDate ?? DateTime.UtcNow,
                    Fornecedor: entity.Fornecedor?.Name ?? entity.FornecedorNameRaw,
                    NumeroEncomenda: entity.ParsedDocumentNumber,
                    Notas: notas,
                    TrabalhoId: null,
                    ReparacaoId: null,
                    IsCogs: false), ct);
                despesasCriadas++;
                continue;
            }

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
                if (string.IsNullOrWhiteSpace(item.NewName))
                    throw new ValidationException("missing_new_fields", $"Item '{item.Description}': falta NewName para criar Part.");
                // Sprint 163d: SKU opcional — auto-gera se vazio (consistente com PartService.CreateAsync).
                // Padrão {PREFIX4}-{NNNN} a partir das primeiras 4 letras da descrição.
                string sku;
                if (string.IsNullOrWhiteSpace(item.NewSku))
                {
                    sku = await GenerateAutoSkuAsync(item.NewName, ct);
                }
                else
                {
                    sku = item.NewSku.Trim().ToUpperInvariant();
                    if (await _parts.SkuExistsAsync(sku, null, ct))
                        throw new ConflictException("part_sku_exists", $"Já existe uma Part com SKU '{sku}'.");
                }
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
                throw new ValidationException("invalid_action", $"Action '{item.Action}' inválida — usa existing/new/despesa/skip.");
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
