import { useState, type FormEvent } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { isAxiosError } from 'axios';
import { Eye, EyeOff, LogIn, Wrench } from 'lucide-react';
import { useAuth } from '../lib/auth/AuthContext';
import { Button } from '../components/ui';

interface LocationState {
  from?: { pathname?: string };
}

export default function Login() {
  const { status, user, login } = useAuth();
  const location = useLocation();
  const from = (location.state as LocationState | null)?.from?.pathname ?? '/';

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (status === 'authenticated') {
    if (user?.requireChangePasswordOnNextLogin) {
      return <Navigate to="/auth/change-password" replace />;
    }
    return <Navigate to={from} replace />;
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login({ email, password });
    } catch (err) {
      const code = isAxiosError(err)
        ? (err.response?.data as { code?: string } | undefined)?.code ?? null
        : null;
      setError(messageFor(code));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="grid min-h-screen place-items-center bg-zinc-50 px-4 py-10 dark:bg-zinc-950">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex flex-col items-center gap-2 text-center">
          <span className="grid h-12 w-12 place-items-center rounded-xl bg-brand-600 text-white shadow-sm">
            <Wrench size={22} strokeWidth={2} />
          </span>
          <div className="text-lg font-semibold tracking-tight">Mender</div>
          <p className="text-xs text-zinc-500">Gestão de oficinas, simples e profissional.</p>
        </div>

        <form
          onSubmit={onSubmit}
          className="space-y-4 rounded-2xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
        >
          <header className="space-y-1">
            <h1 className="text-xl font-semibold tracking-tight">Entrar</h1>
            <p className="text-xs text-zinc-500">Usa o teu email LopesTech.</p>
          </header>

          <div className="space-y-1.5">
            <label htmlFor="email" className="text-xs font-medium text-zinc-600 dark:text-zinc-400">
              Email
            </label>
            <input
              id="email"
              type="email"
              required
              autoComplete="username"
              autoFocus
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none transition focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
            />
          </div>

          <div className="space-y-1.5">
            <label htmlFor="password" className="text-xs font-medium text-zinc-600 dark:text-zinc-400">
              Password
            </label>
            <div className="relative">
              <input
                id="password"
                type={showPassword ? 'text' : 'password'}
                required
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 pr-10 text-sm outline-none transition focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
              />
              <button
                type="button"
                onClick={() => setShowPassword((s) => !s)}
                aria-label={showPassword ? 'Esconder password' : 'Mostrar password'}
                tabIndex={-1}
                className="absolute right-2 top-1/2 -translate-y-1/2 rounded-md p-1 text-zinc-400 transition hover:bg-zinc-100 hover:text-zinc-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-300"
              >
                {showPassword ? <EyeOff size={15} /> : <Eye size={15} />}
              </button>
            </div>
          </div>

          {error && (
            <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">
              {error}
            </div>
          )}

          <Button
            type="submit"
            size="lg"
            loading={submitting}
            leftIcon={!submitting ? <LogIn size={15} /> : undefined}
            className="w-full"
          >
            {submitting ? 'A entrar…' : 'Entrar'}
          </Button>
        </form>

        <div className="mt-6 space-y-1 text-center text-[11px] text-zinc-400">
          <div>© {new Date().getFullYear()} LopesTech · Mender</div>
          <div className="flex justify-center gap-3">
            <a href="/privacidade" className="hover:text-zinc-600 dark:hover:text-zinc-300">Privacidade</a>
            <span aria-hidden>·</span>
            <a href="/termos" className="hover:text-zinc-600 dark:hover:text-zinc-300">Termos</a>
            <span aria-hidden>·</span>
            <a href="/cookies" className="hover:text-zinc-600 dark:hover:text-zinc-300">Cookies</a>
          </div>
        </div>
      </div>
    </div>
  );
}

function messageFor(code: string | null): string {
  switch (code) {
    case 'invalid_credentials':
      return 'Email ou password inválidos.';
    case 'locked_out':
      return 'Conta temporariamente bloqueada após várias tentativas. Tenta daqui a 15 minutos.';
    case 'user_inactive':
      return 'Conta desativada.';
    default:
      return 'Não foi possível entrar. Tenta novamente.';
  }
}
