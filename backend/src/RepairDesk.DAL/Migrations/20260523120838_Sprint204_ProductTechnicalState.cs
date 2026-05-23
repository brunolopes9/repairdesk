using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint204_ProductTechnicalState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BatteryHealthPercent",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalNotes",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalState",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatteryHealthPercent",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TechnicalNotes",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TechnicalState",
                table: "Products");
        }
    }
}
