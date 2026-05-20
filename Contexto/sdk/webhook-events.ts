/**
 * Tipos discriminados dos eventos publicados pelo RepairDesk → subscribers.
 *
 * Sprint 126: payload shapes espelham os anonymous types publicados em
 * `WebhookPublisher.PublishAsync(...)` no backend. A loja headless pode fazer
 * narrowing pelo campo `event` para tratar cada caso com type safety.
 *
 * Uso típico (Next.js route handler):
 * ```ts
 * import { parseWebhookEvent, type RepairDeskWebhookEvent } from '@/lib/repairdesk/webhook-events';
 *
 * export async function POST(req: Request) {
 *   const raw = await req.text();
 *   const sig = req.headers.get('x-repairdesk-signature');
 *   const result = parseWebhookEvent(raw, sig, process.env.REPAIRDESK_WEBHOOK_SECRET!);
 *   if (!result.ok) return new Response(result.error, { status: 401 });
 *
 *   const evt = result.event;  // RepairDeskWebhookEvent — discriminated union
 *   switch (evt.event) {
 *     case 'parts.adicionado':
 *     case 'parts.atualizado':
 *       await revalidateTag(`part:${evt.data.sku}`);
 *       break;
 *     case 'phones.removido':
 *       await deleteCacheEntry(evt.data.slug);
 *       break;
 *     // ...
 *   }
 *   return new Response('ok');
 * }
 * ```
 *
 * Importante: o RepairDesk pode adicionar campos novos a um payload sem incrementar
 * a versão do evento. Faz destructuring defensivo — não dependas da ausência de
 * properties extras.
 */

import { verifyWebhookSignature } from './webhook-verify';

// =================================================================
// PAYLOADS — um por event type
// =================================================================

/** Origem de uma garantia: 'Venda' ou 'Reparacao'. Espelha enum `GarantiaSourceType`. */
export type GarantiaOrigem = 'Venda' | 'Reparacao';

/** Origem de uma venda. Espelha enum `VendaOrigem` (ToString). */
export type VendaOrigemName = 'Balcao' | 'Online' | 'Importacao';

/** Status de uma venda. Espelha enum `VendaStatus` (ToString). */
export type VendaStatusName = 'Pendente' | 'Paga' | 'Cancelada';

export interface GarantiaEmitidaPayload {
  garantiaId: string;
  slug: string;
  origem: GarantiaOrigem;
  vendaId?: string | null;
  vendaNumero?: string | null;
  reparacaoId?: string | null;
  clienteId?: string | null;
  dataInicio: string;
  diasGarantia?: number;
}

export interface GarantiaAnuladaPayload {
  garantiaId: string;
  slug: string;
  origem: GarantiaOrigem;
  vendaId?: string | null;
  reparacaoId?: string | null;
  motivo: string | null;
}

export interface GarantiaExpiradaPayload {
  garantiaId: string;
  slug: string;
  origem: GarantiaOrigem;
  vendaId?: string | null;
  reparacaoId?: string | null;
  dataFim: string;
}

export interface VendaCriadaPayload {
  vendaId: string;
  vendaNumero: string;
  clienteId?: string | null;
  origem: VendaOrigemName;
  totalCents: number;
  status: VendaStatusName;
}

export interface VendaPagaPayload {
  vendaId: string;
  vendaNumero: string;
  clienteId?: string | null;
  totalCents: number;
  paymentMethod: string;
  data: string;
}

export interface VendaCanceladaPayload {
  vendaId: string;
  vendaNumero: string;
  clienteId?: string | null;
  totalCents: number;
  invoiceNumber?: string | null;
}

export interface ReparacaoConcluidaPayload {
  reparacaoId: string;
  reparacaoNumero: string;
  clienteId: string;
  equipamento: string;
  imei?: string | null;
  precoFinalCents?: number | null;
  entregueEm: string;
}

/** Espelha enum `PartCategoria` (ToString). */
export type PartCategoriaName =
  | 'Ecra' | 'Bateria' | 'Conector' | 'Camara' | 'VidroTraseiro' | 'CaboFlex'
  | 'Tampa' | 'Adesivo' | 'Consumivel' | 'Smartphone' | 'Tablet' | 'Acessorio' | 'Outro';

export interface PartCatalogPayload {
  partId: string;
  sku: string | null;
  nome: string;
  categoria: PartCategoriaName;
  marca?: string | null;
  modelo?: string | null;
  qtdStock: number;
  mostrarLojaOnline: boolean;
}

/** Grading de produto. Espelha enum `ProductGrading` (ToString). */
export type ProductGradingName = 'Novo' | 'GradeA' | 'GradeB' | 'GradeC' | 'OpenBox' | 'Premium';

/** Tipo de fornecimento. Espelha enum `ProductSupplyType` (ToString). */
export type ProductSupplyTypeName = 'Stock' | 'Dropship';

