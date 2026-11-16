using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint679Adiantamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdiantamentoCents",
                table: "Vendas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdiantamentoFatura",
                table: "Vendas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdiantamentoRecebidoEm",
                table: "Vendas",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdiantamentoCents",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "AdiantamentoFatura",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "AdiantamentoRecebidoEm",
                table: "Vendas");
        }
    }
}
