using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class AssistenteAcaoConfiguration : IEntityTypeConfiguration<AssistenteAcao>
{
    public void Configure(EntityTypeBuilder<AssistenteAcao> builder)
    {
        builder.ToTable("AssistenteAcoes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Dia).HasMaxLength(10);
        builder.Property(x => x.Antes).HasMaxLength(1000);
        builder.Property(x => x.Resumo).HasMaxLength(300).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt });
    }
}
