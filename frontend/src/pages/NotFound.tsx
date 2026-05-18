import { Link, useLocation, useNavigate } from 'react-router-dom';
import { ArrowLeft, Compass, Home } from 'lucide-react';
import { Button } from '../components/ui';

export default function NotFound() {
  const location = useLocation();
  const navigate = useNavigate();

  return (
    <div className="grid min-h-[60vh] place-items-center">
      <div className="mx-auto max-w-md px-4 text-center">
        <div className="mx-auto mb-6 grid h-16 w-16 place-items-center rounded-full bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">
          <Compass size={28} strokeWidth={1.75} />
        </div>
        <div className="mb-1 font-mono text-xs uppercase tracking-widest text-zinc-400">404</div>
        <h1 className="text-2xl font-semibold tracking-tight">Página não encontrada</h1>
        <p className="mt-2 text-sm text-zinc-500">
          A página <code className="rounded bg-zinc-100 px-1 py-0.5 text-xs dark:bg-zinc-800">{location.pathname}</code> não existe (ou já não existe).
        </p>
        <p className="mt-1 text-xs text-zinc-400">
          Se chegaste aqui através de um link interno, avisa-nos —{' '}
          <a href="mailto:geral@lopestech.pt" className="hover:underline">geral@lopestech.pt</a>.
        </p>
        <div className="mt-6 flex flex-wrap items-center justify-center gap-2">
          <Button variant="secondary" leftIcon={<ArrowLeft size={15} />} onClick={() => navigate(-1)}>
            Voltar
          </Button>
          <Link to="/">
            <Button leftIcon={<Home size={15} />}>Ir para o dashboard</Button>
          </Link>
        </div>
      </div>
    </div>
  );
}
