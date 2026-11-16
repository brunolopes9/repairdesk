using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class ItemParaEncomendarConfiguration : IEntityTypeConfiguration<ItemParaEncomendar>
{
    public void Configure(EntityTypeBuilder<ItemParaEncomendar> builder)
    {
        builder.ToTable("ItensParaEncomendar");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Descricao).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Fornecedor).HasMaxLength(120);
        builder.Property(x => x.Nota).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.Estado });
    }
}
