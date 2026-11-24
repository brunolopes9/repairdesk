using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint707VisitasSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventosSite",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Visitante = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Sessao = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Caminho = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Referrer = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Origem = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Campanha = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Termo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Resultados = table.Column<int>(type: "int", nullable: true),
                    Produto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProdutoSlug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Duracao = table.Column<int>(type: "int", nullable: true),
                    Scroll = table.Column<int>(type: "int", nullable: true),
                    Encomenda = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Cliente = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Dispositivo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Sistema = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Navegador = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Ecra = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Idioma = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    Consentimento = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosSite", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventosSite_TenantId_CreatedAt",
                table: "EventosSite",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosSite_TenantId_Sessao",
                table: "EventosSite",
                columns: new[] { "TenantId", "Sessao" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventosSite");
        }
    }
}
