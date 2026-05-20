using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint147_SupplierInvoiceImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierInvoiceImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FornecedorNameRaw = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PdfSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PdfRelativePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PdfBytesSize = table.Column<int>(type: "int", nullable: false),
                    EmailMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmailSubject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmailFrom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EmailReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParsedTotalCents = table.Column<int>(type: "int", nullable: true),
                    ParsedSubtotalCents = table.Column<int>(type: "int", nullable: true),
                    ParsedItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParsedDocumentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ParsedDocumentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParseConfidence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DespesaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByApiKeyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInvoiceImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInvoiceImports_Despesas_DespesaId",
                        column: x => x.DespesaId,
                        principalTable: "Despesas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierInvoiceImports_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceImports_DespesaId",
                table: "SupplierInvoiceImports",
                column: "DespesaId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceImports_FornecedorId",
                table: "SupplierInvoiceImports",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceImports_TenantId_PdfSha256",
                table: "SupplierInvoiceImports",
                columns: new[] { "TenantId", "PdfSha256" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceImports_TenantId_Status_CreatedAt",
                table: "SupplierInvoiceImports",
                columns: new[] { "TenantId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierInvoiceImports");
        }
    }
}
