using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint99AuditServiceApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServiceApiKeyId",
                table: "AuditEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ServiceApiKeyId",
                table: "AuditEntries",
                column: "ServiceApiKeyId",
                filter: "[ServiceApiKeyId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditEntries_ServiceApiKeys_ServiceApiKeyId",
                table: "AuditEntries",
                column: "ServiceApiKeyId",
                principalTable: "ServiceApiKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditEntries_ServiceApiKeys_ServiceApiKeyId",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_ServiceApiKeyId",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "ServiceApiKeyId",
                table: "AuditEntries");
        }
    }
}
