using RepairDesk.Core.Enums;

namespace RepairDesk.Services.TenantPreferences;

public static class TenantPreferencesDefaults
{
    public const int SchemaVersion = 1;

    public static readonly string[] DefaultPushEstadosPermitidos =
    [
        nameof(RepairStatus.Recebido),
        nameof(RepairStatus.Diagnostico),
        nameof(RepairStatus.AguardaPeca),
        nameof(RepairStatus.EmReparacao),
        nameof(RepairStatus.Pronto),
        nameof(RepairStatus.Entregue),
        nameof(RepairStatus.Cancelado),
        nameof(RepairStatus.Orcamento),
    ];

    public static TenantPreferencesRoot Create()
    {
        return new TenantPreferencesRoot(
            Communication: new CommunicationPrefs(
                WhatsAppEnabled: true,
                TemplatesByState: CreateWhatsAppTemplates(),
                RepeatMode: WhatsAppRepeatMode.Sempre,
                StaleDaysThreshold: 7,
                Push: new PushPrefs(true, DefaultPushEstadosPermitidos)),
            Portal: new PortalPrefs(
                MostrarFotos: true,
                MostrarDiagnostico: true,
                MostrarOrcamento: true,
                MostrarGarantia: true,
                MostrarTimeline: true,
                MostrarAvaliacao: true,
                PermitirAprovarOrcamento: true,
                GoogleReviewMinScore: 4,
                GoogleReviewUrl: null),
            Repairs: new RepairsPrefs(
                EntregarMarcaPago: EntregarMarcaPagoMode.Sim,
                GarantiaAutomatica: GarantiaAutoMode.Sim),
            Sales: new SalesPrefs(
                DefaultMetodoPagamento: nameof(PaymentMethod.MBWay),
                DefaultCondicaoArtigo: (int)CondicaoArtigo.NaoAplicavel,
                EmitirFatura: EmitirFaturaMode.Perguntar,
                VendaGarantia: GarantiaAutoMode.Sim));
    }

    public static Dictionary<string, WhatsAppStateTemplate> CreateWhatsAppTemplates()
    {
        return new Dictionary<string, WhatsAppStateTemplate>(StringComparer.OrdinalIgnoreCase)
        {
            ["Recebido"] = new(true, "Ola {{cliente_nome}}, confirmamos a entrada do teu {{equipamento}} na {{loja_nome}}. Vamos registar tudo e comecar a analise; assim que houver novidades falamos contigo por aqui.", 10),
            ["Diagnostico"] = new(true, "Ola {{cliente_nome}}, o teu {{equipamento}} esta em diagnostico. Estamos a testar com cuidado para perceber a origem do problema e voltamos a contactar assim que tivermos uma conclusao.", 20),
            ["Orcamento"] = new(true, "Ola {{cliente_nome}}, ja temos o orcamento para o teu {{equipamento}}: {{valor}}. Se estiver tudo bem para ti, responde a esta mensagem com \"Aprovo\" ou usa {{link_aprovacao}} para avancarmos.", 30),
            ["AguardaPeca"] = new(true, "Ola {{cliente_nome}}, a reparacao do teu {{equipamento}} esta a aguardar a chegada de {{peca_nome}}. A previsao atual e {{prazo_estimado}}; avisamos-te assim que chegar.", 40),
            ["EmReparacao"] = new(true, "Ola {{cliente_nome}}, comecamos a reparacao do teu {{equipamento}}. Se tudo correr dentro do previsto, voltamos a falar contigo ate {{prazo_estimado}}.", 50),
            ["Pronto"] = new(true, "Ola {{cliente_nome}}, o teu {{equipamento}} ja esta pronto para levantamento na {{loja_nome}}. Podes passar quando der jeito dentro do nosso horario: {{horario_loja}}.", 60),
            ["Entregue"] = new(true, "Ola {{cliente_nome}}, obrigado por teres confiado em nos para tratar do teu {{equipamento}}. Se notares alguma coisa estranha nos proximos dias, responde por aqui.", 70),
            ["Cancelado"] = new(true, "Ola {{cliente_nome}}, confirmamos o cancelamento da reparacao do teu {{equipamento}}. Quando quiseres, podes combinar connosco o levantamento ou os proximos passos.", 80),
            ["LembreteLevantamento"] = new(true, "Ola {{cliente_nome}}, o teu {{equipamento}} esta pronto para levantamento desde {{data_pronto}} e continua guardado na {{loja_nome}}. Quando puderes, passa dentro do horario {{horario_loja}} ou diz-nos se precisas de combinar outro momento.", 90),
            ["PedidoReview"] = new(true, "Ola {{cliente_nome}}, passaram alguns dias desde que levantaste o {{equipamento}}. Se ficou tudo bem, ajudava-nos muito deixares uma avaliacao no Google: {{link_review_google}}. Obrigado pela confianca.", 100),
            ["PrazoDerrapou"] = new(true, "Ola {{cliente_nome}}, a reparacao do teu {{equipamento}} vai demorar mais do que o previsto. Preferimos avisar-te ja: a nova previsao e {{prazo_estimado}}, e se mudar voltamos a contactar.", 110),
        };
    }
}
