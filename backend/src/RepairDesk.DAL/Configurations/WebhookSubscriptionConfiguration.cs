using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("WebhookSubscriptions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Secret).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Events).HasMaxLength(500).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Active });
    }
}
