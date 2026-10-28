using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint573IvaDespesas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeducaoPercent",
                table: "Despesas",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "IvaCents",
                table: "Despesas",
                type: "int",
                nullable: true);

            // Faturas de despesa importadas: fornecedor intra-UE → autoliquidação (IVA 0 na fatura);
            // nas outras, o IVA é o lido do PDF (total − subtotal) quando o total aprovado é o mesmo.
            migrationBuilder.Sql(@"
UPDATE d SET d.ReverseCharge = 1, d.IvaCents = 0
FROM Despesas d
JOIN SupplierInvoiceImports i ON i.DespesaId = d.Id
JOIN Fornecedores f ON f.Id = i.FornecedorId
WHERE f.RegimeIva = 1;

UPDATE d SET d.IvaCents = i.ParsedTotalCents - i.ParsedSubtotalCents
FROM Despesas d
JOIN SupplierInvoiceImports i ON i.DespesaId = d.Id
WHERE d.ReverseCharge = 0 AND d.IvaCents IS NULL
  AND i.ParsedSubtotalCents > 0 AND i.ParsedTotalCents >= i.ParsedSubtotalCents
  AND i.ParsedTotalCents = d.ValorCents;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeducaoPercent",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "IvaCents",
                table: "Despesas");
        }
    }
}
