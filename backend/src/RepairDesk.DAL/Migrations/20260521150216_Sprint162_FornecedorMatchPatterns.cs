using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint162_FornecedorMatchPatterns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MatchPatternsJson",
                table: "Fornecedores",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MatchPatternsJson",
                table: "Fornecedores");
        }
    }
}
