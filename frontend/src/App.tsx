import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { Toaster } from 'sonner';
import Layout from './components/Layout';
import ProtectedRoute from './components/ProtectedRoute';
import { AuthProvider } from './lib/auth/AuthContext';
import Dashboard from './pages/Dashboard';
import Clientes from './pages/clientes/Clientes';
import ClienteDetalhe from './pages/clientes/ClienteDetalhe';
import Login from './pages/Login';
import Reparacoes from './pages/reparacoes/Reparacoes';
import ReparacaoDetalhe from './pages/reparacoes/ReparacaoDetalhe';
import Trabalhos from './pages/trabalhos/Trabalhos';
import TrabalhoDetalhe from './pages/trabalhos/TrabalhoDetalhe';
import Despesas from './pages/despesas/Despesas';
import Stock from './pages/stock/Stock';
import Definicoes from './pages/definicoes/Definicoes';
import Precos from './pages/precos/Precos';
import OnboardingWizard from './pages/OnboardingWizard';
import PortalCliente from './pages/PortalCliente';
import PortalGarantia from './pages/PortalGarantia';

const Placeholder = ({ name }: { name: string }) => (
  <div>
    <h1 className="text-2xl font-semibold">{name}</h1>
    <p className="mt-2 text-sm text-zinc-500">Em construção (Sprint próximo).</p>
  </div>
);

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
      <AuthProvider>
        <Routes>
          {/* Portal cliente público — sem layout, sem auth */}
          <Route path="/r/:slug" element={<PortalCliente />} />
          <Route path="/g/:slug" element={<PortalGarantia />} />

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
            <Route path="/stock" element={<Stock />} />
            <Route path="/precos" element={<Precos />} />
            <Route path="/definicoes" element={<Definicoes />} />
            <Route path="*" element={<Placeholder name="404" />} />
          </Route>
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}
