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
      includeAssets: ['favicon.ico', 'apple-touch-icon.png', 'icons.svg', 'logo-64.png', 'badge-96.png'],
      manifest: {
        name: 'Mender · LopesTech',
        short_name: 'Mender',
        description: 'Gestão de oficinas de reparação — clientes, reparações, stock, faturação.',
        start_url: '/?source=pwa',
        scope: '/',
        display: 'standalone',
        background_color: '#01040F',
        theme_color: '#0EA5E9',
        orientation: 'any',
        lang: 'pt-PT',
        categories: ['business', 'productivity'],
        icons: [
          // 26/11: ícone da LopesTech (o mesmo do site lopestech.pt).
          { src: '/icon-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
          { src: '/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
          { src: '/maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
        shortcuts: [
          { name: 'Nova reparação', short_name: 'Nova', url: '/reparacoes?new=1', icons: [{ src: '/icon-192.png', sizes: '192x192', type: 'image/png' }] },
          { name: 'Dashboard', short_name: 'Dashboard', url: '/', icons: [{ src: '/icon-192.png', sizes: '192x192', type: 'image/png' }] },
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
              request.destination === 'image' && (/^\/(icon-|maskable-|badge-|logo-|apple-touch-icon)/.test(url.pathname) || url.pathname.startsWith('/assets/')),
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
