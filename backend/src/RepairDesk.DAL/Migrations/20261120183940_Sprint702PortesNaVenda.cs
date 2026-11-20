using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint702PortesNaVenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PortesUnitario",
                table: "VendaItems",
                type: "decimal(12,4)",
                precision: 12,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PortesUnitarioSemIva",
                table: "VendaItems",
                type: "decimal(12,4)",
                precision: 12,
                scale: 4,
                nullable: true);

            // 20/11 (Bruno): vendas já feitas com peças do stock passam a contar os portes da compra (divididos por igual).
            migrationBuilder.Sql(@"
UPDATE vi SET
  PortesUnitario = ROUND(d.PortesPagos / u.Unidades, 4),
  PortesUnitarioSemIva = ROUND((d.PortesPagos - CASE WHEN ISNULL(d.PortesIva, 0) < 0 THEN 0 WHEN ISNULL(d.PortesIva, 0) > d.PortesPagos THEN d.PortesPagos ELSE ISNULL(d.PortesIva, 0) END) / u.Unidades, 4)
FROM VendaItems vi
JOIN ComprasLinhas l ON l.Id = vi.CompraLinhaId
JOIN ComprasDocumentos d ON d.Id = l.CompraDocumentoId
JOIN (SELECT CompraDocumentoId, SUM(Quantidade) AS Unidades FROM ComprasLinhas GROUP BY CompraDocumentoId) u ON u.CompraDocumentoId = d.Id
WHERE d.PortesPagos > 0 AND u.Unidades > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PortesUnitario",
                table: "VendaItems");

            migrationBuilder.DropColumn(
                name: "PortesUnitarioSemIva",
                table: "VendaItems");
        }
    }
}
