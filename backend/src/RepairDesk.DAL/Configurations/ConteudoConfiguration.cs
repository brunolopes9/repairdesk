using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

// Sprint 598 (Doc 95): módulo Conteúdo (redes sociais).

public class ConteudoPublicacaoConfiguration : IEntityTypeConfiguration<ConteudoPublicacao>
{
    public void Configure(EntityTypeBuilder<ConteudoPublicacao> builder)
    {
        builder.ToTable("ConteudoPublicacoes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Titulo).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Legenda).HasMaxLength(5000);
        builder.Property(x => x.MotivoRejeicao).HasMaxLength(1000);
        builder.Property(x => x.Notas).HasMaxLength(2000);
        builder.Property(x => x.Linha).HasConversion<int>();
        builder.Property(x => x.Formato).HasConversion<int>();
        builder.Property(x => x.Estado).HasConversion<int>();
        builder.Property(x => x.Origem).HasConversion<int>();

        builder.HasMany(x => x.Ficheiros).WithOne(f => f.Publicacao)
            .HasForeignKey(f => f.PublicacaoId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.Destinos).WithOne(d => d.Publicacao)
            .HasForeignKey(d => d.PublicacaoId).OnDelete(DeleteBehavior.Cascade);

        // Nº visível único por tenant (0 = ainda por atribuir, só em memória).
        builder.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Estado, x.AgendadoPara });
    }
}

public class ConteudoFicheiroConfiguration : IEntityTypeConfiguration<ConteudoFicheiro>
{
    public void Configure(EntityTypeBuilder<ConteudoFicheiro> builder)
    {
        builder.ToTable("ConteudoFicheiros");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NomeOriginal).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Notas).HasMaxLength(1000);
        builder.Property(x => x.DuracaoSeg).HasPrecision(9, 2);
        builder.Property(x => x.Tipo).HasConversion<int>();
        builder.Property(x => x.Estado).HasConversion<int>();

        builder.HasIndex(x => x.StorageKey).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.PublicacaoId, x.Ordem });
        builder.HasIndex(x => new { x.TenantId, x.PublicacaoId, x.CreatedAt });
    }
}

public class ConteudoDestinoConfiguration : IEntityTypeConfiguration<ConteudoDestino>
{
    public void Configure(EntityTypeBuilder<ConteudoDestino> builder)
    {
        builder.ToTable("ConteudoDestinos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.LegendaPropria).HasMaxLength(5000);
        builder.Property(x => x.ExternalId).HasMaxLength(200);
        builder.Property(x => x.Permalink).HasMaxLength(500);
        builder.Property(x => x.UltimoErro).HasMaxLength(2000);
        builder.Property(x => x.Rede).HasConversion<int>();
        builder.Property(x => x.Estado).HasConversion<int>();

        // Uma publicação sai no máximo uma vez em cada rede.
        builder.HasIndex(x => new { x.PublicacaoId, x.Rede }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.Estado, x.ProximaTentativaEm });
    }
}

public class RedeSocialLigacaoConfiguration : IEntityTypeConfiguration<RedeSocialLigacao>
{
    public void Configure(EntityTypeBuilder<RedeSocialLigacao> builder)
    {
        builder.ToTable("RedeSocialLigacoes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Rede).HasConversion<int>();
        builder.Property(x => x.ContaId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ContaNome).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TokenCipherText).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.UltimoErro).HasMaxLength(1000);

        // Uma ligação ativa por rede e tenant (ligar outra vez substitui a anterior).
        builder.HasIndex(x => new { x.TenantId, x.Rede }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
