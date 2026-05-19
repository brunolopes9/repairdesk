// RepairDesk service worker mínimo — Sprint 39
//
// Objectivo: tornar a app "installable" (PWA) + cache leve do shell.
// NÃO é a estratégia offline-full descrita em Contexto/24-PWA-Offline.md
// (sync, conflict resolution, IndexedDB) — isso fica para mais tarde.
//
// Estratégia:
//  - Assets estáticas (HTML/CSS/JS/imagens) → cache-first com network fallback
//  - /api/*  → network only (nunca cache de dados — risco de mostrar stale)
//  - Auto-skipWaiting + clients.claim: nova versão activa imediatamente

const VERSION = 'rd-v1';
const SHELL_CACHE = `rd-shell-${VERSION}`;

// Assets a pré-cachear no install (failsafe — assets reais vão sendo cacheadas em runtime)
const PRECACHE = ['/', '/favicon.svg', '/manifest.webmanifest'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(SHELL_CACHE).then((cache) => cache.addAll(PRECACHE)).then(() => self.skipWaiting()),
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== SHELL_CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;

  const url = new URL(req.url);

  // Never cache API responses — data integrity > offline UX
  if (url.pathname.startsWith('/api/')) return;

  // Never cache cross-origin (extensions, CDN)
  if (url.origin !== self.location.origin) return;

  // Cache-first com network fallback
  event.respondWith(
    caches.match(req).then((cached) => {
      if (cached) {
        // Refresca em background (stale-while-revalidate light)
        fetch(req)
          .then((fresh) => {
            if (fresh && fresh.ok) {
              caches.open(SHELL_CACHE).then((cache) => cache.put(req, fresh.clone()));
            }
          })
          .catch(() => {
            /* offline — fica com a versão cached */
          });
        return cached;
      }
      return fetch(req)
        .then((response) => {
          if (response && response.ok && response.status === 200) {
            const copy = response.clone();
            caches.open(SHELL_CACHE).then((cache) => cache.put(req, copy));
          }
          return response;
        })
        .catch(() => {
          // Sem cache e sem rede — devolve fallback básico
          if (req.mode === 'navigate') {
            return caches.match('/');
          }
          return new Response('Offline', { status: 503, statusText: 'Offline' });
        });
    }),
  );
});

self.addEventListener('push', (event) => {
  let data = {};
  try {
    data = event.data ? event.data.json() : {};
  } catch {
    data = { body: event.data ? event.data.text() : '' };
  }

  const title = data.title || 'Actualização da reparação';
  const options = {
    body: data.body || 'Há novidades no estado da tua reparação.',
    icon: '/favicon.svg',
    badge: '/favicon.svg',
    tag: data.tag || 'repairdesk-repair-update',
    renotify: true,
    data: {
      url: data.url || '/',
    },
  };

  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const target = new URL(event.notification.data?.url || '/', self.location.origin).href;

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clients) => {
      for (const client of clients) {
        if (client.url === target && 'focus' in client) return client.focus();
      }
      return self.clients.openWindow(target);
    }),
  );
});
