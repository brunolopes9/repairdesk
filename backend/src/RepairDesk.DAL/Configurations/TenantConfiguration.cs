using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.LegalName).HasMaxLength(300);
        builder.Property(x => x.Nif).HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.PostalCode).HasMaxLength(20);
        builder.Property(x => x.Locality).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(2);
        builder.Property(x => x.Phone).HasMaxLength(40);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Website).HasMaxLength(300);
        builder.Property(x => x.Iban).HasMaxLength(50);
        builder.Property(x => x.CaePrincipal).HasMaxLength(10);
        builder.Property(x => x.CaeSecundarios).HasMaxLength(200);
        builder.Property(x => x.RegimeFiscal).HasConversion<int>();
        builder.Property(x => x.TermosCondicoes).HasMaxLength(4000);
        builder.Property(x => x.LogoUrl).HasMaxLength(500);
        builder.Property(x => x.PrimaryColor).HasMaxLength(20);
        builder.Property(x => x.OnboardingCompletado).HasDefaultValue(false);
        builder.Property(x => x.GarantiaCoberturaDefault).HasMaxLength(2000);
        builder.Property(x => x.GarantiaExclusoesDefault).HasMaxLength(2000);
        builder.Property(x => x.GoogleReviewUrl).HasMaxLength(500);

        builder.HasIndex(x => x.Nif).IsUnique().HasFilter("[Nif] IS NOT NULL");
    }
}
