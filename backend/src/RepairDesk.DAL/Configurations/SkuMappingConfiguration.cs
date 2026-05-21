using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class SkuMappingConfiguration : IEntityTypeConfiguration<SkuMapping>
{
    public void Configure(EntityTypeBuilder<SkuMapping> builder)
    {
        builder.ToTable("SkuMappings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.SupplierCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SupplierSku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SupplierProductName).HasMaxLength(500);
        builder.Property(x => x.TargetType).HasConversion<int>();
        builder.Property(x => x.Confidence).HasConversion<int>();
        builder.Property(x => x.Notas).HasMaxLength(500);

        // Lookup principal — (tenant, supplier, sku) único. UPSERT ao aprovar.
        builder.HasIndex(x => new { x.TenantId, x.SupplierCode, x.SupplierSku })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Lookup por target — quando Bruno apaga uma Part, queremos invalidar mappings.
        builder.HasIndex(x => new { x.TenantId, x.TargetType, x.TargetId });

        builder.HasOne(x => x.CreatedFromImport)
            .WithMany()
            .HasForeignKey(x => x.CreatedFromImportId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
