import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import './index.css';
import App from './App.tsx';
import { applyTheme, getStoredTheme } from './lib/theme';
import { initSentry } from './lib/monitoring/sentry';

// Sprint 250: Sentry primeiro para apanhar erros do bootstrap. No-op sem DSN.
initSentry();

// Aplica tema cedo para evitar flash
applyTheme(getStoredTheme());

// Registar service worker (PWA installable + shell cache).
// Só em produção — em dev (Vite HMR) seria barrigudo.
if ('serviceWorker' in navigator && import.meta.env.PROD) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      /* silently ignore — não bloqueia a app */
    });
  });
}

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, refetchOnWindowFocus: false, retry: 1 },
    mutations: { retry: 0 },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
);
