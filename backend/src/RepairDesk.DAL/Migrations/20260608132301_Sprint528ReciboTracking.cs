using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint528ReciboTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReciboEmitidoEm",
                table: "Vendas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReciboNumero",
                table: "Vendas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReciboEmitidoEm",
                table: "Trabalhos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReciboNumero",
                table: "Trabalhos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReciboEmitidoEm",
                table: "Reparacoes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReciboNumero",
                table: "Reparacoes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReciboEmitidoEm",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "ReciboNumero",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "ReciboEmitidoEm",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "ReciboNumero",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "ReciboEmitidoEm",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "ReciboNumero",
                table: "Reparacoes");
        }
    }
}
