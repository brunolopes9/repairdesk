using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint620FaturasEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaturasImapHost",
                table: "Tenants",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaturasImapPasswordCifrada",
                table: "Tenants",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FaturasImapPort",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FaturasImapUltimaLeitura",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaturasImapUltimoErro",
                table: "Tenants",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaturasImapUser",
                table: "Tenants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaturasImapHost",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FaturasImapPasswordCifrada",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FaturasImapPort",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FaturasImapUltimaLeitura",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FaturasImapUltimoErro",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FaturasImapUser",
                table: "Tenants");
        }
    }
}
