using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint189_ProductImageSEO : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvifUrl1024w",
                table: "ProductImages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvifUrl2048w",
                table: "ProductImages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvifUrl480w",
                table: "ProductImages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlurDataUrl",
                table: "ProductImages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "ProductImages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OptimizedAt",
                table: "ProductImages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url1024w",
                table: "ProductImages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url2048w",
                table: "ProductImages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url480w",
                table: "ProductImages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "ProductImages",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvifUrl1024w",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "AvifUrl2048w",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "AvifUrl480w",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "BlurDataUrl",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "OptimizedAt",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "Url1024w",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "Url2048w",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "Url480w",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "ProductImages");
        }
    }
}
