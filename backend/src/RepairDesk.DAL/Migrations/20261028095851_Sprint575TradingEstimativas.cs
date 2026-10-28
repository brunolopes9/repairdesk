using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint575TradingEstimativas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Atividade",
                table: "Vendas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Faturas da série TRADING (rewards de prop firms) passam a ser da atividade de trading.
            migrationBuilder.Sql("UPDATE Vendas SET Atividade = 1 WHERE InvoiceNumber LIKE '% TRADING/%'");

            migrationBuilder.AddColumn<DateTime>(
                name: "InicioAtividade",
                table: "Tenants",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Atividade",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "InicioAtividade",
                table: "Tenants");
        }
    }
}
