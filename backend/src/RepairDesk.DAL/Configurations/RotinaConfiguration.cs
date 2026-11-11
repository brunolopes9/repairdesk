using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class RotinaItemConfiguration : IEntityTypeConfiguration<RotinaItem>
{
    public void Configure(EntityTypeBuilder<RotinaItem> builder)
    {
        builder.ToTable("RotinaItens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Titulo).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Detalhe).HasMaxLength(1000);
        builder.Property(x => x.AvisosAntes).HasMaxLength(40);
        builder.Property(x => x.Tipo).HasConversion<int>();
        builder.HasIndex(x => new { x.TenantId, x.UserId });
    }
}

public class RotinaRegistoConfiguration : IEntityTypeConfiguration<RotinaRegisto>
{
    public void Configure(EntityTypeBuilder<RotinaRegisto> builder)
    {
        builder.ToTable("RotinaRegistos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Estado).HasConversion<int?>();
        builder.Property(x => x.Motivo).HasMaxLength(200);
        builder.Property(x => x.Valor).HasPrecision(6, 2);
        builder.Property(x => x.Avisos).HasMaxLength(400).IsRequired();
        builder.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.RotinaItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.RotinaItemId, x.Dia }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Dia });
    }
}
