using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/e2e")]
[AllowAnonymous]
public sealed class E2eController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILookupNormalizer _normalizer;

    public E2eController(AppDbContext db, IConfiguration configuration, ILookupNormalizer normalizer)
    {
        _db = db;
        _configuration = configuration;
        _normalizer = normalizer;
    }

    /// <summary>
    /// Sprint 690: só em E2E — publica um produto da loja com uma foto fictícia (o armazenamento de fotos R2 não existe
    /// no ambiente de testes; publicar exige pelo menos uma foto).
    /// </summary>
    [HttpPost("loja/publicar/{produtoId:guid}")]
    public async Task<IActionResult> PublicarProdutoLoja(Guid produtoId, CancellationToken ct)
    {
        if (!_configuration.GetValue("E2E:Enabled", false)) return NotFound();
        var expectedKey = _configuration["E2E:ApiKey"];
        if (!string.IsNullOrWhiteSpace(expectedKey)
            && (!Request.Headers.TryGetValue("X-E2E-Key", out var suppliedKey)
                || !string.Equals(suppliedKey.ToString(), expectedKey, StringComparison.Ordinal)))
            return Unauthorized();
        var p = await _db.ProdutosLoja.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == produtoId, ct);
        if (p is null) return NotFound();
        _db.ProdutosLojaFotos.Add(new ProdutoLojaFoto
        {
            TenantId = p.TenantId, ProdutoId = p.Id, Ordem = 0, StorageKey = $"e2e/{p.Id:N}.png",
            ContentType = "image/png", Bytes = 68, Enviada = true,
        });
        p.Publicado = true;
        p.PublicadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("reset")]
    public async Task<ActionResult<E2eResetResponse>> Reset(CancellationToken ct)
    {
        if (!_configuration.GetValue("E2E:Enabled", false))
            return NotFound();

        var expectedKey = _configuration["E2E:ApiKey"];
        if (!string.IsNullOrWhiteSpace(expectedKey)
            && (!Request.Headers.TryGetValue("X-E2E-Key", out var suppliedKey)
                || !string.Equals(suppliedKey.ToString(), expectedKey, StringComparison.Ordinal)))
        {
            return Unauthorized();
        }

        var deleted = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        using (_db.HardDeleteScope())
        {
            await DeleteAsync(deleted, "auditEntries", _db.AuditEntries.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "pushSubscriptions", _db.PushSubscriptions.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "systemSettings", _db.SystemSettings.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "refreshTokens", _db.RefreshTokens.IgnoreQueryFilters(), ct);

            // Sprint 690: loja online (o teste 07 cria encomendas, contas e produtos).
            await DeleteAsync(deleted, "encomendasLojaLinhas", _db.EncomendasLojaLinhas.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "encomendasLoja", _db.EncomendasLoja.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "clientesLojaSessoes", _db.ClientesLojaSessoes.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "clientesLoja", _db.ClientesLoja.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "alertasStockLoja", _db.AlertasStockLoja.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "produtosLojaFotos", _db.ProdutosLojaFotos.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "produtosLoja", _db.ProdutosLoja.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "vendaFotos", _db.VendaFotos.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "vendaAssinaturas", _db.VendaAssinaturas.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "vendaComunicacoes", _db.VendaComunicacoes.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "garantias", _db.Garantias.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "avaliacoes", _db.Avaliacoes.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "vendaEstadoLogs", _db.VendaEstadoLogs.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "vendaItems", _db.VendaItems.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "vendas", _db.Vendas.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "comprasLinhas", _db.ComprasLinhas.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "comprasDocumentos", _db.ComprasDocumentos.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "despesas", _db.Despesas.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "clientes", _db.Clientes.IgnoreQueryFilters(), ct);

            await ResetSeedTenantAsync(ct);
            await ResetSeedAdminAsync(ct);
            await _db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return Ok(new E2eResetResponse(true, DbInitializer.LopesTechTenantId, deleted));
    }

    private async Task ResetSeedTenantAsync(CancellationToken ct)
    {
        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == DbInitializer.LopesTechTenantId, ct);
        if (tenant is null)
        {
            _db.Tenants.Add(new Tenant
            {
                Id = DbInitializer.LopesTechTenantId,
                Name = string.Empty,
                LegalName = null,
                Address = null,
                Email = null,
                Country = "PT",
                PrimaryColor = "#0EA5E9",
                IsActive = true,
                OnboardingCompletado = false,
                RegimeFiscal = RegimeFiscal.IsentoArt53,
                GarantiaDiasDefault = 90,
            });
            return;
        }

        tenant.Name = string.Empty;
        tenant.LegalName = null;
        tenant.Nif = null;
        tenant.Address = null;
        tenant.PostalCode = null;
        tenant.Locality = null;
        tenant.Country = "PT";
        tenant.Phone = null;
        tenant.Email = null;
        tenant.Website = null;
        tenant.Iban = null;
        tenant.CaePrincipal = null;
        tenant.CaeSecundarios = null;
        tenant.RegimeFiscal = RegimeFiscal.IsentoArt53;
        tenant.TermosCondicoes = null;
        tenant.LogoUrl = null;
        tenant.PrimaryColor = "#0EA5E9";
        tenant.IsActive = true;
        tenant.IsDeleted = false;
        tenant.OnboardingCompletado = false;
        tenant.GarantiaDiasDefault = 90;
        tenant.GarantiaCoberturaDefault = null;
        tenant.GarantiaExclusoesDefault = null;
        tenant.GarantiaVendaDiasDefault = 1095;
        tenant.GarantiaVendaCoberturaDefault = null;
        tenant.GarantiaVendaExclusoesDefault = null;
        tenant.GoogleReviewUrl = null;
    }

    private async Task ResetSeedAdminAsync(CancellationToken ct)
    {
        var email = _configuration["Seed:AdminEmail"] ?? "bruno.miguel.martins.lopes@gmail.com";
        var normalizedEmail = _normalizer.NormalizeEmail(email);
        var admin = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);
        if (admin is null)
            return;

        admin.TenantId = DbInitializer.LopesTechTenantId;
        admin.IsActive = true;
        admin.EmailConfirmed = true;
        admin.AccessFailedCount = 0;
        admin.LockoutEnd = null;
        admin.LastLoginAt = null;
        admin.LastLoginIp = null;
        // Só em E2E: a regra «muda a password inicial» (segurança em produção) bloqueava todos os testes de UI no 1.º ecrã.
        admin.RequireChangePasswordOnNextLogin = false;
    }

    private static async Task DeleteAsync<T>(
        IDictionary<string, int> deleted,
        string key,
        IQueryable<T> query,
        CancellationToken ct)
        where T : class
    {
        deleted[key] = await query.ExecuteDeleteAsync(ct);
    }
}

public sealed record E2eResetResponse(
    bool Ok,
    Guid TenantId,
    IReadOnlyDictionary<string, int> Deleted);
