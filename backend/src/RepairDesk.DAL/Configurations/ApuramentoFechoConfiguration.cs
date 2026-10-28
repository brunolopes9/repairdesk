using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class ApuramentoFechoConfiguration : IEntityTypeConfiguration<ApuramentoFecho>
{
    public void Configure(EntityTypeBuilder<ApuramentoFecho> builder)
    {
        builder.ToTable("ApuramentoFechos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.IvaLiquidado).HasPrecision(18, 2);
        builder.Property(x => x.IvaDedutivel).HasPrecision(18, 2);
        builder.Property(x => x.Saldo).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.Ano, x.Trimestre }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
