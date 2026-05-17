using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class PartConfiguration : IEntityTypeConfiguration<Part>
{
    public void Configure(EntityTypeBuilder<Part> builder)
    {
        builder.ToTable("Parts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(80);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Categoria).HasConversion<int>();
        builder.Property(x => x.Marca).HasMaxLength(100);
        builder.Property(x => x.Modelo).HasMaxLength(140);
        builder.Property(x => x.Fornecedor).HasMaxLength(200);
        builder.Property(x => x.LocalArmazenamento).HasMaxLength(120);
        builder.Property(x => x.Notas).HasMaxLength(1000);

        builder.HasOne(x => x.PriceTableEntry)
            .WithMany()
            .HasForeignKey(x => x.PriceTableEntryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.TenantId, x.Sku })
            .IsUnique()
            .HasFilter("[Sku] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.Categoria, x.Marca });
        builder.HasIndex(x => new { x.TenantId, x.QtdStock, x.QtdMinima });
        builder.HasIndex(x => new { x.TenantId, x.PriceTableEntryId }).HasFilter("[PriceTableEntryId] IS NOT NULL");
    }
}
