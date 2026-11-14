using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class SubscritorNewsletterConfiguration : IEntityTypeConfiguration<SubscritorNewsletter>
{
    public void Configure(EntityTypeBuilder<SubscritorNewsletter> builder)
    {
        builder.ToTable("SubscritoresNewsletter");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.Ativo);
        builder.Property(x => x.Email).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Origem).HasMaxLength(40);
        builder.Property(x => x.TokenConfirmacaoHash).HasMaxLength(64);
        builder.Property(x => x.TokenCancelamento).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        builder.HasIndex(x => x.TokenConfirmacaoHash).HasFilter("[TokenConfirmacaoHash] IS NOT NULL");
        builder.HasIndex(x => x.TokenCancelamento).IsUnique();
    }
}
