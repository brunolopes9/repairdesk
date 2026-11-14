using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint659ContasLoja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClienteLojaId",
                table: "EncomendasLoja",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientesLoja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Telefone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    FalhasLogin = table.Column<int>(type: "int", nullable: false),
                    BloqueadoAte = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResetTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResetExpiraEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoLoginEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientesLoja", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientesLojaSessoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientesLojaSessoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientesLojaSessoes_ClientesLoja_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "ClientesLoja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncomendasLoja_ClienteLojaId",
                table: "EncomendasLoja",
                column: "ClienteLojaId",
                filter: "[ClienteLojaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientesLoja_ResetTokenHash",
                table: "ClientesLoja",
                column: "ResetTokenHash",
                filter: "[ResetTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClientesLoja_TenantId_Email",
                table: "ClientesLoja",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientesLojaSessoes_ClienteId",
                table: "ClientesLojaSessoes",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientesLojaSessoes_TokenHash",
                table: "ClientesLojaSessoes",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientesLojaSessoes");

            migrationBuilder.DropTable(
                name: "ClientesLoja");

            migrationBuilder.DropIndex(
                name: "IX_EncomendasLoja_ClienteLojaId",
                table: "EncomendasLoja");

            migrationBuilder.DropColumn(
                name: "ClienteLojaId",
                table: "EncomendasLoja");
        }
    }
}
