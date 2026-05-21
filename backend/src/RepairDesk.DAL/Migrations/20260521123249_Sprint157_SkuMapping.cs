using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint157_SkuMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkuMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplierSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SupplierProductName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    UseCount = table.Column<int>(type: "int", nullable: false),
                    CreatedFromImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkuMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkuMappings_SupplierInvoiceImports_CreatedFromImportId",
                        column: x => x.CreatedFromImportId,
                        principalTable: "SupplierInvoiceImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_CreatedFromImportId",
                table: "SkuMappings",
                column: "CreatedFromImportId");

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_TenantId_SupplierCode_SupplierSku",
                table: "SkuMappings",
                columns: new[] { "TenantId", "SupplierCode", "SupplierSku" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_TenantId_TargetType_TargetId",
                table: "SkuMappings",
                columns: new[] { "TenantId", "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkuMappings");
        }
    }
}
