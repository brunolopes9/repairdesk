import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import { Settings, ShieldCheck, Star } from 'lucide-react';
import { EmptyState, PageHeader, SkeletonCard } from '../../components/ui';
import { tenantSettingsApi } from '../../lib/tenantSettings/api';
import {
  REGIME_FISCAL_LABELS,
  type RegimeFiscal,
  type TenantSettings,
  type UpdateTenantSettings,
} from '../../lib/tenantSettings/types';

type SaveState = 'idle' | 'dirty' | 'saving' | 'saved' | 'error';

const SECTIONS = [
  { id: 'empresa', label: 'Empresa' },
  { id: 'fiscal', label: 'Fiscal' },
  { id: 'pagamentos', label: 'Pagamentos' },
  { id: 'posvenda', label: 'Pós-venda' },
  { id: 'aparencia', label: 'Aparência' },
] as const;

type SectionId = (typeof SECTIONS)[number]['id'];

export default function Definicoes() {
  const qc = useQueryClient();
  const { data, isLoading, error: loadError } = useQuery({
    queryKey: ['tenant-settings'],
    queryFn: () => tenantSettingsApi.getMine(),
  });

  const [section, setSection] = useState<SectionId>('empresa');
  const [form, setForm] = useState<UpdateTenantSettings | null>(null);
  const [saveState, setSaveState] = useState<SaveState>('idle');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const debounceRef = useRef<number | null>(null);
  const initialisedRef = useRef(false);

  useEffect(() => {
    if (data && !initialisedRef.current) {
      setForm(toForm(data));
      initialisedRef.current = true;
    }
  }, [data]);

  const mutation = useMutation({
    mutationFn: (payload: UpdateTenantSettings) => tenantSettingsApi.updateMine(payload),
    onMutate: () => setSaveState('saving'),
    onSuccess: (saved) => {
      qc.setQueryData(['tenant-settings'], saved);
      setSaveState('saved');
      setErrorMsg(null);
      window.setTimeout(() => setSaveState((s) => (s === 'saved' ? 'idle' : s)), 1500);
    },
    onError: (err) => {
      setSaveState('error');
      if (isAxiosError(err)) {
        setErrorMsg(err.response?.data?.title ?? err.message);
      } else {
        setErrorMsg('Não foi possível guardar.');
      }
    },
  });

  function update<K extends keyof UpdateTenantSettings>(key: K, value: UpdateTenantSettings[K]) {
    if (!form) return;
    const next = { ...form, [key]: value };
    setForm(next);
    setSaveState('dirty');
    if (debounceRef.current) window.clearTimeout(debounceRef.current);
    debounceRef.current = window.setTimeout(() => {
      mutation.mutate(next);
    }, 1200);
  }

  const indicator = useMemo(() => {
    switch (saveState) {
      case 'saving':
        return <span className="text-xs text-zinc-500">A guardar…</span>;
      case 'saved':
        return <span className="text-xs text-emerald-600 dark:text-emerald-400">Guardado</span>;
      case 'dirty':
        return <span className="text-xs text-amber-600 dark:text-amber-400">Alterado</span>;
      case 'error':
        return <span className="text-xs text-rose-600 dark:text-rose-400">Erro</span>;
      default:
        return null;
    }
  }, [saveState]);

  if (isLoading) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Definicoes"
          description="Dados da empresa usados em orcamentos, faturas e portal cliente."
        />
        <div className="space-y-3">
          <SkeletonCard />
          <SkeletonCard />
        </div>
      </div>
    );
  }
  if (loadError || !data || !form) {
    return (
      <EmptyState
        icon={Settings}
        title="Nao foi possivel carregar as definicoes"
        description="Tenta novamente dentro de instantes. Se persistir, confirma a ligacao ao servidor."
      />
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Definicoes"
        description="Dados da empresa usados em orcamentos, faturas e portal cliente."
        meta={indicator}
      />

      {errorMsg && (
        <div className="rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700 dark:border-rose-900/60 dark:bg-rose-950/30 dark:text-rose-300">
          {errorMsg}
        </div>
      )}

      {/* Tabs */}
      <nav className="flex gap-1 border-b border-zinc-200 dark:border-zinc-800" aria-label="Secções">
        {SECTIONS.map((s) => (
          <button
            key={s.id}
            type="button"
            onClick={() => setSection(s.id)}
            className={`relative -mb-px px-3 py-2 text-sm transition focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 ${
              section === s.id
                ? 'border-b-2 border-brand-500 text-brand-600 dark:text-brand-400'
                : 'border-b-2 border-transparent text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300'
            }`}
          >
            {s.label}
          </button>
        ))}
      </nav>

      <div className="rounded-2xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
        {section === 'empresa' && <EmpresaSection form={form} update={update} />}
        {section === 'fiscal' && <FiscalSection form={form} update={update} />}
        {section === 'pagamentos' && <PagamentosSection form={form} update={update} />}
        {section === 'posvenda' && <PosVendaSection form={form} update={update} />}
        {section === 'aparencia' && <AparenciaSection form={form} update={update} />}
      </div>
    </div>
  );
}

