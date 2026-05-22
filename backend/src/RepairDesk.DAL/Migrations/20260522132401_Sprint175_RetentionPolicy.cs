using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint175_RetentionPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetentionApprovedPdfDays",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionFailedDays",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionRejectedDays",
                table: "Tenants",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetentionApprovedPdfDays",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RetentionFailedDays",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RetentionRejectedDays",
                table: "Tenants");
        }
    }
}
