using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTrabalhoDespesa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Trabalhos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DataInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataConclusao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrcamentoCents = table.Column<int>(type: "int", nullable: true),
                    PrecoFinalCents = table.Column<int>(type: "int", nullable: true),
                    HorasGastas = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EstadoPagamento = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trabalhos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trabalhos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Despesas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    ValorCents = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fornecedor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Despesas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Despesas_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Despesas_Trabalhos_TrabalhoId",
                        column: x => x.TrabalhoId,
                        principalTable: "Trabalhos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_ReparacaoId",
                table: "Despesas",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_TenantId_Categoria",
                table: "Despesas",
                columns: new[] { "TenantId", "Categoria" });

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_TenantId_Data",
                table: "Despesas",
                columns: new[] { "TenantId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_TenantId_ReparacaoId",
                table: "Despesas",
                columns: new[] { "TenantId", "ReparacaoId" },
                filter: "[ReparacaoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_TenantId_TrabalhoId",
                table: "Despesas",
                columns: new[] { "TenantId", "TrabalhoId" },
                filter: "[TrabalhoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_TrabalhoId",
                table: "Despesas",
                column: "TrabalhoId");

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_ClienteId",
                table: "Trabalhos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_TenantId_Categoria",
                table: "Trabalhos",
                columns: new[] { "TenantId", "Categoria" });

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_TenantId_Numero",
                table: "Trabalhos",
                columns: new[] { "TenantId", "Numero" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_TenantId_Status",
                table: "Trabalhos",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Despesas");

            migrationBuilder.DropTable(
                name: "Trabalhos");
        }
    }
}
