using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint31Stock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Modelo = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    PriceTableEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QtdStock = table.Column<int>(type: "int", nullable: false),
                    QtdMinima = table.Column<int>(type: "int", nullable: false),
                    CustoUnitarioCents = table.Column<int>(type: "int", nullable: false),
                    Fornecedor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LocalArmazenamento = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parts_PriceTableEntries_PriceTableEntryId",
                        column: x => x.PriceTableEntryId,
                        principalTable: "PriceTableEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PartMovimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    StockAntes = table.Column<int>(type: "int", nullable: false),
                    StockDepois = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<int>(type: "int", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartMovimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartMovimentos_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartMovimentos_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartMovimentos_PartId",
                table: "PartMovimentos",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PartMovimentos_ReparacaoId",
                table: "PartMovimentos",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_PartMovimentos_TenantId_PartId_CreatedAt",
                table: "PartMovimentos",
                columns: new[] { "TenantId", "PartId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartMovimentos_TenantId_ReparacaoId",
                table: "PartMovimentos",
                columns: new[] { "TenantId", "ReparacaoId" },
                filter: "[ReparacaoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_PriceTableEntryId",
                table: "Parts",
                column: "PriceTableEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_TenantId_Categoria_Marca",
                table: "Parts",
                columns: new[] { "TenantId", "Categoria", "Marca" });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_TenantId_PriceTableEntryId",
                table: "Parts",
                columns: new[] { "TenantId", "PriceTableEntryId" },
                filter: "[PriceTableEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_TenantId_QtdStock_QtdMinima",
                table: "Parts",
                columns: new[] { "TenantId", "QtdStock", "QtdMinima" });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_TenantId_Sku",
                table: "Parts",
                columns: new[] { "TenantId", "Sku" },
                unique: true,
                filter: "[Sku] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartMovimentos");

            migrationBuilder.DropTable(
                name: "Parts");
        }
    }
}
