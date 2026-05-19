using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class PartMovimentoConfiguration : IEntityTypeConfiguration<PartMovimento>
{
    public void Configure(EntityTypeBuilder<PartMovimento> builder)
    {
        builder.ToTable("PartMovimentos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Motivo).HasConversion<int>();
        builder.Property(x => x.Notas).HasMaxLength(1000);

        builder.HasOne(x => x.Part)
            .WithMany(x => x.Movimentos)
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Reparacao)
            .WithMany()
            .HasForeignKey(x => x.ReparacaoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Venda)
            .WithMany()
            .HasForeignKey(x => x.VendaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.TenantId, x.PartId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.ReparacaoId }).HasFilter("[ReparacaoId] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.VendaId }).HasFilter("[VendaId] IS NOT NULL");
    }
}
