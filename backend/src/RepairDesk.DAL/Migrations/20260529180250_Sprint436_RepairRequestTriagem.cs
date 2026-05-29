using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint436_RepairRequestTriagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotasInternas",
                table: "RepairRequests",
                type: "nvarchar(max)",
                nullable: true);

            // Default 1 = Normal (alinhado com o default da entity). Sem isto, pedidos
            // antigos ficariam com Prioridade=0 (Baixa) e desapareciam da inbox visual.
            migrationBuilder.AddColumn<int>(
                name: "Prioridade",
                table: "RepairRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotasInternas",
                table: "RepairRequests");

            migrationBuilder.DropColumn(
                name: "Prioridade",
                table: "RepairRequests");
        }
    }
}
