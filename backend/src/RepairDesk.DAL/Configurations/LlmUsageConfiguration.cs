using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class LlmUsageConfiguration : IEntityTypeConfiguration<LlmUsage>
{
    public void Configure(EntityTypeBuilder<LlmUsage> builder)
    {
        builder.ToTable("LlmUsage");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Operation).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(20);

        // Lookup principal — agregar uso do tenant no período. Index inclui CreatedAt
        // (BaseEntity field) implicitamente via ordering filter.
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        // Breakdown por operação.
        builder.HasIndex(x => new { x.TenantId, x.Operation, x.CreatedAt });
    }
}
