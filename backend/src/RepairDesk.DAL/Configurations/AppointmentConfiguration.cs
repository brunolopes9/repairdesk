using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Nome).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Telefone).HasMaxLength(40);
        builder.Property(x => x.Email).HasMaxLength(160);
        builder.Property(x => x.Equipamento).HasMaxLength(160);
        builder.Property(x => x.Notas).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Source).HasConversion<int>();

        builder.HasIndex(x => new { x.TenantId, x.ScheduledAt });
    }
}
