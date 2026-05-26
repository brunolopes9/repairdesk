using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class StaffPushSubscriptionConfiguration : IEntityTypeConfiguration<StaffPushSubscription>
{
    public void Configure(EntityTypeBuilder<StaffPushSubscription> builder)
    {
        builder.ToTable("StaffPushSubscriptions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Endpoint).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.P256dh).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Auth).HasMaxLength(256).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(500);

        // Um dispositivo (endpoint) por utilizador — re-subscrever actualiza, não duplica.
        builder.HasIndex(x => new { x.UserId, x.Endpoint })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => x.TenantId);
    }
}
