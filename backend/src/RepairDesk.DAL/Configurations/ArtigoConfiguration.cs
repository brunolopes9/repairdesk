using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class ArtigoConfiguration : IEntityTypeConfiguration<Artigo>
{
    public void Configure(EntityTypeBuilder<Artigo> builder)
    {
        builder.ToTable("Artigos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CodigoBarras).HasMaxLength(32).IsRequired();
        builder.Property(x => x.CodigoFabricante).HasMaxLength(32);
        builder.Property(x => x.Categoria).HasMaxLength(80);
        builder.Property(x => x.Localizacao).HasMaxLength(100);
        builder.Property(x => x.Notas).HasMaxLength(1000);

        builder.HasMany(x => x.Lotes)
            .WithOne(x => x.Artigo)
            .HasForeignKey(x => x.ArtigoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Únicos por tenant (a pistola tem de dar sempre o mesmo artigo).
        builder.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CodigoBarras }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.CodigoFabricante }).IsUnique()
            .HasFilter("[CodigoFabricante] IS NOT NULL AND [IsDeleted] = 0");
    }
}
