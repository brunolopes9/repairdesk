using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint239_RefreshTokenLastUsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "Auth_RefreshTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [Auth_RefreshTokens]
                SET [LastUsedAt] = [CreatedAt]
                WHERE [LastUsedAt] IS NULL
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Auth_RefreshTokens_RevokedAt_LastUsedAt",
                table: "Auth_RefreshTokens",
                columns: new[] { "RevokedAt", "LastUsedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Auth_RefreshTokens_RevokedAt_LastUsedAt",
                table: "Auth_RefreshTokens");

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "Auth_RefreshTokens");
        }
    }
}
