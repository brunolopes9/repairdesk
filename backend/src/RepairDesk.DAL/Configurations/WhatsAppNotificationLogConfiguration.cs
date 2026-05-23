using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class WhatsAppNotificationLogConfiguration : IEntityTypeConfiguration<WhatsAppNotificationLog>
{
    public void Configure(EntityTypeBuilder<WhatsAppNotificationLog> builder)
    {
        builder.ToTable("WhatsAppNotificationLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
        builder.Property(x => x.TemplateKey).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(40);
        builder.Property(x => x.Estado).HasConversion<int?>();
        builder.Property(x => x.SentAtUtc).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.SentAtUtc })
            .IsDescending(false, true);
        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId, x.TemplateKey })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
