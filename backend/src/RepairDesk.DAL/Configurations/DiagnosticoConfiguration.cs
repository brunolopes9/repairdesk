using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class DiagnosticoTemplateConfiguration : IEntityTypeConfiguration<DiagnosticoTemplate>
{
    public void Configure(EntityTypeBuilder<DiagnosticoTemplate> builder)
    {
        builder.ToTable("DiagnosticoTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Categoria).HasConversion<int>();

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Template!)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.Categoria, x.IsDefault })
            .HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");
    }
}

public class DiagnosticoTemplateItemConfiguration : IEntityTypeConfiguration<DiagnosticoTemplateItem>
{
    public void Configure(EntityTypeBuilder<DiagnosticoTemplateItem> builder)
    {
        builder.ToTable("DiagnosticoTemplateItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.TemplateId).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(500);
        builder.Property(x => x.Grupo).HasMaxLength(50);
        builder.HasIndex(x => new { x.TemplateId, x.Ordem });
    }
}

public class DiagnosticoExecucaoConfiguration : IEntityTypeConfiguration<DiagnosticoExecucao>
{
    public void Configure(EntityTypeBuilder<DiagnosticoExecucao> builder)
    {
        builder.ToTable("DiagnosticoExecucoes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ReparacaoId).IsRequired();
        builder.Property(x => x.TemplateNomeSnapshot).HasMaxLength(200);
        builder.Property(x => x.Categoria).HasConversion<int>();
        builder.Property(x => x.NotasGerais).HasMaxLength(2000);

        builder.HasOne(x => x.Reparacao)
            .WithMany()
            .HasForeignKey(x => x.ReparacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Execucao!)
            .HasForeignKey(x => x.ExecucaoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Uma reparação tem no máximo 1 execução activa (não-deleted)
        builder.HasIndex(x => x.ReparacaoId).IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class DiagnosticoExecucaoItemConfiguration : IEntityTypeConfiguration<DiagnosticoExecucaoItem>
{
    public void Configure(EntityTypeBuilder<DiagnosticoExecucaoItem> builder)
    {
        builder.ToTable("DiagnosticoExecucaoItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ExecucaoId).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(500);
        builder.Property(x => x.Grupo).HasMaxLength(50);
        builder.Property(x => x.Notas).HasMaxLength(500);
        builder.Property(x => x.Resultado).HasConversion<int>();
        builder.HasIndex(x => new { x.ExecucaoId, x.Ordem });
    }
}
