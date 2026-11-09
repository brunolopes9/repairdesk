using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint599RedesSociaisLigacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ConteudoAutoPublicar",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MetaAppId",
                table: "Tenants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaAppSecretCipherText",
                table: "Tenants",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThreadsAppId",
                table: "Tenants",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThreadsAppSecretCipherText",
                table: "Tenants",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RedeSocialLigacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rede = table.Column<int>(type: "int", nullable: false),
                    ContaId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContaNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TokenCipherText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TokenExpiraEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LigadaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LigadaPorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Valida = table.Column<bool>(type: "bit", nullable: false),
                    UltimoErro = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedeSocialLigacoes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RedeSocialLigacoes_TenantId_Rede",
                table: "RedeSocialLigacoes",
                columns: new[] { "TenantId", "Rede" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RedeSocialLigacoes");

            migrationBuilder.DropColumn(
                name: "ConteudoAutoPublicar",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MetaAppId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MetaAppSecretCipherText",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ThreadsAppId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ThreadsAppSecretCipherText",
                table: "Tenants");
        }
    }
}
