using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class CompraDocumentoConfiguration : IEntityTypeConfiguration<CompraDocumento>
{
    public void Configure(EntityTypeBuilder<CompraDocumento> builder)
    {
        builder.ToTable("ComprasDocumentos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.NumeroFatura).HasMaxLength(100);
        builder.Property(x => x.NumerosEncomenda).HasMaxLength(300);
        builder.Property(x => x.MetodoPagamento).HasMaxLength(50);
        builder.Property(x => x.Notas).HasMaxLength(2000);
        // Euros com 4 casas decimais (SPEC §3.4) — arredondamento só na apresentação.
        builder.Property(x => x.PortesPagos).HasPrecision(12, 4);
        builder.Property(x => x.PortesIva).HasPrecision(12, 4);
        builder.Property(x => x.TotalDocumento).HasPrecision(12, 4);
        builder.Ignore(x => x.FaturaEmFalta);

        builder.HasOne(x => x.Fornecedor)
            .WithMany()
            .HasForeignKey(x => x.FornecedorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Linhas)
            .WithOne(x => x.Documento)
            .HasForeignKey(x => x.CompraDocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Anti-duplicados e listagens: por fornecedor + nº fatura, e por data.
        builder.HasIndex(x => new { x.TenantId, x.FornecedorId, x.NumeroFatura });
        builder.HasIndex(x => new { x.TenantId, x.Data });
        // Sprint 560: nº visível único por tenant; e um PDF importado só documenta uma compra.
        builder.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
        builder.HasIndex(x => x.SupplierInvoiceImportId).IsUnique()
            .HasFilter("[SupplierInvoiceImportId] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public class CompraLinhaConfiguration : IEntityTypeConfiguration<CompraLinha>
{
    public void Configure(EntityTypeBuilder<CompraLinha> builder)
    {
        builder.ToTable("ComprasLinhas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Localizacao).HasMaxLength(100);
        builder.Property(x => x.PrecoUnitarioPago).HasPrecision(12, 4);
        builder.Property(x => x.TaxaIvaCompra).HasPrecision(5, 4);
        builder.Property(x => x.LucroUnitario).HasPrecision(12, 4);
        builder.Ignore(x => x.QuantidadeEmStock);

        builder.HasIndex(x => new { x.TenantId, x.CompraDocumentoId });
        builder.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
    }
}
