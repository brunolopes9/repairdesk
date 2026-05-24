/**
 * Sprint 253 (Doc 77): handling unificado de erros API.
 *
 * Extrai mensagens amigáveis a partir de erros Axios — quer ProblemDetails
 * (formato standard do backend, Sprint 226) quer network/timeout/etc.
 * Devolve string que pode ir directa para toast/inline.
 */
import { isAxiosError } from 'axios';

interface ProblemDetails {
  detail?: string;
  title?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

const STATUS_MESSAGES: Record<number, string> = {
  400: 'Pedido inválido.',
  401: 'Sessão expirou. Faz login outra vez.',
  403: 'Sem permissão para esta operação.',
  404: 'Não encontrado.',
  409: 'Conflito — alguém alterou em paralelo. Recarrega.',
  413: 'Ficheiro demasiado grande.',
  422: 'Dados inválidos.',
  429: 'Demasiados pedidos. Tenta dentro de 1 minuto.',
  500: 'Erro do servidor. A loja foi notificada.',
  502: 'Servidor temporariamente indisponível.',
  503: 'Servidor temporariamente indisponível.',
  504: 'Servidor demorou demasiado a responder.',
};

/**
 * Mensagem amigável para mostrar ao utilizador. Não usar para logging — perde
 * contexto. Para logging usa o erro inteiro com Sentry.
 */
export function apiErrorMessage(err: unknown): string {
  if (!err) return 'Erro desconhecido.';

  if (isAxiosError(err)) {
    // Network errors antes de chegar ao servidor (sem internet, DNS, CORS)
    if (err.code === 'ERR_NETWORK') return 'Sem ligação à internet ou servidor inacessível.';
    if (err.code === 'ECONNABORTED' || err.code === 'ETIMEDOUT') return STATUS_MESSAGES[504];

    const status = err.response?.status;
    const data = err.response?.data as ProblemDetails | undefined;

    // ProblemDetails do backend tem prioridade — geralmente é a mensagem mais útil
    // (ex: "IMEI já usado em venda anterior", "Slug duplicado").
    if (typeof data?.detail === 'string' && data.detail.length > 0) {
      return data.detail;
    }

    // Validation errors (422) — primeira mensagem do primeiro campo
    if (status === 422 && data?.errors) {
      const firstField = Object.keys(data.errors)[0];
      const firstMessage = firstField ? data.errors[firstField]?.[0] : undefined;
      if (firstMessage) return firstMessage;
    }

    if (status && STATUS_MESSAGES[status]) return STATUS_MESSAGES[status];
    if (status) return `Erro ${status}.`;

    return 'Erro de rede.';
  }

  if (err instanceof Error && err.message) return err.message;
  if (typeof err === 'string') return err;
  return 'Erro desconhecido.';
}

/**
 * Variante para casos específicos onde quer só o code (e.g., switch em cima
 * de "validation_error", "not_found", etc).
 */
export function apiErrorCode(err: unknown): string | undefined {
  if (!isAxiosError(err)) return undefined;
  const data = err.response?.data as ProblemDetails | undefined;
  return data?.code;
}
