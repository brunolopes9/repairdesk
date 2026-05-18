using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint41CustomEquipmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EquipmentFieldTemplateId",
                table: "Reparacoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentFieldTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentFieldTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    VisibleInPortal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentFieldDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentFieldDefinitions_EquipmentFieldTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "EquipmentFieldTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentFieldValues_EquipmentFieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "EquipmentFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentFieldValues_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_EquipmentFieldTemplateId",
                table: "Reparacoes",
                column: "EquipmentFieldTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_EquipmentFieldTemplateId",
                table: "Reparacoes",
                columns: new[] { "TenantId", "EquipmentFieldTemplateId" },
                filter: "[EquipmentFieldTemplateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentFieldDefinitions_TemplateId",
                table: "EquipmentFieldDefinitions",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentFieldDefinitions_TenantId_TemplateId_Ordem",
                table: "EquipmentFieldDefinitions",
                columns: new[] { "TenantId", "TemplateId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentFieldTemplates_TenantId_IsActive_Ordem",
                table: "EquipmentFieldTemplates",
                columns: new[] { "TenantId", "IsActive", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentFieldTemplates_TenantId_Nome",
                table: "EquipmentFieldTemplates",
                columns: new[] { "TenantId", "Nome" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentFieldValues_FieldDefinitionId",
                table: "EquipmentFieldValues",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentFieldValues_ReparacaoId_FieldDefinitionId",
                table: "EquipmentFieldValues",
                columns: new[] { "ReparacaoId", "FieldDefinitionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentFieldValues_TenantId_ReparacaoId",
                table: "EquipmentFieldValues",
                columns: new[] { "TenantId", "ReparacaoId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Reparacoes_EquipmentFieldTemplates_EquipmentFieldTemplateId",
                table: "Reparacoes",
                column: "EquipmentFieldTemplateId",
                principalTable: "EquipmentFieldTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reparacoes_EquipmentFieldTemplates_EquipmentFieldTemplateId",
                table: "Reparacoes");

            migrationBuilder.DropTable(
                name: "EquipmentFieldValues");

            migrationBuilder.DropTable(
                name: "EquipmentFieldDefinitions");

            migrationBuilder.DropTable(
                name: "EquipmentFieldTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Reparacoes_EquipmentFieldTemplateId",
                table: "Reparacoes");

            migrationBuilder.DropIndex(
                name: "IX_Reparacoes_TenantId_EquipmentFieldTemplateId",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "EquipmentFieldTemplateId",
                table: "Reparacoes");
        }
    }
}