export interface ProductCatalogPayload {
  productId: string;
  sku: string;
  slug: string;
  brand: string;
  model: string;
  storage?: string | null;
  color?: string | null;
  grading: ProductGradingName;
  supplyType: ProductSupplyTypeName;
  priceCents: number;
  stockQuantity: number;
  mostrarLojaOnline: boolean;
}

// =================================================================
// DISCRIMINATED UNION
// =================================================================

/** Envelope comum a todos os eventos. */
interface BaseEnvelope<E extends string, D> {
  /** UUID v4 único da entrega. Usar para idempotência no receptor. */
  id: string;
  /** Discriminante — usar `switch (evt.event)` para narrowing. */
  event: E;
  /** Tenant que originou o evento. */
  tenantId: string;
  /** Timestamp ISO8601 UTC em que o RepairDesk produziu o evento. */
  createdAt: string;
  data: D;
}

export type RepairDeskWebhookEvent =
  | BaseEnvelope<'garantia.emitida', GarantiaEmitidaPayload>
  | BaseEnvelope<'garantia.anulada', GarantiaAnuladaPayload>
  | BaseEnvelope<'garantia.expirada', GarantiaExpiradaPayload>
  | BaseEnvelope<'venda.criada', VendaCriadaPayload>
  | BaseEnvelope<'venda.paga', VendaPagaPayload>
  | BaseEnvelope<'venda.cancelada', VendaCanceladaPayload>
  | BaseEnvelope<'reparacao.concluida', ReparacaoConcluidaPayload>
  | BaseEnvelope<'parts.adicionado', PartCatalogPayload>
  | BaseEnvelope<'parts.atualizado', PartCatalogPayload>
  | BaseEnvelope<'parts.removido', PartCatalogPayload>
  | BaseEnvelope<'phones.adicionado', ProductCatalogPayload>
  | BaseEnvelope<'phones.atualizado', ProductCatalogPayload>
  | BaseEnvelope<'phones.removido', ProductCatalogPayload>;

/** Lista exaustiva — útil para subscrever a todos os eventos numa só chamada. */
export const ALL_WEBHOOK_EVENT_TYPES = [
  'garantia.emitida', 'garantia.anulada', 'garantia.expirada',
  'venda.criada', 'venda.paga', 'venda.cancelada',
  'reparacao.concluida',
  'parts.adicionado', 'parts.atualizado', 'parts.removido',
  'phones.adicionado', 'phones.atualizado', 'phones.removido',
] as const;

export type RepairDeskWebhookEventType = RepairDeskWebhookEvent['event'];

// =================================================================
// VERIFY + PARSE em uma chamada
// =================================================================

export type ParseWebhookResult =
  | { ok: true; event: RepairDeskWebhookEvent }
  | { ok: false; error: 'missing_signature' | 'invalid_signature' | 'malformed_body' | 'unknown_event' };

/**
 * Verifica a assinatura HMAC do raw body e devolve o evento tipado se válida.
 * Falha de forma estruturada — devolve `{ok:false, error}` em vez de lançar, para
 * que o caller possa mapear o erro para HTTP 401/400 conforme o caso.
 *
 * @param rawBody Corpo cru do request (string ou Buffer). NÃO reformatar.
 * @param signatureHeader Header `X-RepairDesk-Signature`.
 * @param secret Secret da subscription (formato `whsec_*`).
 */
export function parseWebhookEvent(
  rawBody: string | Buffer,
  signatureHeader: string | null | undefined,
  secret: string,
): ParseWebhookResult {
  if (!signatureHeader) return { ok: false, error: 'missing_signature' };
  if (!verifyWebhookSignature(rawBody, signatureHeader, secret)) {
    return { ok: false, error: 'invalid_signature' };
  }

  const bodyText = typeof rawBody === 'string' ? rawBody : rawBody.toString('utf8');
  let parsed: unknown;
  try {
    parsed = JSON.parse(bodyText);
  } catch {
    return { ok: false, error: 'malformed_body' };
  }

  if (
    !parsed || typeof parsed !== 'object' ||
    typeof (parsed as Record<string, unknown>).event !== 'string'
  ) {
    return { ok: false, error: 'malformed_body' };
  }

  const evt = parsed as { event: string };
  if (!(ALL_WEBHOOK_EVENT_TYPES as readonly string[]).includes(evt.event)) {
    // O backend pode publicar eventos novos antes da loja actualizar o SDK.
    // Devolvemos erro para que o caller decida (ack-and-ignore ou 400).
    return { ok: false, error: 'unknown_event' };
  }

  return { ok: true, event: parsed as RepairDeskWebhookEvent };
}

/**
 * Type guard ergonómico para usar em `if`/`filter` quando já se tem o objecto parseado.
 * ```ts
 * if (isWebhookEvent(evt, 'phones.removido')) {
 *   evt.data.slug;  // typed
 * }
 * ```
 */
export function isWebhookEvent<T extends RepairDeskWebhookEventType>(
  evt: RepairDeskWebhookEvent,
  type: T,
): evt is Extract<RepairDeskWebhookEvent, { event: T }> {
  return evt.event === type;
}
