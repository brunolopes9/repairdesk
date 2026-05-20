using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class SupplierInvoiceImportConfiguration : IEntityTypeConfiguration<SupplierInvoiceImport>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceImport> builder)
    {
        builder.ToTable("SupplierInvoiceImports");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.PdfSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PdfRelativePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FornecedorNameRaw).HasMaxLength(200);
        builder.Property(x => x.EmailMessageId).HasMaxLength(500);
        builder.Property(x => x.EmailSubject).HasMaxLength(500);
        builder.Property(x => x.EmailFrom).HasMaxLength(255);
        builder.Property(x => x.ParsedDocumentNumber).HasMaxLength(50);
        builder.Property(x => x.ParseConfidence).HasMaxLength(20);
        builder.Property(x => x.ParsedItemsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<int>();

        builder.HasOne(x => x.Fornecedor)
            .WithMany()
            .HasForeignKey(x => x.FornecedorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Despesa)
            .WithMany()
            .HasForeignKey(x => x.DespesaId)
            .OnDelete(DeleteBehavior.SetNull);

        // Sprint 147: dedupe — mesmo PDF (hash) não pode entrar 2× por tenant.
        builder.HasIndex(x => new { x.TenantId, x.PdfSha256 })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        // Listing por tenant + status (UI "pendentes").
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
    }
}
