using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint151_ProductShopFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompareAtPriceCents",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropshipSupplierSku",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenBoxReason",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurated",
                table: "ProductImages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Fornecedores",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Category_MostrarLojaOnline",
                table: "Products",
                columns: new[] { "TenantId", "Category", "MostrarLojaOnline" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_FornecedorId_DropshipSupplierSku",
                table: "Products",
                columns: new[] { "TenantId", "FornecedorId", "DropshipSupplierSku" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [DropshipSupplierSku] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Fornecedores_TenantId_Code",
                table: "Fornecedores",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Code] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_Category_MostrarLojaOnline",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_FornecedorId_DropshipSupplierSku",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Fornecedores_TenantId_Code",
                table: "Fornecedores");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CompareAtPriceCents",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DropshipSupplierSku",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OpenBoxReason",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsCurated",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Fornecedores");
        }
    }
}
