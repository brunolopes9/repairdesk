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
const ClienteCampanhas = lazy(() => import('./pages/clientes/ClienteCampanhas'));
const ClienteDetalhe = lazy(() => import('./pages/clientes/ClienteDetalhe'));
const Despesas = lazy(() => import('./pages/despesas/Despesas'));
const Compras = lazy(() => import('./pages/compras/Compras'));
const CompraEditor = lazy(() => import('./pages/compras/CompraEditor'));
const Vendas = lazy(() => import('./pages/vendas/Vendas'));
const VendaEditor = lazy(() => import('./pages/vendas/VendaEditor'));
const Auditoria = lazy(() => import('./pages/auditoria/Auditoria'));
const Tarefas = lazy(() => import('./pages/tarefas/Tarefas'));
const Definicoes = lazy(() => import('./pages/definicoes/Definicoes'));
const Perfil = lazy(() => import('./pages/definicoes/Perfil'));
const Preferencias = lazy(() => import('./pages/definicoes/Preferencias'));
const Fornecedores = lazy(() => import('./pages/definicoes/Fornecedores'));
const Automacoes = lazy(() => import('./pages/definicoes/Automacoes'));
const LlmUsage = lazy(() => import('./pages/definicoes/LlmUsage'));
const UsersDefinicoes = lazy(() => import('./pages/definicoes/Users'));
const OnboardingWizard = lazy(() => import('./pages/OnboardingWizard'));
const ChangePassword = lazy(() => import('./pages/ChangePassword'));
const ForgotPassword = lazy(() => import('./pages/auth/ForgotPassword'));
const ResetPassword = lazy(() => import('./pages/auth/ResetPassword'));
const PortalCliente = lazy(() => import('./pages/PortalCliente'));
const PortalGarantia = lazy(() => import('./pages/PortalGarantia'));
const PedidoReparacao = lazy(() => import('./pages/PedidoReparacao'));
const Agendar = lazy(() => import('./pages/Agendar'));
const PedidosOnline = lazy(() => import('./pages/pedidos/PedidosOnline'));
const Agendamentos = lazy(() => import('./pages/agendamentos/Agendamentos'));
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
            <Route path="/agendar/:slug" element={<Agendar />} />

            {/* Páginas legais públicas — RGPD compliance */}
            <Route path="/privacidade" element={<PoliticaPrivacidade />} />
            <Route path="/termos" element={<Termos />} />
            <Route path="/cookies" element={<Cookies />} />
            <Route path="/dpa" element={<Dpa />} />
            <Route path="/sub-processors" element={<SubProcessors />} />

            <Route path="/login" element={<Login />} />
            <Route path="/auth/forgot-password" element={<ForgotPassword />} />
            <Route path="/auth/reset-password" element={<ResetPassword />} />
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
              <Route path="/clientes/campanhas" element={<ClienteCampanhas />} />
              <Route path="/clientes/:id" element={<ClienteDetalhe />} />
              <Route path="/reparacoes" element={<Navigate to="/vendas?tipo=1" replace />} />
              <Route path="/reparacoes/:id" element={<Navigate to="/vendas?tipo=1" replace />} />
              <Route path="/trabalhos" element={<Navigate to="/vendas?tipo=2" replace />} />
              <Route path="/despesas" element={<Despesas />} />
              <Route path="/compras" element={<Compras />} />
              <Route path="/compras/nova" element={<CompraEditor />} />
              <Route path="/compras/:id" element={<CompraEditor />} />
              <Route path="/cash" element={<Navigate to="/vendas" replace />} />
              <Route path="/importacoes" element={<Navigate to="/compras?tab=pending" replace />} />
              <Route path="/vendas" element={<Vendas />} />
              <Route path="/vendas/nova" element={<VendaEditor />} />
              <Route path="/vendas/:id" element={<VendaEditor />} />
              <Route path="/stock" element={<Navigate to="/compras?tab=stock" replace />} />
              <Route path="/precos" element={<Navigate to="/compras?tab=simulador" replace />} />
              <Route path="/relatorios/*" element={<Navigate to="/compras?tab=resumo" replace />} />
              <Route path="/pedidos-online" element={<PedidosOnline />} />
              <Route path="/agendamentos" element={<Agendamentos />} />
              <Route path="/compras-operacao" element={<Navigate to="/compras" replace />} />
              <Route path="/documentos" element={<Navigate to="/vendas" replace />} />
              <Route path="/balcao" element={<Navigate to="/vendas/nova?tipo=0" replace />} />
              <Route path="/catalogo" element={<Navigate to="/compras?tab=stock" replace />} />
              <Route path="/inventario" element={<Navigate to="/compras?tab=stock" replace />} />
              <Route path="/tarefas" element={<Tarefas />} />
              <Route path="/auditoria" element={<Auditoria />} />
              <Route path="/definicoes" element={<Definicoes />} />
              <Route path="/definicoes/perfil" element={<Perfil />} />
              <Route path="/definicoes/preferencias" element={<Preferencias />} />
              <Route path="/definicoes/webhooks" element={<Navigate to="/definicoes" replace />} />
              <Route path="/definicoes/fornecedores" element={<Fornecedores />} />
              <Route path="/definicoes/automacoes" element={<Automacoes />} />
              <Route path="/definicoes/llm-usage" element={<LlmUsage />} />
              <Route path="/definicoes/utilizadores" element={<UsersDefinicoes />} />
              <Route path="/produtos" element={<Navigate to="/compras?tab=stock" replace />} />
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
