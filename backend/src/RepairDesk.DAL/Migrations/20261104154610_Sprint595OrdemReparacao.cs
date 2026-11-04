using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint595OrdemReparacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PeloLink",
                table: "VendaAssinaturas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "VendaFichas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Imei = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SenhaTipo = table.Column<int>(type: "int", nullable: false),
                    SenhaCifrada = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PermiteTestes = table.Column<bool>(type: "bit", nullable: true),
                    EstadoFisico = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Acessorios = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChecklistJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendaFichas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendaFichas_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendaFichas_VendaId",
                table: "VendaFichas",
                column: "VendaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendaFichas");

            migrationBuilder.DropColumn(
                name: "PeloLink",
                table: "VendaAssinaturas");
        }
    }
}
