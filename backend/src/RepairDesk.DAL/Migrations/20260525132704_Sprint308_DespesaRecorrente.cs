using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint308_DespesaRecorrente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRecorrente",
                table: "Despesas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PeriodicidadeMeses",
                table: "Despesas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_TenantId_IsRecorrente",
                table: "Despesas",
                columns: new[] { "TenantId", "IsRecorrente" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Despesas_TenantId_IsRecorrente",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "IsRecorrente",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "PeriodicidadeMeses",
                table: "Despesas");
        }
    }
}
