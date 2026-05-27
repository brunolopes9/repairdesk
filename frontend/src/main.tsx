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
//
// Sprint 378: auto-update agressivo. Antes o SW (Workbox autoUpdate) servia o bundle antigo
// em cache até TODAS as tabs fecharem — fazia parecer que os deploys "não mudavam nada".
// Agora: updateViaCache='none' (re-busca sempre o sw.js), update() periódico, e quando o novo
// SW toma controlo recarrega a página uma vez → versão nova aparece sozinha em ~minuto.
if ('serviceWorker' in navigator && import.meta.env.PROD) {
  let refreshing = false;
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (refreshing) return;
    refreshing = true;
    window.location.reload();
  });
  window.addEventListener('load', async () => {
    try {
      const reg = await navigator.serviceWorker.register('/sw.js', { updateViaCache: 'none' });
      reg.update();
      setInterval(() => reg.update(), 60_000);
    } catch {
      /* silently ignore — não bloqueia a app */
    }
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
