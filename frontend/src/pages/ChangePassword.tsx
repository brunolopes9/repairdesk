import { useState, type FormEvent } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { isAxiosError } from 'axios';
import { KeyRound, ShieldCheck } from 'lucide-react';
import { Button } from '../components/ui';
import { useAuth } from '../lib/auth/AuthContext';

interface LocationState {
  from?: { pathname?: string };
}

export default function ChangePassword() {
  const { status, user, changePassword } = useAuth();
  const location = useLocation();
  const from = (location.state as LocationState | null)?.from?.pathname ?? '/';

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (status === 'anonymous') {
    return <Navigate to="/login" replace />;
  }

  if (status === 'authenticated' && !user?.requireChangePasswordOnNextLogin) {
    return <Navigate to={from === '/auth/change-password' ? '/' : from} replace />;
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);

    if (newPassword !== confirmPassword) {
      setError('A nova password e a confirmacao nao coincidem.');
      return;
    }

    setSubmitting(true);
    try {
      await changePassword({ currentPassword, newPassword });
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
      <div className="w-full max-w-md">
        <div className="mb-8 flex flex-col items-center gap-2 text-center">
          <span className="grid h-12 w-12 place-items-center rounded-xl bg-amber-600 text-white shadow-sm">
            <ShieldCheck size={22} strokeWidth={2} />
          </span>
          <div className="text-lg font-semibold tracking-tight">Alterar password</div>
          <p className="text-xs text-zinc-500">
            A conta ainda usa a password inicial. Define uma password tua antes de continuar.
          </p>
        </div>

        <form
          onSubmit={onSubmit}
          className="space-y-4 rounded-2xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
        >
          <PasswordField
            id="currentPassword"
            label="Password actual"
            autoComplete="current-password"
            value={currentPassword}
            onChange={setCurrentPassword}
          />
          <PasswordField
            id="newPassword"
            label="Nova password"
            autoComplete="new-password"
            value={newPassword}
            onChange={setNewPassword}
          />
          <PasswordField
            id="confirmPassword"
            label="Confirmar nova password"
            autoComplete="new-password"
            value={confirmPassword}
            onChange={setConfirmPassword}
          />

          {error && (
            <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">
              {error}
            </div>
          )}

          <Button
            type="submit"
            size="lg"
            loading={submitting}
            leftIcon={!submitting ? <KeyRound size={15} /> : undefined}
            className="w-full"
          >
            {submitting ? 'A guardar...' : 'Guardar password'}
          </Button>
        </form>
      </div>
    </div>
  );
}

function PasswordField({
  id,
  label,
  autoComplete,
  value,
  onChange,
}: {
  id: string;
  label: string;
  autoComplete: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="text-xs font-medium text-zinc-600 dark:text-zinc-400">
        {label}
      </label>
      <input
        id={id}
        type="password"
        required
        minLength={8}
        autoComplete={autoComplete}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none transition focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
      />
    </div>
  );
}

function messageFor(code: string | null): string {
  switch (code) {
    case 'password_change_failed':
      return 'Confirma a password actual e usa uma nova password forte.';
    case 'user_inactive':
      return 'Conta desativada.';
    default:
      return 'Nao foi possivel alterar a password. Tenta novamente.';
  }
}
