import { useState, type FormEvent } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { isAxiosError } from 'axios';
import { ArrowLeft, Mail } from 'lucide-react';
import { api } from '../../lib/api';
import { Button } from '../../components/ui';
import { AuthAlert, AuthField, AuthHeader, AuthShell } from '../../components/auth/AuthShell';

export default function ForgotPassword() {
  const location = useLocation();
  const initial = (location.state as { login?: string } | null)?.login ?? '';

  const [login, setLogin] = useState(initial);
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await api.post('/auth/forgot-password', { login: login.trim() });
      setSent(true);
    } catch (err) {
      setError(
        isAxiosError(err) && err.response?.status === 429
          ? 'Demasiados pedidos. Espera uns minutos e tenta outra vez.'
          : 'Não foi possível enviar o pedido. Tenta novamente.',
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AuthShell>
      <AuthHeader
        title="Esqueci a palavra-passe"
        subtitle="Enviamos-te um link para escolheres uma nova palavra-passe."
      />

      {sent ? (
        <AuthAlert tone="success">
          Se existir uma conta com esses dados, vais receber um email com o link nos próximos minutos.
          Confirma também o spam. O link é válido durante 1 hora.
        </AuthAlert>
      ) : (
        <form onSubmit={onSubmit} className="space-y-4">
          <AuthField
            id="login"
            label="Email ou utilizador"
            type="text"
            required
            autoComplete="username"
            autoCapitalize="none"
            spellCheck={false}
            autoFocus
            value={login}
            onChange={(e) => setLogin(e.target.value)}
          />

          {error && <AuthAlert>{error}</AuthAlert>}

          <Button
            type="submit"
            size="lg"
            loading={submitting}
            leftIcon={!submitting ? <Mail size={15} /> : undefined}
            className="w-full"
          >
            {submitting ? 'A enviar…' : 'Enviar link'}
          </Button>
        </form>
      )}

      <Link
        to="/login"
        className="inline-flex items-center gap-1 text-xs font-medium text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300"
      >
        <ArrowLeft size={13} /> Voltar ao login
      </Link>
    </AuthShell>
  );
}
