using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>, IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Brand).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Storage).HasMaxLength(50);
        builder.Property(x => x.Color).HasMaxLength(50);
        builder.Property(x => x.Grading).HasConversion<int>();
        builder.Property(x => x.SupplyType).HasConversion<int>();
        builder.Property(x => x.Category).HasConversion<int>();
        builder.Property(x => x.DropshipSupplierSku).HasMaxLength(100);
        // Sprint 305: grade raw do fornecedor (preservado sem normalizar). Max 16 chars cobre
        // todos os formatos conhecidos (Molano "A+", GSM Brokers "A Premium", etc).
        builder.Property(x => x.SupplierGrade).HasMaxLength(16);
        builder.Property(x => x.OpenBoxReason).HasMaxLength(500);
        builder.Property(x => x.DescriptionMarkdown).HasColumnType("nvarchar(max)");
        builder.Property(x => x.AttributesJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.SeoTitle).HasMaxLength(200);
        builder.Property(x => x.SeoDescription).HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique().HasFilter("[IsDeleted] = 0");
        // Sprint 151: dedupe por SKU do fornecedor — importer CSV Molano usa para upsert
        // idempotente (re-importar mesmo CSV não duplica). Unique filtrado para não exigir
        // todos os produtos próprios terem DropshipSupplierSku (nullable).
        builder.HasIndex(x => new { x.TenantId, x.FornecedorId, x.DropshipSupplierSku })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [DropshipSupplierSku] IS NOT NULL");
        // Lookup principal — listagem de catálogo na loja: active + mostrarLojaOnline.
        builder.HasIndex(x => new { x.TenantId, x.Active, x.MostrarLojaOnline });
        builder.HasIndex(x => new { x.TenantId, x.Brand, x.Model });
        // Sprint 151: filtros loja por categoria (Phone vs Accessory).
        builder.HasIndex(x => new { x.TenantId, x.Category, x.MostrarLojaOnline });

        builder.HasOne(x => x.Fornecedor)
            .WithMany()
            .HasForeignKey(x => x.FornecedorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Alt).HasMaxLength(200);
        builder.HasIndex(x => new { x.ProductId, x.Ordem });
    }
}
