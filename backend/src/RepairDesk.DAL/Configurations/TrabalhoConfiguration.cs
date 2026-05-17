using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class TrabalhoConfiguration : IEntityTypeConfiguration<Trabalho>
{
    public void Configure(EntityTypeBuilder<Trabalho> builder)
    {
        builder.ToTable("Trabalhos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Numero).IsRequired();
        builder.Property(x => x.Titulo).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(4000);
        builder.Property(x => x.Notas).HasMaxLength(2000);
        builder.Property(x => x.HorasGastas).HasPrecision(8, 2);

        builder.Property(x => x.Categoria).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.EstadoPagamento).HasConversion<int>();

        builder.HasOne(x => x.Cliente)
            .WithMany()
            .HasForeignKey(x => x.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.Categoria });
    }
}
