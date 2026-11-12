using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint632FaturasCloudflare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaturasEmailEndereco",
                table: "Tenants",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaturasEmailSegredoHash",
                table: "Tenants",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FaturasEmailUltimoEm",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_FaturasEmailEndereco",
                table: "Tenants",
                column: "FaturasEmailEndereco",
                unique: true,
                filter: "[FaturasEmailEndereco] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_FaturasEmailEndereco",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FaturasEmailEndereco",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FaturasEmailSegredoHash",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FaturasEmailUltimoEm",
                table: "Tenants");
        }
    }
}
