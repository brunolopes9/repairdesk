using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class ClienteLojaConfiguration : IEntityTypeConfiguration<ClienteLoja>
{
    public void Configure(EntityTypeBuilder<ClienteLoja> builder)
    {
        builder.ToTable("ClientesLoja");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Telefone).HasMaxLength(30);
        builder.Property(x => x.PasswordHash).HasMaxLength(400).IsRequired();
        builder.Property(x => x.ResetTokenHash).HasMaxLength(64);
        builder.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        builder.HasIndex(x => x.ResetTokenHash).HasFilter("[ResetTokenHash] IS NOT NULL");
    }
}

public class ClienteLojaSessaoConfiguration : IEntityTypeConfiguration<ClienteLojaSessao>
{
    public void Configure(EntityTypeBuilder<ClienteLojaSessao> builder)
    {
        builder.ToTable("ClientesLojaSessoes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.ClienteId);
        builder.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
    }
}
