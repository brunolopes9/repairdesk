using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint635Alimentacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RotinaAlimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RotinaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Quantidade = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Kcal = table.Column<decimal>(type: "decimal(7,1)", precision: 7, scale: 1, nullable: false),
                    ProteinaG = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: false),
                    HidratosG = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: false),
                    GorduraG = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RotinaAlimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RotinaAlimentos_RotinaItens_RotinaItemId",
                        column: x => x.RotinaItemId,
                        principalTable: "RotinaItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RotinaAlimentos_RotinaItemId",
                table: "RotinaAlimentos",
                column: "RotinaItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RotinaAlimentos");
        }
    }
}
