using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint655LojaArtigo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ArtigoId",
                table: "ProdutosLoja",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProdutosLoja_ArtigoId",
                table: "ProdutosLoja",
                column: "ArtigoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdutosLoja_TenantId_ArtigoId",
                table: "ProdutosLoja",
                columns: new[] { "TenantId", "ArtigoId" },
                unique: true,
                filter: "[ArtigoId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ProdutosLoja_Artigos_ArtigoId",
                table: "ProdutosLoja",
                column: "ArtigoId",
                principalTable: "Artigos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProdutosLoja_Artigos_ArtigoId",
                table: "ProdutosLoja");

            migrationBuilder.DropIndex(
                name: "IX_ProdutosLoja_ArtigoId",
                table: "ProdutosLoja");

            migrationBuilder.DropIndex(
                name: "IX_ProdutosLoja_TenantId_ArtigoId",
                table: "ProdutosLoja");

            migrationBuilder.DropColumn(
                name: "ArtigoId",
                table: "ProdutosLoja");
        }
    }
}
