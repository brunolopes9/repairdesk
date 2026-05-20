using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint127GarantiaPorCondicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defaults seguem DL 84/2021: 18m (540 dias) é o mínimo legal para refurbished/usado
            // com acordo expresso; OpenBox fica 2 anos (730) como middle-ground entre novo e
            // refurbished. Tenants existentes herdam estes valores — se a política comercial
            // for diferente (ex: 3 anos uniformes), editam em /definicoes.
            migrationBuilder.AddColumn<int>(
                name: "GarantiaVendaOpenBoxDias",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 730);

            migrationBuilder.AddColumn<int>(
                name: "GarantiaVendaRecondicionadoDias",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 540);

            migrationBuilder.AddColumn<int>(
                name: "GarantiaVendaUsadoDias",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 540);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GarantiaVendaOpenBoxDias",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "GarantiaVendaRecondicionadoDias",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "GarantiaVendaUsadoDias",
                table: "Tenants");
        }
    }
}
