import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Eye, Loader2, MessageCircle, RotateCcw, ShoppingCart, Wrench } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { SkeletonCard } from '../../components/ui';
import { toast } from '../../lib/toast';
import { tenantPreferencesApi } from '../../lib/tenantPreferences/api';
import type {
  CommunicationPrefs,
  EmitirFaturaMode,
  EntregarMarcaPagoMode,
  GarantiaAutoMode,
  PortalPrefs,
  RepairsPrefs,
  SalesPrefs,
  TenantPreferencesRoot,
  WhatsAppRepeatMode,
} from '../../lib/tenantPreferences/types';
import { CONDICAO_ARTIGO_LABEL, type CondicaoArtigo } from '../../lib/vendas/types';

type TabKey = 'communication' | 'portal' | 'repairs' | 'sales';
type SaveState = 'idle' | 'dirty' | 'saving' | 'saved' | 'error';

const tabs: Array<{ key: TabKey; label: string; icon: typeof MessageCircle }> = [
  { key: 'communication', label: 'Comunicação', icon: MessageCircle },
  { key: 'portal', label: 'Portal Cliente', icon: Eye },
  { key: 'repairs', label: 'Reparações', icon: Wrench },
  { key: 'sales', label: 'Vendas', icon: ShoppingCart },
];

const repairStates = ['Recebido', 'Diagnostico', 'AguardaPeca', 'EmReparacao', 'Pronto', 'Entregue', 'Cancelado', 'Orcamento'];

const repeatLabels: Record<WhatsAppRepeatMode, string> = {
  0: 'Sempre',
  1: 'Uma vez por reparação',
  2: 'Marcar manualmente',
};

const yesAskNoLabels: Record<GarantiaAutoMode | EntregarMarcaPagoMode, string> = {
  0: 'Sim',
  1: 'Perguntar',
  2: 'Não',
};

const emitirFaturaLabels: Record<EmitirFaturaMode, string> = {
  0: 'Nunca',
  1: 'Perguntar',
  2: 'Automático',
};

const paymentNames = ['Dinheiro', 'MBWay', 'Multibanco', 'TransferenciaBancaria', 'Cartao', 'Outro'];

