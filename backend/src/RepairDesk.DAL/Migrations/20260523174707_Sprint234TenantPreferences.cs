using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepairDesk.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Sprint234TenantPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PreferencesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantPreferences_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO TenantPreferences (Id, TenantId, Version, PreferencesJson, CreatedAt, IsDeleted)
                SELECT NEWID(), t.Id, 1, N'{"communication":{"whatsAppEnabled":true,"templatesByState":{"Recebido":{"enabled":true,"texto":"Ola {{cliente_nome}}, confirmamos a entrada do teu {{equipamento}} na {{loja_nome}}. Vamos registar tudo e comecar a analise; assim que houver novidades falamos contigo por aqui.","order":10},"Diagnostico":{"enabled":true,"texto":"Ola {{cliente_nome}}, o teu {{equipamento}} esta em diagnostico. Estamos a testar com cuidado para perceber a origem do problema e voltamos a contactar assim que tivermos uma conclusao.","order":20},"Orcamento":{"enabled":true,"texto":"Ola {{cliente_nome}}, ja temos o orcamento para o teu {{equipamento}}: {{valor}}. Se estiver tudo bem para ti, responde a esta mensagem com \"Aprovo\" ou usa {{link_aprovacao}} para avancarmos.","order":30},"AguardaPeca":{"enabled":true,"texto":"Ola {{cliente_nome}}, a reparacao do teu {{equipamento}} esta a aguardar a chegada de {{peca_nome}}. A previsao atual e {{prazo_estimado}}; avisamos-te assim que chegar.","order":40},"EmReparacao":{"enabled":true,"texto":"Ola {{cliente_nome}}, comecamos a reparacao do teu {{equipamento}}. Se tudo correr dentro do previsto, voltamos a falar contigo ate {{prazo_estimado}}.","order":50},"Pronto":{"enabled":true,"texto":"Ola {{cliente_nome}}, o teu {{equipamento}} ja esta pronto para levantamento na {{loja_nome}}. Podes passar quando der jeito dentro do nosso horario: {{horario_loja}}.","order":60},"Entregue":{"enabled":true,"texto":"Ola {{cliente_nome}}, obrigado por teres confiado em nos para tratar do teu {{equipamento}}. Se notares alguma coisa estranha nos proximos dias, responde por aqui.","order":70},"Cancelado":{"enabled":true,"texto":"Ola {{cliente_nome}}, confirmamos o cancelamento da reparacao do teu {{equipamento}}. Quando quiseres, podes combinar connosco o levantamento ou os proximos passos.","order":80},"LembreteLevantamento":{"enabled":true,"texto":"Ola {{cliente_nome}}, o teu {{equipamento}} esta pronto para levantamento desde {{data_pronto}} e continua guardado na {{loja_nome}}. Quando puderes, passa dentro do horario {{horario_loja}} ou diz-nos se precisas de combinar outro momento.","order":90},"PedidoReview":{"enabled":true,"texto":"Ola {{cliente_nome}}, passaram alguns dias desde que levantaste o {{equipamento}}. Se ficou tudo bem, ajudava-nos muito deixares uma avaliacao no Google: {{link_review_google}}. Obrigado pela confianca.","order":100},"PrazoDerrapou":{"enabled":true,"texto":"Ola {{cliente_nome}}, a reparacao do teu {{equipamento}} vai demorar mais do que o previsto. Preferimos avisar-te ja: a nova previsao e {{prazo_estimado}}, e se mudar voltamos a contactar.","order":110}},"repeatMode":0,"staleDaysThreshold":7,"push":{"enabled":true,"estadosPermitidos":["Recebido","Diagnostico","AguardaPeca","EmReparacao","Pronto","Entregue","Cancelado","Orcamento"]}},"portal":{"mostrarFotos":true,"mostrarDiagnostico":true,"mostrarOrcamento":true,"mostrarGarantia":true,"mostrarTimeline":true,"mostrarAvaliacao":true,"permitirAprovarOrcamento":true,"googleReviewMinScore":4,"googleReviewUrl":null},"repairs":{"entregarMarcaPago":0,"garantiaAutomatica":0},"sales":{"defaultMetodoPagamento":"MBWay","defaultCondicaoArtigo":0,"emitirFatura":1,"vendaGarantia":0}}', SYSUTCDATETIME(), CAST(0 AS bit)
                FROM Tenants t
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM TenantPreferences p
                    WHERE p.TenantId = t.Id
                );
                """);

            migrationBuilder.CreateTable(
                name: "WhatsAppNotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppNotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantPreferences_TenantId",
                table: "TenantPreferences",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppNotificationLogs_TenantId_EntityType_EntityId_TemplateKey",
                table: "WhatsAppNotificationLogs",
                columns: new[] { "TenantId", "EntityType", "EntityId", "TemplateKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppNotificationLogs_TenantId_SentAtUtc",
                table: "WhatsAppNotificationLogs",
                columns: new[] { "TenantId", "SentAtUtc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantPreferences");

            migrationBuilder.DropTable(
                name: "WhatsAppNotificationLogs");
        }
    }
}
