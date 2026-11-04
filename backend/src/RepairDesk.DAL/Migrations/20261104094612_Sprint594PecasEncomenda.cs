using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint594PecasEncomenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Encomenda",
                table: "VendaItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncomendaNota",
                table: "VendaItems",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Encomenda",
                table: "VendaItems");

            migrationBuilder.DropColumn(
                name: "EncomendaNota",
                table: "VendaItems");
        }
    }
}
