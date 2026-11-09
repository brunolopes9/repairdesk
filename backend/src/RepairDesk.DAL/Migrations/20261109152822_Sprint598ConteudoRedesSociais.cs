using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint598ConteudoRedesSociais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConteudoPublicacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Linha = table.Column<int>(type: "int", nullable: false),
                    Formato = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Legenda = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    AgendadoPara = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    AprovadoPorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AprovadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRejeicao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AutoAprovada = table.Column<bool>(type: "bit", nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    ArtigoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PublicadaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteudoPublicacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConteudoDestinos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rede = table.Column<int>(type: "int", nullable: false),
                    LegendaPropria = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Permalink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    ProximaTentativaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoErro = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PublicadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteudoDestinos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConteudoDestinos_ConteudoPublicacoes_PublicacaoId",
                        column: x => x.PublicacaoId,
                        principalTable: "ConteudoPublicacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConteudoFicheiros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NomeOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    Largura = table.Column<int>(type: "int", nullable: true),
                    Altura = table.Column<int>(type: "int", nullable: true),
                    DuracaoSeg = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProcessadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteudoFicheiros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConteudoFicheiros_ConteudoPublicacoes_PublicacaoId",
                        column: x => x.PublicacaoId,
                        principalTable: "ConteudoPublicacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConteudoDestinos_PublicacaoId_Rede",
                table: "ConteudoDestinos",
                columns: new[] { "PublicacaoId", "Rede" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ConteudoDestinos_TenantId_Estado_ProximaTentativaEm",
                table: "ConteudoDestinos",
                columns: new[] { "TenantId", "Estado", "ProximaTentativaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_ConteudoFicheiros_PublicacaoId_Ordem",
                table: "ConteudoFicheiros",
                columns: new[] { "PublicacaoId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_ConteudoFicheiros_StorageKey",
                table: "ConteudoFicheiros",
                column: "StorageKey",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ConteudoFicheiros_TenantId_PublicacaoId_CreatedAt",
                table: "ConteudoFicheiros",
                columns: new[] { "TenantId", "PublicacaoId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConteudoPublicacoes_TenantId_Estado_AgendadoPara",
                table: "ConteudoPublicacoes",
                columns: new[] { "TenantId", "Estado", "AgendadoPara" });

            migrationBuilder.CreateIndex(
                name: "IX_ConteudoPublicacoes_TenantId_Numero",
                table: "ConteudoPublicacoes",
                columns: new[] { "TenantId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConteudoDestinos");

            migrationBuilder.DropTable(
                name: "ConteudoFicheiros");

            migrationBuilder.DropTable(
                name: "ConteudoPublicacoes");
        }
    }
}
