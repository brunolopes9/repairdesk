using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint362_ProductModelFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_ProductModels_ModelTemplateId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_ModelTemplateId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ModelTemplateId",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ModelId",
                table: "Products",
                column: "ModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ProductModels_ModelId",
                table: "Products",
                column: "ModelId",
                principalTable: "ProductModels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_ProductModels_ModelId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_ModelId",
                table: "Products");

            migrationBuilder.AddColumn<Guid>(
                name: "ModelTemplateId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_ModelTemplateId",
                table: "Products",
                column: "ModelTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ProductModels_ModelTemplateId",
                table: "Products",
                column: "ModelTemplateId",
                principalTable: "ProductModels",
                principalColumn: "Id");
        }
    }
}
