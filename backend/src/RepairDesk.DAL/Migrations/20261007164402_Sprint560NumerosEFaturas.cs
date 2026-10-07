using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint560NumerosEFaturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Numero",
                table: "ComprasLinhas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Numero",
                table: "ComprasDocumentos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Sprint 560: nº visível para os dados que já existem — documentos por data, lotes pela ordem
            // dos documentos (e de criação dentro de cada um). Inclui apagados: nºs nunca se reutilizam.
            migrationBuilder.Sql(@"
WITH d AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY TenantId ORDER BY [Data], CreatedAt, Id) AS N
    FROM ComprasDocumentos)
UPDATE c SET Numero = d.N FROM ComprasDocumentos c JOIN d ON d.Id = c.Id;

WITH l AS (
    SELECT cl.Id, ROW_NUMBER() OVER (PARTITION BY cl.TenantId ORDER BY cd.Numero, cl.CreatedAt, cl.Id) AS N
    FROM ComprasLinhas cl JOIN ComprasDocumentos cd ON cd.Id = cl.CompraDocumentoId)
UPDATE x SET Numero = l.N FROM ComprasLinhas x JOIN l ON l.Id = x.Id;
");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasLinhas_TenantId_Numero",
                table: "ComprasLinhas",
                columns: new[] { "TenantId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComprasDocumentos_SupplierInvoiceImportId",
                table: "ComprasDocumentos",
                column: "SupplierInvoiceImportId",
                unique: true,
                filter: "[SupplierInvoiceImportId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasDocumentos_TenantId_Numero",
                table: "ComprasDocumentos",
                columns: new[] { "TenantId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ComprasLinhas_TenantId_Numero",
                table: "ComprasLinhas");

            migrationBuilder.DropIndex(
                name: "IX_ComprasDocumentos_SupplierInvoiceImportId",
                table: "ComprasDocumentos");

            migrationBuilder.DropIndex(
                name: "IX_ComprasDocumentos_TenantId_Numero",
                table: "ComprasDocumentos");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "ComprasLinhas");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "ComprasDocumentos");
        }
    }
}
