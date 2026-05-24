/**
 * Sprint 250 (Doc 75 área 11 P0): Sentry browser SDK.
 *
 * Env-gated via VITE_SENTRY_DSN. Sem DSN não inicializa — útil em dev/CI.
 * Cria projecto em sentry.io e mete DSN em .env.production ou via build args.
 *
 * Defaults conservadores:
 * - Tracing apenas em production a 10% (não em dev/preview).
 * - Replay desligado por default (custo + RGPD) — Bruno active manualmente
 *   se algum dia precisar de session replay.
 * - sendDefaultPii: false — não envia cookies/headers de auth.
 */
import * as Sentry from '@sentry/react';

export function initSentry() {
  const dsn = import.meta.env.VITE_SENTRY_DSN;
  if (!dsn) return;

  Sentry.init({
    dsn,
    environment: import.meta.env.MODE,
    release: import.meta.env.VITE_RELEASE_VERSION,
    tracesSampleRate: import.meta.env.PROD ? 0.1 : 0,
    sendDefaultPii: false,
    integrations: [
      Sentry.browserTracingIntegration(),
    ],
    // Filtra erros conhecidos / browser noise.
    ignoreErrors: [
      // Browser extensions / monitoring agents
      'top.GLOBALS',
      'ResizeObserver loop limit exceeded',
      'ResizeObserver loop completed with undelivered notifications',
      // Network errors fora do nosso controlo
      'Failed to fetch',
      'NetworkError when attempting to fetch resource',
      'Load failed',
    ],
    beforeSend(event) {
      // Defesa em profundidade — strip cookie header se algum integration o anexar
      if (event.request?.cookies) delete event.request.cookies;
      if (event.request?.headers?.authorization) {
        event.request.headers.authorization = '[redacted]';
      }
      return event;
    },
  });
}
