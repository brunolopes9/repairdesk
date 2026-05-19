using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.LastError).HasMaxLength(2000);

        builder.HasOne(x => x.Subscription)
            .WithMany()
            .HasForeignKey(x => x.WebhookSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Processor: query Pending com NextRetryAt <= now, ordered por NextRetryAt asc.
        builder.HasIndex(x => new { x.Status, x.NextRetryAt })
            .HasFilter("[Status] = 0");
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}
