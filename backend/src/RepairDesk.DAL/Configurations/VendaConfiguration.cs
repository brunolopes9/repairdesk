using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Configurations;

public class VendaConfiguration : IEntityTypeConfiguration<Venda>, IEntityTypeConfiguration<VendaItem>
{
    public void Configure(EntityTypeBuilder<Venda> builder)
    {
        builder.ToTable("Vendas");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Numero).IsRequired();
        builder.Property(x => x.PaymentMethod).HasConversion<int>();
        builder.Property(x => x.Tipo).HasConversion<int>();
        builder.Property(x => x.Estado).HasConversion<int>();
        builder.Property(x => x.Equipamento).HasMaxLength(200);
        builder.Property(x => x.Problema).HasMaxLength(2000);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(120);
        builder.Property(x => x.Notas).HasMaxLength(2000);

        builder.HasOne(x => x.Cliente)
            .WithMany()
            .HasForeignKey(x => x.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Venda)
            .HasForeignKey(x => x.VendaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.Numero })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.Data });
        builder.HasIndex(x => new { x.TenantId, x.Estado });
    }

    public void Configure(EntityTypeBuilder<VendaItem> builder)
    {
        builder.ToTable("VendaItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(300).IsRequired();
        builder.Property(x => x.IvaRate).HasPrecision(5, 2);
        builder.Property(x => x.Imei).HasMaxLength(16);
        builder.Property(x => x.Imei2).HasMaxLength(16);
        builder.Property(x => x.FornecedorNome).HasMaxLength(200);
        builder.Property(x => x.Condicao).HasConversion<int>().HasDefaultValue(CondicaoArtigo.NaoAplicavel);
        builder.Property(x => x.CustoUnitarioPago).HasPrecision(12, 4);
        builder.Property(x => x.TaxaIvaCompra).HasPrecision(5, 4);
        builder.Ignore(x => x.TotalCents);

        builder.HasOne(x => x.CompraLinha)
            .WithMany()
            .HasForeignKey(x => x.CompraLinhaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.CompraLinhaId }).HasFilter("[CompraLinhaId] IS NOT NULL");

        builder.HasOne(x => x.Part)
            .WithMany()
            .HasForeignKey(x => x.PartId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.TenantId, x.VendaId });
        builder.HasIndex(x => new { x.TenantId, x.PartId }).HasFilter("[PartId] IS NOT NULL");
        // Index para lookup rapido de IMEI vendido antes (anti-duplicacao + procura)
        builder.HasIndex(x => new { x.TenantId, x.Imei }).HasFilter("[Imei] IS NOT NULL");
    }
}
