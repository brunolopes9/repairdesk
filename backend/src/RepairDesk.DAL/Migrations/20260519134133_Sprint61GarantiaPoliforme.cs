using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint61GarantiaPoliforme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Garantias_ReparacaoId",
                table: "Garantias");

            migrationBuilder.AddColumn<string>(
                name: "GarantiaVendaCoberturaDefault",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GarantiaVendaDiasDefault",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 1095);

            migrationBuilder.AddColumn<string>(
                name: "GarantiaVendaExclusoesDefault",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReparacaoId",
                table: "Garantias",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "Garantias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "VendaId",
                table: "Garantias",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Garantias_ReparacaoId",
                table: "Garantias",
                column: "ReparacaoId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [ReparacaoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Garantias_VendaId",
                table: "Garantias",
                column: "VendaId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [VendaId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Garantias_OneSource",
                table: "Garantias",
                sql: "([ReparacaoId] IS NOT NULL AND [VendaId] IS NULL) OR ([ReparacaoId] IS NULL AND [VendaId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Garantias_Vendas_VendaId",
                table: "Garantias",
                column: "VendaId",
                principalTable: "Vendas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Garantias_Vendas_VendaId",
                table: "Garantias");

            migrationBuilder.DropIndex(
                name: "IX_Garantias_ReparacaoId",
                table: "Garantias");

            migrationBuilder.DropIndex(
                name: "IX_Garantias_VendaId",
                table: "Garantias");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Garantias_OneSource",
                table: "Garantias");

            migrationBuilder.DropColumn(
                name: "GarantiaVendaCoberturaDefault",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "GarantiaVendaDiasDefault",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "GarantiaVendaExclusoesDefault",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "Garantias");

            migrationBuilder.DropColumn(
                name: "VendaId",
                table: "Garantias");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReparacaoId",
                table: "Garantias",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Garantias_ReparacaoId",
                table: "Garantias",
                column: "ReparacaoId",
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
