using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class TenantPreferencesConfiguration : IEntityTypeConfiguration<TenantPreferences>
{
    public void Configure(EntityTypeBuilder<TenantPreferences> builder)
    {
        builder.ToTable("TenantPreferences");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.PreferencesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("{}")
            .IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithOne()
            .HasForeignKey<TenantPreferences>(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}
