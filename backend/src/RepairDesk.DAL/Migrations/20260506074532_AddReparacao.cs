using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddReparacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reparacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Equipamento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Imei = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Avaria = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Diagnostico = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    EstadoSince = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntregueEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrcamentoCents = table.Column<int>(type: "int", nullable: true),
                    OrcamentoAprovado = table.Column<bool>(type: "bit", nullable: false),
                    PrecoFinalCents = table.Column<int>(type: "int", nullable: true),
                    CustoPecasCents = table.Column<int>(type: "int", nullable: false),
                    HorasGastas = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EstadoPagamento = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reparacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reparacoes_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReparacaoEstadoLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstadoFrom = table.Column<int>(type: "int", nullable: true),
                    EstadoTo = table.Column<int>(type: "int", nullable: false),
                    MudouEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacaoEstadoLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReparacaoEstadoLogs_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoEstadoLogs_ReparacaoId",
                table: "ReparacaoEstadoLogs",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoEstadoLogs_TenantId_ReparacaoId_MudouEm",
                table: "ReparacaoEstadoLogs",
                columns: new[] { "TenantId", "ReparacaoId", "MudouEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_ClienteId",
                table: "Reparacoes",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_ClienteId",
                table: "Reparacoes",
                columns: new[] { "TenantId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_Estado",
                table: "Reparacoes",
                columns: new[] { "TenantId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_Numero",
                table: "Reparacoes",
                columns: new[] { "TenantId", "Numero" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReparacaoEstadoLogs");

            migrationBuilder.DropTable(
                name: "Reparacoes");
        }
    }
}
