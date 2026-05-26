using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint343_ReparacaoAssignedTo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToUserId",
                table: "Reparacoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_AssignedToUserId",
                table: "Reparacoes",
                column: "AssignedToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reparacoes_Auth_Users_AssignedToUserId",
                table: "Reparacoes",
                column: "AssignedToUserId",
                principalTable: "Auth_Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reparacoes_Auth_Users_AssignedToUserId",
                table: "Reparacoes");

            migrationBuilder.DropIndex(
                name: "IX_Reparacoes_AssignedToUserId",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "Reparacoes");
        }
    }
}
