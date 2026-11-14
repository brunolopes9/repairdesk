using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class EncomendaLojaConfiguration : IEntityTypeConfiguration<EncomendaLoja>
{
    public void Configure(EntityTypeBuilder<EncomendaLoja> builder)
    {
        builder.ToTable("EncomendasLoja");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Referencia).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Estado).HasConversion<int>();
        builder.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Telefone).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Nif).HasMaxLength(20);
        builder.Property(x => x.Morada).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CodigoPostal).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Localidade).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Notas).HasMaxLength(500);
        builder.Property(x => x.Transportadora).HasMaxLength(60).IsRequired();
        builder.Property(x => x.StripeSessionId).HasMaxLength(255);
        builder.Property(x => x.StripePaymentIntentId).HasMaxLength(255);
        builder.Property(x => x.NumeroSeguimento).HasMaxLength(80);
        builder.Property(x => x.Problema).HasMaxLength(300);
        builder.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
        builder.HasIndex(x => x.Referencia).IsUnique();
        builder.HasIndex(x => x.StripeSessionId).IsUnique().HasFilter("[StripeSessionId] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.Estado });
        builder.HasIndex(x => x.ClienteLojaId).HasFilter("[ClienteLojaId] IS NOT NULL");
        builder.HasMany(x => x.Linhas).WithOne(l => l.Encomenda).HasForeignKey(l => l.EncomendaId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EncomendaLojaLinhaConfiguration : IEntityTypeConfiguration<EncomendaLojaLinha>
{
    public void Configure(EntityTypeBuilder<EncomendaLojaLinha> builder)
    {
        builder.ToTable("EncomendasLojaLinhas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Titulo).HasMaxLength(160).IsRequired();
        builder.HasIndex(x => x.EncomendaId);
    }
}
