using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint581SkuFornecedorArtigosAgrupados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PecaReparacao",
                table: "ComprasLinhas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SkuFornecedor",
                table: "ComprasLinhas",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Agrupado",
                table: "Artigos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ArtigoCodigos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtigoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtigoCodigos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtigoCodigos_Artigos_ArtigoId",
                        column: x => x.ArtigoId,
                        principalTable: "Artigos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtigoCodigos_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ArtigoFusoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrigemSku = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrigemNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LoteIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodigosMovidosJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodigosCriadosJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DesfeitaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtigoFusoes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtigoCodigos_ArtigoId",
                table: "ArtigoCodigos",
                column: "ArtigoId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtigoCodigos_FornecedorId",
                table: "ArtigoCodigos",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtigoCodigos_TenantId_FornecedorId_Codigo",
                table: "ArtigoCodigos",
                columns: new[] { "TenantId", "FornecedorId", "Codigo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ArtigoFusoes_TenantId_DestinoId",
                table: "ArtigoFusoes",
                columns: new[] { "TenantId", "DestinoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtigoCodigos");

            migrationBuilder.DropTable(
                name: "ArtigoFusoes");

            migrationBuilder.DropColumn(
                name: "PecaReparacao",
                table: "ComprasLinhas");

            migrationBuilder.DropColumn(
                name: "SkuFornecedor",
                table: "ComprasLinhas");

            migrationBuilder.DropColumn(
                name: "Agrupado",
                table: "Artigos");
        }
    }
}
