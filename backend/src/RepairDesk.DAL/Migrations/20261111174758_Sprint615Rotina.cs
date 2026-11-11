using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint615Rotina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RotinaItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Detalhe = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Hora = table.Column<TimeOnly>(type: "time", nullable: false),
                    DuracaoMin = table.Column<int>(type: "int", nullable: false),
                    Dias = table.Column<int>(type: "int", nullable: false),
                    IntervaloDias = table.Column<int>(type: "int", nullable: true),
                    DataInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    AvisosAntes = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Insistir = table.Column<bool>(type: "bit", nullable: false),
                    AvisarFim = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RotinaItens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RotinaRegistos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RotinaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Dia = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: true),
                    RegistadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdiadoAte = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Valor = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Avisos = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RotinaRegistos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RotinaRegistos_RotinaItens_RotinaItemId",
                        column: x => x.RotinaItemId,
                        principalTable: "RotinaItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RotinaItens_TenantId_UserId",
                table: "RotinaItens",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RotinaRegistos_RotinaItemId_Dia",
                table: "RotinaRegistos",
                columns: new[] { "RotinaItemId", "Dia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RotinaRegistos_TenantId_UserId_Dia",
                table: "RotinaRegistos",
                columns: new[] { "TenantId", "UserId", "Dia" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RotinaRegistos");

            migrationBuilder.DropTable(
                name: "RotinaItens");
        }
    }
}
