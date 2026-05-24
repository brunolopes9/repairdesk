import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { toast } from 'sonner';
import './index.css';
import App from './App.tsx';
import { ErrorBoundary } from './components/ErrorBoundary';
import { applyTheme, getStoredTheme } from './lib/theme';
import { apiErrorMessage } from './lib/errors';
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

// Sprint 253 (Doc 77): MutationCache global onError como fallback. Mutations com
// onError próprio NÃO disparam este (handlers locais ganham precedência via React
// Query — só o cache-level corre se não houver handler local).
const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, refetchOnWindowFocus: false, retry: 1 },
    mutations: { retry: 0 },
  },
  mutationCache: new MutationCache({
    onError(error, _vars, _ctx, mutation) {
      // Skip se a mutation declarou onError próprio — ele já mostrou UI específica.
      if (mutation.options.onError) return;
      toast.error(apiErrorMessage(error));
    },
  }),
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary scope="root">
      <QueryClientProvider client={queryClient}>
        <App />
      </QueryClientProvider>
    </ErrorBoundary>
  </StrictMode>,
);
