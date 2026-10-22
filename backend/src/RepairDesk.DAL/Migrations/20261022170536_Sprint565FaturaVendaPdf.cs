using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint565FaturaVendaPdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaturaPdfPath",
                table: "Vendas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaturaPdfSha256",
                table: "Vendas",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vendas_TenantId_InvoiceNumber",
                table: "Vendas",
                columns: new[] { "TenantId", "InvoiceNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vendas_TenantId_InvoiceNumber",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "FaturaPdfPath",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "FaturaPdfSha256",
                table: "Vendas");
        }
    }
}
