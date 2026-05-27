import { Suspense, lazy } from 'react';
import { BrowserRouter, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { Toaster } from 'sonner';
import CommandPalette from './components/CommandPalette';
import { ConfirmProvider } from './components/ConfirmDialog';
import CookieBanner from './components/CookieBanner';
import { ErrorBoundary } from './components/ErrorBoundary';
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
const Compras = lazy(() => import('./pages/compras/Compras'));
const Cash = lazy(() => import('./pages/cash/Cash'));
const Stock = lazy(() => import('./pages/stock/Stock'));
const Vendas = lazy(() => import('./pages/vendas/Vendas'));
const Auditoria = lazy(() => import('./pages/auditoria/Auditoria'));
const Definicoes = lazy(() => import('./pages/definicoes/Definicoes'));
const Preferencias = lazy(() => import('./pages/definicoes/Preferencias'));
const Webhooks = lazy(() => import('./pages/definicoes/Webhooks'));
const Fornecedores = lazy(() => import('./pages/definicoes/Fornecedores'));
const PartKitsPage = lazy(() => import('./pages/definicoes/PartKits'));
const Automacoes = lazy(() => import('./pages/definicoes/Automacoes'));
const LlmUsage = lazy(() => import('./pages/definicoes/LlmUsage'));
const UsersDefinicoes = lazy(() => import('./pages/definicoes/Users'));
const Produtos = lazy(() => import('./pages/produtos/Produtos'));
const Precos = lazy(() => import('./pages/precos/Precos'));
const RelatorioIva = lazy(() => import('./pages/relatorios/Iva'));
const RelatorioNegocio = lazy(() => import('./pages/relatorios/Negocio'));
const RelatorioProdutividade = lazy(() => import('./pages/relatorios/Produtividade'));
const OnboardingWizard = lazy(() => import('./pages/OnboardingWizard'));
const ChangePassword = lazy(() => import('./pages/ChangePassword'));
const PortalCliente = lazy(() => import('./pages/PortalCliente'));
const PortalGarantia = lazy(() => import('./pages/PortalGarantia'));
const PedidoReparacao = lazy(() => import('./pages/PedidoReparacao'));
const PedidosOnline = lazy(() => import('./pages/reparacoes/PedidosOnline'));
const Agendamentos = lazy(() => import('./pages/agendamentos/Agendamentos'));
const ComprasOperacao = lazy(() => import('./pages/compras/ComprasOperacao'));
const Balcao = lazy(() => import('./pages/balcao/Balcao'));
const PoliticaPrivacidade = lazy(() => import('./pages/legal/PoliticaPrivacidade'));
const Termos = lazy(() => import('./pages/legal/Termos'));
const Cookies = lazy(() => import('./pages/legal/Cookies'));
const Dpa = lazy(() => import('./pages/legal/Dpa'));
const SubProcessors = lazy(() => import('./pages/legal/SubProcessors'));

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
        <ConfirmProvider>
        <CommandPalette />
        <KeyboardHelp />
        <GShortcuts />
        <Suspense fallback={<RouteLoading />}>
          <RouteErrorBoundary>
          <Routes>
            {/* Portal cliente público — sem layout, sem auth */}
            <Route path="/r/:slug" element={<PortalCliente />} />
            <Route path="/g/:slug" element={<PortalGarantia />} />
            <Route path="/pedido/:slug" element={<PedidoReparacao />} />

            {/* Páginas legais públicas — RGPD compliance */}
            <Route path="/privacidade" element={<PoliticaPrivacidade />} />
            <Route path="/termos" element={<Termos />} />
            <Route path="/cookies" element={<Cookies />} />
            <Route path="/dpa" element={<Dpa />} />
            <Route path="/sub-processors" element={<SubProcessors />} />

            <Route path="/login" element={<Login />} />
            <Route
              path="/auth/change-password"
              element={
                <ProtectedRoute>
                  <ChangePassword />
                </ProtectedRoute>
              }
            />
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
              <Route path="/compras" element={<Compras />} />
              <Route path="/cash" element={<Cash />} />
              <Route path="/importacoes" element={<Navigate to="/compras?tab=pending" replace />} />
              <Route path="/vendas" element={<Vendas />} />
              <Route path="/stock" element={<Stock />} />
              <Route path="/precos" element={<Precos />} />
              <Route path="/relatorios/iva" element={<RelatorioIva />} />
              <Route path="/relatorios/negocio" element={<RelatorioNegocio />} />
              <Route path="/relatorios/produtividade" element={<RelatorioProdutividade />} />
              <Route path="/pedidos-online" element={<PedidosOnline />} />
              <Route path="/agendamentos" element={<Agendamentos />} />
              <Route path="/compras-operacao" element={<ComprasOperacao />} />
              <Route path="/balcao" element={<Balcao />} />
              <Route path="/auditoria" element={<Auditoria />} />
              <Route path="/definicoes" element={<Definicoes />} />
              <Route path="/definicoes/preferencias" element={<Preferencias />} />
              <Route path="/definicoes/webhooks" element={<Webhooks />} />
              <Route path="/definicoes/fornecedores" element={<Fornecedores />} />
              <Route path="/definicoes/kits" element={<PartKitsPage />} />
              <Route path="/definicoes/automacoes" element={<Automacoes />} />
              <Route path="/definicoes/llm-usage" element={<LlmUsage />} />
              <Route path="/definicoes/utilizadores" element={<UsersDefinicoes />} />
              <Route path="/produtos" element={<Produtos />} />
              <Route path="*" element={<NotFound />} />
            </Route>
          </Routes>
          </RouteErrorBoundary>
        </Suspense>
        </ConfirmProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}

/**
 * Sprint 253 (Doc 77): boundary que reseta quando o utilizador navega. Sem
 * isto, um erro numa rota persiste mesmo depois do clique para outro link.
 */
function RouteErrorBoundary({ children }: { children: React.ReactNode }) {
  const location = useLocation();
  return (
    <ErrorBoundary scope={location.pathname} key={location.pathname}>
      {children}
    </ErrorBoundary>
  );
}
