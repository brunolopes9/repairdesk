using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint558RemoveCaixaPrecosTrabalhos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Despesas_Trabalhos_TrabalhoId",
                table: "Despesas");

            migrationBuilder.DropForeignKey(
                name: "FK_Parts_PriceTableEntries_PriceTableEntryId",
                table: "Parts");

            migrationBuilder.DropForeignKey(
                name: "FK_RepairRequests_Trabalhos_TrabalhoId",
                table: "RepairRequests");

            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "PartKitItems");

            migrationBuilder.DropTable(
                name: "PriceTableEntries");

            migrationBuilder.DropTable(
                name: "ServiceItems");

            migrationBuilder.DropTable(
                name: "StockTakeItems");

            migrationBuilder.DropTable(
                name: "Trabalhos");

            migrationBuilder.DropTable(
                name: "DailyClosings");

            migrationBuilder.DropTable(
                name: "PartKits");

            migrationBuilder.DropTable(
                name: "StockTakes");

            migrationBuilder.DropIndex(
                name: "IX_Parts_PriceTableEntryId",
                table: "Parts");

            migrationBuilder.DropIndex(
                name: "IX_Parts_TenantId_PriceTableEntryId",
                table: "Parts");

            migrationBuilder.DropIndex(
                name: "IX_Despesas_TenantId_TrabalhoId",
                table: "Despesas");

            migrationBuilder.DropIndex(
                name: "IX_Despesas_TrabalhoId",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "PriceTableEntryId",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "TrabalhoId",
                table: "Despesas");

            // Os ids antigos apontavam para Trabalhos (apagados): limpar antes de virar FK para Vendas.
            migrationBuilder.Sql("UPDATE [RepairRequests] SET [TrabalhoId] = NULL;");
            migrationBuilder.RenameColumn(
                name: "TrabalhoId",
                table: "RepairRequests",
                newName: "VendaId");

            migrationBuilder.RenameIndex(
                name: "IX_RepairRequests_TrabalhoId",
                table: "RepairRequests",
                newName: "IX_RepairRequests_VendaId");

            migrationBuilder.AddForeignKey(
                name: "FK_RepairRequests_Vendas_VendaId",
                table: "RepairRequests",
                column: "VendaId",
                principalTable: "Vendas",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RepairRequests_Vendas_VendaId",
                table: "RepairRequests");

            migrationBuilder.RenameColumn(
                name: "VendaId",
                table: "RepairRequests",
                newName: "TrabalhoId");

            migrationBuilder.RenameIndex(
                name: "IX_RepairRequests_VendaId",
                table: "RepairRequests",
                newName: "IX_RepairRequests_TrabalhoId");

            migrationBuilder.AddColumn<Guid>(
                name: "PriceTableEntryId",
                table: "Parts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TrabalhoId",
                table: "Despesas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DailyClosings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActualClosingCents = table.Column<int>(type: "int", nullable: true),
                    CardCents = table.Column<int>(type: "int", nullable: false),
                    CashEntriesCents = table.Column<int>(type: "int", nullable: false),
                    CashExitsCents = table.Column<int>(type: "int", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DiffCents = table.Column<int>(type: "int", nullable: true),
                    ExpectedClosingCents = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MbwayCents = table.Column<int>(type: "int", nullable: false),
                    MultibancoCents = table.Column<int>(type: "int", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OpeningCents = table.Column<int>(type: "int", nullable: false),
                    OtherCents = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ZReportPdfUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyClosings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartKits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    MaoDeObraCents = table.Column<int>(type: "int", nullable: false),
                    MaoDeObraDescricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrecoFinalCents = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartKits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceTableEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustoPecaCents = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PvpCents = table.Column<int>(type: "int", nullable: false),
                    Servico = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TempoEstimadoMin = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceTableEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GarantiaDiasCliente = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrecoCents = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockTakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trabalhos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataConclusao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EstadoPagamento = table.Column<int>(type: "int", nullable: false),
                    HorasGastas = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    InvoiceEmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    OrcamentoCents = table.Column<int>(type: "int", nullable: true),
                    PrecoFinalCents = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trabalhos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trabalhos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyClosingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AmountCents = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashMovements_DailyClosings_DailyClosingId",
                        column: x => x.DailyClosingId,
                        principalTable: "DailyClosings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CashMovements_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CashMovements_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PartKitItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartKitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartKitItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartKitItems_PartKits_PartKitId",
                        column: x => x.PartKitId,
                        principalTable: "PartKits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartKitItems_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockTakeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockTakeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContadoByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    QtdContada = table.Column<int>(type: "int", nullable: true),
                    QtdSistema = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTakeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockTakeItems_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockTakeItems_StockTakes_StockTakeId",
                        column: x => x.StockTakeId,
                        principalTable: "StockTakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_PriceTableEntryId",
                table: "Parts",
                column: "PriceTableEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_TenantId_PriceTableEntryId",
                table: "Parts",
                columns: new[] { "TenantId", "PriceTableEntryId" },
                filter: "[PriceTableEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_TenantId_TrabalhoId",
                table: "Despesas",
                columns: new[] { "TenantId", "TrabalhoId" },
                filter: "[TrabalhoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_TrabalhoId",
                table: "Despesas",
                column: "TrabalhoId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_DailyClosingId",
                table: "CashMovements",
                column: "DailyClosingId",
                filter: "[DailyClosingId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ReparacaoId",
                table: "CashMovements",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_TenantId_LocationId_OccurredAt",
                table: "CashMovements",
                columns: new[] { "TenantId", "LocationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_TenantId_OccurredAt",
                table: "CashMovements",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_VendaId",
                table: "CashMovements",
                column: "VendaId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_Tenant_Location_Date_Unique",
                table: "DailyClosings",
                columns: new[] { "TenantId", "LocationId", "Date" },
                unique: true,
                filter: "[LocationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_TenantId_Date",
                table: "DailyClosings",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_PartKitItems_PartId",
                table: "PartKitItems",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PartKitItems_PartKitId",
                table: "PartKitItems",
                column: "PartKitId");

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

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItems_PartId",
                table: "StockTakeItems",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItems_StockTakeId",
                table: "StockTakeItems",
                column: "StockTakeId");

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_ClienteId",
                table: "Trabalhos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_TenantId_Categoria",
                table: "Trabalhos",
                columns: new[] { "TenantId", "Categoria" });

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_TenantId_Numero",
                table: "Trabalhos",
                columns: new[] { "TenantId", "Numero" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_TenantId_Status",
                table: "Trabalhos",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Despesas_Trabalhos_TrabalhoId",
                table: "Despesas",
                column: "TrabalhoId",
                principalTable: "Trabalhos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Parts_PriceTableEntries_PriceTableEntryId",
                table: "Parts",
                column: "PriceTableEntryId",
                principalTable: "PriceTableEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RepairRequests_Trabalhos_TrabalhoId",
                table: "RepairRequests",
                column: "TrabalhoId",
                principalTable: "Trabalhos",
                principalColumn: "Id");
        }
    }
}
