using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("PushSubscriptions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ReparacaoId).IsRequired();
        builder.Property(x => x.Endpoint).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.P256dh).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Auth).HasMaxLength(256).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(500);

        builder.HasOne(x => x.Reparacao)
            .WithMany()
            .HasForeignKey(x => x.ReparacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ReparacaoId, x.Endpoint })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => new { x.TenantId, x.ReparacaoId });
    }
}
