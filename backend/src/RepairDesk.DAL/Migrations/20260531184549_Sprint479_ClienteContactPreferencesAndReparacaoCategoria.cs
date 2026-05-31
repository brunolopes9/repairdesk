using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint479_ClienteContactPreferencesAndReparacaoCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Categoria",
                table: "Reparacoes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AceitaMarketing",
                table: "Clientes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContactoPreferido",
                table: "Clientes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NaoContactar",
                table: "Clientes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "AceitaMarketing",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "ContactoPreferido",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "NaoContactar",
                table: "Clientes");
        }
    }
}
