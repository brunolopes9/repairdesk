using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class PriceTableEntryConfiguration : IEntityTypeConfiguration<PriceTableEntry>
{
    public void Configure(EntityTypeBuilder<PriceTableEntry> builder)
    {
        builder.ToTable("PriceTableEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Categoria).HasConversion<int>();
        builder.Property(x => x.Marca).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Modelo).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Servico).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Notas).HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.Categoria, x.Marca, x.Modelo });
        builder.HasIndex(x => new { x.TenantId, x.Marca, x.Modelo, x.Servico }).IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
