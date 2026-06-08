using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class ShopConditionImageConfiguration : IEntityTypeConfiguration<ShopConditionImage>
{
    public void Configure(EntityTypeBuilder<ShopConditionImage> builder)
    {
        builder.ToTable("ShopConditionImages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Grade).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.Url480w).HasMaxLength(1024);
        builder.Property(x => x.Url1024w).HasMaxLength(1024);
        builder.Property(x => x.Url2048w).HasMaxLength(1024);
        builder.Property(x => x.BlurDataUrl).HasMaxLength(8000);
        builder.Property(x => x.Alt).HasMaxLength(300);

        // 1 imagem por grau, por tenant.
        builder.HasIndex(x => new { x.TenantId, x.Grade }).IsUnique();
    }
}
