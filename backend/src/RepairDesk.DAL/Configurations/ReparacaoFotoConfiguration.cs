using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class ReparacaoFotoConfiguration : IEntityTypeConfiguration<ReparacaoFoto>
{
    public void Configure(EntityTypeBuilder<ReparacaoFoto> builder)
    {
        builder.ToTable("ReparacaoFotos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ReparacaoId).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Tipo).HasConversion<int>();
        builder.Property(x => x.Legenda).HasMaxLength(500);

        builder.HasOne(x => x.Reparacao)
            .WithMany()
            .HasForeignKey(x => x.ReparacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ReparacaoId, x.Tipo, x.Ordem });
        builder.HasIndex(x => x.StorageKey).IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
