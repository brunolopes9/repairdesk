using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint566Artigos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ArtigoId",
                table: "ComprasLinhas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Artigos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodigoBarras = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CodigoFabricante = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PrecoVendaCents = table.Column<int>(type: "int", nullable: true),
                    Localizacao = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artigos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComprasLinhas_ArtigoId",
                table: "ComprasLinhas",
                column: "ArtigoId");

            migrationBuilder.CreateIndex(
                name: "IX_Artigos_TenantId_CodigoBarras",
                table: "Artigos",
                columns: new[] { "TenantId", "CodigoBarras" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Artigos_TenantId_CodigoFabricante",
                table: "Artigos",
                columns: new[] { "TenantId", "CodigoFabricante" },
                unique: true,
                filter: "[CodigoFabricante] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Artigos_TenantId_Numero",
                table: "Artigos",
                columns: new[] { "TenantId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Artigos_TenantId_Sku",
                table: "Artigos",
                columns: new[] { "TenantId", "Sku" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ComprasLinhas_Artigos_ArtigoId",
                table: "ComprasLinhas",
                column: "ArtigoId",
                principalTable: "Artigos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ComprasLinhas_Artigos_ArtigoId",
                table: "ComprasLinhas");

            migrationBuilder.DropTable(
                name: "Artigos");

            migrationBuilder.DropIndex(
                name: "IX_ComprasLinhas_ArtigoId",
                table: "ComprasLinhas");

            migrationBuilder.DropColumn(
                name: "ArtigoId",
                table: "ComprasLinhas");
        }
    }
}
