using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class VendaFotoConfiguration : IEntityTypeConfiguration<VendaFoto>
{
    public void Configure(EntityTypeBuilder<VendaFoto> builder)
    {
        builder.ToTable("VendaFotos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.VendaId).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Tipo).HasConversion<int>();
        builder.Property(x => x.Legenda).HasMaxLength(500);

        builder.HasOne(x => x.Venda)
            .WithMany()
            .HasForeignKey(x => x.VendaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.VendaId, x.Tipo, x.Ordem });
        builder.HasIndex(x => x.StorageKey).IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
