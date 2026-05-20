/**
 * Verificação de assinatura HMAC-SHA256 para webhooks RepairDesk → loja.
 *
 * Sprint 121C: helper portátil para a loja online (Next.js / qualquer framework Node)
 * validar autenticidade dos webhooks. Mesmo algoritmo do backend (Sprint 102).
 *
 * Uso típico (Next.js route handler):
 * ```ts
 * import { verifyWebhookSignature } from '@/lib/repairdesk/webhook-verify';
 *
 * export async function POST(req: Request) {
 *   const body = await req.text();  // RAW — antes de JSON.parse!
 *   const sig = req.headers.get('x-repairdesk-signature') ?? '';
 *   const secret = process.env.REPAIRDESK_WEBHOOK_SECRET!;
 *
 *   if (!verifyWebhookSignature(body, sig, secret)) {
 *     return new Response('Invalid signature', { status: 401 });
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

/**
 * Valida que o header `X-RepairDesk-Signature` foi gerado pelo backend com o secret
 * conhecido. Devolve `false` se falhar (assinatura ausente, malformada ou diferente).
 *
 * @param body Raw body do request (string ou Buffer).
 * @param signatureHeader Conteúdo do header `X-RepairDesk-Signature` (formato `sha256=<hex>`).
 * @param secret Secret da subscription (formato `whsec_<base64url>`). Aceita também o secret
 *               já sem o prefixo `whsec_` para compatibilidade com receptores que o strip
 *               antes de chamar este helper.
 */
export function verifyWebhookSignature(
  body: string | Buffer,
  signatureHeader: string | null | undefined,
  secret: string,
): boolean {
  if (!signatureHeader || !signatureHeader.startsWith('sha256=')) return false;

  // Strip prefix do secret se presente (matching o backend, que também faz strip).
  const key = secret.startsWith('whsec_') ? secret.slice('whsec_'.length) : secret;
  const expected = 'sha256=' + createHmac('sha256', key)
    .update(body)
    .digest('hex');

  const a = Buffer.from(signatureHeader);
  const b = Buffer.from(expected);
  if (a.length !== b.length) return false;
  return timingSafeEqual(a, b);
}
