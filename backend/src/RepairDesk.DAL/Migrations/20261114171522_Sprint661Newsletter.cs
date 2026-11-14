using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint661Newsletter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscritoresNewsletter",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    TokenConfirmacaoHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ConfirmacaoEnviadaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TokenCancelamento = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CanceladoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscritoresNewsletter", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscritoresNewsletter_TenantId_Email",
                table: "SubscritoresNewsletter",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscritoresNewsletter_TokenCancelamento",
                table: "SubscritoresNewsletter",
                column: "TokenCancelamento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscritoresNewsletter_TokenConfirmacaoHash",
                table: "SubscritoresNewsletter",
                column: "TokenConfirmacaoHash",
                filter: "[TokenConfirmacaoHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscritoresNewsletter");
        }
    }
}
