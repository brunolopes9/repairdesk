using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint613Loja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProdutosLoja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Grau = table.Column<int>(type: "int", nullable: true),
                    GarantiaMeses = table.Column<int>(type: "int", nullable: false),
                    RegimeIva = table.Column<int>(type: "int", nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Disponibilidade = table.Column<int>(type: "int", nullable: false),
                    PrecoCents = table.Column<long>(type: "bigint", nullable: false),
                    CustoCents = table.Column<long>(type: "bigint", nullable: true),
                    Cor = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RamGb = table.Column<int>(type: "int", nullable: true),
                    ArmazenamentoGb = table.Column<int>(type: "int", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    SpecsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    SeoTitulo = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: true),
                    SeoDescricao = table.Column<string>(type: "nvarchar(170)", maxLength: 170, nullable: true),
                    Publicado = table.Column<bool>(type: "bit", nullable: false),
                    PublicadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProdutosLoja", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProdutosLojaFotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    Alt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Enviada = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProdutosLojaFotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProdutosLojaFotos_ProdutosLoja_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "ProdutosLoja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProdutosLoja_TenantId_Numero",
                table: "ProdutosLoja",
                columns: new[] { "TenantId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProdutosLoja_TenantId_Publicado",
                table: "ProdutosLoja",
                columns: new[] { "TenantId", "Publicado" });

            migrationBuilder.CreateIndex(
                name: "IX_ProdutosLoja_TenantId_Slug",
                table: "ProdutosLoja",
                columns: new[] { "TenantId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProdutosLojaFotos_ProdutoId_Ordem",
                table: "ProdutosLojaFotos",
                columns: new[] { "ProdutoId", "Ordem" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProdutosLojaFotos");

            migrationBuilder.DropTable(
                name: "ProdutosLoja");
        }
    }
}
