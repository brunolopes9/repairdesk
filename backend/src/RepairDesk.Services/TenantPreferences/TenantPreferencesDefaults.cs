using RepairDesk.Core.Enums;

namespace RepairDesk.Services.TenantPreferences;

public static class TenantPreferencesDefaults
{
    public const int SchemaVersion = 1;

    /// <summary>Doc 94 Fase 4c: estados da Venda (reparação) que disparam push ao cliente.</summary>
    public static readonly string[] DefaultPushEstadosPermitidos =
    [
        nameof(VendaEstado.Orcamento),
        nameof(VendaEstado.EmCurso),
        nameof(VendaEstado.AEsperaPeca),
        nameof(VendaEstado.Pronta),
        nameof(VendaEstado.Entregue),
        nameof(VendaEstado.Cancelada),
    ];

    /// <summary>Nomes antigos (RepairStatus) → estados da Venda, para preferências já gravadas.</summary>
    public static readonly IReadOnlyDictionary<string, string> EstadoAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Recebido"] = nameof(VendaEstado.Orcamento),
            ["Diagnostico"] = nameof(VendaEstado.Orcamento),
            ["AguardaPeca"] = nameof(VendaEstado.AEsperaPeca),
            ["EmReparacao"] = nameof(VendaEstado.EmCurso),
            ["Pronto"] = nameof(VendaEstado.Pronta),
            ["Cancelado"] = nameof(VendaEstado.Cancelada),
        };

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
                VendaGarantia: GarantiaAutoMode.Sim),
            Booking: new BookingPrefs(
                OpenHour: 9,
                CloseHour: 19,
                SlotMinutes: 30));
    }

    public static Dictionary<string, WhatsAppStateTemplate> CreateWhatsAppTemplates()
    {
        return new Dictionary<string, WhatsAppStateTemplate>(StringComparer.OrdinalIgnoreCase)
        {
            ["Orcamento"] = new(true, "Ola {{cliente_nome}}, ja temos o orcamento para o teu {{equipamento}}: {{valor}}. Se estiver tudo bem para ti, responde a esta mensagem com \"Aprovo\" ou usa {{link_aprovacao}} para avancarmos.", 30),
            ["AEsperaPeca"] = new(true, "Ola {{cliente_nome}}, a reparacao do teu {{equipamento}} esta a aguardar a chegada de {{peca_nome}}. A previsao atual e {{prazo_estimado}}; avisamos-te assim que chegar.", 40),
            ["EmCurso"] = new(true, "Ola {{cliente_nome}}, comecamos a reparacao do teu {{equipamento}}. Se tudo correr dentro do previsto, voltamos a falar contigo ate {{prazo_estimado}}.", 50),
            ["Pronta"] = new(true, "Ola {{cliente_nome}}, o teu {{equipamento}} ja esta pronto para levantamento na {{loja_nome}}. Podes passar quando der jeito dentro do nosso horario: {{horario_loja}}. Podes tambem acompanhar e pagar online aqui: {{portal_link}}", 60),
            ["Entregue"] = new(true, "Ola {{cliente_nome}}, obrigado por teres confiado em nos para tratar do teu {{equipamento}}. Se notares alguma coisa estranha nos proximos dias, responde por aqui.", 70),
            ["Cancelada"] = new(true, "Ola {{cliente_nome}}, confirmamos o cancelamento da reparacao do teu {{equipamento}}. Quando quiseres, podes combinar connosco o levantamento ou os proximos passos.", 80),
            ["LembreteLevantamento"] = new(true, "Ola {{cliente_nome}}, o teu {{equipamento}} esta pronto para levantamento desde {{data_pronto}} e continua guardado na {{loja_nome}}. Quando puderes, passa dentro do horario {{horario_loja}} ou diz-nos se precisas de combinar outro momento.", 90),
            ["PedidoReview"] = new(true, "Ola {{cliente_nome}}, passaram alguns dias desde que levantaste o {{equipamento}}. Se ficou tudo bem, ajudava-nos muito deixares uma avaliacao no Google: {{link_review_google}}. Obrigado pela confianca.", 100),
            ["PrazoDerrapou"] = new(true, "Ola {{cliente_nome}}, a reparacao do teu {{equipamento}} vai demorar mais do que o previsto. Preferimos avisar-te ja: a nova previsao e {{prazo_estimado}}, e se mudar voltamos a contactar.", 110),
        };
    }
}
