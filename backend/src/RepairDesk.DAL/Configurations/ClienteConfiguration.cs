using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Telefone).HasMaxLength(40);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Nif).HasMaxLength(20);
        builder.Property(x => x.Notas).HasMaxLength(2000);

        builder.HasIndex(x => new { x.TenantId, x.Telefone });
        builder.HasIndex(x => new { x.TenantId, x.Nome });
        builder.HasIndex(x => new { x.TenantId, x.Nif })
            .HasFilter("[Nif] IS NOT NULL AND [IsDeleted] = 0");
    }
}
