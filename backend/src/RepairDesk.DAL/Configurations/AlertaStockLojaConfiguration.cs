using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class AlertaStockLojaConfiguration : IEntityTypeConfiguration<AlertaStockLoja>
{
    public void Configure(EntityTypeBuilder<AlertaStockLoja> builder)
    {
        builder.ToTable("AlertasStockLoja");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(160).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Slug, x.Email }).IsUnique();
    }
}
