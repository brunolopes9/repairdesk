using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint205_ProductIsOpenBox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOpenBox",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Sprint 205: backfill — produtos com Grading legacy=OpenBox (4) marca IsOpenBox=true.
            // Sprint 197 mapeou OpenBox para Origin=Used+Grade=A++, mas perdeu distinção semântica.
            // Esta query recupera com base no Grading legacy ainda presente em DB.
            migrationBuilder.Sql("UPDATE Products SET IsOpenBox = 1 WHERE Grading = 4;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOpenBox",
                table: "Products");
        }
    }
}
