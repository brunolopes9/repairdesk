using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class TenantBillingSettingsConfiguration : IEntityTypeConfiguration<TenantBillingSettings>
{
    public void Configure(EntityTypeBuilder<TenantBillingSettings> builder)
    {
        builder.ToTable("TenantBillingSettings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Provider).HasConversion<int>();
        builder.Property(x => x.DefaultDocumentType).HasConversion<int>();

        builder.Property(x => x.ApiKeyCipherText).HasMaxLength(4000);
        builder.Property(x => x.ClientId).HasMaxLength(300);
        builder.Property(x => x.ClientSecretCipherText).HasMaxLength(4000);
        builder.Property(x => x.RefreshTokenCipherText).HasMaxLength(4000);
        builder.Property(x => x.ExemptionReason).HasMaxLength(20);

        builder.HasOne(x => x.Tenant)
            .WithOne(x => x.BillingSettings)
            .HasForeignKey<TenantBillingSettings>(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}
