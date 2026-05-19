import { Suspense, lazy } from 'react';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { Toaster } from 'sonner';
import CommandPalette from './components/CommandPalette';
import CookieBanner from './components/CookieBanner';
import GShortcuts from './components/GShortcuts';
import KeyboardHelp from './components/KeyboardHelp';
import Layout from './components/Layout';
import ProtectedRoute from './components/ProtectedRoute';
import { AuthProvider } from './lib/auth/AuthContext';

// Login mantém-se eager (primeira página em utilizadores não autenticados).
import Login from './pages/Login';
// NotFound também é pequeno e usado em fallback.
import NotFound from './pages/NotFound';

// Tudo o resto é code-split por route — reduz bundle inicial ~30-40%.
const Dashboard = lazy(() => import('./pages/Dashboard'));
const Clientes = lazy(() => import('./pages/clientes/Clientes'));
const ClienteDetalhe = lazy(() => import('./pages/clientes/ClienteDetalhe'));
const Reparacoes = lazy(() => import('./pages/reparacoes/Reparacoes'));
const ReparacaoDetalhe = lazy(() => import('./pages/reparacoes/ReparacaoDetalhe'));
const Trabalhos = lazy(() => import('./pages/trabalhos/Trabalhos'));
const TrabalhoDetalhe = lazy(() => import('./pages/trabalhos/TrabalhoDetalhe'));
const Despesas = lazy(() => import('./pages/despesas/Despesas'));
const Stock = lazy(() => import('./pages/stock/Stock'));
const Vendas = lazy(() => import('./pages/vendas/Vendas'));
const Auditoria = lazy(() => import('./pages/auditoria/Auditoria'));
const Definicoes = lazy(() => import('./pages/definicoes/Definicoes'));
const Webhooks = lazy(() => import('./pages/definicoes/Webhooks'));
const Precos = lazy(() => import('./pages/precos/Precos'));
const RelatorioIva = lazy(() => import('./pages/relatorios/Iva'));
const OnboardingWizard = lazy(() => import('./pages/OnboardingWizard'));
const PortalCliente = lazy(() => import('./pages/PortalCliente'));
const PortalGarantia = lazy(() => import('./pages/PortalGarantia'));
const PoliticaPrivacidade = lazy(() => import('./pages/legal/PoliticaPrivacidade'));
const Termos = lazy(() => import('./pages/legal/Termos'));
const Cookies = lazy(() => import('./pages/legal/Cookies'));

function RouteLoading() {
  // Skeleton subtil que mantém estrutura tipo "page com header + 3 cards".
  // Mais profissional que spinner — utilizador percebe "está a vir conteúdo aqui".
  return (
    <div className="mx-auto max-w-5xl space-y-4 px-4 py-6">
      <div className="space-y-2">
        <div className="h-7 w-1/3 animate-pulse rounded bg-zinc-200 dark:bg-zinc-800" />
        <div className="h-4 w-1/2 animate-pulse rounded bg-zinc-200/70 dark:bg-zinc-800/70" />
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        {[0, 1, 2].map((i) => (
          <div key={i} className="space-y-2 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
            <div className="h-3 w-1/3 animate-pulse rounded bg-zinc-200 dark:bg-zinc-800" />
            <div className="h-5 w-2/3 animate-pulse rounded bg-zinc-200 dark:bg-zinc-800" />
            <div className="h-3 w-1/2 animate-pulse rounded bg-zinc-200/70 dark:bg-zinc-800/70" />
          </div>
        ))}
      </div>
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <Toaster
        position="bottom-right"
        toastOptions={{
          className: 'text-sm',
        }}
        closeButton
        richColors
      />
      <CookieBanner />
      <AuthProvider>
        <CommandPalette />
        <KeyboardHelp />
        <GShortcuts />
        <Suspense fallback={<RouteLoading />}>
          <Routes>
            {/* Portal cliente público — sem layout, sem auth */}
            <Route path="/r/:slug" element={<PortalCliente />} />
            <Route path="/g/:slug" element={<PortalGarantia />} />

            {/* Páginas legais públicas — RGPD compliance */}
            <Route path="/privacidade" element={<PoliticaPrivacidade />} />
            <Route path="/termos" element={<Termos />} />
            <Route path="/cookies" element={<Cookies />} />

            <Route path="/login" element={<Login />} />
            <Route
              path="/bemvindo"
              element={
                <ProtectedRoute>
                  <OnboardingWizard />
                </ProtectedRoute>
              }
            />
            <Route
              element={
                <ProtectedRoute>
                  <Layout />
                </ProtectedRoute>
              }
            >
              <Route index element={<Dashboard />} />
              <Route path="/clientes" element={<Clientes />} />
              <Route path="/clientes/:id" element={<ClienteDetalhe />} />
              <Route path="/reparacoes" element={<Reparacoes />} />
              <Route path="/reparacoes/:id" element={<ReparacaoDetalhe />} />
              <Route path="/trabalhos" element={<Trabalhos />} />
              <Route path="/trabalhos/:id" element={<TrabalhoDetalhe />} />
              <Route path="/despesas" element={<Despesas />} />
              <Route path="/vendas" element={<Vendas />} />
              <Route path="/stock" element={<Stock />} />
              <Route path="/precos" element={<Precos />} />
              <Route path="/relatorios/iva" element={<RelatorioIva />} />
              <Route path="/auditoria" element={<Auditoria />} />
              <Route path="/definicoes" element={<Definicoes />} />
              <Route path="/definicoes/webhooks" element={<Webhooks />} />
              <Route path="*" element={<NotFound />} />
            </Route>
          </Routes>
        </Suspense>
      </AuthProvider>
    </BrowserRouter>
  );
}
