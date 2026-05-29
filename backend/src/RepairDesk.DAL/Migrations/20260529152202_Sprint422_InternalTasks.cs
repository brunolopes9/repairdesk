using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint422_InternalTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InternalTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalTasks_Auth_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Auth_Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InternalTasks_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternalTasks_AssignedToUserId",
                table: "InternalTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalTasks_ReparacaoId",
                table: "InternalTasks",
                column: "ReparacaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InternalTasks");
        }
    }
}
