using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint437_RepairRequestToTrabalho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TrabalhoId",
                table: "RepairRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepairRequests_TrabalhoId",
                table: "RepairRequests",
                column: "TrabalhoId");

            migrationBuilder.AddForeignKey(
                name: "FK_RepairRequests_Trabalhos_TrabalhoId",
                table: "RepairRequests",
                column: "TrabalhoId",
                principalTable: "Trabalhos",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RepairRequests_Trabalhos_TrabalhoId",
                table: "RepairRequests");

            migrationBuilder.DropIndex(
                name: "IX_RepairRequests_TrabalhoId",
                table: "RepairRequests");

            migrationBuilder.DropColumn(
                name: "TrabalhoId",
                table: "RepairRequests");
        }
    }
}
