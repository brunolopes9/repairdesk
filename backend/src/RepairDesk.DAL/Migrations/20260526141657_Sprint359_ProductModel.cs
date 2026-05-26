using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint359_ProductModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ModelId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModelTemplateId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescriptionMarkdown = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpecsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatteryUpgradePriceCents = table.Column<int>(type: "int", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Series = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductModelImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Alt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Url480w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url1024w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url2048w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvifUrl480w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvifUrl1024w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvifUrl2048w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BlurDataUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    OptimizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductModelImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductModelImages_ProductModels_ProductModelId",
                        column: x => x.ProductModelId,
                        principalTable: "ProductModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ModelTemplateId",
                table: "Products",
                column: "ModelTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductModelImages_ProductModelId",
                table: "ProductModelImages",
                column: "ProductModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ProductModels_ModelTemplateId",
                table: "Products",
                column: "ModelTemplateId",
                principalTable: "ProductModels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_ProductModels_ModelTemplateId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "ProductModelImages");

            migrationBuilder.DropTable(
                name: "ProductModels");

            migrationBuilder.DropIndex(
                name: "IX_Products_ModelTemplateId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ModelId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ModelTemplateId",
                table: "Products");
        }
    }
}
