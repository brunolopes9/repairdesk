using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint366_StaffPushSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffPushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    P256dh = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Auth = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffPushSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffPushSubscriptions_TenantId",
                table: "StaffPushSubscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPushSubscriptions_UserId_Endpoint",
                table: "StaffPushSubscriptions",
                columns: new[] { "UserId", "Endpoint" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffPushSubscriptions");
        }
    }
}
