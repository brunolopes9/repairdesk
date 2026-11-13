using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint654LojaPromoUnidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PrecoAnteriorCents",
                table: "ProdutosLoja",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Unidades",
                table: "ProdutosLoja",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrecoAnteriorCents",
                table: "ProdutosLoja");

            migrationBuilder.DropColumn(
                name: "Unidades",
                table: "ProdutosLoja");
        }
    }
}
