using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint60EstimateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EstimateEmittedAt",
                table: "Trabalhos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateExternalId",
                table: "Trabalhos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateNumber",
                table: "Trabalhos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimatePdfUrl",
                table: "Trabalhos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimateEmittedAt",
                table: "Reparacoes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateExternalId",
                table: "Reparacoes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateNumber",
                table: "Reparacoes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimatePdfUrl",
                table: "Reparacoes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_TenantId_EstimateExternalId",
                table: "Trabalhos",
                columns: new[] { "TenantId", "EstimateExternalId" },
                filter: "[EstimateExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_EstimateExternalId",
                table: "Reparacoes",
                columns: new[] { "TenantId", "EstimateExternalId" },
                filter: "[EstimateExternalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trabalhos_TenantId_EstimateExternalId",
                table: "Trabalhos");

            migrationBuilder.DropIndex(
                name: "IX_Reparacoes_TenantId_EstimateExternalId",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "EstimateEmittedAt",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "EstimateExternalId",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "EstimateNumber",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "EstimatePdfUrl",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "EstimateEmittedAt",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "EstimateExternalId",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "EstimateNumber",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "EstimatePdfUrl",
                table: "Reparacoes");
        }
    }
}
