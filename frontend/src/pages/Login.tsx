import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation } from 'react-router-dom';
import { isAxiosError } from 'axios';
import { Eye, EyeOff, LogIn } from 'lucide-react';
import { useAuth } from '../lib/auth/AuthContext';
import { Button } from '../components/ui';
import { AuthAlert, AuthField, AuthHeader, AuthShell } from '../components/auth/AuthShell';

interface LocationState {
  from?: { pathname?: string };
}

export default function Login() {
  const { status, user, login } = useAuth();
  const location = useLocation();
  const from = (location.state as LocationState | null)?.from?.pathname ?? '/';
  const passwordWasReset = new URLSearchParams(location.search).get('reset') === '1';

  const [identifier, setIdentifier] = useState('');
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
      await login({ login: identifier.trim(), password });
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
    <AuthShell
      footer={
        <div className="space-y-1">
          <div>© {new Date().getFullYear()} LopesTech · Mender</div>
          <div className="flex justify-center gap-3">
            <a href="/privacidade" className="hover:text-zinc-600 dark:hover:text-zinc-300">Privacidade</a>
            <span aria-hidden>·</span>
            <a href="/termos" className="hover:text-zinc-600 dark:hover:text-zinc-300">Termos</a>
            <span aria-hidden>·</span>
            <a href="/cookies" className="hover:text-zinc-600 dark:hover:text-zinc-300">Cookies</a>
          </div>
        </div>
      }
    >
      <form onSubmit={onSubmit} className="space-y-4">
        <AuthHeader title="Entrar" subtitle="Com o teu email ou nome de utilizador." />

        {passwordWasReset && (
          <AuthAlert tone="success">Palavra-passe alterada. Já podes entrar com a nova.</AuthAlert>
        )}

        <AuthField
          id="login"
          label="Email ou utilizador"
          type="text"
          required
          autoComplete="username"
          autoCapitalize="none"
          spellCheck={false}
          autoFocus
          value={identifier}
          onChange={(e) => setIdentifier(e.target.value)}
        />

        <div className="space-y-1">
          <AuthField
            id="password"
            label="Palavra-passe"
            type={showPassword ? 'text' : 'password'}
            required
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            trailing={
              <button
                type="button"
                onClick={() => setShowPassword((s) => !s)}
                aria-label={showPassword ? 'Esconder palavra-passe' : 'Mostrar palavra-passe'}
                tabIndex={-1}
                className="absolute right-2 top-1/2 -translate-y-1/2 rounded-md p-1 text-zinc-400 transition hover:bg-zinc-100 hover:text-zinc-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-300"
              >
                {showPassword ? <EyeOff size={15} /> : <Eye size={15} />}
              </button>
            }
          />
          <div className="text-right">
            <Link
              to="/auth/forgot-password"
              state={{ login: identifier.trim() }}
              className="text-xs font-medium text-brand-700 hover:underline dark:text-brand-400"
            >
              Esqueci a palavra-passe
            </Link>
          </div>
        </div>

        {error && <AuthAlert>{error}</AuthAlert>}

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
    </AuthShell>
  );
}

function messageFor(code: string | null): string {
  switch (code) {
    case 'invalid_credentials':
      return 'Utilizador ou palavra-passe inválidos.';
    case 'locked_out':
      return 'Conta temporariamente bloqueada após várias tentativas. Tenta daqui a 15 minutos ou repõe a palavra-passe.';
    case 'user_inactive':
      return 'Conta desativada.';
    default:
      return 'Não foi possível entrar. Tenta novamente.';
  }
}