function EmpresaSection({
  form,
  update,
}: {
  form: UpdateTenantSettings;
  update: <K extends keyof UpdateTenantSettings>(key: K, value: UpdateTenantSettings[K]) => void;
}) {
  return (
    <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
      <Field label="Nome comercial" required>
        <input
          type="text"
          value={form.name ?? ''}
          onChange={(e) => update('name', e.target.value)}
          className={inputCls}
          placeholder="LopesTech"
        />
      </Field>
      <Field label="Nome legal (denominação social)">
        <input
          type="text"
          value={form.legalName ?? ''}
          onChange={(e) => update('legalName', e.target.value || null)}
          className={inputCls}
          placeholder="Bruno Miguel Martins Lopes"
        />
      </Field>
      <Field label="NIF">
        <input
          type="text"
          value={form.nif ?? ''}
          onChange={(e) => update('nif', e.target.value || null)}
          className={inputCls}
          inputMode="numeric"
          placeholder="263758141"
        />
      </Field>
      <Field label="Telefone">
        <input
          type="tel"
          value={form.phone ?? ''}
          onChange={(e) => update('phone', e.target.value || null)}
          className={inputCls}
          placeholder="+351 …"
        />
      </Field>
      <Field label="Email">
        <input
          type="email"
          value={form.email ?? ''}
          onChange={(e) => update('email', e.target.value || null)}
          className={inputCls}
        />
      </Field>
      <Field label="Website">
        <input
          type="url"
          value={form.website ?? ''}
          onChange={(e) => update('website', e.target.value || null)}
          className={inputCls}
          placeholder="https://lopestech.pt"
        />
      </Field>
      <Field label="Morada" className="sm:col-span-2">
        <input
          type="text"
          value={form.address ?? ''}
          onChange={(e) => update('address', e.target.value || null)}
          className={inputCls}
        />
      </Field>
      <Field label="Código postal">
        <input
          type="text"
          value={form.postalCode ?? ''}
          onChange={(e) => update('postalCode', e.target.value || null)}
          className={inputCls}
          placeholder="3500-001"
        />
      </Field>
      <Field label="Localidade">
        <input
          type="text"
          value={form.locality ?? ''}
          onChange={(e) => update('locality', e.target.value || null)}
          className={inputCls}
          placeholder="Viseu"
        />
      </Field>
    </div>
  );
}

function FiscalSection({
  form,
  update,
}: {
  form: UpdateTenantSettings;
  update: <K extends keyof UpdateTenantSettings>(key: K, value: UpdateTenantSettings[K]) => void;
}) {
  return (
    <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
      <Field label="Regime fiscal">
        <select
          value={form.regimeFiscal}
          onChange={(e) => update('regimeFiscal', Number(e.target.value) as RegimeFiscal)}
          className={inputCls}
        >
          {Object.entries(REGIME_FISCAL_LABELS).map(([v, label]) => (
            <option key={v} value={v}>{label}</option>
          ))}
        </select>
      </Field>
      <Field label="CAE principal">
        <input
          type="text"
          value={form.caePrincipal ?? ''}
          onChange={(e) => update('caePrincipal', e.target.value || null)}
          className={inputCls}
          placeholder="62100"
        />
      </Field>
      <Field label="CAE secundários (separados por vírgula)" className="sm:col-span-2">
        <input
          type="text"
          value={form.caeSecundarios ?? ''}
          onChange={(e) => update('caeSecundarios', e.target.value || null)}
          className={inputCls}
          placeholder="47401, 58290, 95101, 95102"
        />
      </Field>
      <Field label="Termos e condições (rodapé de orçamentos)" className="sm:col-span-2">
        <textarea
          value={form.termosCondicoes ?? ''}
          onChange={(e) => update('termosCondicoes', e.target.value || null)}
          className={`${inputCls} min-h-[120px] resize-y`}
          placeholder="Garantia de 90 dias sobre o serviço prestado…"
        />
      </Field>
    </div>
  );
}

function PagamentosSection({
  form,
  update,
}: {
  form: UpdateTenantSettings;
  update: <K extends keyof UpdateTenantSettings>(key: K, value: UpdateTenantSettings[K]) => void;
}) {
  return (
    <div className="grid grid-cols-1 gap-5">
      <Field label="IBAN" hint="Aparece no rodapé do PDF de orçamento.">
        <input
          type="text"
          value={form.iban ?? ''}
          onChange={(e) => update('iban', e.target.value || null)}
          className={`${inputCls} font-mono`}
          placeholder="PT50 0000 0000 0000 0000 0000 0"
        />
      </Field>
    </div>
  );
}

