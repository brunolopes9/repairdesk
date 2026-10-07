using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint557VendasUnificadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vendas_TenantId_Status",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "Vendas");

            // Status (Pendente 0 / Paga 1 / Cancelada 2) → Estado (Pronta 2 / Entregue 3 / Cancelada 4).
            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Vendas",
                newName: "Estado");
            migrationBuilder.Sql("UPDATE [Vendas] SET [Estado] = CASE [Estado] WHEN 0 THEN 2 WHEN 1 THEN 3 WHEN 2 THEN 4 ELSE [Estado] END;");

            migrationBuilder.AddColumn<string>(
                name: "Equipamento",
                table: "Vendas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Vendas antigas eram do Balcão → Produto (0).
            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "Vendas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Problema",
                table: "Vendas",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompraLinhaId",
                table: "VendaItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoUnitarioPago",
                table: "VendaItems",
                type: "decimal(12,4)",
                precision: 12,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxaIvaCompra",
                table: "VendaItems",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vendas_TenantId_Estado",
                table: "Vendas",
                columns: new[] { "TenantId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_VendaItems_CompraLinhaId",
                table: "VendaItems",
                column: "CompraLinhaId");

            migrationBuilder.CreateIndex(
                name: "IX_VendaItems_TenantId_CompraLinhaId",
                table: "VendaItems",
                columns: new[] { "TenantId", "CompraLinhaId" },
                filter: "[CompraLinhaId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_VendaItems_ComprasLinhas_CompraLinhaId",
                table: "VendaItems",
                column: "CompraLinhaId",
                principalTable: "ComprasLinhas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VendaItems_ComprasLinhas_CompraLinhaId",
                table: "VendaItems");

            migrationBuilder.DropIndex(
                name: "IX_Vendas_TenantId_Estado",
                table: "Vendas");

            migrationBuilder.DropIndex(
                name: "IX_VendaItems_CompraLinhaId",
                table: "VendaItems");

            migrationBuilder.DropIndex(
                name: "IX_VendaItems_TenantId_CompraLinhaId",
                table: "VendaItems");

            migrationBuilder.DropColumn(
                name: "Equipamento",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "Problema",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "CompraLinhaId",
                table: "VendaItems");

            migrationBuilder.DropColumn(
                name: "CustoUnitarioPago",
                table: "VendaItems");

            migrationBuilder.DropColumn(
                name: "TaxaIvaCompra",
                table: "VendaItems");

            migrationBuilder.Sql("UPDATE [Vendas] SET [Estado] = CASE [Estado] WHEN 3 THEN 1 WHEN 4 THEN 2 ELSE 0 END;");
            migrationBuilder.RenameColumn(
                name: "Estado",
                table: "Vendas",
                newName: "Status");

            migrationBuilder.AddColumn<int>(
                name: "Origem",
                table: "Vendas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Vendas_TenantId_Status",
                table: "Vendas",
                columns: new[] { "TenantId", "Status" });
        }
    }
}
