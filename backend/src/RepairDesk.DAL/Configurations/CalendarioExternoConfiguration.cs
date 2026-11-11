using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class CalendarioExternoConfiguration : IEntityTypeConfiguration<CalendarioExterno>
{
    public void Configure(EntityTypeBuilder<CalendarioExterno> builder)
    {
        builder.ToTable("CalendariosExternos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(60).IsRequired();
        builder.Property(x => x.UrlCifrada).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Dominio).HasMaxLength(120).IsRequired();
        builder.Property(x => x.UltimoErro).HasMaxLength(300);
        builder.HasIndex(x => new { x.TenantId, x.UserId });
    }
}
