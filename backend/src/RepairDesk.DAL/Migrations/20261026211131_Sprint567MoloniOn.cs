using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint567MoloniOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MoloniDocumentoId",
                table: "Vendas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MoloniDocumentoTipo",
                table: "Vendas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MoloniOrcamentoId",
                table: "Vendas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MoloniOrcamentoNumero",
                table: "Vendas",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MoloniReciboId",
                table: "Vendas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MoloniReciboNumero",
                table: "Vendas",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MoloniApiKeyCipherText",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MoloniCompanyId",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MoloniConfigJson",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MoloniCustomerId",
                table: "Clientes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MoloniProductId",
                table: "Artigos",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MoloniDocumentoId",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "MoloniDocumentoTipo",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "MoloniOrcamentoId",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "MoloniOrcamentoNumero",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "MoloniReciboId",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "MoloniReciboNumero",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "MoloniApiKeyCipherText",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MoloniCompanyId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MoloniConfigJson",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MoloniCustomerId",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "MoloniProductId",
                table: "Artigos");
        }
    }
}
