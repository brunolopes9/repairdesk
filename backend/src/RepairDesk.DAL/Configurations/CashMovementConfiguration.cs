using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("CashMovements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.PaymentMethod).HasConversion<int>();

        builder.HasOne(x => x.DailyClosing)
            .WithMany()
            .HasForeignKey(x => x.DailyClosingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Venda)
            .WithMany()
            .HasForeignKey(x => x.VendaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Reparacao)
            .WithMany()
            .HasForeignKey(x => x.ReparacaoId)
            .OnDelete(DeleteBehavior.SetNull);

        // Sprint 300: índices pensados para queries hot:
        // - Dashboard "movimentos hoje": (TenantId, OccurredAt DESC)
        // - Cálculo fecho do dia: (TenantId, LocationId, DailyClosingId)
        // - Multi-location lookup: (TenantId, LocationId, OccurredAt)
        builder.HasIndex(x => new { x.TenantId, x.OccurredAt });
        builder.HasIndex(x => new { x.TenantId, x.LocationId, x.OccurredAt });
        builder.HasIndex(x => x.DailyClosingId).HasFilter("[DailyClosingId] IS NOT NULL");
    }
}
