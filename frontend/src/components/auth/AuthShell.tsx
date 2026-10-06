import type { InputHTMLAttributes, ReactNode } from 'react';
import { Wrench } from 'lucide-react';

/** Moldura comum das páginas públicas de autenticação (login, recuperar e repor palavra-passe). */
export function AuthShell({ children, footer }: { children: ReactNode; footer?: ReactNode }) {
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

        <div className="space-y-4 rounded-2xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
          {children}
        </div>

        {footer && <div className="mt-6 text-center text-[11px] text-zinc-400">{footer}</div>}
      </div>
    </div>
  );
}

export function AuthHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <header className="space-y-1">
      <h1 className="text-xl font-semibold tracking-tight">{title}</h1>
      {subtitle && <p className="text-xs text-zinc-500">{subtitle}</p>}
    </header>
  );
}

interface AuthFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  id: string;
  label: string;
  trailing?: ReactNode;
}

export function AuthField({ id, label, trailing, className, ...input }: AuthFieldProps) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="text-xs font-medium text-zinc-600 dark:text-zinc-400">
        {label}
      </label>
      <div className="relative">
        <input
          id={id}
          {...input}
          className={`w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none transition focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950 ${trailing ? 'pr-10' : ''} ${className ?? ''}`}
        />
        {trailing}
      </div>
    </div>
  );
}

export function AuthAlert({ tone = 'error', children }: { tone?: 'error' | 'success'; children: ReactNode }) {
  const styles =
    tone === 'error'
      ? 'border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300'
      : 'border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300';
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} className={`rounded-lg border px-3 py-2 text-sm ${styles}`}>
      {children}
    </div>
  );
}

/** Regras do backend (Identity): 8+ caracteres, maiúscula, minúscula e número. */
export function passwordProblem(password: string): string | null {
  if (password.length < 8) return 'Usa pelo menos 8 caracteres.';
  if (!/[a-z]/.test(password) || !/[A-Z]/.test(password)) return 'Usa letras maiúsculas e minúsculas.';
  if (!/\d/.test(password)) return 'Inclui pelo menos um número.';
  return null;
}
