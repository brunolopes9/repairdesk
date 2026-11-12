using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint633FinancasDono : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ObjetivoMensal",
                table: "Tenants",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Ambito",
                table: "Despesas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Natureza",
                table: "Despesas",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObjetivoMensal",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Ambito",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "Natureza",
                table: "Despesas");
        }
    }
}
