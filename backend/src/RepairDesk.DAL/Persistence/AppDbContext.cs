using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser? _currentUser;
    private bool _hardDeleteMode;
    private bool _suppressAudit;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext, ICurrentUser? currentUser = null)
        : base(options)
    {
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantBillingSettings> TenantBillingSettings => Set<TenantBillingSettings>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Reparacao> Reparacoes => Set<Reparacao>();
    public DbSet<ReparacaoEstadoLog> ReparacaoEstadoLogs => Set<ReparacaoEstadoLog>();
    public DbSet<Trabalho> Trabalhos => Set<Trabalho>();
    public DbSet<Despesa> Despesas => Set<Despesa>();
    public DbSet<DiagnosticoTemplate> DiagnosticoTemplates => Set<DiagnosticoTemplate>();
    public DbSet<DiagnosticoTemplateItem> DiagnosticoTemplateItems => Set<DiagnosticoTemplateItem>();
    public DbSet<DiagnosticoExecucao> DiagnosticoExecucoes => Set<DiagnosticoExecucao>();
    public DbSet<DiagnosticoExecucaoItem> DiagnosticoExecucaoItems => Set<DiagnosticoExecucaoItem>();
    public DbSet<Garantia> Garantias => Set<Garantia>();
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();
    public DbSet<PriceTableEntry> PriceTableEntries => Set<PriceTableEntry>();
    public DbSet<ReparacaoFoto> ReparacaoFotos => Set<ReparacaoFoto>();
    public DbSet<EquipmentFieldTemplate> EquipmentFieldTemplates => Set<EquipmentFieldTemplate>();
    public DbSet<EquipmentFieldDefinition> EquipmentFieldDefinitions => Set<EquipmentFieldDefinition>();
    public DbSet<EquipmentFieldValue> EquipmentFieldValues => Set<EquipmentFieldValue>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<PartMovimento> PartMovimentos => Set<PartMovimento>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<AppUser>().ToTable("Auth_Users");
        modelBuilder.Entity<AppRole>().ToTable("Auth_Roles");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("Auth_UserRoles");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("Auth_UserClaims");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("Auth_UserLogins");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("Auth_UserTokens");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("Auth_RoleClaims");

        ApplyGlobalFilters(modelBuilder);
    }

    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            var isSoftDelete = typeof(BaseEntity).IsAssignableFrom(clrType);
            var isMultiTenant = typeof(ITenantEntity).IsAssignableFrom(clrType);

            if (!isSoftDelete && !isMultiTenant) continue;

            var parameter = Expression.Parameter(clrType, "e");
            Expression? body = null;

            if (isSoftDelete)
            {
                var prop = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                body = Expression.Equal(prop, Expression.Constant(false));
            }

            if (isMultiTenant)
            {
                var prop = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                var propAsNullable = Expression.Convert(prop, typeof(Guid?));
                var tenantIdExpr = Expression.Property(
                    Expression.Constant(this),
                    nameof(CurrentTenantId));
                var tenantCheck = Expression.OrElse(
                    Expression.Equal(tenantIdExpr, Expression.Constant(null, typeof(Guid?))),
                    Expression.Equal(propAsNullable, tenantIdExpr));
                body = body is null ? tenantCheck : Expression.AndAlso(body, tenantCheck);
            }

            if (body is null) continue;

            var lambda = Expression.Lambda(body, parameter);
            modelBuilder.Entity(clrType).HasQueryFilter(lambda);
        }
    }

    public Guid? CurrentTenantId => _tenantContext.TenantId;

    public override int SaveChanges()
    {
        StampAuditFields();
        EnforceTenantOnInsert();
        var auditEntries = CaptureAuditEntries();
        var result = base.SaveChanges();
        PersistAuditEntries(auditEntries);
        return result;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditFields();
        EnforceTenantOnInsert();
        var auditEntries = CaptureAuditEntries();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        PersistAuditEntries(auditEntries);
        return result;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        EnforceTenantOnInsert();
        var auditEntries = CaptureAuditEntries();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await PersistAuditEntriesAsync(auditEntries, cancellationToken);
        return result;
    }

    public IDisposable HardDeleteScope()
    {
        var previousHardDelete = _hardDeleteMode;
        var previousSuppressAudit = _suppressAudit;
        _hardDeleteMode = true;
        _suppressAudit = true;
        return new Scope(() =>
        {
            _hardDeleteMode = previousHardDelete;
            _suppressAudit = previousSuppressAudit;
        });
    }

    private void StampAuditFields()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Deleted when entry.Entity is BaseEntity && !_hardDeleteMode:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }

    private void EnforceTenantOnInsert()
    {
        if (!_tenantContext.HasTenant) return;
        var tenantId = _tenantContext.TenantId!.Value;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = tenantId;
            }
        }
    }

    private List<AuditEntry> CaptureAuditEntries()
    {
        if (_suppressAudit) return new List<AuditEntry>();

        var now = DateTime.UtcNow;
        var entries = new List<AuditEntry>();
        foreach (var entry in ChangeTracker.Entries()
                     .Where(e => e.Entity is not AuditEntry && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var tenantId = GetTenantId(entry);
            if (tenantId is null) continue;

            var entityId = GetEntityId(entry);
            var action = ResolveAuditAction(entry);
            var changes = BuildChanges(entry, action);
            entries.Add(new AuditEntry
            {
                TenantId = tenantId.Value,
                AppUserId = _currentUser?.UserId,
                Action = action,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entityId,
                ChangesJson = changes.Count == 0 ? null : JsonSerializer.Serialize(changes),
                IpAddress = _currentUser?.IpAddress,
                UserAgent = _currentUser?.UserAgent,
                CreatedAt = now,
            });
        }
        return entries;
    }

    private void PersistAuditEntries(IReadOnlyList<AuditEntry> auditEntries)
    {
        if (auditEntries.Count == 0) return;
        try
        {
            _suppressAudit = true;
            AuditEntries.AddRange(auditEntries);
            base.SaveChanges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Failed to persist audit entries: {ex}");
        }
        finally
        {
            _suppressAudit = false;
        }
    }

    private async Task PersistAuditEntriesAsync(IReadOnlyList<AuditEntry> auditEntries, CancellationToken ct)
    {
        if (auditEntries.Count == 0) return;
        try
        {
            _suppressAudit = true;
            AuditEntries.AddRange(auditEntries);
            await base.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Failed to persist audit entries: {ex}");
        }
        finally
        {
            _suppressAudit = false;
        }
    }

    private Guid? GetTenantId(EntityEntry entry)
    {
        if (entry.Entity is ITenantEntity tenantEntity && tenantEntity.TenantId != Guid.Empty)
            return tenantEntity.TenantId;
        return _tenantContext.TenantId;
    }

    private static Guid? GetEntityId(EntityEntry entry)
    {
        var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");
        return prop?.CurrentValue is Guid id ? id : prop?.OriginalValue is Guid original ? original : null;
    }

    private static AuditAction ResolveAuditAction(EntityEntry entry)
    {
        if (entry.State == EntityState.Added) return AuditAction.Create;
        if (entry.State == EntityState.Deleted) return AuditAction.Delete;
        if (entry.Properties.Any(p => p.Metadata.Name == nameof(BaseEntity.IsDeleted) && p.IsModified && p.CurrentValue is true))
            return AuditAction.Delete;
        return AuditAction.Update;
    }

    private static Dictionary<string, object?> BuildChanges(EntityEntry entry, AuditAction action)
    {
        var result = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            if (IsSensitiveAuditProperty(name)) continue;
            if (prop.Metadata.IsPrimaryKey()) continue;
            if (action == AuditAction.Update && !prop.IsModified) continue;

            result[name] = action switch
            {
                AuditAction.Create => new { current = prop.CurrentValue },
                AuditAction.Delete => new { original = prop.OriginalValue, current = prop.CurrentValue },
                _ => new { original = prop.OriginalValue, current = prop.CurrentValue },
            };
        }
        return result;
    }

    private static bool IsSensitiveAuditProperty(string name)
    {
        var normalized = name.ToLowerInvariant();
        return normalized.Contains("password")
               || normalized.Contains("apikey")
               || normalized.Contains("secret")
               || normalized.Contains("token")
               || normalized.Contains("securitystamp")
               || normalized.Contains("concurrencystamp");
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public Scope(Action onDispose) => _onDispose = onDispose;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onDispose();
        }
    }
}
