using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint584NumeroDespesaNomeFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Numero",
                table: "Despesas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NomeFatura",
                table: "Artigos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Sprint 584: as despesas que já existem recebem nº pela data (por empresa, incluindo as apagadas).
            migrationBuilder.Sql(@"WITH n AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY Data, CreatedAt, Id) AS Num FROM Despesas)
UPDATE d SET d.Numero = n.Num FROM Despesas d JOIN n ON n.Id = d.Id;");

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_TenantId_Numero",
                table: "Despesas",
                columns: new[] { "TenantId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Despesas_TenantId_Numero",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "NomeFatura",
                table: "Artigos");
        }
    }
}
