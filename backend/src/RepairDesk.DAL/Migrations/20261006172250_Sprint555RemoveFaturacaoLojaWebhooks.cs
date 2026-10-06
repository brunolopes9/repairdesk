using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint555RemoveFaturacaoLojaWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Avencas");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropTable(
                name: "ProductModelImages");

            migrationBuilder.DropTable(
                name: "ShopConditionImages");

            migrationBuilder.DropTable(
                name: "TenantBillingSettings");

            migrationBuilder.DropTable(
                name: "WebhookDeliveries");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "WebhookSubscriptions");

            migrationBuilder.DropTable(
                name: "ProductModels");

            migrationBuilder.DropIndex(
                name: "IX_Vendas_TenantId_InvoiceExternalId",
                table: "Vendas");

            migrationBuilder.DropIndex(
                name: "IX_Trabalhos_TenantId_EstimateExternalId",
                table: "Trabalhos");

            migrationBuilder.DropIndex(
                name: "IX_Trabalhos_TenantId_InvoiceExternalId",
                table: "Trabalhos");

            migrationBuilder.DropIndex(
                name: "IX_Reparacoes_TenantId_EstimateExternalId",
                table: "Reparacoes");

            migrationBuilder.DropIndex(
                name: "IX_Reparacoes_TenantId_InvoiceExternalId",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "InvoiceExternalId",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "InvoicePdfUrl",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "InvoiceProvider",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "ReciboEmitidoEm",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "ReciboNumero",
                table: "Vendas");

            migrationBuilder.DropColumn(
                name: "EstimateEmittedAt",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "EstimateExternalId",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "EstimateNumber",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "EstimatePdfUrl",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "InvoiceExternalId",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "InvoicePdfUrl",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "InvoiceProvider",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "ReciboEmitidoEm",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "ReciboNumero",
                table: "Trabalhos");

            migrationBuilder.DropColumn(
                name: "EstimateEmittedAt",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "EstimateExternalId",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "EstimateNumber",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "EstimatePdfUrl",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "InvoiceExternalId",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "InvoicePdfUrl",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "InvoiceProvider",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "ReciboEmitidoEm",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "ReciboNumero",
                table: "Reparacoes");

            migrationBuilder.DropColumn(
                name: "MostrarLojaOnline",
                table: "Parts");

            migrationBuilder.DropColumn(
                name: "ExpirationNotifiedAt",
                table: "Garantias");

            migrationBuilder.DropColumn(
                name: "CsvColumnMappingJson",
                table: "Fornecedores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceExternalId",
                table: "Vendas",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoicePdfUrl",
                table: "Vendas",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceProvider",
                table: "Vendas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReciboEmitidoEm",
                table: "Vendas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReciboNumero",
                table: "Vendas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimateEmittedAt",
                table: "Trabalhos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateExternalId",
                table: "Trabalhos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateNumber",
                table: "Trabalhos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimatePdfUrl",
                table: "Trabalhos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceExternalId",
                table: "Trabalhos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoicePdfUrl",
                table: "Trabalhos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceProvider",
                table: "Trabalhos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReciboEmitidoEm",
                table: "Trabalhos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReciboNumero",
                table: "Trabalhos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimateEmittedAt",
                table: "Reparacoes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateExternalId",
                table: "Reparacoes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimateNumber",
                table: "Reparacoes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstimatePdfUrl",
                table: "Reparacoes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceExternalId",
                table: "Reparacoes",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoicePdfUrl",
                table: "Reparacoes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceProvider",
                table: "Reparacoes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReciboEmitidoEm",
                table: "Reparacoes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReciboNumero",
                table: "Reparacoes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MostrarLojaOnline",
                table: "Parts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationNotifiedAt",
                table: "Garantias",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CsvColumnMappingJson",
                table: "Fornecedores",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Avencas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativa = table.Column<bool>(type: "bit", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IvaRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodicidadeMeses = table.Column<int>(type: "int", nullable: false),
                    ProximaEmissao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UltimaEmissaoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValorCents = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avencas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Avencas_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    BatteryUpgradePriceCents = table.Column<int>(type: "int", nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DescriptionMarkdown = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Series = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpecsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShopConditionImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alt = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BlurDataUrl = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Url1024w = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Url2048w = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Url480w = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Width = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopConditionImages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantBillingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApiKeyCipherText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ClientSecretCipherText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultDocumentType = table.Column<int>(type: "int", nullable: false),
                    DefaultMaturityDateId = table.Column<int>(type: "int", nullable: true),
                    DefaultPaymentMethodId = table.Column<int>(type: "int", nullable: true),
                    DefaultProductId = table.Column<int>(type: "int", nullable: true),
                    DefaultSerieId = table.Column<int>(type: "int", nullable: true),
                    DefaultTaxId = table.Column<int>(type: "int", nullable: true),
                    ExemptionReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FallbackCustomerId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    RefreshTokenCipherText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SandboxMode = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBillingSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantBillingSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebhookSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisabledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Events = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastDeliveryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Secret = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductModelImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvifUrl1024w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvifUrl2048w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvifUrl480w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BlurDataUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    OptimizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Url1024w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url2048w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url480w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductModelImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductModelImages_ProductModels_ProductModelId",
                        column: x => x.ProductModelId,
                        principalTable: "ProductModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    AttributesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatteryHealthPercent = table.Column<int>(type: "int", nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompareAtPriceCents = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustoUnitarioCents = table.Column<int>(type: "int", nullable: false),
                    DescriptionMarkdown = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DropshipSupplierSku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Grade = table.Column<int>(type: "int", nullable: false),
                    Grading = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsOpenBox = table.Column<bool>(type: "bit", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MostrarLojaOnline = table.Column<bool>(type: "bit", nullable: false),
                    OpenBoxReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    PriceCents = table.Column<int>(type: "int", nullable: false),
                    SeoDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SeoTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StockMinima = table.Column<int>(type: "int", nullable: false),
                    StockQuantity = table.Column<int>(type: "int", nullable: false),
                    Storage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SupplierGrade = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    SupplyType = table.Column<int>(type: "int", nullable: false),
                    TechnicalNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnicalState = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Fornecedores_FornecedorId",
                        column: x => x.FornecedorId,
                        principalTable: "Fornecedores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Products_ProductModels_ModelId",
                        column: x => x.ModelId,
                        principalTable: "ProductModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WebhookSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FailedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LastResponseCode = table.Column<int>(type: "int", nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookDeliveries_WebhookSubscriptions_WebhookSubscriptionId",
                        column: x => x.WebhookSubscriptionId,
                        principalTable: "WebhookSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AvifUrl1024w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvifUrl2048w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvifUrl480w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BlurDataUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    IsCurated = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    OptimizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Url1024w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url2048w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url480w = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vendas_TenantId_InvoiceExternalId",
                table: "Vendas",
                columns: new[] { "TenantId", "InvoiceExternalId" },
                filter: "[InvoiceExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_TenantId_EstimateExternalId",
                table: "Trabalhos",
                columns: new[] { "TenantId", "EstimateExternalId" },
                filter: "[EstimateExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhos_TenantId_InvoiceExternalId",
                table: "Trabalhos",
                columns: new[] { "TenantId", "InvoiceExternalId" },
                filter: "[InvoiceExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_EstimateExternalId",
                table: "Reparacoes",
                columns: new[] { "TenantId", "EstimateExternalId" },
                filter: "[EstimateExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reparacoes_TenantId_InvoiceExternalId",
                table: "Reparacoes",
                columns: new[] { "TenantId", "InvoiceExternalId" },
                filter: "[InvoiceExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Avencas_ClienteId",
                table: "Avencas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId_Ordem",
                table: "ProductImages",
                columns: new[] { "ProductId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductModelImages_ProductModelId",
                table: "ProductModelImages",
                column: "ProductModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_FornecedorId",
                table: "Products",
                column: "FornecedorId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ModelId",
                table: "Products",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Active_MostrarLojaOnline",
                table: "Products",
                columns: new[] { "TenantId", "Active", "MostrarLojaOnline" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Brand_Model",
                table: "Products",
                columns: new[] { "TenantId", "Brand", "Model" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Category_MostrarLojaOnline",
                table: "Products",
                columns: new[] { "TenantId", "Category", "MostrarLojaOnline" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_FornecedorId_DropshipSupplierSku",
                table: "Products",
                columns: new[] { "TenantId", "FornecedorId", "DropshipSupplierSku" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [DropshipSupplierSku] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Sku",
                table: "Products",
                columns: new[] { "TenantId", "Sku" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Slug",
                table: "Products",
                columns: new[] { "TenantId", "Slug" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ShopConditionImages_TenantId_Grade",
                table: "ShopConditionImages",
                columns: new[] { "TenantId", "Grade" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantBillingSettings_TenantId",
                table: "TenantBillingSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_Status_NextRetryAt",
                table: "WebhookDeliveries",
                columns: new[] { "Status", "NextRetryAt" },
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_TenantId_CreatedAt",
                table: "WebhookDeliveries",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_WebhookSubscriptionId",
                table: "WebhookDeliveries",
                column: "WebhookSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscriptions_TenantId_Active",
                table: "WebhookSubscriptions",
                columns: new[] { "TenantId", "Active" });
        }
    }
}
