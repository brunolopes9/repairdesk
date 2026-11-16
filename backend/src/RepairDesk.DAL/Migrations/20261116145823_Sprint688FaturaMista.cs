using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint688FaturaMista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompraDocumentoId",
                table: "Despesas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_CompraDocumentoId",
                table: "Despesas",
                column: "CompraDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_TenantId_CompraDocumentoId",
                table: "Despesas",
                columns: new[] { "TenantId", "CompraDocumentoId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Despesas_ComprasDocumentos_CompraDocumentoId",
                table: "Despesas",
                column: "CompraDocumentoId",
                principalTable: "ComprasDocumentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Despesas_ComprasDocumentos_CompraDocumentoId",
                table: "Despesas");

            migrationBuilder.DropIndex(
                name: "IX_Despesas_CompraDocumentoId",
                table: "Despesas");

            migrationBuilder.DropIndex(
                name: "IX_Despesas_TenantId_CompraDocumentoId",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "CompraDocumentoId",
                table: "Despesas");
        }
    }
}
