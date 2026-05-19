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

            await DeleteAsync(deleted, "vendaItems", _db.VendaItems.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "vendas", _db.Vendas.IgnoreQueryFilters(), ct);

            await DeleteAsync(deleted, "partMovimentos", _db.PartMovimentos.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "parts", _db.Parts.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "priceTableEntries", _db.PriceTableEntries.IgnoreQueryFilters(), ct);

            await DeleteAsync(deleted, "reparacaoFotos", _db.ReparacaoFotos.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "garantias", _db.Garantias.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "avaliacoes", _db.Avaliacoes.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "diagnosticoExecucaoItems", _db.DiagnosticoExecucaoItems.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "diagnosticoExecucoes", _db.DiagnosticoExecucoes.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "equipmentFieldValues", _db.EquipmentFieldValues.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "reparacaoEstadoLogs", _db.ReparacaoEstadoLogs.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "despesas", _db.Despesas.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "trabalhos", _db.Trabalhos.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "reparacoes", _db.Reparacoes.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "clientes", _db.Clientes.IgnoreQueryFilters(), ct);
            await DeleteAsync(deleted, "tenantBillingSettings", _db.TenantBillingSettings.IgnoreQueryFilters(), ct);

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
