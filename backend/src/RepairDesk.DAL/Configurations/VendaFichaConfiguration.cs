using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class VendaFichaConfiguration : IEntityTypeConfiguration<VendaFicha>
{
    public void Configure(EntityTypeBuilder<VendaFicha> builder)
    {
        builder.ToTable("VendaFichas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.HasIndex(x => x.VendaId).IsUnique();
        builder.HasOne(x => x.Venda).WithMany().HasForeignKey(x => x.VendaId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Imei).HasMaxLength(20);
        builder.Property(x => x.SenhaTipo).HasConversion<int>();
        builder.Property(x => x.SenhaCifrada).HasMaxLength(1000);
        builder.Property(x => x.EstadoFisico).HasMaxLength(1000);
        builder.Property(x => x.Acessorios).HasMaxLength(500);
        builder.Property(x => x.ChecklistJson).HasMaxLength(4000).IsRequired();
    }
}
