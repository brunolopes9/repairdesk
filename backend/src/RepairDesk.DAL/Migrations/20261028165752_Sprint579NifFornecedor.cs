using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint579NifFornecedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nif",
                table: "Fornecedores",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fornecedores_TenantId_Nif",
                table: "Fornecedores",
                columns: new[] { "TenantId", "Nif" },
                filter: "[IsDeleted] = 0 AND [Nif] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Fornecedores_TenantId_Nif",
                table: "Fornecedores");

            migrationBuilder.DropColumn(
                name: "Nif",
                table: "Fornecedores");
        }
    }
}
