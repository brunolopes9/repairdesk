using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint704PagamentoNoSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MbEntidade",
                table: "EncomendasLoja",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MbReferencia",
                table: "EncomendasLoja",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MbValidade",
                table: "EncomendasLoja",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetodoPagamento",
                table: "EncomendasLoja",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PagamentoPedidoId",
                table: "EncomendasLoja",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MbEntidade",
                table: "EncomendasLoja");

            migrationBuilder.DropColumn(
                name: "MbReferencia",
                table: "EncomendasLoja");

            migrationBuilder.DropColumn(
                name: "MbValidade",
                table: "EncomendasLoja");

            migrationBuilder.DropColumn(
                name: "MetodoPagamento",
                table: "EncomendasLoja");

            migrationBuilder.DropColumn(
                name: "PagamentoPedidoId",
                table: "EncomendasLoja");
        }
    }
}
