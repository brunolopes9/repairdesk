namespace RepairDesk.Services.TenantPreferences;

public sealed record TenantPreferencesRoot(
    CommunicationPrefs Communication,
    PortalPrefs Portal,
    RepairsPrefs Repairs,
    SalesPrefs Sales,
    BookingPrefs Booking);

/// <summary>
/// Sprint 396 (Doc 84): horário de funcionamento para o booking online. Os slots públicos
/// (/agendar/{slug}) são gerados entre OpenHour e CloseHour de SlotMinutes em SlotMinutes.
/// </summary>
public sealed record BookingPrefs(
    int OpenHour,
    int CloseHour,
    int SlotMinutes);

public sealed record CommunicationPrefs(
    bool WhatsAppEnabled,
    Dictionary<string, WhatsAppStateTemplate> TemplatesByState,
    WhatsAppRepeatMode RepeatMode,
    int StaleDaysThreshold,
    PushPrefs Push);

public sealed record WhatsAppStateTemplate(
    bool Enabled,
    string Texto,
    int Order);

public enum WhatsAppRepeatMode
{
    Sempre = 0,
    UmaVez = 1,
    MarcarManualmente = 2,
}

public sealed record PushPrefs(
    bool Enabled,
    string[] EstadosPermitidos);

public sealed record PortalPrefs(
    bool MostrarFotos,
    bool MostrarDiagnostico,
    bool MostrarOrcamento,
    bool MostrarGarantia,
    bool MostrarTimeline,
    bool MostrarAvaliacao,
    bool PermitirAprovarOrcamento,
    int GoogleReviewMinScore,
    string? GoogleReviewUrl);

public sealed record RepairsPrefs(
    EntregarMarcaPagoMode EntregarMarcaPago,
    GarantiaAutoMode GarantiaAutomatica);

public enum EntregarMarcaPagoMode
{
    Sim = 0,
    Perguntar = 1,
    Nao = 2,
}

public enum GarantiaAutoMode
{
    Sim = 0,
    Perguntar = 1,
    Nao = 2,
}

public sealed record SalesPrefs(
    string DefaultMetodoPagamento,
    int DefaultCondicaoArtigo,
    EmitirFaturaMode EmitirFatura,
    GarantiaAutoMode VendaGarantia);

public enum EmitirFaturaMode
{
    Nunca = 0,
    Perguntar = 1,
    Automatico = 2,
}

public sealed record WhatsAppNotificationStatusDto(bool JaEnviado);

public sealed record CreateWhatsAppNotificationLogRequest(
    Guid EntityId,
    string TemplateKey,
    string EntityType = "Reparacao",
    string? Phone = null,
    int? Estado = null);
