using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint559ReparacoesComoVendas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Doc 94 Fase 4c ("recomeçar limpo", com backup automático antes da migração): as reparações
            // antigas deixam de existir. Limpa o que só fazia sentido ligado a elas ANTES de mudar as FKs,
            // senão as novas FKs para Vendas falhavam com ids de Reparações.
            migrationBuilder.Sql("DELETE FROM [Avaliacoes];");
            migrationBuilder.Sql("DELETE FROM [PushSubscriptions];");
            migrationBuilder.Sql("UPDATE [InternalTasks] SET [ReparacaoId] = NULL;");
            migrationBuilder.Sql("DELETE FROM [Garantias] WHERE [ReparacaoId] IS NOT NULL;");
            migrationBuilder.Sql("DELETE FROM [Payments] WHERE [ReparacaoId] IS NOT NULL AND [VendaId] IS NULL;");
            migrationBuilder.Sql("UPDATE [WhatsAppNotificationLogs] SET [Estado] = NULL, [EntityType] = 'Reparacao-antiga' WHERE [EntityType] = 'Reparacao';");

            migrationBuilder.DropForeignKey(
                name: "FK_Avaliacoes_Reparacoes_ReparacaoId",
                table: "Avaliacoes");

            migrationBuilder.DropForeignKey(
                name: "FK_Despesas_Reparacoes_ReparacaoId",
                table: "Despesas");

            migrationBuilder.DropForeignKey(
                name: "FK_Garantias_Reparacoes_ReparacaoId",
                table: "Garantias");

            migrationBuilder.DropForeignKey(
                name: "FK_InternalTasks_Reparacoes_ReparacaoId",
                table: "InternalTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_PushSubscriptions_Reparacoes_ReparacaoId",
                table: "PushSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_RepairRequests_Reparacoes_ReparacaoId",
                table: "RepairRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_VendaItems_Parts_PartId",
                table: "VendaItems");

            migrationBuilder.DropTable(
                name: "DiagnosticoExecucaoItems");

            migrationBuilder.DropTable(
                name: "DiagnosticoTemplateItems");

            migrationBuilder.DropTable(
                name: "EquipmentFieldValues");

            migrationBuilder.DropTable(
                name: "PartMovimentos");

            migrationBuilder.DropTable(
                name: "ReparacaoAssinaturas");

            migrationBuilder.DropTable(
                name: "ReparacaoComunicacoes");

            migrationBuilder.DropTable(
                name: "ReparacaoEstadoLogs");

            migrationBuilder.DropTable(
                name: "ReparacaoFotos");

            migrationBuilder.DropTable(
                name: "ReparacaoTagAssignments");

            migrationBuilder.DropTable(
                name: "ReparacaoTimeEntries");

            migrationBuilder.DropTable(
                name: "SignatureCaptures");

            migrationBuilder.DropTable(
                name: "SkuMappings");

            migrationBuilder.DropTable(
                name: "DiagnosticoExecucoes");

            migrationBuilder.DropTable(
                name: "EquipmentFieldDefinitions");

            migrationBuilder.DropTable(
                name: "Parts");

            migrationBuilder.DropTable(
                name: "ReparacaoTags");

            migrationBuilder.DropTable(
                name: "DiagnosticoTemplates");

            migrationBuilder.DropTable(
                name: "Reparacoes");

            migrationBuilder.DropTable(
                name: "EquipmentFieldTemplates");

            migrationBuilder.DropIndex(
                name: "IX_VendaItems_PartId",
                table: "VendaItems");

            migrationBuilder.DropIndex(
                name: "IX_VendaItems_TenantId_PartId",
                table: "VendaItems");

            migrationBuilder.DropIndex(
                name: "IX_RepairRequests_ReparacaoId",
                table: "RepairRequests");

            migrationBuilder.DropIndex(
                name: "IX_Garantias_ReparacaoId",
                table: "Garantias");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Garantias_OneSource",
                table: "Garantias");

            migrationBuilder.DropIndex(
                name: "IX_Despesas_ReparacaoId",
                table: "Despesas");

            migrationBuilder.DropIndex(
                name: "IX_Despesas_TenantId_ReparacaoId",
                table: "Despesas");

            migrationBuilder.DropColumn(
                name: "PartId",
                table: "VendaItems");

            migrationBuilder.DropColumn(
                name: "ReparacaoId",
                table: "RepairRequests");

            migrationBuilder.DropColumn(
                name: "ReparacaoId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReparacaoId",
                table: "Garantias");

            migrationBuilder.DropColumn(
                name: "ReparacaoId",
                table: "Despesas");

            migrationBuilder.RenameColumn(
                name: "ReparacaoId",
                table: "PushSubscriptions",
                newName: "VendaId");

            migrationBuilder.RenameIndex(
                name: "IX_PushSubscriptions_TenantId_ReparacaoId",
                table: "PushSubscriptions",
                newName: "IX_PushSubscriptions_TenantId_VendaId");

            migrationBuilder.RenameIndex(
                name: "IX_PushSubscriptions_ReparacaoId_Endpoint",
                table: "PushSubscriptions",
                newName: "IX_PushSubscriptions_VendaId_Endpoint");

            migrationBuilder.RenameColumn(
                name: "ReparacaoId",
                table: "InternalTasks",
                newName: "VendaId");

            migrationBuilder.RenameIndex(
                name: "IX_InternalTasks_ReparacaoId",
                table: "InternalTasks",
                newName: "IX_InternalTasks_VendaId");

            migrationBuilder.RenameColumn(
                name: "ReparacaoId",
                table: "Avaliacoes",
                newName: "VendaId");

            migrationBuilder.RenameIndex(
                name: "IX_Avaliacoes_ReparacaoId",
                table: "Avaliacoes",
                newName: "IX_Avaliacoes_VendaId");

            migrationBuilder.AddColumn<DateTime>(
                name: "PrevistoPara",
                table: "Vendas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicSlug",
                table: "Vendas",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VendaAssinaturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    PngBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    AssinadaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendaAssinaturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendaAssinaturas_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendaComunicacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Direcao = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendaComunicacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendaComunicacoes_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendaEstadoLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstadoFrom = table.Column<int>(type: "int", nullable: true),
                    EstadoTo = table.Column<int>(type: "int", nullable: false),
                    MudouEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendaEstadoLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendaEstadoLogs_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendaFotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Legenda = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VisivelNoPortal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendaFotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendaFotos_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vendas_PublicSlug",
                table: "Vendas",
                column: "PublicSlug",
                unique: true,
                filter: "[PublicSlug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VendaAssinaturas_VendaId",
                table: "VendaAssinaturas",
                column: "VendaId");

            migrationBuilder.CreateIndex(
                name: "IX_VendaComunicacoes_VendaId",
                table: "VendaComunicacoes",
                column: "VendaId");

            migrationBuilder.CreateIndex(
                name: "IX_VendaEstadoLogs_TenantId_VendaId_MudouEm",
                table: "VendaEstadoLogs",
                columns: new[] { "TenantId", "VendaId", "MudouEm" });

            migrationBuilder.CreateIndex(
                name: "IX_VendaEstadoLogs_VendaId",
                table: "VendaEstadoLogs",
                column: "VendaId");

            migrationBuilder.CreateIndex(
                name: "IX_VendaFotos_StorageKey",
                table: "VendaFotos",
                column: "StorageKey",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_VendaFotos_VendaId_Tipo_Ordem",
                table: "VendaFotos",
                columns: new[] { "VendaId", "Tipo", "Ordem" });

            migrationBuilder.AddForeignKey(
                name: "FK_Avaliacoes_Vendas_VendaId",
                table: "Avaliacoes",
                column: "VendaId",
                principalTable: "Vendas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InternalTasks_Vendas_VendaId",
                table: "InternalTasks",
                column: "VendaId",
                principalTable: "Vendas",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PushSubscriptions_Vendas_VendaId",
                table: "PushSubscriptions",
                column: "VendaId",
                principalTable: "Vendas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Avaliacoes_Vendas_VendaId",
                table: "Avaliacoes");

            migrationBuilder.DropForeignKey(
                name: "FK_InternalTasks_Vendas_VendaId",
                table: "InternalTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_PushSubscriptions_Vendas_VendaId",
                table: "PushSubscriptions");

            migrationBuilder.DropTable(
                name: "VendaAssinaturas");

            migrationBuilder.DropTable(
                name: "VendaComunicacoes");

            migrationBuilder.DropTable(
                name: "VendaEstadoLogs");

            migrationBuilder.DropTable(
                name: "VendaFotos");

            migrationBuilder.DropIndex(
                name: "IX_Vendas_PublicSlug",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "PrevistoPara",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "PublicSlug",
                table: "Vendas");

            migrationBuilder.RenameColumn(
                name: "VendaId",
                table: "PushSubscriptions",
                newName: "ReparacaoId");

            migrationBuilder.RenameIndex(
                name: "IX_PushSubscriptions_VendaId_Endpoint",
                table: "PushSubscriptions",
                newName: "IX_PushSubscriptions_ReparacaoId_Endpoint");

            migrationBuilder.RenameIndex(
                name: "IX_PushSubscriptions_TenantId_VendaId",
                table: "PushSubscriptions",
                newName: "IX_PushSubscriptions_TenantId_ReparacaoId");

            migrationBuilder.RenameColumn(
                name: "VendaId",
                table: "InternalTasks",
                newName: "ReparacaoId");

            migrationBuilder.RenameIndex(
                name: "IX_InternalTasks_VendaId",
                table: "InternalTasks",
                newName: "IX_InternalTasks_ReparacaoId");

            migrationBuilder.RenameColumn(
                name: "VendaId",
                table: "Avaliacoes",
                newName: "ReparacaoId");

            migrationBuilder.RenameIndex(
                name: "IX_Avaliacoes_VendaId",
                table: "Avaliacoes",
                newName: "IX_Avaliacoes_ReparacaoId");

            migrationBuilder.AddColumn<Guid>(
                name: "PartId",
                table: "VendaItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReparacaoId",
                table: "RepairRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReparacaoId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReparacaoId",
                table: "Garantias",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReparacaoId",
                table: "Despesas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiagnosticoTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticoTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentFieldTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentFieldTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustoUnitarioCents = table.Column<int>(type: "int", nullable: false),
                    Fornecedor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LocalArmazenamento = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Marca = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Modelo = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    QtdMinima = table.Column<int>(type: "int", nullable: false),
                    QtdStock = table.Column<int>(type: "int", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReparacaoTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorHex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacaoTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SkuMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedFromImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Confidence = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupplierCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplierProductName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupplierSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UseCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkuMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkuMappings_SupplierInvoiceImports_CreatedFromImportId",
                        column: x => x.CreatedFromImportId,
                        principalTable: "SupplierInvoiceImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticoTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Grupo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Peso = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticoTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticoTemplateItems_DiagnosticoTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "DiagnosticoTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VisibleInPortal = table.Column<bool>(type: "bit", nullable: false)
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
                name: "Reparacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentFieldTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Avaria = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustoPecasCents = table.Column<int>(type: "int", nullable: false),
                    Diagnostico = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EntregueEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Equipamento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    EstadoFisicoInicial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstadoPagamento = table.Column<int>(type: "int", nullable: false),
                    EstadoSince = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HorasGastas = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Imei = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    InvoiceEmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    OrcamentoAprovado = table.Column<bool>(type: "bit", nullable: false),
                    OrcamentoCents = table.Column<int>(type: "int", nullable: true),
                    PrecoFinalCents = table.Column<int>(type: "int", nullable: true),
                    PrevistoEntregueEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicSlug = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    SinalCents = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reparacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reparacoes_Auth_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Auth_Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Reparacoes_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reparacoes_EquipmentFieldTemplates_EquipmentFieldTemplateId",
                        column: x => x.EquipmentFieldTemplateId,
                        principalTable: "EquipmentFieldTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticoExecucoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    CompletadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    NotasGerais = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Score = table.Column<int>(type: "int", nullable: true),
                    TemplateNomeSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticoExecucoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticoExecucoes_DiagnosticoTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "DiagnosticoTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DiagnosticoExecucoes_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "PartMovimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Motivo = table.Column<int>(type: "int", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    ReverseCharge = table.Column<bool>(type: "bit", nullable: false),
                    StockAntes = table.Column<int>(type: "int", nullable: false),
                    StockDepois = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartMovimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartMovimentos_Parts_PartId",
                        column: x => x.PartId,
                        principalTable: "Parts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartMovimentos_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PartMovimentos_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReparacaoAssinaturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssinadaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PngBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacaoAssinaturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReparacaoAssinaturas_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReparacaoComunicacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direcao = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacaoComunicacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReparacaoComunicacoes_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReparacaoEstadoLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EstadoFrom = table.Column<int>(type: "int", nullable: true),
                    EstadoTo = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    MudouEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacaoEstadoLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReparacaoEstadoLogs_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReparacaoFotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Legenda = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VisivelNoPortal = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacaoFotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReparacaoFotos_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReparacaoTagAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoTagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacaoTagAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReparacaoTagAssignments_ReparacaoTags_ReparacaoTagId",
                        column: x => x.ReparacaoTagId,
                        principalTable: "ReparacaoTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReparacaoTagAssignments_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReparacaoTimeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReparacaoTimeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReparacaoTimeEntries_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignatureCaptures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReparacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssinanteContacto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssinanteNome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImagemDataUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RemoteIp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureCaptures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignatureCaptures_Auth_Users_CapturedByUserId",
                        column: x => x.CapturedByUserId,
                        principalTable: "Auth_Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SignatureCaptures_Reparacoes_ReparacaoId",
                        column: x => x.ReparacaoId,
                        principalTable: "Reparacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosticoExecucaoItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecucaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Grupo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Peso = table.Column<int>(type: "int", nullable: false),
                    Resultado = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticoExecucaoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiagnosticoExecucaoItems_DiagnosticoExecucoes_ExecucaoId",
                        column: x => x.ExecucaoId,
                        principalTable: "DiagnosticoExecucoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendaItems_PartId",
                table: "VendaItems",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_VendaItems_TenantId_PartId",
                table: "VendaItems",
                columns: new[] { "TenantId", "PartId" },
                filter: "[PartId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RepairRequests_ReparacaoId",
                table: "RepairRequests",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Garantias_ReparacaoId",
                table: "Garantias",
                column: "ReparacaoId",
                unique: true,
                filter: "[IsDeleted] = 0 AND [ReparacaoId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Garantias_OneSource",
                table: "Garantias",
                sql: "([ReparacaoId] IS NOT NULL AND [VendaId] IS NULL) OR ([ReparacaoId] IS NULL AND [VendaId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_ReparacaoId",
                table: "Despesas",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Despesas_TenantId_ReparacaoId",
                table: "Despesas",
                columns: new[] { "TenantId", "ReparacaoId" },
                filter: "[ReparacaoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticoExecucaoItems_ExecucaoId_Ordem",
                table: "DiagnosticoExecucaoItems",
                columns: new[] { "ExecucaoId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticoExecucoes_ReparacaoId",
                table: "DiagnosticoExecucoes",
                column: "ReparacaoId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticoExecucoes_TemplateId",
                table: "DiagnosticoExecucoes",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticoTemplateItems_TemplateId_Ordem",
                table: "DiagnosticoTemplateItems",
                columns: new[] { "TemplateId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticoTemplates_TenantId_Categoria_IsDefault",
                table: "DiagnosticoTemplates",
                columns: new[] { "TenantId", "Categoria", "IsDefault" },
                filter: "[IsDefault] = 1 AND [IsDeleted] = 0");

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

            migrationBuilder.CreateIndex(
                name: "IX_PartMovimentos_PartId",
                table: "PartMovimentos",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_PartMovimentos_ReparacaoId",
                table: "PartMovimentos",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_PartMovimentos_TenantId_PartId_CreatedAt",
                table: "PartMovimentos",
                columns: new[] { "TenantId", "PartId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PartMovimentos_TenantId_ReparacaoId",
                table: "PartMovimentos",
                columns: new[] { "TenantId", "ReparacaoId" },
                filter: "[ReparacaoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PartMovimentos_TenantId_VendaId",
                table: "PartMovimentos",
                columns: new[] { "TenantId", "VendaId" },
                filter: "[VendaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PartMovimentos_VendaId",
                table: "PartMovimentos",
                column: "VendaId");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_TenantId_Categoria_Marca",
                table: "Parts",
                columns: new[] { "TenantId", "Categoria", "Marca" });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_TenantId_QtdStock_QtdMinima",
                table: "Parts",
                columns: new[] { "TenantId", "QtdStock", "QtdMinima" });

            migrationBuilder.CreateIndex(
                name: "IX_Parts_TenantId_Sku",
                table: "Parts",
                columns: new[] { "TenantId", "Sku" },
                unique: true,
                filter: "[Sku] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoAssinaturas_ReparacaoId",
                table: "ReparacaoAssinaturas",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoComunicacoes_ReparacaoId",
                table: "ReparacaoComunicacoes",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoEstadoLogs_ReparacaoId",
                table: "ReparacaoEstadoLogs",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoEstadoLogs_TenantId_ReparacaoId_MudouEm",
                table: "ReparacaoEstadoLogs",
                columns: new[] { "TenantId", "ReparacaoId", "MudouEm" });

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoFotos_ReparacaoId_Tipo_Ordem",
                table: "ReparacaoFotos",
                columns: new[] { "ReparacaoId", "Tipo", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoFotos_StorageKey",
                table: "ReparacaoFotos",
                column: "StorageKey",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoTagAssignments_ReparacaoId",
                table: "ReparacaoTagAssignments",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoTagAssignments_ReparacaoTagId",
                table: "ReparacaoTagAssignments",
                column: "ReparacaoTagId");

            migrationBuilder.CreateIndex(
                name: "IX_ReparacaoTimeEntries_ReparacaoId",
                table: "ReparacaoTimeEntries",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_AssignedToUserId",
                table: "Reparacoes",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_ClienteId",
                table: "Reparacoes",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_EquipmentFieldTemplateId",
                table: "Reparacoes",
                column: "EquipmentFieldTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_PublicSlug",
                table: "Reparacoes",
                column: "PublicSlug",
                unique: true,
                filter: "[PublicSlug] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_ClienteId",
                table: "Reparacoes",
                columns: new[] { "TenantId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_EquipmentFieldTemplateId",
                table: "Reparacoes",
                columns: new[] { "TenantId", "EquipmentFieldTemplateId" },
                filter: "[EquipmentFieldTemplateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_Estado",
                table: "Reparacoes",
                columns: new[] { "TenantId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_Numero",
                table: "Reparacoes",
                columns: new[] { "TenantId", "Numero" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureCaptures_CapturedByUserId",
                table: "SignatureCaptures",
                column: "CapturedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureCaptures_ReparacaoId",
                table: "SignatureCaptures",
                column: "ReparacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_CreatedFromImportId",
                table: "SkuMappings",
                column: "CreatedFromImportId");

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_TenantId_SupplierCode_SupplierSku",
                table: "SkuMappings",
                columns: new[] { "TenantId", "SupplierCode", "SupplierSku" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_TenantId_TargetType_TargetId",
                table: "SkuMappings",
                columns: new[] { "TenantId", "TargetType", "TargetId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Avaliacoes_Reparacoes_ReparacaoId",
                table: "Avaliacoes",
                column: "ReparacaoId",
                principalTable: "Reparacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Despesas_Reparacoes_ReparacaoId",
                table: "Despesas",
                column: "ReparacaoId",
                principalTable: "Reparacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Garantias_Reparacoes_ReparacaoId",
                table: "Garantias",
                column: "ReparacaoId",
                principalTable: "Reparacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InternalTasks_Reparacoes_ReparacaoId",
                table: "InternalTasks",
                column: "ReparacaoId",
                principalTable: "Reparacoes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PushSubscriptions_Reparacoes_ReparacaoId",
                table: "PushSubscriptions",
                column: "ReparacaoId",
                principalTable: "Reparacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RepairRequests_Reparacoes_ReparacaoId",
                table: "RepairRequests",
                column: "ReparacaoId",
                principalTable: "Reparacoes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VendaItems_Parts_PartId",
                table: "VendaItems",
                column: "PartId",
                principalTable: "Parts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
