import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import { AlertTriangle, CheckCircle2, Cpu, Plus, Settings, ShieldCheck, Star, Trash2 } from 'lucide-react';
import { EmptyState, PageHeader, SkeletonCard } from '../../components/ui';
import { tenantSettingsApi } from '../../lib/tenantSettings/api';
import { validateNif } from '../../lib/nif/validator';
import { validateIban } from '../../lib/iban/validator';
import { backupApi, formatBytes } from '../../lib/admin/backup';
import { equipmentFieldTemplatesApi, toUpsert } from '../../lib/equipmentFields/api';
import {
  DEVICE_CATEGORY_LABEL,
  EQUIPMENT_FIELD_TYPE,
  EQUIPMENT_FIELD_TYPE_LABEL,
  type EquipmentFieldTemplate,
  type UpsertEquipmentFieldTemplate,
} from '../../lib/equipmentFields/types';
import { toast } from '../../lib/toast';
import {
  REGIME_FISCAL_LABELS,
  type RegimeFiscal,
  type TenantBillingSettings,
  type TenantSettings,
  type UpdateTenantBillingSettings,
  type UpdateTenantSettings,
} from '../../lib/tenantSettings/types';

type SaveState = 'idle' | 'dirty' | 'saving' | 'saved' | 'error';

const SECTIONS = [
  { id: 'empresa', label: 'Empresa' },
  { id: 'fiscal', label: 'Fiscal' },
  { id: 'faturacao', label: 'Faturação' },
  { id: 'pagamentos', label: 'Pagamentos' },
  { id: 'posvenda', label: 'Pós-venda' },
  { id: 'campos', label: 'Campos personalizados' },
  { id: 'aparencia', label: 'Aparência' },
  { id: 'backups', label: 'Backups' },
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
        {section === 'faturacao' && <FaturacaoSection />}
        {section === 'pagamentos' && <PagamentosSection form={form} update={update} />}
        {section === 'posvenda' && <PosVendaSection form={form} update={update} />}
        {section === 'campos' && <CamposPersonalizadosSection />}
        {section === 'aparencia' && <AparenciaSection form={form} update={update} />}
        {section === 'backups' && <BackupsSection />}
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
          maxLength={9}
        />
        <TenantNifFeedback nif={form.nif ?? ''} />
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

function FaturacaoSection() {
  const qc = useQueryClient();
  const billing = useQuery({
    queryKey: ['tenant-billing-settings'],
    queryFn: () => tenantSettingsApi.getBilling(),
  });
  const tenant = useQuery({
    queryKey: ['tenant-settings'],
    queryFn: () => tenantSettingsApi.getMine(),
  });

  const [form, setForm] = useState<UpdateTenantBillingSettings | null>(null);
  const [series, setSeries] = useState<{ id: number; name: string }[]>([]);
  const [showConnect, setShowConnect] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);

  useEffect(() => {
    if (billing.data) setForm(toBillingForm(billing.data));
  }, [billing.data]);

  const save = useMutation({
    mutationFn: (payload: UpdateTenantBillingSettings) => tenantSettingsApi.updateBilling(payload),
    onSuccess: (saved) => {
      qc.setQueryData(['tenant-billing-settings'], saved);
      setForm(toBillingForm(saved));
      toast.success('Guardado', 'Definições Moloni atualizadas.');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível guardar.'),
  });

  const test = useMutation({
    mutationFn: () => tenantSettingsApi.testBillingConnection(),
    onSuccess: (r) => toast.success('Ligação validada', r.message),
    onError: (err) => toast.fromError(err, 'A Moloni rejeitou a ligação.'),
  });

  const sync = useMutation({
    mutationFn: () => tenantSettingsApi.syncBillingSeries(),
    onSuccess: async (items) => {
      setSeries(items.map((s) => ({ id: s.id, name: s.name })));
      await qc.invalidateQueries({ queryKey: ['tenant-billing-settings'] });
      toast.success('Séries sincronizadas', items.length ? `${items.length} série(s) recebida(s).` : 'Sem séries disponíveis.');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível sincronizar séries.'),
  });

  const disconnect = useMutation({
    mutationFn: () => tenantSettingsApi.disconnectMoloni(),
    onSuccess: (saved) => {
      qc.setQueryData(['tenant-billing-settings'], saved);
      setForm(toBillingForm(saved));
      toast.success('Desligado', 'A conta Moloni foi desligada. Os tokens foram apagados.');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível desligar.'),
  });

  if (billing.isLoading || !form) {
    return <p className="text-sm text-zinc-500">A carregar faturação…</p>;
  }

  function update<K extends keyof UpdateTenantBillingSettings>(key: K, value: UpdateTenantBillingSettings[K]) {
    if (!form) return;
    setForm({ ...form, [key]: value });
  }

  const connected = form.hasApiKey && form.hasRefreshToken;
  const regimeNormal = tenant.data?.regimeFiscal === 1;
  const canConnect = !!form.clientId && (form.hasClientSecret || !!form.clientSecret);

  return (
    <div className="space-y-6">
      {/* Header explicativo */}
      <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4 text-sm dark:border-zinc-800 dark:bg-zinc-950">
        <p className="font-medium">Moloni — certificação AT Nº 2860</p>
        <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
          A faturação fiscal é emitida pela tua conta Moloni Flex (ou superior, com API). Configura uma vez,
          o RepairDesk gere os tokens automaticamente — não precisas de copiar ou renovar nada.
        </p>
      </div>

      {/* Passo 1: Provider + credenciais da app */}
      <section className="space-y-4">
        <div>
          <h3 className="text-sm font-semibold">1. Registo da aplicação</h3>
          <p className="text-xs text-zinc-500">Dados que obténs no painel Moloni → Configurações → Developers.</p>
        </div>
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <Field label="Provider">
            <select value={form.provider} onChange={(e) => update('provider', Number(e.target.value) as 0 | 1 | 2)} className={inputCls}>
              <option value={0}>Desativado</option>
              <option value={1}>Moloni</option>
              <option value={2} disabled>InvoiceXpress (em breve)</option>
            </select>
          </Field>
          <Field label="Ambiente" hint="Sandbox para testes; Produção para faturas reais.">
            <label className="flex min-h-[38px] items-center gap-2 rounded-lg border border-zinc-200 px-3 text-sm dark:border-zinc-800">
              <input type="checkbox" checked={form.sandboxMode} onChange={(e) => update('sandboxMode', e.target.checked)} />
              <span>Modo sandbox</span>
            </label>
          </Field>
          <Field label="Developer ID" hint="Identificador único da tua app na Moloni (ex: repairdesk-lopestech).">
            <input
              type="text"
              value={form.clientId ?? ''}
              onChange={(e) => update('clientId', e.target.value || null)}
              className={inputCls}
              placeholder="repairdesk-lopestech"
              autoComplete="off"
            />
          </Field>
          <Field label="Client Secret" hint="Chave gerada pela Moloni após guardares o Developer ID. Encriptada no servidor.">
            <input
              type="password"
              value={form.clientSecret ?? ''}
              onChange={(e) => update('clientSecret', e.target.value || null)}
              className={inputCls}
              placeholder={form.hasClientSecret ? '••••••••••••' : 'Chave Secreta Moloni'}
              autoComplete="off"
            />
          </Field>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => save.mutate(form)}
            disabled={save.isPending}
            className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60"
          >
            {save.isPending ? 'A guardar…' : 'Guardar credenciais'}
          </button>
        </div>
      </section>

      {/* Passo 2: Ligar conta Moloni (OAuth password grant) */}
      <section className="space-y-3 border-t border-zinc-200 pt-5 dark:border-zinc-800">
        <div>
          <h3 className="text-sm font-semibold">2. Ligar conta Moloni</h3>
          <p className="text-xs text-zinc-500">
            Autorização inicial. O RepairDesk troca as credenciais por tokens encriptados — a password nunca é guardada.
          </p>
        </div>

        {connected ? (
          <div className="flex flex-wrap items-center gap-3 rounded-lg border border-emerald-200 bg-emerald-50/50 px-4 py-3 text-sm dark:border-emerald-900/40 dark:bg-emerald-950/30">
            <CheckCircle2 size={18} strokeWidth={2} className="text-emerald-600 dark:text-emerald-400" />
            <span className="font-medium text-emerald-900 dark:text-emerald-100">Conta Moloni ligada</span>
            <span className="text-xs text-emerald-700 dark:text-emerald-300">Tokens renovados automaticamente.</span>
            <button
              type="button"
              onClick={() => disconnect.mutate()}
              disabled={disconnect.isPending}
              className="ml-auto rounded-md px-2 py-1 text-xs text-red-600 hover:bg-red-50 disabled:opacity-60 dark:hover:bg-red-950/40"
            >
              {disconnect.isPending ? 'A desligar…' : 'Desligar'}
            </button>
          </div>
        ) : (
          <div className="space-y-2">
            <button
              type="button"
              onClick={() => setShowConnect(true)}
              disabled={!canConnect || form.provider !== 1}
              className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:cursor-not-allowed disabled:opacity-50"
            >
              Ligar Moloni
            </button>
            {!canConnect && (
              <p className="text-xs text-amber-700 dark:text-amber-400">
                Preenche Developer ID e Client Secret no passo 1 e clica em <strong>Guardar credenciais</strong> primeiro.
              </p>
            )}
          </div>
        )}
      </section>

      {/* Passo 3: Configuração da empresa Moloni (só visível se ligado) */}
      {connected && (
        <section className="space-y-4 border-t border-zinc-200 pt-5 dark:border-zinc-800">
          <div>
            <h3 className="text-sm font-semibold">3. Configuração da empresa</h3>
            <p className="text-xs text-zinc-500">
              Define qual empresa Moloni usar, série e tipo de documento por defeito.
            </p>
          </div>
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <Field
              label="Company ID"
              hint="ID numérico da empresa Moloni. Vês na URL quando abres a empresa no painel Moloni."
            >
              <NumberInput value={form.companyId} onChange={(v) => update('companyId', v)} />
            </Field>
            <Field
              label="Tipo de documento por defeito"
              hint="Simplificada: B2C sem NIF até €1000. Fatura: B2B com NIF, qualquer valor."
            >
              <select value={form.defaultDocumentType} onChange={(e) => update('defaultDocumentType', Number(e.target.value) as 0 | 1)} className={inputCls}>
                <option value={0}>Fatura simplificada</option>
                <option value={1}>Fatura</option>
              </select>
            </Field>
            <Field label="Série Moloni" hint="Identificador da série comunicada à AT (ex: M, FT, 2026).">
              <div className="flex gap-2">
                <NumberInput value={form.defaultSerieId} onChange={(v) => update('defaultSerieId', v)} />
                <button
                  type="button"
                  onClick={() => sync.mutate()}
                  disabled={sync.isPending}
                  className="rounded-lg border border-zinc-200 px-3 text-xs text-zinc-600 hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
                >
                  {sync.isPending ? 'A sincronizar…' : 'Sincronizar'}
                </button>
              </div>
              {series.length > 0 && (
                <select className={`${inputCls} mt-2`} value={form.defaultSerieId ?? ''} onChange={(e) => update('defaultSerieId', numOrNull(e.target.value))}>
                  <option value="">Escolher série</option>
                  {series.map((s) => <option key={s.id} value={s.id}>{s.name} ({s.id})</option>)}
                </select>
              )}
            </Field>
          </div>

          {/* IDs avançados — colapsível */}
          <div className="rounded-lg border border-zinc-200 dark:border-zinc-800">
            <button
              type="button"
              onClick={() => setShowAdvanced((v) => !v)}
              className="flex w-full items-center justify-between px-4 py-3 text-left text-sm font-medium hover:bg-zinc-50 dark:hover:bg-zinc-900"
            >
              <span>IDs operacionais Moloni (necessários para emitir)</span>
              <span className="text-xs text-zinc-500">{showAdvanced ? 'Esconder' : 'Mostrar'}</span>
            </button>
            {showAdvanced && (
              <div className="border-t border-zinc-200 px-4 py-4 dark:border-zinc-800">
                <p className="mb-3 text-xs text-zinc-500">
                  Vais ao painel Moloni e copias estes IDs internos. Em sprint futuro vão ser auto-descobertos.
                </p>
                <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
                  <Field label="Produto/serviço ID" hint="Cria em Moloni → Tabelas → Artigos um 'Serviço de reparação' e copia o ID.">
                    <NumberInput value={form.defaultProductId} onChange={(v) => update('defaultProductId', v)} />
                  </Field>
                  <Field label="Tax ID IVA" hint="Em Moloni → Tabelas → Impostos. Copia o ID do IVA aplicável (6%, 13%, 23%).">
                    <NumberInput value={form.defaultTaxId} onChange={(v) => update('defaultTaxId', v)} />
                  </Field>
                  <Field label="Método pagamento ID" hint="Em Moloni → Tabelas → Métodos de pagamento (Numerário, MBWay, Multibanco…).">
                    <NumberInput value={form.defaultPaymentMethodId} onChange={(v) => update('defaultPaymentMethodId', v)} />
                  </Field>
                  <Field label="Prazo vencimento ID" hint="Em Moloni → Tabelas → Datas de vencimento (Pronto pagamento, 30 dias…).">
                    <NumberInput value={form.defaultMaturityDateId} onChange={(v) => update('defaultMaturityDateId', v)} />
                  </Field>
                  <Field label="Cliente fallback ID" hint="Cliente Moloni 'Consumidor final' usado em vendas B2C sem NIF.">
                    <NumberInput value={form.fallbackCustomerId} onChange={(v) => update('fallbackCustomerId', v)} />
                  </Field>
                  {!regimeNormal && (
                    <Field label="Motivo isenção IVA" hint="Código M01-M99. Só preencher se regime de isenção (Art. 53). Em regime normal deixa vazio.">
                      <input value={form.exemptionReason ?? ''} onChange={(e) => update('exemptionReason', e.target.value || null)} className={inputCls} placeholder="M02" />
                    </Field>
                  )}
                </div>
              </div>
            )}
          </div>
        </section>
      )}

      {connected && (
        <div className="flex flex-wrap gap-2 border-t border-zinc-200 pt-5 dark:border-zinc-800">
          <button
            type="button"
            onClick={() => save.mutate(form)}
            disabled={save.isPending}
            className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60"
          >
            {save.isPending ? 'A guardar…' : 'Guardar configuração'}
          </button>
          <button
            type="button"
            onClick={() => test.mutate()}
            disabled={test.isPending}
            className="rounded-lg border border-zinc-200 px-4 py-2 text-sm text-zinc-700 hover:bg-zinc-50 disabled:opacity-60 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
          >
            {test.isPending ? 'A testar…' : 'Testar emissão'}
          </button>
        </div>
      )}

      {showConnect && (
        <ConnectMoloniModal
          onClose={() => setShowConnect(false)}
          onSuccess={(saved) => {
            qc.setQueryData(['tenant-billing-settings'], saved);
            setForm(toBillingForm(saved));
            setShowConnect(false);
            toast.success('Ligado a Moloni', 'Tokens recebidos e encriptados. Empresa auto-selecionada se única.');
          }}
        />
      )}
    </div>
  );
}

function ConnectMoloniModal({
  onClose,
  onSuccess,
}: {
  onClose: () => void;
  onSuccess: (saved: TenantBillingSettings) => void;
}) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');

  const connect = useMutation({
    mutationFn: () => tenantSettingsApi.connectMoloni({ username, password }),
    onSuccess,
    onError: (err) => toast.fromError(err, 'A Moloni rejeitou as credenciais.'),
  });

  function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!username || !password) return;
    connect.mutate();
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-label="Ligar Moloni"
      onClick={onClose}
    >
      <form
        onSubmit={submit}
        onClick={(e) => e.stopPropagation()}
        className="w-full max-w-md rounded-2xl border border-zinc-200 bg-white p-5 shadow-2xl dark:border-zinc-700 dark:bg-zinc-900"
      >
        <h2 className="text-base font-semibold">Ligar conta Moloni</h2>
        <p className="mt-1 text-xs text-zinc-500">
          Credenciais usadas <strong>uma única vez</strong> para obter tokens OAuth2. A password
          NUNCA é guardada no RepairDesk — só os tokens (encriptados, renovados automaticamente).
        </p>

        <div className="mt-4 space-y-3">
          <Field label="Email Moloni">
            <input
              type="email"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              className={inputCls}
              placeholder="contacto@lopestech.pt"
              autoComplete="off"
              autoFocus
              required
            />
          </Field>
          <Field label="Password Moloni">
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className={inputCls}
              placeholder="••••••••"
              autoComplete="off"
              required
            />
          </Field>
        </div>

        <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50/50 p-3 text-xs text-amber-900 dark:border-amber-900/40 dark:bg-amber-950/30 dark:text-amber-200">
          <strong>Segurança:</strong> usa sempre HTTPS. A password viaja apenas do teu browser para o
          backend RepairDesk → Moloni; nada é armazenado. Se preferires, cria um subutilizador Moloni
          dedicado ao RepairDesk com permissões mínimas.
        </div>

        <div className="mt-5 flex justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg px-4 py-2 text-sm text-zinc-700 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
          >
            Cancelar
          </button>
          <button
            type="submit"
            disabled={connect.isPending || !username || !password}
            className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60"
          >
            {connect.isPending ? 'A ligar…' : 'Ligar'}
          </button>
        </div>
      </form>
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
        <IbanFeedback iban={form.iban ?? ''} />
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

function CamposPersonalizadosSection() {
  const qc = useQueryClient();
  const templates = useQuery({
    queryKey: ['equipment-field-templates'],
    queryFn: () => equipmentFieldTemplatesApi.list(true),
  });
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [draft, setDraft] = useState<UpsertEquipmentFieldTemplate | null>(null);

  useEffect(() => {
    if (!templates.data || templates.data.length === 0 || selectedId) return;
    const first = templates.data[0];
    setSelectedId(first.id);
    setDraft(toUpsert(first));
  }, [selectedId, templates.data]);

  function pick(template: EquipmentFieldTemplate) {
    setSelectedId(template.id);
    setDraft(toUpsert(template));
  }

  function newTemplate() {
    setSelectedId(null);
    setDraft({
      nome: 'Laptop',
      categoria: 2,
      isActive: true,
      fields: [
        { label: 'Marca', type: EQUIPMENT_FIELD_TYPE.Text, options: [], required: false, ordem: 0, visibleInPortal: true },
        { label: 'Modelo', type: EQUIPMENT_FIELD_TYPE.Text, options: [], required: false, ordem: 1, visibleInPortal: true },
      ],
    });
  }

  const save = useMutation({
    mutationFn: () => {
      if (!draft) throw new Error('Sem template para guardar.');
      return selectedId ? equipmentFieldTemplatesApi.update(selectedId, draft) : equipmentFieldTemplatesApi.create(draft);
    },
    onSuccess: (saved) => {
      toast.success('Template guardado');
      setSelectedId(saved.id);
      setDraft(toUpsert(saved));
      qc.invalidateQueries({ queryKey: ['equipment-field-templates'] });
      qc.invalidateQueries({ queryKey: ['equipment-field-templates-active'] });
    },
    onError: (err) => toast.fromError(err, 'Não foi possível guardar o template.'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => equipmentFieldTemplatesApi.remove(id),
    onSuccess: () => {
      toast.success('Template apagado');
      setSelectedId(null);
      setDraft(null);
      qc.invalidateQueries({ queryKey: ['equipment-field-templates'] });
      qc.invalidateQueries({ queryKey: ['equipment-field-templates-active'] });
    },
    onError: (err) => toast.fromError(err, 'Não foi possível apagar. Se estiver em uso, desactiva-o.'),
  });

  const updateDraft = (patch: Partial<UpsertEquipmentFieldTemplate>) =>
    setDraft((d) => d ? { ...d, ...patch } : d);

  const updateField = (index: number, patch: Partial<UpsertEquipmentFieldTemplate['fields'][number]>) =>
    setDraft((d) => {
      if (!d) return d;
      const fields = d.fields.map((f, i) => i === index ? { ...f, ...patch } : f);
      return { ...d, fields };
    });

  const addField = () =>
    setDraft((d) => d ? {
      ...d,
      fields: [...d.fields, { label: '', type: EQUIPMENT_FIELD_TYPE.Text, options: [], required: false, ordem: d.fields.length, visibleInPortal: true }],
    } : d);

  const removeField = (index: number) =>
    setDraft((d) => d ? { ...d, fields: d.fields.filter((_, i) => i !== index).map((f, i) => ({ ...f, ordem: i })) } : d);

  return (
    <div className="grid gap-5 lg:grid-cols-[240px_1fr]">
      <aside className="space-y-2">
        <button
          type="button"
          onClick={newTemplate}
          className="inline-flex w-full items-center justify-center gap-1 rounded-lg bg-brand-600 px-3 py-2 text-sm font-medium text-white hover:bg-brand-700"
        >
          <Plus size={14} /> Novo template
        </button>
        {templates.isLoading && <p className="text-xs text-zinc-500">A carregar...</p>}
        <ul className="space-y-1">
          {templates.data?.map((template) => (
            <li key={template.id}>
              <button
                type="button"
                onClick={() => pick(template)}
                className={`w-full rounded-lg border px-3 py-2 text-left text-sm transition ${
                  selectedId === template.id
                    ? 'border-brand-300 bg-brand-50 text-brand-900 dark:border-brand-800 dark:bg-brand-950/30 dark:text-brand-200'
                    : 'border-zinc-200 hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800'
                }`}
              >
                <span className="block font-medium">{template.nome}</span>
                <span className="text-[11px] text-zinc-500">
                  {DEVICE_CATEGORY_LABEL[template.categoria] ?? 'Outro'} · {template.fields.length} campos
                  {!template.isActive && ' · inactivo'}
                </span>
              </button>
            </li>
          ))}
        </ul>
      </aside>

      {draft ? (
        <div className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-[1fr_180px_120px]">
            <Field label="Nome do template">
              <input value={draft.nome} onChange={(e) => updateDraft({ nome: e.target.value })} className={inputCls} />
            </Field>
            <Field label="Categoria">
              <select value={draft.categoria} onChange={(e) => updateDraft({ categoria: Number(e.target.value) })} className={inputCls}>
                {Object.entries(DEVICE_CATEGORY_LABEL).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </select>
            </Field>
            <label className="mt-6 flex items-center gap-2 text-sm">
              <input type="checkbox" checked={draft.isActive} onChange={(e) => updateDraft({ isActive: e.target.checked })} />
              Activo
            </label>
          </div>

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <h3 className="flex items-center gap-2 text-sm font-semibold"><Cpu size={15} /> Campos</h3>
              <button type="button" onClick={addField} disabled={draft.fields.length >= 20} className="rounded-md border border-zinc-200 px-2 py-1 text-xs hover:bg-zinc-50 disabled:opacity-50 dark:border-zinc-800 dark:hover:bg-zinc-800">
                + Campo
              </button>
            </div>

            {draft.fields.map((field, index) => (
              <div key={field.id ?? index} className="grid gap-2 rounded-lg border border-zinc-200 p-3 dark:border-zinc-800 md:grid-cols-[1fr_130px_1fr_auto]">
                <input value={field.label} onChange={(e) => updateField(index, { label: e.target.value })} placeholder="Label" className={inputCls} />
                <select value={field.type} onChange={(e) => updateField(index, { type: Number(e.target.value) as typeof field.type })} className={inputCls}>
                  {Object.entries(EQUIPMENT_FIELD_TYPE_LABEL).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                </select>
                <input
                  value={field.options.join(', ')}
                  disabled={field.type !== EQUIPMENT_FIELD_TYPE.Select}
                  onChange={(e) => updateField(index, { options: e.target.value.split(',').map((v) => v.trim()).filter(Boolean) })}
                  placeholder="Opções, separadas por vírgula"
                  className={inputCls}
                />
                <button type="button" onClick={() => removeField(index)} className="rounded-md p-2 text-zinc-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/40" title="Remover campo">
                  <Trash2 size={15} />
                </button>
                <div className="flex flex-wrap gap-4 text-xs text-zinc-600 dark:text-zinc-300 md:col-span-4">
                  <label className="flex items-center gap-1"><input type="checkbox" checked={field.required} onChange={(e) => updateField(index, { required: e.target.checked })} /> Obrigatório</label>
                  <label className="flex items-center gap-1"><input type="checkbox" checked={field.visibleInPortal} onChange={(e) => updateField(index, { visibleInPortal: e.target.checked })} /> Visível no portal/PDF</label>
                </div>
              </div>
            ))}
          </div>

          <div className="flex justify-between gap-2">
            <button
              type="button"
              disabled={!selectedId || remove.isPending}
              onClick={() => selectedId && remove.mutate(selectedId)}
              className="rounded-lg px-3 py-2 text-sm text-red-600 hover:bg-red-50 disabled:opacity-50 dark:hover:bg-red-950/40"
            >
              Apagar
            </button>
            <button
              type="button"
              disabled={!draft.nome.trim() || save.isPending}
              onClick={() => save.mutate()}
              className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60"
            >
              {save.isPending ? 'A guardar...' : 'Guardar template'}
            </button>
          </div>
        </div>
      ) : (
        <div className="rounded-lg border border-dashed border-zinc-300 p-8 text-center text-sm text-zinc-500 dark:border-zinc-700">
          Escolhe um template ou cria um novo.
        </div>
      )}
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

function BackupsSection() {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['admin-backups'],
    queryFn: () => backupApi.list(),
    staleTime: 30_000,
  });

  const runNow = useMutation({
    mutationFn: () => backupApi.runNow(),
    onSuccess: (r) => {
      toast.success('Backup concluído', `${r.fileName} (${formatBytes(r.sizeBytes)})${r.uploadedToR2 ? ' · enviado para R2' : ''}`);
      refetch();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível correr o backup agora.'),
  });

  return (
    <div className="space-y-5">
      <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4 text-sm dark:border-zinc-800 dark:bg-zinc-950">
        <div className="flex items-start gap-3">
          <ShieldCheck size={18} strokeWidth={2} className="mt-0.5 flex-none text-emerald-600 dark:text-emerald-400" />
          <div className="flex-1 space-y-1">
            <p className="font-medium">Backups automáticos</p>
            <p className="text-xs text-zinc-600 dark:text-zinc-400">
              Backup da base de dados todos os dias às 03:00. Ficheiros guardados em <code className="rounded bg-zinc-100 px-1 dark:bg-zinc-800">./backups/</code> no host (sobrevive a <code className="rounded bg-zinc-100 px-1 dark:bg-zinc-800">docker compose down -v</code>). Retention 30 dias local.
              {' '}Se configurares Cloudflare R2 (env <code className="rounded bg-zinc-100 px-1 dark:bg-zinc-800">Backup__R2__Bucket</code>), cópia off-site automática.
            </p>
            {data?.latestLocalBackupAt && (
              <p className="mt-1 text-xs text-zinc-500">
                Último backup: <strong>{new Date(data.latestLocalBackupAt).toLocaleString('pt-PT')}</strong>
              </p>
            )}
            {data?.status === 'disabled' && (
              <p className="mt-1 inline-flex items-center gap-1 text-xs text-amber-700 dark:text-amber-400">
                <AlertTriangle size={11} strokeWidth={2} /> Backup desactivado em <code className="rounded bg-zinc-100 px-1 dark:bg-zinc-800">.env</code>. Mete <code>Backup__Enabled=true</code> e reinicia.
              </p>
            )}
          </div>
        </div>
        <div className="mt-3 flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => runNow.mutate()}
            disabled={runNow.isPending}
            className="inline-flex items-center gap-1 rounded-lg bg-brand-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-60"
          >
            {runNow.isPending ? 'A correr…' : 'Correr backup agora'}
          </button>
          <button
            type="button"
            onClick={() => refetch()}
            className="rounded-lg border border-zinc-200 px-3 py-1.5 text-xs text-zinc-600 hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
          >
            Atualizar lista
          </button>
        </div>
      </div>

      <BackupListSection title="Backups locais" items={data?.local ?? []} loading={isLoading} error={isError} />
      {data?.r2 && data.r2.length > 0 && (
        <BackupListSection title="Backups em Cloudflare R2 (off-site)" items={data.r2} loading={false} error={false} />
      )}
    </div>
  );
}

function BackupListSection({ title, items, loading, error }: { title: string; items: ReturnType<typeof backupApi.list> extends Promise<infer T> ? T extends { local: infer L } ? L : never : never; loading: boolean; error: boolean }) {
  return (
    <div>
      <h3 className="mb-2 text-sm font-semibold">{title} <span className="text-xs font-normal text-zinc-500">· {items.length}</span></h3>
      {loading ? (
        <p className="text-xs text-zinc-500">A carregar…</p>
      ) : error ? (
        <p className="text-xs text-rose-600">Não foi possível obter a lista.</p>
      ) : items.length === 0 ? (
        <p className="text-xs text-zinc-500">Sem backups ainda. Carrega "Correr backup agora" para criar o primeiro.</p>
      ) : (
        <ul className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
          {items.map((b) => (
            <li key={`${b.location}-${b.fileName}`} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
              <div className="min-w-0 flex-1">
                <div className="truncate font-mono text-xs">{b.fileName}</div>
                <div className="text-[11px] text-zinc-500">
                  {new Date(b.timestamp).toLocaleString('pt-PT')} · {formatBytes(b.sizeBytes)}
                </div>
              </div>
              <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-[10px] font-medium text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">{b.status}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function IbanFeedback({ iban }: { iban: string }) {
  if (!iban) return null;
  const v = validateIban(iban);
  if (v.isValid) {
    return (
      <p className="mt-1 inline-flex items-center gap-1 text-[11px] text-emerald-700 dark:text-emerald-400">
        <CheckCircle2 size={11} strokeWidth={2} /> {v.display}
      </p>
    );
  }
  if (v.message) {
    return (
      <p className="mt-1 inline-flex items-center gap-1 text-[11px] text-amber-700 dark:text-amber-400">
        <AlertTriangle size={11} strokeWidth={2} /> {v.message}
      </p>
    );
  }
  return null;
}

function TenantNifFeedback({ nif }: { nif: string }) {
  if (!nif) return null;
  const v = validateNif(nif);
  if (v.isValid) {
    return (
      <p className="mt-1 inline-flex items-center gap-1 text-[11px] text-emerald-700 dark:text-emerald-400">
        <CheckCircle2 size={11} strokeWidth={2} /> {v.message}
      </p>
    );
  }
  if (v.message) {
    return (
      <p className="mt-1 inline-flex items-center gap-1 text-[11px] text-amber-700 dark:text-amber-400">
        <AlertTriangle size={11} strokeWidth={2} /> {v.message}
      </p>
    );
  }
  return null;
}

function NumberInput({ value, onChange }: { value: number | null; onChange: (value: number | null) => void }) {
  return (
    <input
      type="number"
      min={0}
      value={value ?? ''}
      onChange={(e) => onChange(numOrNull(e.target.value))}
      className={inputCls}
    />
  );
}

function numOrNull(value: string): number | null {
  if (value.trim() === '') return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function toForm(t: TenantSettings): UpdateTenantSettings {
  const { id: _id, ...rest } = t;
  return rest;
}

function toBillingForm(t: TenantBillingSettings): UpdateTenantBillingSettings {
  return {
    ...t,
    apiKey: t.apiKeyMasked ?? null,
    clientSecret: t.hasClientSecret ? '****' : null,
    refreshToken: t.hasRefreshToken ? '****' : null,
  };
}
