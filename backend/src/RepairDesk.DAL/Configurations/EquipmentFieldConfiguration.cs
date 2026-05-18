using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Configurations;

public class EquipmentFieldTemplateConfiguration : IEntityTypeConfiguration<EquipmentFieldTemplate>
{
    public void Configure(EntityTypeBuilder<EquipmentFieldTemplate> builder)
    {
        builder.ToTable("EquipmentFieldTemplates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Nome).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Categoria).HasConversion<int>();

        builder.HasMany(x => x.Fields)
            .WithOne(x => x.Template!)
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.Nome })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.IsActive, x.Ordem });
    }
}

public class EquipmentFieldDefinitionConfiguration : IEntityTypeConfiguration<EquipmentFieldDefinition>
{
    public void Configure(EntityTypeBuilder<EquipmentFieldDefinition> builder)
    {
        builder.ToTable("EquipmentFieldDefinitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.OptionsJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => new { x.TenantId, x.TemplateId, x.Ordem });
    }
}

public class EquipmentFieldValueConfiguration : IEntityTypeConfiguration<EquipmentFieldValue>
{
    public void Configure(EntityTypeBuilder<EquipmentFieldValue> builder)
    {
        builder.ToTable("EquipmentFieldValues");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(4000);

        builder.HasOne(x => x.Reparacao)
            .WithMany(x => x.EquipmentFieldValues)
            .HasForeignKey(x => x.ReparacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FieldDefinition)
            .WithMany(x => x.Values)
            .HasForeignKey(x => x.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ReparacaoId, x.FieldDefinitionId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.ReparacaoId });
    }
}
