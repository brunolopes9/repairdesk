import { useEffect, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Lock, UserCircle } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { BackButton } from '../../components/ui';
import { useAuth } from '../../lib/auth/AuthContext';
import { toast } from '../../lib/toast';
import { apiErrorMessage } from '../../lib/errors';

/**
 * Sprint 420 (Doc 90 Tier 1 #2): página "O meu perfil".
 *
 * Permite ao utilizador alterar dados próprios (nome a mostrar, telefone) sem
 * passar pelo admin de utilizadores. Email fica read-only (requer fluxo de
 * verificação separado). Roles read-only (só admin altera no /definicoes/utilizadores).
 *
 * Inclui também acesso rápido a "Alterar palavra-passe" — usa o fluxo existente
 * change-password que revoga refresh tokens e força re-login.
 */
export default function Perfil() {
  const { user, updateMe, changePassword } = useAuth();
  const [displayName, setDisplayName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [pwOpen, setPwOpen] = useState(false);
  const [currentPw, setCurrentPw] = useState('');
  const [newPw, setNewPw] = useState('');

  useEffect(() => {
    if (!user) return;
    setDisplayName(user.displayName ?? '');
    setPhoneNumber(user.phoneNumber ?? '');
  }, [user]);

  const save = useMutation({
    mutationFn: () =>
      updateMe({
        displayName: displayName.trim(),
        phoneNumber: phoneNumber.trim() || null,
      }),
    onSuccess: () => toast.success('Perfil actualizado.'),
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao guardar.'),
  });

  const pwMut = useMutation({
    mutationFn: () => changePassword({ currentPassword: currentPw, newPassword: newPw }),
    onSuccess: () => {
      toast.success('Palavra-passe alterada — sessão renovada.');
      setPwOpen(false);
      setCurrentPw('');
      setNewPw('');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao alterar palavra-passe.'),
  });

  if (!user) return null;

  const input =
    'w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand-400 disabled:bg-zinc-50 disabled:text-zinc-500 dark:border-zinc-700 dark:bg-zinc-950 dark:disabled:bg-zinc-900';
  const dirty =
    displayName.trim() !== (user.displayName ?? '') ||
    (phoneNumber.trim() || null) !== (user.phoneNumber ?? null);

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <BackButton to="/definicoes" />
          <h1 className="mt-1 flex items-center gap-2 text-2xl font-semibold tracking-tight">
            <UserCircle size={24} /> O meu perfil
          </h1>
          <p className="text-sm text-zinc-500">Dados que apareçem nos PDFs, recibos e timeline das reparações que executas.</p>
        </div>
      </div>

      <section className="space-y-4 rounded-xl border border-zinc-200 bg-white p-5 dark:border-zinc-800 dark:bg-zinc-900">
        <h2 className="text-sm font-semibold">Dados pessoais</h2>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Nome a mostrar">
            <input
              className={input}
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              maxLength={100}
              placeholder="Ex: Bruno Lopes"
            />
          </Field>
          <Field label="Telefone">
            <input
              className={input}
              value={phoneNumber}
              onChange={(e) => setPhoneNumber(e.target.value)}
              maxLength={30}
              placeholder="+351 912 345 678"
            />
          </Field>
          <Field label="Email" hint="Para alterar contacta o admin.">
            <input className={input} value={user.email} disabled />
          </Field>
          <Field label="Funções" hint="Atribuídas pelo admin em Utilizadores.">
            <input className={input} value={user.roles.join(', ') || '—'} disabled />
          </Field>
        </div>

        <div className="flex justify-end">
          <Button onClick={() => save.mutate()} disabled={!dirty} loading={save.isPending}>
            Guardar alterações
          </Button>
        </div>
      </section>

      <section className="space-y-3 rounded-xl border border-zinc-200 bg-white p-5 dark:border-zinc-800 dark:bg-zinc-900">
        <button
          type="button"
          onClick={() => setPwOpen((v) => !v)}
          className="flex w-full items-center justify-between text-left"
        >
          <span className="flex items-center gap-2 text-sm font-semibold">
            <Lock size={16} /> Alterar palavra-passe
          </span>
          <span className="text-xs text-zinc-400">{pwOpen ? 'fechar' : 'abrir'}</span>
        </button>
        {pwOpen && (
          <form
            className="space-y-3 border-t border-zinc-100 pt-3 dark:border-zinc-800"
            onSubmit={(e) => {
              e.preventDefault();
              pwMut.mutate();
            }}
          >
            <Field label="Palavra-passe actual">
              <input
                className={input}
                type="password"
                autoComplete="current-password"
                value={currentPw}
                onChange={(e) => setCurrentPw(e.target.value)}
                required
              />
            </Field>
            <Field label="Nova palavra-passe" hint="Mínimo 8 caracteres.">
              <input
                className={input}
                type="password"
                autoComplete="new-password"
                value={newPw}
                onChange={(e) => setNewPw(e.target.value)}
                required
                minLength={8}
              />
            </Field>
            <p className="text-xs text-zinc-500">
              Vai ser pedido login novamente a seguir — todas as sessões activas são revogadas.
            </p>
            <div className="flex justify-end">
              <Button type="submit" loading={pwMut.isPending} disabled={!currentPw || newPw.length < 8}>
                Alterar
              </Button>
            </div>
          </form>
        )}
      </section>
    </div>
  );
}

function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium text-zinc-600 dark:text-zinc-300">{label}</span>
      {children}
      {hint && <span className="mt-1 block text-[11px] text-zinc-400">{hint}</span>}
    </label>
  );
}
