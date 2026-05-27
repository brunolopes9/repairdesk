export type WhatsAppRepeatMode = 0 | 1 | 2;
export type EntregarMarcaPagoMode = 0 | 1 | 2;
export type GarantiaAutoMode = 0 | 1 | 2;
export type EmitirFaturaMode = 0 | 1 | 2;

export interface WhatsAppStateTemplate {
  enabled: boolean;
  texto: string;
  order: number;
}

export interface PushPrefs {
  enabled: boolean;
  estadosPermitidos: string[];
}

export interface CommunicationPrefs {
  whatsAppEnabled: boolean;
  templatesByState: Record<string, WhatsAppStateTemplate>;
  repeatMode: WhatsAppRepeatMode;
  staleDaysThreshold: number;
  push: PushPrefs;
}

export interface PortalPrefs {
  mostrarFotos: boolean;
  mostrarDiagnostico: boolean;
  mostrarOrcamento: boolean;
  mostrarGarantia: boolean;
  mostrarTimeline: boolean;
  mostrarAvaliacao: boolean;
  permitirAprovarOrcamento: boolean;
  googleReviewMinScore: number;
  googleReviewUrl: string | null;
}

export interface RepairsPrefs {
  entregarMarcaPago: EntregarMarcaPagoMode;
  garantiaAutomatica: GarantiaAutoMode;
}

export interface SalesPrefs {
  defaultMetodoPagamento: string;
  defaultCondicaoArtigo: number;
  emitirFatura: EmitirFaturaMode;
  vendaGarantia: GarantiaAutoMode;
}

export interface BookingPrefs {
  openHour: number;
  closeHour: number;
  slotMinutes: number;
}

export interface TenantPreferencesRoot {
  communication: CommunicationPrefs;
  portal: PortalPrefs;
  repairs: RepairsPrefs;
  sales: SalesPrefs;
  booking: BookingPrefs;
}

export interface WhatsAppNotificationStatus {
  jaEnviado: boolean;
}
