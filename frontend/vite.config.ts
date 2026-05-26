import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    // Sprint 194: PWA Fase 1 (doc 24). App instalável + cache app shell.
    // Substitui o SW manual da Sprint 39. SEM offline write — Fases 2-4 ficam
    // para quando houver clientes reais com WiFi instável.
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg', 'icons.svg'],
      manifest: {
        name: 'Mender · LopesTech',
        short_name: 'Mender',
        description: 'Gestão de oficinas de reparação — clientes, reparações, stock, faturação.',
        start_url: '/?source=pwa',
        scope: '/',
        display: 'standalone',
        background_color: '#0EA5E9',
        theme_color: '#0EA5E9',
        orientation: 'any',
        lang: 'pt-PT',
        categories: ['business', 'productivity'],
        icons: [
          { src: '/favicon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any maskable' },
        ],
        shortcuts: [
          { name: 'Nova reparação', short_name: 'Nova', url: '/reparacoes?new=1', icons: [{ src: '/favicon.svg', sizes: 'any' }] },
          { name: 'Dashboard', short_name: 'Dashboard', url: '/', icons: [{ src: '/favicon.svg', sizes: 'any' }] },
        ],
      },
      workbox: {
        // Sprint 365: injeta os handlers de push (push-sw.js em /public) no SW gerado.
        // generateSW não traz listeners de 'push' — sem isto a notificação nunca aparece.
        importScripts: ['push-sw.js'],
        // Doc 24: nunca cachear /api/* autenticado no SW — multi-tenant safety.
        // Cache só assets estáticos. App shell NetworkFirst (evita preso em build velha).
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api\//],
        runtimeCaching: [
          {
            urlPattern: ({ request }) => request.mode === 'navigate',
            handler: 'NetworkFirst',
            options: {
              cacheName: 'pages',
              networkTimeoutSeconds: 3,
              expiration: { maxEntries: 50, maxAgeSeconds: 24 * 60 * 60 },
            },
          },
          {
            urlPattern: ({ request, url }) =>
              request.destination === 'image' && (url.pathname === '/favicon.svg' || url.pathname.startsWith('/assets/')),
            handler: 'CacheFirst',
            options: {
              cacheName: 'static-images',
              expiration: { maxEntries: 60, maxAgeSeconds: 30 * 24 * 60 * 60 },
            },
          },
          {
            urlPattern: ({ request }) => request.destination === 'font',
            handler: 'CacheFirst',
            options: {
              cacheName: 'fonts',
              expiration: { maxEntries: 10, maxAgeSeconds: 365 * 24 * 60 * 60 },
            },
          },
        ],
      },
      devOptions: {
        enabled: false,
      },
    }),
  ],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
});
