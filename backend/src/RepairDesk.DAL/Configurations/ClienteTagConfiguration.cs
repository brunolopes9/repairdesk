using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public sealed class ClienteTagConfiguration : IEntityTypeConfiguration<ClienteTag>
{
    public void Configure(EntityTypeBuilder<ClienteTag> builder)
    {
        builder.Property(x => x.Nome).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CorHex).HasMaxLength(16).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Nome })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public sealed class ClienteTagAssignmentConfiguration : IEntityTypeConfiguration<ClienteTagAssignment>
{
    public void Configure(EntityTypeBuilder<ClienteTagAssignment> builder)
    {
        builder.HasIndex(x => new { x.ClienteId, x.ClienteTagId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
