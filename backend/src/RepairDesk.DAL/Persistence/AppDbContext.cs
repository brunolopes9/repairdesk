using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
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
                var tenantIdExpr = Expression.Property(
                    Expression.Constant(this),
                    nameof(CurrentTenantId));
                var tenantCheck = Expression.OrElse(
                    Expression.Equal(tenantIdExpr, Expression.Constant(null, typeof(Guid?))),
                    Expression.Equal(prop, Expression.Convert(tenantIdExpr, typeof(Guid))));
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
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditFields();
        EnforceTenantOnInsert();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        EnforceTenantOnInsert();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
                case EntityState.Deleted when entry.Entity is BaseEntity:
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
}
