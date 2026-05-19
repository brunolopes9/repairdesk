using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint62VendaImei : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Imei",
                table: "VendaItems",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Imei2",
                table: "VendaItems",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GarantiaVendaExclusoesDefault",
                table: "Tenants",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GarantiaVendaCoberturaDefault",
                table: "Tenants",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendaItems_TenantId_Imei",
                table: "VendaItems",
                columns: new[] { "TenantId", "Imei" },
                filter: "[Imei] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendaItems_TenantId_Imei",
                table: "VendaItems");

            migrationBuilder.DropColumn(
                name: "Imei",
                table: "VendaItems");

            migrationBuilder.DropColumn(
                name: "Imei2",
                table: "VendaItems");

            migrationBuilder.AlterColumn<string>(
                name: "GarantiaVendaExclusoesDefault",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GarantiaVendaCoberturaDefault",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
