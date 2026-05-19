using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class GarantiaConfiguration : IEntityTypeConfiguration<Garantia>
{
    public void Configure(EntityTypeBuilder<Garantia> builder)
    {
        builder.ToTable("Garantias", t =>
        {
            // DL 84/2021 — origem da garantia: Reparação OU Venda, exactamente um.
            t.HasCheckConstraint(
                "CK_Garantias_OneSource",
                "([ReparacaoId] IS NOT NULL AND [VendaId] IS NULL) OR ([ReparacaoId] IS NULL AND [VendaId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Cobertura).HasMaxLength(2000);
        builder.Property(x => x.Exclusoes).HasMaxLength(2000);
        builder.Property(x => x.MotivoAnulacao).HasMaxLength(500);
        builder.Property(x => x.SourceType).HasConversion<int>();

        builder.HasOne(x => x.Reparacao)
            .WithMany()
            .HasForeignKey(x => x.ReparacaoId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
        builder.HasOne(x => x.Venda)
            .WithMany()
            .HasForeignKey(x => x.VendaId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasIndex(x => x.Slug).IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.ReparacaoId).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [ReparacaoId] IS NOT NULL");
        builder.HasIndex(x => x.VendaId).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [VendaId] IS NOT NULL");
    }
}

public class AvaliacaoConfiguration : IEntityTypeConfiguration<Avaliacao>
{
    public void Configure(EntityTypeBuilder<Avaliacao> builder)
    {
        builder.ToTable("Avaliacoes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ReparacaoId).IsRequired();
        builder.Property(x => x.Score).IsRequired();
        builder.Property(x => x.Comentario).HasMaxLength(2000);

        builder.HasOne(x => x.Reparacao)
            .WithMany()
            .HasForeignKey(x => x.ReparacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ReparacaoId).IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
