using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class EventoSiteConfiguration : IEntityTypeConfiguration<EventoSite>
{
    public void Configure(EntityTypeBuilder<EventoSite> builder)
    {
        builder.ToTable("EventosSite");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Visitante).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Sessao).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Caminho).HasMaxLength(300);
        builder.Property(x => x.Titulo).HasMaxLength(200);
        builder.Property(x => x.Referrer).HasMaxLength(300);
        builder.Property(x => x.Origem).HasMaxLength(60);
        builder.Property(x => x.Campanha).HasMaxLength(100);
        builder.Property(x => x.Termo).HasMaxLength(120);
        builder.Property(x => x.Produto).HasMaxLength(200);
        builder.Property(x => x.ProdutoSlug).HasMaxLength(200);
        builder.Property(x => x.Valor).HasPrecision(10, 2);
        builder.Property(x => x.Encomenda).HasMaxLength(20);
        builder.Property(x => x.Cliente).HasMaxLength(120);
        builder.Property(x => x.Dispositivo).HasMaxLength(20);
        builder.Property(x => x.Sistema).HasMaxLength(30);
        builder.Property(x => x.Navegador).HasMaxLength(30);
        builder.Property(x => x.Ecra).HasMaxLength(20);
        builder.Property(x => x.Idioma).HasMaxLength(12);
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.Sessao });
    }
}
