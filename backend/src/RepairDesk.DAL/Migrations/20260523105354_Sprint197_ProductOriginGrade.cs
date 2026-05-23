using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint197_ProductOriginGrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Grade",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Sprint 197: populate Origin+Grade dos legacy Grading values.
            // ProductGrading: Novo=0, GradeA=1, GradeB=2, GradeC=3, OpenBox=4, Premium=5
            // ProductOrigin:  New=0, Used=1, Refurbished=2
            // ProductGrade:   Sealed=0, APlusPlus=1, APlus=2, A=3, BPlus=4, B=5, CPlus=6, C=7
            // Decisão Bruno: OpenBox = Used+A++ (sem reparação, exposição).
            migrationBuilder.Sql(@"
                UPDATE Products SET Origin = 0, Grade = 0 WHERE Grading = 0;  -- Novo → New+Sealed
                UPDATE Products SET Origin = 2, Grade = 3 WHERE Grading = 1;  -- GradeA → Refurbished+A
                UPDATE Products SET Origin = 2, Grade = 5 WHERE Grading = 2;  -- GradeB → Refurbished+B
                UPDATE Products SET Origin = 2, Grade = 7 WHERE Grading = 3;  -- GradeC → Refurbished+C
                UPDATE Products SET Origin = 1, Grade = 1 WHERE Grading = 4;  -- OpenBox → Used+A++
                UPDATE Products SET Origin = 1, Grade = 1 WHERE Grading = 5;  -- Premium → Used+A++ (Molano-speak)
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Grade",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Products");
        }
    }
}
