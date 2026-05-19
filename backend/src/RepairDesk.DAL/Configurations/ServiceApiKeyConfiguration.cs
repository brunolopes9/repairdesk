using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class ServiceApiKeyConfiguration : IEntityTypeConfiguration<ServiceApiKey>
{
    public void Configure(EntityTypeBuilder<ServiceApiKey> builder)
    {
        builder.ToTable("ServiceApiKeys");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.KeyPrefix).HasMaxLength(32).IsRequired();
        builder.Property(x => x.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RevokedReason).HasMaxLength(500);

        // Lookup principal — hash deve ser único.
        builder.HasIndex(x => x.KeyHash).IsUnique().HasFilter("[IsDeleted] = 0");
        // Listing por tenant.
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}
