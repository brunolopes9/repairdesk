using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint703PortesPorEnvio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 22/11 (Bruno): faturas do mesmo fornecedor no mesmo dia = o mesmo envio; os portes dividem-se por todas as unidades.
            migrationBuilder.Sql(@"
WITH envio AS (
  SELECT d.Id, d.FornecedorId, CAST(d.Data AS date) AS Dia FROM ComprasDocumentos d
), tot AS (
  SELECT d.FornecedorId, CAST(d.Data AS date) AS Dia, SUM(d.PortesPagos) AS Portes,
         SUM(CASE WHEN ISNULL(d.PortesIva,0) < 0 THEN 0 ELSE ISNULL(d.PortesIva,0) END) AS Iva,
         (SELECT SUM(l.Quantidade) FROM ComprasLinhas l JOIN ComprasDocumentos d2 ON d2.Id = l.CompraDocumentoId
           WHERE d2.FornecedorId = d.FornecedorId AND CAST(d2.Data AS date) = CAST(d.Data AS date)) AS Unidades
  FROM ComprasDocumentos d GROUP BY d.FornecedorId, CAST(d.Data AS date)
)
UPDATE vi SET
  PortesUnitario = CASE WHEN t.Portes > 0 THEN ROUND(t.Portes / t.Unidades, 4) ELSE NULL END,
  PortesUnitarioSemIva = CASE WHEN t.Portes > 0 THEN ROUND((t.Portes - CASE WHEN t.Iva > t.Portes THEN t.Portes ELSE t.Iva END) / t.Unidades, 4) ELSE NULL END
FROM VendaItems vi
JOIN ComprasLinhas l ON l.Id = vi.CompraLinhaId
JOIN envio e ON e.Id = l.CompraDocumentoId
JOIN tot t ON t.FornecedorId = e.FornecedorId AND t.Dia = e.Dia
WHERE t.Unidades > 0;");


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
