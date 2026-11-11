/* eslint-disable no-restricted-globals */
// Sprint 365: handlers de Web Push para o service worker.
//
// O VitePWA usa a estratégia generateSW (Workbox), que gera o SW automaticamente
// mas NÃO inclui qualquer listener de 'push' — por isso a subscrição era criada e o
// backend até enviava, mas o browser não tinha código para MOSTRAR a notificação
// (nem no telemóvel nem no desktop). Este ficheiro é injetado no SW gerado via
// workbox.importScripts (ver vite.config.ts).
//
// Payload enviado pelo backend (PushNotificationPayload, camelCase):
//   { title, body, url, tag, estado }

self.addEventListener('push', (event) => {
  let data = {};
  try {
    data = event.data ? event.data.json() : {};
  } catch (_e) {
    data = { title: 'Mender', body: event.data ? event.data.text() : '' };
  }

  const title = data.title || 'Mender';
  const options = {
    body: data.body || '',
    icon: '/favicon.svg',
    badge: '/favicon.svg',
    // tag agrupa notificações do mesmo assunto; renotify volta a vibrar/alertar.
    tag: data.tag || undefined,
    renotify: Boolean(data.tag),
    // Sprint 615 (rotina): avisos insistentes ficam no ecrã até tocar e vibram com mais força.
    requireInteraction: Boolean(data.requireInteraction),
    vibrate: data.requireInteraction ? [300, 150, 300, 150, 600] : [200, 100, 200],
    // Botões («✓ Feito», «+15 min») — cada um abre o seu URL (o ecrã da rotina faz a ação).
    actions: Array.isArray(data.actions)
      ? data.actions.slice(0, 2).map((a) => ({ action: a.action, title: a.title }))
      : undefined,
    data: {
      url: data.url || '/',
      acoes: Array.isArray(data.actions)
        ? Object.fromEntries(data.actions.map((a) => [a.action, a.url]))
        : {},
    },
  };

  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const dados = event.notification.data || {};
  const targetUrl = (event.action && dados.acoes && dados.acoes[event.action]) || dados.url || '/';

  event.waitUntil(
    (async () => {
      const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
      // Reaproveita uma janela já aberta da app, se houver.
      for (const client of windows) {
        if ('focus' in client) {
          await client.focus();
          if ('navigate' in client) {
            try {
              await client.navigate(targetUrl);
            } catch (_e) {
              /* navegação cross-origin/proibida — ignora, a janela já está focada */
            }
          }
          return;
        }
      }
      if (self.clients.openWindow) {
        await self.clients.openWindow(targetUrl);
      }
    })(),
  );
});
