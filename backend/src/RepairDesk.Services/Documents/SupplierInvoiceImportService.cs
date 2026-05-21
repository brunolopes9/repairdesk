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
}

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
    string PdfRelativePath);

public sealed class SupplierInvoiceImportService : ISupplierInvoiceImportService
{
    private readonly ISupplierInvoiceImportRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IFornecedorRepository _fornecedores;
    private readonly ISupplierInvoiceStorage _storage;
    private readonly Despesas.IDespesaService _despesas;
    private readonly IAuditLogger _audit;
    private readonly ILogger<SupplierInvoiceImportService> _logger;

    public SupplierInvoiceImportService(
        ISupplierInvoiceImportRepository repo,
        ITenantContext tenant,
        IFornecedorRepository fornecedores,
        ISupplierInvoiceStorage storage,
        Despesas.IDespesaService despesas,
        IAuditLogger audit,
        ILogger<SupplierInvoiceImportService> logger)
    {
        _repo = repo;
        _tenant = tenant;
        _fornecedores = fornecedores;
        _storage = storage;
        _despesas = despesas;
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
        try
        {
            using var pdfStream = new MemoryStream(pdfBytes);
            var extracted = PdfTextExtractor.Extract(pdfStream, "supplier-invoice.pdf");
            parsed = SupplierPdfParser.Parse(extracted.Text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Parser failed on PDF (will store as Failed)");
        }

        // 3. Reconcile fornecedor (raw name → existing entity).
        Guid? fornecedorId = null;
        var fornecedorNameRaw = parsed?.SupplierName;
        if (!string.IsNullOrWhiteSpace(fornecedorNameRaw))
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
        return entities.Select(x => new SupplierInvoiceImportDto(
            x.Id,
            x.FornecedorId,
            x.Fornecedor != null ? x.Fornecedor.Name : x.FornecedorNameRaw,
            x.ParsedDocumentNumber,
            x.ParsedDocumentDate,
            x.ParsedTotalCents,
            x.Status.ToString(),
            x.ParseConfidence,
            x.CreatedAt,
            x.PdfRelativePath)).ToList();
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
        x.PdfRelativePath);

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
