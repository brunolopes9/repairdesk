using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint300_CashRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyClosings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OpeningCents = table.Column<int>(type: "int", nullable: false),
                    ExpectedClosingCents = table.Column<int>(type: "int", nullable: false),
                    ActualClosingCents = table.Column<int>(type: "int", nullable: true),
                    DiffCents = table.Column<int>(type: "int", nullable: true),
                    CashEntriesCents = table.Column<int>(type: "int", nullable: false),
                    CashExitsCents = table.Column<int>(type: "int", nullable: false),
                    MbwayCents = table.Column<int>(type: "int", nullable: false),
                    MultibancoCents = table.Column<int>(type: "int", nullable: false),
                    CardCents = table.Column<int>(type: "int", nullable: false),
                    OtherCents = table.Column<int>(type: "int", nullable: false),
                    ZReportPdfUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyClosings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DailyClosingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    AmountCents = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "DailyClosings");
        }
    }
}
