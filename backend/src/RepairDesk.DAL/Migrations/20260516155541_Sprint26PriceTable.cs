using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint26PriceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceTableEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Servico = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustoPecaCents = table.Column<int>(type: "int", nullable: true),
                    PvpCents = table.Column<int>(type: "int", nullable: false),
                    TempoEstimadoMin = table.Column<int>(type: "int", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceTableEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceTableEntries_TenantId_Categoria_Marca_Modelo",
                table: "PriceTableEntries",
                columns: new[] { "TenantId", "Categoria", "Marca", "Modelo" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceTableEntries_TenantId_Marca_Modelo_Servico",
                table: "PriceTableEntries",
                columns: new[] { "TenantId", "Marca", "Modelo", "Servico" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceTableEntries");
        }
    }
}
