using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class ProdutoLojaConfiguration : IEntityTypeConfiguration<ProdutoLoja>
{
    public void Configure(EntityTypeBuilder<ProdutoLoja> builder)
    {
        builder.ToTable("ProdutosLoja");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Titulo).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Marca).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Modelo).HasMaxLength(80);
        builder.Property(x => x.Cor).HasMaxLength(40);
        builder.Property(x => x.Descricao).HasMaxLength(8000);
        builder.Property(x => x.SpecsJson).HasMaxLength(8000);
        builder.Property(x => x.SeoTitulo).HasMaxLength(70);
        builder.Property(x => x.SeoDescricao).HasMaxLength(170);
        builder.Property(x => x.Categoria).HasConversion<int>();
        builder.Property(x => x.Estado).HasConversion<int>();
        builder.Property(x => x.Grau).HasConversion<int?>();
        builder.Property(x => x.Origem).HasConversion<int>();
        builder.Property(x => x.Disponibilidade).HasConversion<int>();
        builder.Property(x => x.RegimeIva).HasConversion<int>();
        builder.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Publicado });
        builder.HasMany(x => x.Fotos).WithOne(f => f.Produto).HasForeignKey(f => f.ProdutoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProdutoLojaFotoConfiguration : IEntityTypeConfiguration<ProdutoLojaFoto>
{
    public void Configure(EntityTypeBuilder<ProdutoLojaFoto> builder)
    {
        builder.ToTable("ProdutosLojaFotos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StorageKey).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Alt).HasMaxLength(200);
        builder.HasIndex(x => new { x.ProdutoId, x.Ordem });
    }
}
