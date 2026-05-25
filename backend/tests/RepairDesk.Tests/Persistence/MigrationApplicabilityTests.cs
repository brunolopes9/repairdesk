using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RepairDesk.Core.Abstractions;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Persistence;

public class MigrationApplicabilityTests
{
    [Fact]
    public void Sprint308_DespesaRecorrente_GeneratesNonDestructiveSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=RepairDeskMigrationTest;Trusted_Connection=True;")
            .Options;

        using var db = new AppDbContext(options, new NoTenantContext());
        var migrator = db.GetService<IMigrator>();

        var sql = migrator.GenerateScript(
            fromMigration: "20260525082059_Sprint305_ProductSupplierGrade",
            toMigration: "20260525132704_Sprint308_DespesaRecorrente");

        sql.Should().Contain("[IsRecorrente]");
        sql.Should().Contain("[PeriodicidadeMeses]");
        sql.Should().NotContain("DROP TABLE");
    }

    private sealed class NoTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public bool HasTenant => false;
    }
}
