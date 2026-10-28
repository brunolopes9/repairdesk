using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint580IrsJovemCoeficientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CodigoTradingRevistoEm",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeducoesColeta",
                table: "Tenants",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IrsJovemAno",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ServicosNaTabela151",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TradingNaTabela151",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: true); // prudente: trading a 0,75 até o dono dizer o contrário
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoTradingRevistoEm",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DeducoesColeta",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IrsJovemAno",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ServicosNaTabela151",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TradingNaTabela151",
                table: "Tenants");
        }
    }
}
