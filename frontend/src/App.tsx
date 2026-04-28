import { BrowserRouter, Route, Routes } from 'react-router-dom';
import Layout from './components/Layout';
import Dashboard from './pages/Dashboard';

const Placeholder = ({ name }: { name: string }) => (
  <div>
    <h1 className="text-2xl font-semibold">{name}</h1>
    <p className="mt-2 text-sm text-zinc-500">Em construção (Sprint próximo).</p>
  </div>
);

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="/clientes" element={<Placeholder name="Clientes" />} />
          <Route path="/reparacoes" element={<Placeholder name="Reparações" />} />
          <Route path="/pecas" element={<Placeholder name="Peças" />} />
          <Route path="/faturacao" element={<Placeholder name="Faturação" />} />
          <Route path="*" element={<Placeholder name="404" />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
