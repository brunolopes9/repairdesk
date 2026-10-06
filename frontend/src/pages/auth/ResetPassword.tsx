import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { isAxiosError } from 'axios';
import { ArrowLeft, KeyRound } from 'lucide-react';
import { api } from '../../lib/api';
import { Button } from '../../components/ui';
import { AuthAlert, AuthField, AuthHeader, AuthShell, passwordProblem } from '../../components/auth/AuthShell';

export default function ResetPassword() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const userId = params.get('uid') ?? '';
  const token = params.get('token') ?? '';

  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [linkInvalid, setLinkInvalid] = useState(!userId || !token);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);

    const problem = passwordProblem(password);
    if (problem) return setError(problem);
    if (password !== confirm) return setError('As palavras-passe não coincidem.');

    setSubmitting(true);
    try {
      await api.post('/auth/reset-password', { userId, token, newPassword: password });
      navigate('/login?reset=1', { replace: true });
    } catch (err) {
      const code = isAxiosError(err)
        ? (err.response?.data as { code?: string } | undefined)?.code ?? null
        : null;
      if (code === 'invalid_token') setLinkInvalid(true);
      else if (code === 'password_invalid') setError('Palavra-passe fraca. Usa 8+ caracteres com maiúsculas, minúsculas e números.');
      else setError('Não foi possível alterar a palavra-passe. Tenta novamente.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AuthShell>
      <AuthHeader title="Nova palavra-passe" subtitle="Escolhe a palavra-passe que vais usar a partir de agora." />

      {linkInvalid ? (
        <div className="space-y-4">
          <AuthAlert>Este link expirou ou já foi usado. Pede um novo — é válido durante 1 hora.</AuthAlert>
          <Link to="/auth/forgot-password" className="block">
            <Button size="lg" className="w-full">Pedir novo link</Button>
          </Link>
        </div>
      ) : (
        <form onSubmit={onSubmit} className="space-y-4">
          <AuthField
            id="password"
            label="Nova palavra-passe"
            type="password"
            required
            autoFocus
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          <AuthField
            id="confirm"
            label="Confirmar palavra-passe"
            type="password"
            required
            autoComplete="new-password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
          />
          <p className="text-[11px] text-zinc-500">Mínimo 8 caracteres, com maiúsculas, minúsculas e números.</p>

          {error && <AuthAlert>{error}</AuthAlert>}

          <Button
            type="submit"
            size="lg"
            loading={submitting}
            leftIcon={!submitting ? <KeyRound size={15} /> : undefined}
            className="w-full"
          >
            {submitting ? 'A guardar…' : 'Guardar palavra-passe'}
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
