import { useEffect, useState, type FormEvent } from 'react';
import { isAxiosError } from 'axios';
import { AlertTriangle, Building2, CheckCircle2, Loader2 } from 'lucide-react';
import { clientesApi } from '../../lib/clientes/api';
import type { AtNifLookup, Cliente, ClienteForm } from '../../lib/clientes/types';
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

type AtLookupState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'found'; data: AtNifLookup }
  | { status: 'not_found' }
  | { status: 'rate_limited' }
  | { status: 'offline' }
  | { status: 'error' };

export default function ClienteFormView({ initial, onSubmit, onCancel, submitting }: Props) {
  const [nome, setNome] = useState('');
  const [telefone, setTelefone] = useState('');
  const [email, setEmail] = useState('');
  const [nif, setNif] = useState('');
  const [notas, setNotas] = useState('');
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [generic, setGeneric] = useState<string | null>(null);
  const [atLookup, setAtLookup] = useState<AtLookupState>({ status: 'idle' });

  useEffect(() => {
    setNome(initial?.nome ?? '');
    setTelefone(initial?.telefone ?? '');
    setEmail(initial?.email ?? '');
    setNif(initial?.nif ?? '');
    setNotas(initial?.notas ?? '');
    setErrors({});
    setGeneric(null);
    setAtLookup({ status: 'idle' });
  }, [initial]);

  useEffect(() => {
    const validation = validateNif(nif);
    if (!validation.isValid) {
      setAtLookup({ status: 'idle' });
      return;
    }

    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      setAtLookup({ status: 'loading' });
      clientesApi.lookupAtNif(nif, controller.signal)
        .then((data) => setAtLookup({ status: 'found', data }))
        .catch((err) => {
          if (controller.signal.aborted) return;
          if (isAxiosError(err)) {
            if (err.response?.status === 404) setAtLookup({ status: 'not_found' });
            else if (err.response?.status === 429) setAtLookup({ status: 'rate_limited' });
            else if (err.response?.status === 503) setAtLookup({ status: 'offline' });
            else setAtLookup({ status: 'error' });
          } else {
            setAtLookup({ status: 'error' });
          }
        });
    }, 250);

    return () => {
      controller.abort();
      window.clearTimeout(timer);
    };
  }, [nif]);

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
        <NifFeedback nif={nif} lookup={atLookup} currentNome={nome} onAcceptName={setNome} />
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
  'min-h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950';

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

function NifFeedback({
  nif,
  lookup,
  currentNome,
  onAcceptName,
}: {
  nif: string;
  lookup: AtLookupState;
  currentNome: string;
  onAcceptName: (nome: string) => void;
}) {
  if (!nif) return null;
  const v = validateNif(nif);
  if (v.isValid) {
    return (
      <div className="mt-1 space-y-2">
        <p className="inline-flex items-center gap-1 text-xs text-emerald-700 dark:text-emerald-400">
          <CheckCircle2 size={12} strokeWidth={2} /> {v.message}
        </p>
        <AtLookupFeedback lookup={lookup} currentNome={currentNome} onAcceptName={onAcceptName} />
      </div>
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

function AtLookupFeedback({
  lookup,
  currentNome,
  onAcceptName,
}: {
  lookup: AtLookupState;
  currentNome: string;
  onAcceptName: (nome: string) => void;
}) {
  if (lookup.status === 'idle') return null;
  if (lookup.status === 'loading') {
    return (
      <p className="inline-flex items-center gap-1 text-xs text-zinc-500">
        <Loader2 size={12} className="animate-spin" /> A verificar AT...
      </p>
    );
  }
  if (lookup.status === 'found') {
    const alreadyMatches = currentNome.trim().toLowerCase() === lookup.data.nome.trim().toLowerCase();
    return (
      <div className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-800 dark:border-emerald-900/70 dark:bg-emerald-950/30 dark:text-emerald-300">
        <div className="flex flex-wrap items-center gap-2">
          <Building2 size={13} />
          <span className="font-medium">{lookup.data.nome}</span>
          {!alreadyMatches && (
            <button
              type="button"
              onClick={() => onAcceptName(lookup.data.nome)}
              className="rounded border border-emerald-300 px-2 py-0.5 font-medium hover:bg-emerald-100 dark:border-emerald-800 dark:hover:bg-emerald-900/50"
            >
              Aceitar nome
            </button>
          )}
          {alreadyMatches && <span className="text-emerald-600 dark:text-emerald-400">Nome já aceite</span>}
        </div>
        {lookup.data.morada && <div className="mt-1 text-emerald-700/80 dark:text-emerald-300/80">{lookup.data.morada}</div>}
      </div>
    );
  }
  if (lookup.status === 'not_found') {
    return <p className="text-xs text-zinc-500">NIF válido localmente, sem confirmação na AT.</p>;
  }
  if (lookup.status === 'rate_limited') {
    return <p className="text-xs text-amber-700 dark:text-amber-400">Limite diário de consultas AT atingido. Podes guardar o cliente na mesma.</p>;
  }
  if (lookup.status === 'offline') {
    return <p className="text-xs text-amber-700 dark:text-amber-400">AT indisponível agora. NIF válido localmente, podes continuar.</p>;
  }
  return <p className="text-xs text-zinc-500">Não foi possível confirmar na AT agora.</p>;
}
