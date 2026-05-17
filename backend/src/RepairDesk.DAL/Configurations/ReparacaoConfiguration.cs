using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class ReparacaoConfiguration : IEntityTypeConfiguration<Reparacao>
{
    public void Configure(EntityTypeBuilder<Reparacao> builder)
    {
        builder.ToTable("Reparacoes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Numero).IsRequired();
        builder.Property(x => x.Equipamento).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Imei).HasMaxLength(40);
        builder.Property(x => x.Avaria).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Diagnostico).HasMaxLength(2000);
        builder.Property(x => x.Notas).HasMaxLength(2000);
        builder.Property(x => x.HorasGastas).HasPrecision(8, 2);
        builder.Property(x => x.PublicSlug).HasMaxLength(16);

        builder.Property(x => x.Estado).HasConversion<int>();
        builder.Property(x => x.EstadoPagamento).HasConversion<int>();

        builder.HasOne(x => x.Cliente)
            .WithMany()
            .HasForeignKey(x => x.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Timeline)
            .WithOne(x => x.Reparacao!)
            .HasForeignKey(x => x.ReparacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.Estado });
        builder.HasIndex(x => new { x.TenantId, x.ClienteId });
        builder.HasIndex(x => x.PublicSlug).IsUnique()
            .HasFilter("[PublicSlug] IS NOT NULL");
    }
}

public class ReparacaoEstadoLogConfiguration : IEntityTypeConfiguration<ReparacaoEstadoLog>
{
    public void Configure(EntityTypeBuilder<ReparacaoEstadoLog> builder)
    {
        builder.ToTable("ReparacaoEstadoLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ReparacaoId).IsRequired();
        builder.Property(x => x.EstadoTo).HasConversion<int>();
        builder.Property(x => x.EstadoFrom).HasConversion<int?>();
        builder.Property(x => x.Notas).HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.ReparacaoId, x.MudouEm });
    }
}
