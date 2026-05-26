using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint344_SignatureCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignatureCaptures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    ImagemDataUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssinanteNome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssinanteContacto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemoteIp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CapturedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureCaptures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignatureCaptures_Auth_Users_CapturedByUserId",
                        column: x => x.CapturedByUserId,
                        principalTable: "Auth_Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SignatureCaptures_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignatureCaptures_CapturedByUserId",
                table: "SignatureCaptures",
                column: "CapturedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureCaptures_ReparacaoId",
                table: "SignatureCaptures",
                column: "ReparacaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignatureCaptures");
        }
    }
}
