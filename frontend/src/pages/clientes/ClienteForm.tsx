import { useEffect, useState, type FormEvent } from 'react';
import { isAxiosError } from 'axios';
import { AlertTriangle, CheckCircle2 } from 'lucide-react';
import type { Cliente, ClienteForm } from '../../lib/clientes/types';
import { validateNif } from '../../lib/nif/validator';
import { formatPhonePT } from '../../lib/phone/formatter';

interface Props {
  initial?: Cliente | null;
  onSubmit: (form: ClienteForm) => Promise<void>;
  onCancel: () => void;
  submitting: boolean;
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export default function ClienteFormView({ initial, onSubmit, onCancel, submitting }: Props) {
  const [nome, setNome] = useState('');
  const [telefone, setTelefone] = useState('');
  const [email, setEmail] = useState('');
  const [nif, setNif] = useState('');
  const [notas, setNotas] = useState('');
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [generic, setGeneric] = useState<string | null>(null);

  useEffect(() => {
    setNome(initial?.nome ?? '');
    setTelefone(initial?.telefone ?? '');
    setEmail(initial?.email ?? '');
    setNif(initial?.nif ?? '');
    setNotas(initial?.notas ?? '');
    setErrors({});
    setGeneric(null);
  }, [initial]);

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setErrors({});
    setGeneric(null);
    try {
      await onSubmit({
        nome: nome.trim(),
        telefone: telefone.trim(),
        email: email.trim() || null,
        nif: nif.trim() || null,
        notas: notas.trim() || null,
      });
    } catch (err) {
      if (isAxiosError(err)) {
        const data = err.response?.data as ProblemDetails | undefined;
        if (data?.errors) setErrors(data.errors);
        else setGeneric(data?.detail ?? data?.title ?? 'Erro ao guardar.');
      } else {
        setGeneric('Erro ao guardar.');
      }
    }
  }

  return (
    <form id="cliente-form" onSubmit={handleSubmit} className="space-y-3">
      {generic && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">
          {generic}
        </div>
      )}
      <Field label="Nome" required errors={errors.nome}>
        <input
          required
          value={nome}
          onChange={(e) => setNome(e.target.value)}
          className={inputCls}
          autoFocus
        />
      </Field>
      <Field label="Telefone (opcional — vazio para clientes via Messenger)" errors={errors.telefone}>
        <input
          inputMode="tel"
          value={telefone}
          onChange={(e) => setTelefone(e.target.value)}
          className={inputCls}
        />
        <PhoneFeedback phone={telefone} />
      </Field>
      <Field label="Email" errors={errors.email}>
        <input
          type="email"
          inputMode="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className={inputCls}
        />
      </Field>
      <Field label="NIF (9 dígitos)" errors={errors.nif}>
        <input
          inputMode="numeric"
          maxLength={9}
          value={nif}
          onChange={(e) => setNif(e.target.value.replace(/\D/g, ''))}
          className={inputCls}
        />
        <NifFeedback nif={nif} />
      </Field>
      <Field label="Notas" errors={errors.notas}>
        <textarea
          rows={3}
          value={notas}
          onChange={(e) => setNotas(e.target.value)}
          className={inputCls + ' resize-none'}
        />
      </Field>

      <div className="hidden">
        <button type="submit" disabled={submitting} />
        <button type="button" onClick={onCancel} />
      </div>
    </form>
  );
}

const inputCls =
  'w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950';

function Field({
  label,
  required,
  errors,
  children,
}: {
  label: string;
  required?: boolean;
  errors?: string[];
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1">
      <label className="text-xs font-medium uppercase tracking-wide text-zinc-500">
        {label} {required && <span className="text-red-500">*</span>}
      </label>
      {children}
      {errors && errors.length > 0 && (
        <p className="text-xs text-red-600 dark:text-red-400">{errors.join(' ')}</p>
      )}
    </div>
  );
}

function PhoneFeedback({ phone }: { phone: string }) {
  if (!phone) return null;
  const info = formatPhonePT(phone);
  if (info.kind === 'incomplete') {
    return (
      <p className="mt-1 text-xs text-zinc-500">
        {info.digits.length}/9 dígitos
      </p>
    );
  }
  if (info.isValid) {
    const kindLabel = info.kind === 'mobile' ? 'Telemóvel' : 'Fixo';
    return (
      <p className="mt-1 inline-flex items-center gap-1 text-xs text-emerald-700 dark:text-emerald-400">
        <CheckCircle2 size={12} strokeWidth={2} /> {kindLabel} · {info.display}
      </p>
    );
  }
  return (
    <p className="mt-1 inline-flex items-center gap-1 text-xs text-amber-700 dark:text-amber-400">
      <AlertTriangle size={12} strokeWidth={2} /> Formato não-reconhecido (esperado 9 dígitos PT)
    </p>
  );
}

function NifFeedback({ nif }: { nif: string }) {
  if (!nif) return null;
  const v = validateNif(nif);
  if (v.isValid) {
    return (
      <p className="mt-1 inline-flex items-center gap-1 text-xs text-emerald-700 dark:text-emerald-400">
        <CheckCircle2 size={12} strokeWidth={2} /> {v.message}
      </p>
    );
  }
  if (v.message) {
    return (
      <p className="mt-1 inline-flex items-center gap-1 text-xs text-amber-700 dark:text-amber-400">
        <AlertTriangle size={12} strokeWidth={2} /> {v.message}
      </p>
    );
  }
  return null;
}
