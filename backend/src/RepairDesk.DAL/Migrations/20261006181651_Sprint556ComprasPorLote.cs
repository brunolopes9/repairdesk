using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint556ComprasPorLote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Pais",
                table: "Fornecedores",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegimeIva",
                table: "Fornecedores",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // IntraUe (bool) → RegimeIva: intra-UE passa a UeAutoliquidacao (1); o resto fica Nacional (0).
            migrationBuilder.Sql("UPDATE [Fornecedores] SET [RegimeIva] = 1 WHERE [IntraUe] = 1;");

            migrationBuilder.DropColumn(
                name: "IntraUe",
                table: "Fornecedores");

            migrationBuilder.CreateTable(
                name: "ComprasDocumentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NumeroFatura = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NumerosEncomenda = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MetodoPagamento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PortesPagos = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    PortesIva = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    TotalDocumento = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SupplierInvoiceImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprasDocumentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprasDocumentos_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComprasLinhas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompraDocumentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    PrecoUnitarioPago = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    TaxaIvaCompra = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    LucroUnitario = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    QuantidadeVendida = table.Column<int>(type: "int", nullable: false),
                    QuantidadeAbatida = table.Column<int>(type: "int", nullable: false),
                    Localizacao = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprasLinhas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprasLinhas_ComprasDocumentos_CompraDocumentoId",
                        column: x => x.CompraDocumentoId,
                        principalTable: "ComprasDocumentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComprasDocumentos_FornecedorId",
                table: "ComprasDocumentos",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasDocumentos_TenantId_Data",
                table: "ComprasDocumentos",
                columns: new[] { "TenantId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_ComprasDocumentos_TenantId_FornecedorId_NumeroFatura",
                table: "ComprasDocumentos",
                columns: new[] { "TenantId", "FornecedorId", "NumeroFatura" });

            migrationBuilder.CreateIndex(
                name: "IX_ComprasLinhas_CompraDocumentoId",
                table: "ComprasLinhas",
                column: "CompraDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasLinhas_TenantId_CompraDocumentoId",
                table: "ComprasLinhas",
                columns: new[] { "TenantId", "CompraDocumentoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComprasLinhas");

            migrationBuilder.DropTable(
                name: "ComprasDocumentos");

            migrationBuilder.DropColumn(
                name: "Pais",
                table: "Fornecedores");

            migrationBuilder.DropColumn(
                name: "RegimeIva",
                table: "Fornecedores");

            migrationBuilder.AddColumn<bool>(
                name: "IntraUe",
                table: "Fornecedores",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
