using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint433_PartKitBundlePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaoDeObraCents",
                table: "PartKits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MaoDeObraDescricao",
                table: "PartKits",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrecoFinalCents",
                table: "PartKits",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaoDeObraCents",
                table: "PartKits");

            migrationBuilder.DropColumn(
                name: "MaoDeObraDescricao",
                table: "PartKits");

            migrationBuilder.DropColumn(
                name: "PrecoFinalCents",
                table: "PartKits");
        }
    }
}
