using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint649TipoFaturasPeloHistorico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sprint 649: tipo de faturas de cada fornecedor deduzido do histórico (só os que ainda estão em «perguntar»).
            // Tem compras registadas → as faturas dele são compras (peças). Só tem despesas → são despesas.
            migrationBuilder.Sql(@"
UPDATE f SET f.DefaultImportAction = 1
FROM Fornecedores f
WHERE f.DefaultImportAction = 0
  AND EXISTS (SELECT 1 FROM ComprasDocumentos c WHERE c.FornecedorId = f.Id);

UPDATE f SET f.DefaultImportAction = 2
FROM Fornecedores f
WHERE f.DefaultImportAction = 0
  AND EXISTS (SELECT 1 FROM Despesas d WHERE d.TenantId = f.TenantId AND d.IsDeleted = 0
              AND LTRIM(RTRIM(d.Fornecedor)) = f.Name);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
