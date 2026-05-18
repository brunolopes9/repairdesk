using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint40Billing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvoiceEmittedAt",
                table: "Trabalhos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceExternalId",
                table: "Trabalhos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Trabalhos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoicePdfUrl",
                table: "Trabalhos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceProvider",
                table: "Trabalhos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoiceEmittedAt",
                table: "Reparacoes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceExternalId",
                table: "Reparacoes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Reparacoes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoicePdfUrl",
                table: "Reparacoes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceProvider",
                table: "Reparacoes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TenantBillingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    ApiKeyCipherText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientSecretCipherText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RefreshTokenCipherText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    DefaultDocumentType = table.Column<int>(type: "int", nullable: false),
                    DefaultSerieId = table.Column<int>(type: "int", nullable: true),
                    SandboxMode = table.Column<bool>(type: "bit", nullable: false),
                    DefaultProductId = table.Column<int>(type: "int", nullable: true),
                    DefaultTaxId = table.Column<int>(type: "int", nullable: true),
                    DefaultPaymentMethodId = table.Column<int>(type: "int", nullable: true),
                    DefaultMaturityDateId = table.Column<int>(type: "int", nullable: true),
                    FallbackCustomerId = table.Column<int>(type: "int", nullable: true),
                    ExemptionReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBillingSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantBillingSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_TenantId_InvoiceExternalId",
                table: "Trabalhos",
                columns: new[] { "TenantId", "InvoiceExternalId" },
                filter: "[InvoiceExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_InvoiceExternalId",
                table: "Reparacoes",
                columns: new[] { "TenantId", "InvoiceExternalId" },
                filter: "[InvoiceExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBillingSettings_TenantId",
                table: "TenantBillingSettings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantBillingSettings");

            migrationBuilder.DropIndex(
                name: "IX_Trabalhos_TenantId_InvoiceExternalId",
                table: "Trabalhos");

            migrationBuilder.DropIndex(
                name: "IX_Reparacoes_TenantId_InvoiceExternalId",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "InvoiceEmittedAt",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "InvoiceExternalId",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "InvoicePdfUrl",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "InvoiceProvider",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "InvoiceEmittedAt",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "InvoiceExternalId",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "InvoicePdfUrl",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "InvoiceProvider",
                table: "Reparacoes");
        }
    }
}
