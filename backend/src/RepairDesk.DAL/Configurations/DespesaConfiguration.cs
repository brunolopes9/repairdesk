using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class DespesaConfiguration : IEntityTypeConfiguration<Despesa>
{
    public void Configure(EntityTypeBuilder<Despesa> builder)
    {
        builder.ToTable("Despesas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Fornecedor).HasMaxLength(200);
        builder.Property(x => x.NumeroEncomenda).HasMaxLength(100);
        builder.Property(x => x.Notas).HasMaxLength(1000);
        builder.Property(x => x.Categoria).HasConversion<int>();
        builder.Property(x => x.IsRecorrente).HasDefaultValue(false);

        builder.HasOne(x => x.Trabalho)
            .WithMany()
            .HasForeignKey(x => x.TrabalhoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Reparacao)
            .WithMany()
            .HasForeignKey(x => x.ReparacaoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.TenantId, x.Data });
        builder.HasIndex(x => new { x.TenantId, x.Categoria });
        builder.HasIndex(x => new { x.TenantId, x.IsRecorrente });
        builder.HasIndex(x => new { x.TenantId, x.TrabalhoId }).HasFilter("[TrabalhoId] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.ReparacaoId }).HasFilter("[ReparacaoId] IS NOT NULL");
    }
}