export default function Preferencias() {
  const qc = useQueryClient();
  const [active, setActive] = useState<TabKey>('communication');
  const [draft, setDraft] = useState<TenantPreferencesRoot | null>(null);
  const [saveState, setSaveState] = useState<SaveState>('idle');
  const [savedFlash, setSavedFlash] = useState(false);
  const serverJson = useRef<string>('');

  const query = useQuery({
    queryKey: ['tenant-preferences'],
    queryFn: () => tenantPreferencesApi.get(),
    staleTime: 60_000,
  });

  useEffect(() => {
    if (!query.data) return;
    const json = JSON.stringify(query.data);
    serverJson.current = json;
    setDraft(query.data);
    setSaveState('idle');
  }, [query.data]);

  const save = useMutation({
    mutationFn: (payload: TenantPreferencesRoot) => tenantPreferencesApi.update(payload),
    onMutate: () => setSaveState('saving'),
    onSuccess: (data) => {
      const json = JSON.stringify(data);
      serverJson.current = json;
      setDraft(data);
      setSaveState('saved');
      setSavedFlash(true);
      qc.setQueryData(['tenant-preferences'], data);
      qc.invalidateQueries({ queryKey: ['tenant-preferences'] });
      window.setTimeout(() => setSavedFlash(false), 2000);
      window.setTimeout(() => setSaveState((s) => (s === 'saved' ? 'idle' : s)), 2200);
    },
    onError: (err) => {
      setSaveState('error');
      toast.fromError(err, 'Não foi possível guardar as preferências.');
    },
  });

  const reset = useMutation({
    mutationFn: (group: TabKey) => tenantPreferencesApi.resetGroup(group),
    onSuccess: (data) => {
      const json = JSON.stringify(data);
      serverJson.current = json;
      setDraft(data);
      setSaveState('saved');
      setSavedFlash(true);
      qc.setQueryData(['tenant-preferences'], data);
      toast.success('Defaults Mender restaurados', 'Aplicado a partir de agora.');
      window.setTimeout(() => setSavedFlash(false), 2000);
    },
    onError: (err) => toast.fromError(err, 'Não foi possível restaurar defaults.'),
  });

  useEffect(() => {
    if (!draft) return;
    const json = JSON.stringify(draft);
    if (json === serverJson.current) return;
    setSaveState('dirty');
    const handle = window.setTimeout(() => save.mutate(draft), 900);
    return () => window.clearTimeout(handle);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft]);

  const sortedTemplates = useMemo(() => {
    if (!draft) return [];
    return Object.entries(draft.communication.templatesByState)
      .sort((a, b) => a[1].order - b[1].order);
  }, [draft]);

  if (query.isLoading || !draft) {
    return (
      <div className="space-y-4">
        <SkeletonCard />
        <SkeletonCard />
        <SkeletonCard />
      </div>
    );
  }

  function patchCommunication(patch: Partial<CommunicationPrefs>) {
    setDraft((d) => d ? { ...d, communication: { ...d.communication, ...patch } } : d);
  }

  function patchPortal(patch: Partial<PortalPrefs>) {
    setDraft((d) => d ? { ...d, portal: { ...d.portal, ...patch } } : d);
  }

  function patchRepairs(patch: Partial<RepairsPrefs>) {
    setDraft((d) => d ? { ...d, repairs: { ...d.repairs, ...patch } } : d);
  }

  function patchSales(patch: Partial<SalesPrefs>) {
    setDraft((d) => d ? { ...d, sales: { ...d.sales, ...patch } } : d);
  }

  function patchTemplate(key: string, patch: { enabled?: boolean; texto?: string }) {
    setDraft((d) => {
      if (!d) return d;
      const current = d.communication.templatesByState[key];
      if (!current) return d;
      return {
        ...d,
        communication: {
          ...d.communication,
          templatesByState: {
            ...d.communication.templatesByState,
            [key]: { ...current, ...patch },
          },
        },
      };
    });
  }

  function togglePushEstado(estado: string) {
    if (!draft) return;
    const current = draft.communication.push.estadosPermitidos;
    const next = current.includes(estado)
      ? current.filter((x) => x !== estado)
      : [...current, estado];
    patchCommunication({ push: { ...draft.communication.push, estadosPermitidos: next } });
  }

  function restoreActiveGroup() {
    const tab = tabs.find((t) => t.key === active);
    if (!confirm(`Restaurar defaults Mender para ${tab?.label ?? 'este grupo'}?`)) return;
    reset.mutate(active);
  }

  const ActiveIcon = tabs.find((t) => t.key === active)?.icon ?? MessageCircle;

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Preferências da loja</h1>
          <p className="text-sm text-zinc-500">Aplicado a partir de agora.</p>
        </div>
        <div className="flex items-center gap-2">
          <SaveIndicator state={saveState} flash={savedFlash} />
          <Button
            type="button"
            variant="secondary"
            onClick={restoreActiveGroup}
            loading={reset.isPending}
            leftIcon={<RotateCcw size={15} strokeWidth={2} />}
          >
            Restaurar defaults Mender
          </Button>
        </div>
      </div>

      <div className="flex gap-2 overflow-x-auto border-b border-zinc-200 pb-2 dark:border-zinc-800">
        {tabs.map((tab) => {
          const Icon = tab.icon;
          const selected = active === tab.key;
          return (
            <button
              key={tab.key}
              type="button"
              onClick={() => setActive(tab.key)}
              className={`inline-flex min-h-11 shrink-0 items-center gap-2 rounded-lg px-3 text-sm font-medium transition ${
                selected
                  ? 'bg-zinc-900 text-white dark:bg-zinc-100 dark:text-zinc-950'
                  : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-900'
              }`}
            >
              <Icon size={16} strokeWidth={1.8} />
              {tab.label}
            </button>
          );
        })}
      </div>

      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mb-4 flex items-center gap-2 text-sm font-semibold">
          <ActiveIcon size={17} strokeWidth={2} />
          {tabs.find((t) => t.key === active)?.label}
        </div>

        {active === 'communication' && (
          <div className="space-y-5">
            <SettingRow label="WhatsApp manual" hint="Controla o botão wa.me e os templates por estado.">
              <Toggle checked={draft.communication.whatsAppEnabled} onChange={(v) => patchCommunication({ whatsAppEnabled: v })} />
            </SettingRow>
            <div className="grid gap-3 md:grid-cols-2">
              <Field label="Repetição WhatsApp">
                <select
                  value={draft.communication.repeatMode}
                  onChange={(e) => patchCommunication({ repeatMode: Number(e.target.value) as WhatsAppRepeatMode })}
                  className={inputCls}
                >
                  {Object.entries(repeatLabels).map(([value, label]) => (
                    <option key={value} value={value}>{label}</option>
                  ))}
                </select>
              </Field>
              <Field label="Dias para lembrete de levantamento">
                <input
                  type="number"
                  min={1}
                  max={90}
                  value={draft.communication.staleDaysThreshold}
                  onChange={(e) => patchCommunication({ staleDaysThreshold: Number(e.target.value) || 7 })}
                  className={inputCls}
                />
              </Field>
            </div>
            <SettingRow label="Push notifications" hint="Envia actualizações para clientes que subscreveram o portal.">
              <Toggle
                checked={draft.communication.push.enabled}
                onChange={(v) => patchCommunication({ push: { ...draft.communication.push, enabled: v } })}
              />
            </SettingRow>
            <div className="flex flex-wrap gap-2">
              {repairStates.map((estado) => (
                <button
                  key={estado}
                  type="button"
                  onClick={() => togglePushEstado(estado)}
                  className={`min-h-10 rounded-full border px-3 text-xs font-medium ${
                    draft.communication.push.estadosPermitidos.includes(estado)
                      ? 'border-brand-500 bg-brand-50 text-brand-700 dark:bg-brand-950/40 dark:text-brand-300'
                      : 'border-zinc-200 text-zinc-500 dark:border-zinc-800'
                  }`}
                >
                  {estado}
                </button>
              ))}
            </div>
            <div className="space-y-3">
              {sortedTemplates.map(([key, template]) => (
                <div key={key} className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
                  <div className="mb-2 flex items-center justify-between gap-2">
                    <div className="text-sm font-medium">{key}</div>
                    <Toggle checked={template.enabled} onChange={(v) => patchTemplate(key, { enabled: v })} />
                  </div>
                  <textarea
                    value={template.texto}
                    onChange={(e) => patchTemplate(key, { texto: e.target.value })}
                    rows={3}
                    className={`${inputCls} min-h-24 resize-y leading-relaxed`}
                  />
                </div>
              ))}
            </div>
          </div>
        )}

        {active === 'portal' && (
          <div className="space-y-3">
            <ToggleRow label="Mostrar fotos" checked={draft.portal.mostrarFotos} onChange={(v) => patchPortal({ mostrarFotos: v })} />
            <ToggleRow label="Mostrar diagnóstico" checked={draft.portal.mostrarDiagnostico} onChange={(v) => patchPortal({ mostrarDiagnostico: v })} />
            <ToggleRow label="Mostrar orçamento" checked={draft.portal.mostrarOrcamento} onChange={(v) => patchPortal({ mostrarOrcamento: v })} />
            <ToggleRow label="Mostrar garantia" checked={draft.portal.mostrarGarantia} onChange={(v) => patchPortal({ mostrarGarantia: v })} />
            <ToggleRow label="Mostrar timeline" checked={draft.portal.mostrarTimeline} onChange={(v) => patchPortal({ mostrarTimeline: v })} />
            <ToggleRow label="Mostrar avaliação" checked={draft.portal.mostrarAvaliacao} onChange={(v) => patchPortal({ mostrarAvaliacao: v })} />
            <ToggleRow label="Permitir aprovar orçamento online" checked={draft.portal.permitirAprovarOrcamento} onChange={(v) => patchPortal({ permitirAprovarOrcamento: v })} />
            <div className="grid gap-3 md:grid-cols-2">
              <Field label="Score mínimo Google Review">
                <input
                  type="number"
                  min={1}
                  max={5}
                  value={draft.portal.googleReviewMinScore}
                  onChange={(e) => patchPortal({ googleReviewMinScore: Number(e.target.value) || 4 })}
                  className={inputCls}
                />
              </Field>
              <Field label="URL Google Review">
                <input
                  value={draft.portal.googleReviewUrl ?? ''}
                  onChange={(e) => patchPortal({ googleReviewUrl: e.target.value.trim() || null })}
                  placeholder="https://g.page/r/..."
                  className={inputCls}
                />
              </Field>
            </div>
          </div>
        )}

        {active === 'repairs' && (
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Ao entregar, marcar como pago">
              <select
                value={draft.repairs.entregarMarcaPago}
                onChange={(e) => patchRepairs({ entregarMarcaPago: Number(e.target.value) as EntregarMarcaPagoMode })}
                className={inputCls}
              >
                {Object.entries(yesAskNoLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </select>
            </Field>
            <Field label="Garantia automática em reparações">
              <select
                value={draft.repairs.garantiaAutomatica}
                onChange={(e) => patchRepairs({ garantiaAutomatica: Number(e.target.value) as GarantiaAutoMode })}
                className={inputCls}
              >
                {Object.entries(yesAskNoLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </select>
            </Field>
          </div>
        )}

        {active === 'sales' && (
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Método de pagamento default">
              <select
                value={draft.sales.defaultMetodoPagamento}
                onChange={(e) => patchSales({ defaultMetodoPagamento: e.target.value })}
                className={inputCls}
              >
                {paymentNames.map((name) => <option key={name} value={name}>{name}</option>)}
              </select>
            </Field>
            <Field label="Condição default do artigo">
              <select
                value={draft.sales.defaultCondicaoArtigo}
                onChange={(e) => patchSales({ defaultCondicaoArtigo: Number(e.target.value) })}
                className={inputCls}
              >
                {Object.entries(CONDICAO_ARTIGO_LABEL).map(([value, label]) => (
                  <option key={value} value={value}>{label || CONDICAO_ARTIGO_LABEL[Number(value) as CondicaoArtigo]}</option>
                ))}
              </select>
            </Field>
            <Field label="Emitir fatura em venda POS">
              <select
                value={draft.sales.emitirFatura}
                onChange={(e) => patchSales({ emitirFatura: Number(e.target.value) as EmitirFaturaMode })}
                className={inputCls}
              >
                {Object.entries(emitirFaturaLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </select>
            </Field>
            <Field label="Garantia automática em vendas">
              <select
                value={draft.sales.vendaGarantia}
                onChange={(e) => patchSales({ vendaGarantia: Number(e.target.value) as GarantiaAutoMode })}
                className={inputCls}
              >
                {Object.entries(yesAskNoLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </select>
            </Field>
          </div>
        )}
      </section>
    </div>
  );
}

function SaveIndicator({ state, flash }: { state: SaveState; flash: boolean }) {
  if (state === 'saving') {
    return <span className="inline-flex min-h-10 items-center gap-1.5 rounded-md px-2 text-xs text-zinc-500"><Loader2 size={14} className="animate-spin" /> A guardar</span>;
  }
  if (state === 'saved' || flash) {
    return <span className="inline-flex min-h-10 items-center gap-1.5 rounded-md px-2 text-xs text-emerald-600"><CheckCircle2 size={14} /> Guardado</span>;
  }
  if (state === 'dirty') {
    return <span className="inline-flex min-h-10 items-center rounded-md px-2 text-xs text-amber-600">Alterações pendentes</span>;
  }
  if (state === 'error') {
    return <span className="inline-flex min-h-10 items-center rounded-md px-2 text-xs text-red-600">Erro ao guardar</span>;
  }
  return <span className="inline-flex min-h-10 items-center rounded-md px-2 text-xs text-zinc-400">Aplicado a partir de agora</span>;
}

function ToggleRow({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <SettingRow label={label}>
      <Toggle checked={checked} onChange={onChange} />
    </SettingRow>
  );
}

function SettingRow({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className="flex min-h-14 items-center justify-between gap-3 rounded-lg border border-zinc-200 px-3 py-2 dark:border-zinc-800">
      <div className="min-w-0">
        <div className="text-sm font-medium text-zinc-900 dark:text-zinc-100">{label}</div>
        {hint && <div className="mt-0.5 text-xs text-zinc-500">{hint}</div>}
      </div>
      {children}
    </div>
  );
}

function Toggle({ checked, onChange }: { checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className={`relative h-8 w-14 rounded-full transition focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 ${
        checked ? 'bg-brand-600' : 'bg-zinc-300 dark:bg-zinc-700'
      }`}
    >
      <span
        className={`absolute top-1 h-6 w-6 rounded-full bg-white shadow-sm transition ${
          checked ? 'left-7' : 'left-1'
        }`}
      />
    </button>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block space-y-1">
      <span className="text-xs font-medium uppercase tracking-wide text-zinc-500">{label}</span>
      {children}
    </label>
  );
}

const inputCls = 'min-h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950';
