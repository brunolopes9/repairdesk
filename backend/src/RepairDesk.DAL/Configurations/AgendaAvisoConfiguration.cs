using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class AgendaAvisoConfiguration : IEntityTypeConfiguration<AgendaAviso>
{
    public void Configure(EntityTypeBuilder<AgendaAviso> builder)
    {
        builder.ToTable("AgendaAvisos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Chave).HasMaxLength(40).IsRequired();
        // Garante «uma vez por empresa» mesmo com dois servidores ao mesmo tempo.
        builder.HasIndex(x => new { x.TenantId, x.Chave }).IsUnique();
    }
}