function PosVendaSection({
  form,
  update,
}: {
  form: UpdateTenantSettings;
  update: <K extends keyof UpdateTenantSettings>(key: K, value: UpdateTenantSettings[K]) => void;
}) {
  return (
    <div className="grid grid-cols-1 gap-5">
      <div className="flex items-start gap-2 rounded-lg border border-emerald-200 bg-emerald-50/50 p-3 text-xs text-emerald-900 dark:border-emerald-900/40 dark:bg-emerald-950/30 dark:text-emerald-200">
        <ShieldCheck size={15} strokeWidth={2} className="mt-0.5 flex-none text-emerald-600 dark:text-emerald-400" />
        <span>A <strong>garantia digital</strong> é emitida automaticamente quando marcas uma reparação como <em>Entregue</em>. O cliente recebe um QR/link <code>/g/&#123;código&#125;</code> permanente para verificar a validade.</span>
      </div>

      <Field label="Dias de garantia (default)" hint="Período aplicado por defeito a novas reparações. Podes editar individualmente.">
        <input
          type="number"
          min={1}
          max={3650}
          value={form.garantiaDiasDefault ?? 90}
          onChange={(e) => update('garantiaDiasDefault', Math.max(1, Number(e.target.value) || 0))}
          className={inputCls}
        />
      </Field>

      <Field label="Cobertura (o que a garantia cobre)" hint="Aparece na página pública de verificação de garantia. Sê claro e simples.">
        <textarea
          rows={4}
          value={form.garantiaCoberturaDefault ?? ''}
          onChange={(e) => update('garantiaCoberturaDefault', e.target.value || null)}
          placeholder="Ex: A garantia cobre defeitos de fabrico das peças substituídas e o serviço prestado."
          className={`${inputCls} resize-none`}
        />
      </Field>

      <Field label="Exclusões (o que a garantia NÃO cobre)">
        <textarea
          rows={4}
          value={form.garantiaExclusoesDefault ?? ''}
          onChange={(e) => update('garantiaExclusoesDefault', e.target.value || null)}
          placeholder="Ex: Não cobre danos por queda, contacto com líquidos, abertura por terceiros ou uso indevido."
          className={`${inputCls} resize-none`}
        />
      </Field>

      <div className="border-t border-zinc-200 pt-4 dark:border-zinc-800">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <Star size={15} strokeWidth={2} className="text-amber-500" />
          Avaliações & Google Reviews
        </h3>
        <p className="mt-1 text-xs text-zinc-500">
          Quando o cliente avalia 4-5 estrelas no portal público, é redirecionado para deixar review no Google.
          Avaliações 1-3 estrelas ficam internas (sem redireccionamento — evita reviews negativas públicas e tu vês o sinal).
        </p>
      </div>

      <Field
        label="URL do Google Reviews da tua loja"
        hint="Encontra o link em Google Business Profile → Pedir reviews. Se vazio, nenhum redireccionamento."
      >
        <input
          type="url"
          value={form.googleReviewUrl ?? ''}
          onChange={(e) => update('googleReviewUrl', e.target.value || null)}
          placeholder="https://g.page/r/.../review"
          className={inputCls}
        />
      </Field>
    </div>
  );
}

function AparenciaSection({
  form,
  update,
}: {
  form: UpdateTenantSettings;
  update: <K extends keyof UpdateTenantSettings>(key: K, value: UpdateTenantSettings[K]) => void;
}) {
  return (
    <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
      <Field label="Logo (URL)" hint="Upload de ficheiro virá em breve. Por agora cola um URL público." className="sm:col-span-2">
        <input
          type="url"
          value={form.logoUrl ?? ''}
          onChange={(e) => update('logoUrl', e.target.value || null)}
          className={inputCls}
          placeholder="https://…/logo.png"
        />
      </Field>
      {form.logoUrl && (
        <div className="sm:col-span-2">
          <p className="mb-2 text-xs text-zinc-500">Pré-visualização:</p>
          <div className="inline-flex items-center justify-center rounded-lg border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-950">
            <img src={form.logoUrl} alt="Logo" className="max-h-16" onError={(e) => { (e.target as HTMLImageElement).style.opacity = '0.3'; }} />
          </div>
        </div>
      )}
      <Field label="Cor principal (hex)">
        <input
          type="text"
          value={form.primaryColor ?? ''}
          onChange={(e) => update('primaryColor', e.target.value || null)}
          className={`${inputCls} font-mono`}
          placeholder="#0EA5E9"
        />
      </Field>
    </div>
  );
}

function Field({
  label,
  required,
  hint,
  className,
  children,
}: {
  label: string;
  required?: boolean;
  hint?: string;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <label className={`block ${className ?? ''}`}>
      <span className="mb-1 block text-xs font-medium text-zinc-600 dark:text-zinc-400">
        {label}
        {required && <span className="ml-1 text-rose-500">*</span>}
      </span>
      {children}
      {hint && <span className="mt-1 block text-[11px] text-zinc-500">{hint}</span>}
    </label>
  );
}

const inputCls =
  'block w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-100 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-800 dark:bg-zinc-950 dark:focus:ring-brand-900/40';

function toForm(t: TenantSettings): UpdateTenantSettings {
  const { id: _id, ...rest } = t;
  return rest;
}
