using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class DailyClosingConfiguration : IEntityTypeConfiguration<DailyClosing>
{
    public void Configure(EntityTypeBuilder<DailyClosing> builder)
    {
        builder.ToTable("DailyClosings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Notas).HasMaxLength(2000);
        builder.Property(x => x.ZReportPdfUrl).HasMaxLength(500);

        // Sprint 300: um único fecho por (tenant, location, dia). Constraint impede
        // dupla abertura — operador que abre duas vezes recebe ConflictException.
        // LocationId NULL na Phase A.1; o filtro permite duplicates apenas em locations
        // diferentes (real-world impossível agora, mas correcto quando Pillar C).
        builder.HasIndex(x => new { x.TenantId, x.LocationId, x.Date })
            .IsUnique()
            .HasDatabaseName("IX_DailyClosings_Tenant_Location_Date_Unique");

        // Queries hot: dashboard "últimos N fechos" + relatório mensal de caixa
        builder.HasIndex(x => new { x.TenantId, x.Date });
    }
}
