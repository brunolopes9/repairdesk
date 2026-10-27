using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class CategoriaArtigoConfiguration : IEntityTypeConfiguration<CategoriaArtigo>
{
    public void Configure(EntityTypeBuilder<CategoriaArtigo> builder)
    {
        builder.ToTable("CategoriasArtigo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Nome).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Nome }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
