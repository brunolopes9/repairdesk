/**
 * Verificação de assinatura HMAC-SHA256 para webhooks RepairDesk → loja.
 *
 * Sprint 121C: helper portátil para a loja online (Next.js / qualquer framework Node)
 * validar autenticidade dos webhooks. Mesmo algoritmo do backend (Sprint 102).
 *
 * Sprint 161c (BREAKING): a assinatura agora inclui timestamp para prevenir replay
 * attacks. O header `X-RepairDesk-Timestamp` (Unix seconds) é assinado em conjunto
 * com o body: `sha256(secret, "{timestamp}.{body}")`.
 *
 * Uso típico (Next.js route handler):
 * ```ts
 * import { verifyWebhookSignature } from '@/lib/repairdesk/webhook-verify';
 *
 * export async function POST(req: Request) {
 *   const body = await req.text();  // RAW — antes de JSON.parse!
 *   const sig = req.headers.get('x-repairdesk-signature') ?? '';
 *   const ts = req.headers.get('x-repairdesk-timestamp') ?? '';
 *   const secret = process.env.REPAIRDESK_WEBHOOK_SECRET!;
 *
 *   const result = verifyWebhookSignature(body, sig, ts, secret);
 *   if (!result.ok) {
 *     return new Response(`Invalid: ${result.reason}`, { status: 401 });
 *   }
 *
 *   const event = JSON.parse(body);
 *   // ... processar
 * }
 * ```
 *
 * Crítico: o body tem de ser o RAW recebido (Buffer/string original), NÃO um objecto
 * re-serializado. Reformatar JSON quebra a assinatura porque whitespace e ordem de
 * chaves diferem.
 */

import { createHmac, timingSafeEqual } from 'node:crypto';

export type VerifyResult =
  | { ok: true }
  | { ok: false; reason: 'missing_signature' | 'missing_timestamp' | 'timestamp_invalid' | 'timestamp_too_old' | 'timestamp_too_new' | 'signature_mismatch' };

/**
 * Tolerance window default em segundos. Webhooks com timestamp fora desta janela
 * são rejeitados para prevenir replay attacks. 5 minutos é o standard Stripe.
 */
const DEFAULT_TOLERANCE_SECONDS = 5 * 60;

/**
 * Valida que o header `X-RepairDesk-Signature` foi gerado pelo backend com o secret
 * conhecido e que o timestamp está dentro da tolerance window (anti-replay).
 *
 * @param body Raw body do request (string ou Buffer).
 * @param signatureHeader Conteúdo do header `X-RepairDesk-Signature` (formato `sha256=<hex>`).
 * @param timestampHeader Conteúdo do header `X-RepairDesk-Timestamp` (Unix seconds).
 * @param secret Secret da subscription (formato `whsec_<base64url>` ou já sem prefixo).
 * @param toleranceSeconds Janela aceite entre timestamp e agora. Default 300 (5 min).
 */
export function verifyWebhookSignature(
  body: string | Buffer,
  signatureHeader: string | null | undefined,
  timestampHeader: string | null | undefined,
  secret: string,
  toleranceSeconds: number = DEFAULT_TOLERANCE_SECONDS,
): VerifyResult {
  if (!signatureHeader || !signatureHeader.startsWith('sha256=')) {
    return { ok: false, reason: 'missing_signature' };
  }
  if (!timestampHeader) {
    return { ok: false, reason: 'missing_timestamp' };
  }
  const timestamp = Number.parseInt(timestampHeader, 10);
  if (!Number.isFinite(timestamp) || timestamp <= 0) {
    return { ok: false, reason: 'timestamp_invalid' };
  }

  const nowSeconds = Math.floor(Date.now() / 1000);
  const delta = nowSeconds - timestamp;
  if (delta > toleranceSeconds) return { ok: false, reason: 'timestamp_too_old' };
  // Pequena tolerance para clock skew futuro (~60s).
  if (-delta > 60) return { ok: false, reason: 'timestamp_too_new' };

  // Strip prefix do secret se presente (matching o backend, que também faz strip).
  const key = secret.startsWith('whsec_') ? secret.slice('whsec_'.length) : secret;
  const bodyStr = typeof body === 'string' ? body : body.toString('utf8');
  const signedPayload = `${timestamp}.${bodyStr}`;
  const expected = 'sha256=' + createHmac('sha256', key).update(signedPayload).digest('hex');

  const a = Buffer.from(signatureHeader);
  const b = Buffer.from(expected);
  if (a.length !== b.length) return { ok: false, reason: 'signature_mismatch' };
  if (!timingSafeEqual(a, b)) return { ok: false, reason: 'signature_mismatch' };
  return { ok: true };
}

/**
 * Sprint 161d: helper de dedupe em memória por X-RepairDesk-Delivery (UUID).
 * Previne processar 2× o mesmo webhook se o backend reentrega antes de ver 200.
 *
 * Para produção sério, usa Redis/database em vez deste Map em memória.
 *
 * ```ts
 * const dedupe = new WebhookDeliveryDeduper();
 *
 * export async function POST(req: Request) {
 *   const deliveryId = req.headers.get('x-repairdesk-delivery') ?? '';
 *   if (dedupe.hasSeen(deliveryId)) {
 *     return new Response('Already processed', { status: 200 });
 *   }
 *   // ... process ...
 *   dedupe.markSeen(deliveryId);
 *   return new Response('OK', { status: 200 });
 * }
 * ```
 */
export class WebhookDeliveryDeduper {
  private readonly seen = new Map<string, number>();
  private readonly ttlMs: number;
  private readonly maxSize: number;

  constructor(ttlSeconds = 24 * 60 * 60, maxSize = 10_000) {
    this.ttlMs = ttlSeconds * 1000;
    this.maxSize = maxSize;
  }

  hasSeen(deliveryId: string): boolean {
    if (!deliveryId) return false;
    this.gc();
    return this.seen.has(deliveryId);
  }

  markSeen(deliveryId: string): void {
    if (!deliveryId) return;
    if (this.seen.size >= this.maxSize) this.gc(true);
    this.seen.set(deliveryId, Date.now());
  }

  private gc(force = false): void {
    const cutoff = Date.now() - this.ttlMs;
    if (!force && this.seen.size < this.maxSize / 2) return;
    for (const [k, t] of this.seen) {
      if (t < cutoff) this.seen.delete(k);
    }
  }
}
