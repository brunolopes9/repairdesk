import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import { AlertTriangle, CheckCircle2, Clock3, Cloud, Copy, Cpu, DatabaseBackup, HardDrive, Key, Loader2, Plus, RefreshCw, RotateCcw, Settings, ShieldCheck, Star, Trash2, Wand2 } from 'lucide-react';
import Modal from '../../components/Modal';
import { EmptyState, PageHeader, SkeletonCard, SkeletonRow } from '../../components/ui';
import { tenantSettingsApi } from '../../lib/tenantSettings/api';
import { serviceKeysApi, type ServiceApiKey } from '../../lib/serviceKeys/api';
import { toast } from '../../lib/toast';
import { validateNif } from '../../lib/nif/validator';
import { validateIban } from '../../lib/iban/validator';
import { backupApi, formatBytes, type BackupFileDto, type BackupHealthStatus, type BackupSnapshotDto } from '../../lib/admin/backup';
import { equipmentFieldTemplatesApi, toUpsert } from '../../lib/equipmentFields/api';
import {
  DEVICE_CATEGORY_LABEL,
  EQUIPMENT_FIELD_TYPE,
  EQUIPMENT_FIELD_TYPE_LABEL,
  type EquipmentFieldTemplate,
  type UpsertEquipmentFieldTemplate,
} from '../../lib/equipmentFields/types';
import {
  REGIME_FISCAL_LABELS,
  type MoloniAutoDiscoverStep,
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
  { id: 'apikeys', label: 'Chaves de API' },
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
      <nav className="-mx-4 flex gap-1 overflow-x-auto border-b border-zinc-200 px-4 dark:border-zinc-800 sm:mx-0 sm:px-0" aria-label="Secções">
        {SECTIONS.map((s) => (
          <button
            key={s.id}
            type="button"
            onClick={() => setSection(s.id)}
            className={`relative -mb-px min-h-11 whitespace-nowrap px-3 py-2 text-sm transition focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 ${
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
        {section === 'apikeys' && <ApiKeysSection />}
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
  const [autoDiscoverSteps, setAutoDiscoverSteps] = useState<MoloniAutoDiscoverStep[]>([]);
  // Sprint 117: auto-save Faturação. Pattern igual à secção principal de Definições.
  const [billingSaveState, setBillingSaveState] = useState<'idle' | 'dirty' | 'saving' | 'saved' | 'error'>('idle');
  const billingDebounceRef = useRef<number | null>(null);
  const oauthPollRef = useRef<number | null>(null);
  const [redirectUri, setRedirectUri] = useState<string>(() => {
    if (typeof window === 'undefined') return '';
    return localStorage.getItem('moloni_redirect_uri') ?? 'https://lopestech.pt/api/billing/moloni/callback';
  });
  const [pasteUrl, setPasteUrl] = useState('');
  const [showPasteModal, setShowPasteModal] = useState(false);
  const [companyChoices, setCompanyChoices] = useState<{ id: number; name: string }[] | null>(null);

  useEffect(() => {
    if (billing.data) setForm(toBillingForm(billing.data));
  }, [billing.data]);

  useEffect(() => {
    function stopPolling() {
      if (oauthPollRef.current != null) {
        window.clearInterval(oauthPollRef.current);
        oauthPollRef.current = null;
      }
    }

    function handleOAuthMessage(event: MessageEvent) {
      if (event.origin !== window.location.origin) return;
      const data = event.data as { type?: string; status?: string; message?: string } | null;
      if (data?.type !== 'moloni-oauth') return;

      stopPolling();
      qc.invalidateQueries({ queryKey: ['tenant-billing-settings'] });
      if (data.status === 'connected') toast.success('Ligado a Moloni', 'Autorizacao concluida sem partilhar password.');
      else toast.error('Ligacao Moloni falhou', data.message ?? 'Tenta novamente.');
    }

    window.addEventListener('message', handleOAuthMessage);
    return () => {
      stopPolling();
      window.removeEventListener('message', handleOAuthMessage);
    };
  }, [qc]);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const status = params.get('moloni');
    if (!status) return;

    const message = params.get('msg') ?? undefined;
    if (window.opener) {
      window.opener.postMessage({ type: 'moloni-oauth', status, message }, window.location.origin);
      window.close();
      return;
    }

    qc.invalidateQueries({ queryKey: ['tenant-billing-settings'] });
    if (status === 'connected') toast.success('Ligado a Moloni', 'Autorizacao concluida.');
    if (status === 'error') toast.error('Ligacao Moloni falhou', message ?? 'Tenta novamente.');
    window.history.replaceState({}, '', window.location.pathname);
  }, [qc]);

  const save = useMutation({
    mutationFn: (payload: UpdateTenantBillingSettings) => tenantSettingsApi.updateBilling(payload),
    onMutate: () => setBillingSaveState('saving'),
    onSuccess: (saved) => {
      qc.setQueryData(['tenant-billing-settings'], saved);
      setForm(toBillingForm(saved));
      setBillingSaveState('saved');
      window.setTimeout(() => setBillingSaveState((s) => (s === 'saved' ? 'idle' : s)), 1500);
    },
    onError: (err) => {
      setBillingSaveState('error');
      toast.fromError(err, 'Não foi possível guardar.');
    },
  });

  // Sprint 117: substitui botão "Guardar credenciais" — debounce 1200ms ao mudar form.
  function updateBilling(next: UpdateTenantBillingSettings) {
    setForm(next);
    setBillingSaveState('dirty');
    if (billingDebounceRef.current) window.clearTimeout(billingDebounceRef.current);
    billingDebounceRef.current = window.setTimeout(() => save.mutate(next), 1200);
  }

  const test = useMutation({
    mutationFn: () => tenantSettingsApi.testBillingConnection(),
    onSuccess: (r) => toast.success('Ligação validada', r.message),
    onError: (err) => toast.fromError(err, 'A Moloni rejeitou a ligação.'),
  });

  // Sprint 156: diagnose Moloni — valida cada ID configurado.
  const [diagnoseResult, setDiagnoseResult] = useState<import('../../lib/tenantSettings/api').MoloniDiagnoseResult | null>(null);
  const diagnose = useMutation({
    mutationFn: () => tenantSettingsApi.diagnoseMoloni(),
    onSuccess: (r) => {
      setDiagnoseResult(r);
      if (r.allOk) toast.success('Diagnóstico OK', 'Toda a configuração Moloni está válida.');
      else toast.fromError(new Error('Diagnóstico encontrou problemas'), 'Vê o painel abaixo.');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível fazer diagnóstico.'),
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

  const detectCompany = useMutation({
    mutationFn: () => tenantSettingsApi.listMoloniCompanies(),
    onSuccess: async (companies) => {
      if (companies.length === 0) {
        toast.fromError(new Error('Sem empresas'), 'A tua conta Moloni não tem empresas configuradas.');
        return;
      }
      if (companies.length === 1) {
        const c = companies[0];
        setForm((prev) => (prev ? { ...prev, companyId: c.id } : prev));
        save.mutate({ ...form!, companyId: c.id });
        toast.success('Empresa detectada', `${c.name} (ID ${c.id}) seleccionada automaticamente.`);
      } else {
        // Abre modal de selecção (em vez de window.prompt que obrigava a copiar ID à mão)
        setCompanyChoices(companies);
      }
    },
    onError: (err) => toast.fromError(err, 'Não foi possível listar empresas Moloni.'),
  });

  const autoDiscover = useMutation({
    mutationFn: () => tenantSettingsApi.autoDiscoverMoloni(),
    onMutate: () => {
      setAutoDiscoverSteps([
        { key: 'product', label: 'Produto/serviço', success: false, created: false, id: null, name: null, message: 'A procurar serviço de reparação...' },
        { key: 'tax', label: 'IVA', success: false, created: false, id: null, name: null, message: 'A escolher imposto...' },
        { key: 'payment', label: 'Método de pagamento', success: false, created: false, id: null, name: null, message: 'A escolher Numerário...' },
        { key: 'maturity', label: 'Prazo de vencimento', success: false, created: false, id: null, name: null, message: 'A escolher pronto pagamento...' },
        { key: 'customer', label: 'Cliente fallback', success: false, created: false, id: null, name: null, message: 'A procurar Consumidor Final...' },
      ]);
    },
    onSuccess: (result) => {
      qc.setQueryData(['tenant-billing-settings'], result.settings);
      setForm(toBillingForm(result.settings));
      setAutoDiscoverSteps(result.steps);
      const failed = result.steps.filter((step) => !step.success).length;
      if (failed > 0) {
        toast.warning('Auto-configuração parcial', `${failed} item(ns) precisam de revisão manual.`);
      } else {
        toast.success('Moloni auto-configurado', 'IDs operacionais preenchidos automaticamente.');
      }
    },
    onError: (err) => toast.fromError(err, 'Não foi possível auto-configurar a Moloni.'),
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

  const startOAuth = useMutation({
    mutationFn: () => tenantSettingsApi.startMoloniOAuth(redirectUri),
    onSuccess: (result) => {
      // Abre Moloni numa nova tab e mostra modal para o user colar o URL final.
      // (Necessario porque o callback Moloni vai para o URL configurado — ex: lopestech.pt —
      // e nao para o Mender localhost.)
      window.open(result.authorizationUrl, '_blank', 'noopener,noreferrer');
      setPasteUrl('');
      setShowPasteModal(true);
    },
    onError: (err) => toast.fromError(err, 'Nao foi possivel iniciar OAuth Moloni.'),
  });

  const completeOAuth = useMutation({
    mutationFn: (params: { code: string; state: string }) => tenantSettingsApi.completeMoloniOAuth(params),
    onSuccess: (saved) => {
      qc.setQueryData(['tenant-billing-settings'], saved);
      setForm(toBillingForm(saved));
      setShowPasteModal(false);
      setPasteUrl('');
      toast.success('Conta Moloni ligada', 'Tokens guardados. A partir de agora, autenticação é automática.');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível concluir OAuth Moloni.'),
  });

  function submitPastedUrl() {
    try {
      // Aceita URL completa (https://lopestech.pt/...?code=X&state=Y) ou query-string parcial
      const trimmed = pasteUrl.trim();
      let queryString = trimmed;
      const qIndex = trimmed.indexOf('?');
      if (qIndex >= 0) queryString = trimmed.substring(qIndex + 1);
      const params = new URLSearchParams(queryString);
      const code = params.get('code');
      const state = params.get('state');
      const error = params.get('error');
      if (error) {
        toast.fromError(new Error(error), `Moloni devolveu erro: ${error}`);
        return;
      }
      if (!code || !state) {
        toast.fromError(new Error('URL inválido'), 'O URL colado não contém ?code= e ?state=. Verifica.');
        return;
      }
      completeOAuth.mutate({ code, state });
    } catch (e) {
      toast.fromError(e, 'Não foi possível interpretar o URL colado.');
    }
  }

  if (billing.isLoading || !form) {
    return (
      <div className="space-y-3">
        <SkeletonCard />
        <SkeletonCard />
        <SkeletonCard />
      </div>
    );
  }

  function update<K extends keyof UpdateTenantBillingSettings>(key: K, value: UpdateTenantBillingSettings[K]) {
    if (!form) return;
    updateBilling({ ...form, [key]: value });
  }

  const isMoloni = form.provider === 1;
  const isInvoiceXpress = form.provider === 2;
  const connected = isMoloni
    ? form.hasApiKey && form.hasRefreshToken
    : isInvoiceXpress
      ? form.hasApiKey && !!form.clientId
      : false;
  const regimeNormal = tenant.data?.regimeFiscal === 1;
  const canConnect = isMoloni && !!form.clientId && (form.hasClientSecret || !!form.clientSecret);

  return (
    <div className="space-y-6">
      {/* Banner de aviso PROD vs sandbox */}
      {connected && !form.sandboxMode && (
        <div className="rounded-lg border border-red-300 bg-red-50 p-4 text-sm dark:border-red-900/40 dark:bg-red-950/30">
          <p className="font-medium text-red-900 dark:text-red-200">⚠️ Modo PRODUÇÃO — todas as faturas vão à AT</p>
          <p className="mt-1 text-xs text-red-800 dark:text-red-300">
            Estás ligado à Moloni em produção. Cada fatura emitida é <strong>comunicada à Autoridade Tributária em tempo real</strong>
            e entra na tua declaração IVA trimestral. Para testes seguros, activa "Modo sandbox" no passo 1 (quando a sandbox
            Moloni estiver disponível). Se emitires uma fatura por engano, podes anular emitindo uma Nota de Crédito no painel
            Moloni — saldo IVA fica a zero.
          </p>
        </div>
      )}

      {isInvoiceXpress && (
        <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4 text-sm dark:border-zinc-800 dark:bg-zinc-950">
          <p className="font-medium">InvoiceXpress - provider certificado AT</p>
          <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
            Usa a tua conta InvoiceXpress existente. Preenche Account Name, API key e serie por defeito; o Mender cria
            faturas, faturas simplificadas e notas de credito pela API.
          </p>
        </div>
      )}

      {/* Header explicativo */}
      {!isInvoiceXpress && (
      <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4 text-sm dark:border-zinc-800 dark:bg-zinc-950">
        <p className="font-medium">Moloni — certificação AT Nº 2860</p>
        <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
          A faturação fiscal é emitida pela tua conta Moloni Flex (ou superior, com API). Configura uma vez,
          o Mender gere os tokens automaticamente — não precisas de copiar ou renovar nada.
        </p>
      </div>

      )}

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
              <option value={2}>InvoiceXpress</option>
            </select>
          </Field>
          <Field label="Ambiente" hint="Sandbox para testes; Produção para faturas reais.">
            <label className="flex min-h-[38px] items-center gap-2 rounded-lg border border-zinc-200 px-3 text-sm dark:border-zinc-800">
              <input type="checkbox" checked={form.sandboxMode} onChange={(e) => update('sandboxMode', e.target.checked)} />
              <span>Modo sandbox</span>
            </label>
          </Field>
          {isMoloni && (
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
          )}
          {isMoloni && (
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
          )}
          {isInvoiceXpress && (
            <>
              <Field label="Account Name" hint="Subdominio da tua conta: https://ACCOUNT.app.invoicexpress.com">
                <input
                  type="text"
                  value={form.clientId ?? ''}
                  onChange={(e) => update('clientId', e.target.value || null)}
                  className={inputCls}
                  placeholder="a-minha-loja"
                  autoComplete="off"
                />
              </Field>
              <Field label="API key" hint="Chave da tua conta InvoiceXpress. Fica encriptada no servidor.">
                <input
                  type="password"
                  value={form.apiKey ?? ''}
                  onChange={(e) => update('apiKey', e.target.value || null)}
                  className={inputCls}
                  placeholder={form.hasApiKey ? '••••••••••••' : 'API key InvoiceXpress'}
                  autoComplete="off"
                />
              </Field>
              <Field label="NIF da empresa" hint="Vem das definicoes da empresa; confirma antes de emitir faturas reais.">
                <input
                  type="text"
                  value={tenant.data?.nif ?? ''}
                  className={inputCls}
                  disabled
                  placeholder="Preenche em Empresa > NIF"
                />
              </Field>
            </>
          )}
        </div>
        <div className="flex flex-wrap items-center gap-2 text-xs">
          {billingSaveState === 'saving' && <span className="text-zinc-500">A guardar…</span>}
          {billingSaveState === 'saved' && <span className="text-emerald-600 dark:text-emerald-400">Guardado ✓</span>}
          {billingSaveState === 'dirty' && <span className="text-zinc-500">Alterações por guardar…</span>}
          {billingSaveState === 'error' && <span className="text-rose-600">Erro ao guardar — verifica acima.</span>}
          {billingSaveState === 'idle' && <span className="text-zinc-400">Alterações guardam-se automaticamente.</span>}
        </div>
      </section>

      {isMoloni && (
      <>
      {/* Passo 2: Ligar conta Moloni (OAuth password grant) */}
      <section className="space-y-3 border-t border-zinc-200 pt-5 dark:border-zinc-800">
        <div>
          <h3 className="text-sm font-semibold">2. Ligar conta Moloni</h3>
          <p className="text-xs text-zinc-500">
            Vamos abrir o login Moloni numa nova janela. Tu autorizas — o Mender nunca vê a tua password.
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
              className="ml-auto min-h-10 rounded-md px-3 py-2 text-xs text-red-600 hover:bg-red-50 disabled:opacity-60 dark:hover:bg-red-950/40"
            >
              {disconnect.isPending ? 'A desligar…' : 'Desligar'}
            </button>
          </div>
        ) : (
          <div className="space-y-3">
            <Field
              label="URL de Callback configurado no Moloni"
              hint="Tem de coincidir EXACTAMENTE com o que tens em Moloni > Configurações > Developers > URI de Resposta."
            >
              <input
                type="url"
                value={redirectUri}
                onChange={(e) => {
                  setRedirectUri(e.target.value);
                  if (typeof window !== 'undefined') localStorage.setItem('moloni_redirect_uri', e.target.value);
                }}
                className={inputCls}
                placeholder="https://lopestech.pt/api/billing/moloni/callback"
              />
            </Field>
            <button
              type="button"
              onClick={() => startOAuth.mutate()}
              disabled={!canConnect || form.provider !== 1 || startOAuth.isPending || !redirectUri}
              className="min-h-11 rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {startOAuth.isPending ? 'A abrir...' : 'Ligar Moloni via OAuth'}
            </button>
            <details className="text-xs">
              <summary className="cursor-pointer text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300">Avançado: ligar com email + password</summary>
              <div className="mt-2 rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
                <p className="mb-2 text-zinc-600 dark:text-zinc-400">
                  Método legacy <code>password grant</code>. Útil quando o callback OAuth não pode ser configurado.
                </p>
                <button
                  type="button"
                  onClick={() => setShowConnect(true)}
                  disabled={!canConnect || form.provider !== 1}
                  className="min-h-11 rounded-lg border border-zinc-200 px-3 py-2 text-xs text-zinc-700 hover:bg-zinc-50 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
                >
                  Usar email + password
                </button>
              </div>
            </details>
            {!canConnect && (
              <p className="text-xs text-amber-700 dark:text-amber-400">
                Preenche Developer ID e Client Secret no passo 1 (guardam-se automaticamente).
              </p>
            )}
          </div>
        )}
      </section>

      {/* Passo 3: Configuração da empresa Moloni (só visível se ligado) */}
      </>
      )}
      {isMoloni && connected && (
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
              hint="ID numérico da empresa Moloni. Clica Detectar para auto-preencher (se só tens 1 empresa) ou escolher de uma lista."
            >
              <div className="flex gap-2">
                <NumberInput value={form.companyId} onChange={(v) => update('companyId', v)} />
                <button
                  type="button"
                  onClick={() => detectCompany.mutate()}
                  disabled={detectCompany.isPending}
                  className="rounded-lg border border-zinc-200 px-3 text-xs text-zinc-600 hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
                >
                  {detectCompany.isPending ? 'A detectar...' : 'Detectar'}
                </button>
              </div>
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
          <div className="rounded-lg border border-brand-200 bg-brand-50/40 p-4 dark:border-brand-900/40 dark:bg-brand-950/20">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Auto-configurar IDs operacionais</p>
                <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
                  Procura serviço de reparação, IVA, Numerário, pronto pagamento e Consumidor Final na tua conta Moloni.
                </p>
              </div>
              <button
                type="button"
                onClick={() => autoDiscover.mutate()}
                disabled={autoDiscover.isPending || !form.companyId}
                className="inline-flex items-center justify-center gap-2 rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {autoDiscover.isPending ? <Loader2 size={16} className="animate-spin" /> : <Wand2 size={16} />}
                {autoDiscover.isPending ? 'A configurar...' : 'Auto-configurar tudo'}
              </button>
            </div>
            {!form.companyId && (
              <p className="mt-3 text-xs text-amber-700 dark:text-amber-300">
                Confirma primeiro o Company ID Moloni para saber onde procurar.
              </p>
            )}
            {(autoDiscover.isPending || autoDiscoverSteps.length > 0) && (
              <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
                {autoDiscoverSteps.map((step) => (
                  <div
                    key={step.key}
                    className="flex min-h-[44px] items-center gap-2 rounded-md border border-zinc-200 bg-white px-3 py-2 text-xs dark:border-zinc-800 dark:bg-zinc-950"
                  >
                    {autoDiscover.isPending ? (
                      <Loader2 size={15} className="shrink-0 animate-spin text-brand-600" />
                    ) : step.success ? (
                      <CheckCircle2 size={15} className="shrink-0 text-emerald-600" />
                    ) : (
                      <AlertTriangle size={15} className="shrink-0 text-amber-600" />
                    )}
                    <span className="min-w-0">
                      <span className="block font-medium text-zinc-800 dark:text-zinc-100">{step.label}</span>
                      <span className="block truncate text-zinc-500">
                        {step.success
                          ? `${step.created ? 'Criado' : 'Encontrado'}: ${step.name ?? step.id}`
                          : step.message}
                      </span>
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>

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
                  Estes campos ficam preenchidos pela auto-configuração. Mantêm-se editáveis para corrigires casos especiais.
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

      {isInvoiceXpress && (
        <section className="space-y-4 border-t border-zinc-200 pt-5 dark:border-zinc-800">
          <div>
            <h3 className="text-sm font-semibold">2. Configuracao InvoiceXpress</h3>
            <p className="text-xs text-zinc-500">
              Define o tipo de documento, a serie por defeito e o motivo de isencao quando aplicavel.
            </p>
          </div>
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <Field
              label="Tipo de documento por defeito"
              hint="Simplificada para B2C; Fatura para clientes com NIF ou empresas."
            >
              <select value={form.defaultDocumentType} onChange={(e) => update('defaultDocumentType', Number(e.target.value) as 0 | 1)} className={inputCls}>
                <option value={0}>Fatura simplificada</option>
                <option value={1}>Fatura</option>
              </select>
            </Field>
            <Field label="Serie InvoiceXpress" hint="Sequence ID da serie ativa na InvoiceXpress. Podes sincronizar ou preencher manualmente.">
              <div className="flex gap-2">
                <NumberInput value={form.defaultSerieId} onChange={(v) => update('defaultSerieId', v)} />
                <button
                  type="button"
                  onClick={() => sync.mutate()}
                  disabled={sync.isPending || !form.clientId || (!form.hasApiKey && !form.apiKey)}
                  className="rounded-lg border border-zinc-200 px-3 text-xs text-zinc-600 hover:bg-zinc-100 disabled:opacity-60 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
                >
                  {sync.isPending ? 'A sincronizar...' : 'Sincronizar'}
                </button>
              </div>
              {series.length > 0 && (
                <select className={`${inputCls} mt-2`} value={form.defaultSerieId ?? ''} onChange={(e) => update('defaultSerieId', numOrNull(e.target.value))}>
                  <option value="">Escolher serie</option>
                  {series.map((s) => <option key={s.id} value={s.id}>{s.name} ({s.id})</option>)}
                </select>
              )}
            </Field>
            {!regimeNormal && (
              <Field label="Motivo isencao IVA" hint="Codigo M01-M99. Obrigatorio para faturas sem IVA.">
                <input value={form.exemptionReason ?? ''} onChange={(e) => update('exemptionReason', e.target.value || null)} className={inputCls} placeholder="M01" />
              </Field>
            )}
          </div>
        </section>
      )}

      {connected && (
        <div className="space-y-3 border-t border-zinc-200 pt-5 dark:border-zinc-800">
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => test.mutate()}
              disabled={test.isPending}
              className="rounded-lg border border-zinc-200 px-4 py-2 text-sm text-zinc-700 hover:bg-zinc-50 disabled:opacity-60 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              {test.isPending ? 'A testar…' : 'Testar emissão'}
            </button>
            {/* Sprint 156: diagnóstico Moloni — valida cada ID configurado para Bruno saber qual está mal. */}
            <button
              type="button"
              onClick={() => diagnose.mutate()}
              disabled={diagnose.isPending}
              className="rounded-lg border border-amber-300 bg-amber-50 px-4 py-2 text-sm font-medium text-amber-800 hover:bg-amber-100 disabled:opacity-60 dark:border-amber-800/40 dark:bg-amber-950/30 dark:text-amber-200"
              title="Quando emissão falha com 'Database error', clica aqui para identificar qual ID Moloni está inválido."
            >
              {diagnose.isPending ? 'A diagnosticar…' : '🔍 Diagnosticar Moloni'}
            </button>
          </div>

          {diagnoseResult && (
            <div className={`rounded-lg border p-3 text-xs ${diagnoseResult.allOk ? 'border-emerald-300 bg-emerald-50 dark:border-emerald-800/40 dark:bg-emerald-950/30' : 'border-rose-300 bg-rose-50 dark:border-rose-800/40 dark:bg-rose-950/30'}`}>
              <div className="mb-2 font-semibold">
                {diagnoseResult.allOk
                  ? '✅ Configuração Moloni OK'
                  : `⚠️ Encontrámos ${diagnoseResult.checks.filter(c => !c.ok).length} problema(s)`}
              </div>
              <ul className="space-y-1">
                {diagnoseResult.checks.map((check, i) => (
                  <li key={i} className="flex items-start gap-2">
                    <span className={check.ok ? 'text-emerald-700 dark:text-emerald-400' : 'text-rose-700 dark:text-rose-400'}>
                      {check.ok ? '✓' : '✗'}
                    </span>
                    <div className="flex-1">
                      <div>
                        <strong>{check.step}</strong>
                        {check.idValue && <span className="ml-1 font-mono text-[10px] text-zinc-500">({check.idLabel}={check.idValue})</span>}
                      </div>
                      <div className={check.ok ? 'text-zinc-600 dark:text-zinc-400' : 'text-rose-700 dark:text-rose-300'}>
                        {check.message}
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
              {!diagnoseResult.allOk && (
                <div className="mt-3 rounded bg-zinc-50 p-2 text-zinc-700 dark:bg-zinc-900 dark:text-zinc-300">
                  <strong>Fix:</strong> clica "Auto-configurar tudo" acima para corrigir IDs em falta, ou
                  arranja manualmente os IDs no painel Moloni e cola aqui.
                </div>
              )}
            </div>
          )}
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

      {companyChoices && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          onClick={() => setCompanyChoices(null)}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            className="w-full max-w-md rounded-2xl border border-zinc-200 bg-white p-5 shadow-2xl dark:border-zinc-700 dark:bg-zinc-900"
          >
            <h2 className="text-base font-semibold">Escolhe a empresa Moloni</h2>
            <p className="mt-1 text-xs text-zinc-500">
              A tua conta Moloni tem {companyChoices.length} empresas. Selecciona a que queres usar com este tenant.
            </p>

            <ul className="mt-4 space-y-1.5">
              {companyChoices.map((c) => (
                <li key={c.id}>
                  <button
                    type="button"
                    onClick={() => {
                      setForm((prev) => (prev ? { ...prev, companyId: c.id } : prev));
                      if (form) save.mutate({ ...form, companyId: c.id });
                      setCompanyChoices(null);
                    }}
                    className="flex w-full items-center justify-between rounded-lg border border-zinc-200 px-3 py-2.5 text-left text-sm hover:border-brand-400 hover:bg-brand-50/50 dark:border-zinc-700 dark:hover:border-brand-600 dark:hover:bg-brand-950/30"
                  >
                    <span className="font-medium">{c.name}</span>
                    <span className="font-mono text-xs text-zinc-500">ID {c.id}</span>
                  </button>
                </li>
              ))}
            </ul>

            <div className="mt-4 flex justify-end">
              <button
                type="button"
                onClick={() => setCompanyChoices(null)}
                className="rounded-lg px-3 py-1.5 text-xs text-zinc-700 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
              >
                Cancelar
              </button>
            </div>
          </div>
        </div>
      )}

      {showPasteModal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          onClick={() => setShowPasteModal(false)}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            className="w-full max-w-lg rounded-2xl border border-zinc-200 bg-white p-5 shadow-2xl dark:border-zinc-700 dark:bg-zinc-900"
          >
            <h2 className="text-base font-semibold">Cola o URL após autorizares na Moloni</h2>
            <p className="mt-1 text-xs text-zinc-500">
              Abriste uma nova aba com login Moloni. Depois de fazeres login e autorizares,
              a Moloni vai redireccionar-te para o teu URL de callback (ex: <code className="font-mono text-[11px]">{redirectUri}</code>).
              Esse URL pode mostrar uma página 404 — não há problema. <strong>Copia o URL completo da barra de endereço</strong> e cola aqui.
            </p>

            <textarea
              value={pasteUrl}
              onChange={(e) => setPasteUrl(e.target.value)}
              className={`${inputCls} mt-3 font-mono text-xs`}
              placeholder="https://lopestech.pt/api/billing/moloni/callback?code=abc123...&state=xyz789..."
              rows={3}
              autoFocus
            />

            <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50/50 p-3 text-[11px] text-amber-900 dark:border-amber-900/40 dark:bg-amber-950/30 dark:text-amber-200">
              <strong>Como copiar:</strong> selecciona a URL completa da barra do browser (Ctrl+L → Ctrl+C) e cola aqui (Ctrl+V).
              O Mender extrai automaticamente o <code>code</code> e o <code>state</code>.
            </div>

            <div className="mt-5 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setShowPasteModal(false)}
                className="rounded-lg px-4 py-2 text-sm text-zinc-700 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={submitPastedUrl}
                disabled={completeOAuth.isPending || !pasteUrl.trim()}
                className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60"
              >
                {completeOAuth.isPending ? 'A validar...' : 'Concluir ligação'}
              </button>
            </div>
          </div>
        </div>
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
          NUNCA é guardada no Mender — só os tokens (encriptados, renovados automaticamente).
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
          backend Mender → Moloni; nada é armazenado. Se preferires, cria um subutilizador Moloni
          dedicado ao Mender com permissões mínimas.
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

      <div className="mt-6 border-t border-zinc-200 pt-4 dark:border-zinc-800">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <ShieldCheck size={15} strokeWidth={2} className="text-emerald-600 dark:text-emerald-400" />
          Garantia de Vendas (DL 84/2021)
        </h3>
        <p className="mt-1 text-xs text-zinc-500">
          Aplica-se quando vendes equipamentos no balcão (POS) ou online. Bens novos: 3 anos (1095 dias). Refurbished pode reduzir até 18 meses (540 dias) <strong>se acordado expressamente</strong>.
        </p>
      </div>

      <Field
        label="Dias de garantia · Bens Novos"
        hint="Padrão legal DL 84/2021: 1095 (3 anos). Aplica-se também a items sem condição definida."
      >
        <input
          type="number"
          min={540}
          max={3650}
          value={form.garantiaVendaDiasDefault ?? 1095}
          onChange={(e) => update('garantiaVendaDiasDefault', Math.max(540, Number(e.target.value) || 1095))}
          className={inputCls}
        />
      </Field>

      <Field
        label="Dias de garantia · Open Box"
        hint="Sem uso mas embalagem aberta. Default 730 (2 anos)."
      >
        <input
          type="number"
          min={540}
          max={3650}
          value={form.garantiaVendaOpenBoxDias ?? 730}
          onChange={(e) => update('garantiaVendaOpenBoxDias', Math.max(540, Number(e.target.value) || 730))}
          className={inputCls}
        />
      </Field>

      <Field
        label="Dias de garantia · Recondicionado"
        hint="Mínimo legal DL 84/2021 art. 12.º n.º 4: 540 (18m) com acordo expresso. Política comercial LopesTech: 1095."
      >
        <input
          type="number"
          min={540}
          max={3650}
          value={form.garantiaVendaRecondicionadoDias ?? 540}
          onChange={(e) => update('garantiaVendaRecondicionadoDias', Math.max(540, Number(e.target.value) || 540))}
          className={inputCls}
        />
      </Field>

      <Field
        label="Dias de garantia · Usado"
        hint="Bens em segunda mão. Default 540 (18m mínimo legal)."
      >
        <input
          type="number"
          min={540}
          max={3650}
          value={form.garantiaVendaUsadoDias ?? 540}
          onChange={(e) => update('garantiaVendaUsadoDias', Math.max(540, Number(e.target.value) || 540))}
          className={inputCls}
        />
      </Field>

      <Field label="Cobertura (Vendas)" hint="Aparece na garantia digital + PDF. Usa linguagem clara ao consumidor.">
        <textarea
          rows={4}
          value={form.garantiaVendaCoberturaDefault ?? ''}
          onChange={(e) => update('garantiaVendaCoberturaDefault', e.target.value || null)}
          placeholder="Ex: Conformidade do bem com o descrito na fatura (DL 84/2021). Direito a reparação, substituição, redução de preço ou resolução do contrato."
          className={`${inputCls} resize-none`}
        />
      </Field>

      <Field label="Exclusões (Vendas)">
        <textarea
          rows={4}
          value={form.garantiaVendaExclusoesDefault ?? ''}
          onChange={(e) => update('garantiaVendaExclusoesDefault', e.target.value || null)}
          placeholder="Ex: Danos por uso indevido, líquidos, quedas, abertura/desmontagem, desgaste normal de bateria e acessórios."
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

      {/* Sprint 175b: retention policy de faturas de fornecedor importadas. */}
      <div className="mt-6 space-y-3 rounded-lg border border-zinc-200 p-4 dark:border-zinc-700">
        <div>
          <h3 className="text-sm font-semibold">Retenção de faturas importadas (RGPD)</h3>
          <p className="mt-1 text-xs text-zinc-500">
            Apaga automaticamente faturas antigas conforme o estado. Metadata (totais, items, IVA) fica
            sempre — só os PDFs raw são apagados. Deixa vazio (ou 0) para nunca apagar.
            <strong className="ml-1">PT (CIRS art. 123.º): documentos fiscais aprovados devem ficar 10 anos.</strong>
          </p>
        </div>
        <Field label="Rejeitadas — dias até apagar" hint="Default: 15 dias. Faturas rejeitadas são lixo.">
          <input
            type="number"
            min={0}
            max={3650}
            value={form.retentionRejectedDays ?? ''}
            onChange={(e) => update('retentionRejectedDays', e.target.value ? Number(e.target.value) : null)}
            placeholder="15"
            className={inputCls}
          />
        </Field>
        <Field label="Falhas de parsing — dias até apagar" hint="Default: 30 dias. Tempo para debug.">
          <input
            type="number"
            min={0}
            max={3650}
            value={form.retentionFailedDays ?? ''}
            onChange={(e) => update('retentionFailedDays', e.target.value ? Number(e.target.value) : null)}
            placeholder="30"
            className={inputCls}
          />
        </Field>
        <Field
          label="Aprovadas — dias até apagar PDF raw"
          hint="Default: vazio = permanente (recomendado PT). Mete um número se quiseres apagar PDFs aprovados depois de X dias — metadata fica sempre."
        >
          <input
            type="number"
            min={0}
            max={36500}
            value={form.retentionApprovedPdfDays ?? ''}
            onChange={(e) => update('retentionApprovedPdfDays', e.target.value ? Number(e.target.value) : null)}
            placeholder="(vazio = permanente)"
            className={inputCls}
          />
        </Field>
      </div>
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
        {templates.isLoading && (
          <div className="space-y-2">
            <SkeletonRow columns={1} />
            <SkeletonRow columns={1} />
            <SkeletonRow columns={1} />
          </div>
        )}
        <ul className="space-y-1">
          {templates.data?.map((template) => (
            <li key={template.id}>
              <button
                type="button"
                onClick={() => pick(template)}
                className={`min-h-11 w-full rounded-lg border px-3 py-2 text-left text-sm transition ${
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
            <label className="flex min-h-11 items-center gap-2 text-sm sm:mt-6">
              <input type="checkbox" checked={draft.isActive} onChange={(e) => updateDraft({ isActive: e.target.checked })} className="scale-125 sm:scale-100" />
              Activo
            </label>
          </div>

          <div className="space-y-2">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h3 className="flex items-center gap-2 text-sm font-semibold"><Cpu size={15} /> Campos</h3>
              <button type="button" onClick={addField} disabled={draft.fields.length >= 20} className="min-h-10 rounded-md border border-zinc-200 px-3 py-2 text-xs hover:bg-zinc-50 disabled:opacity-50 dark:border-zinc-800 dark:hover:bg-zinc-800">
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
                <button type="button" onClick={() => removeField(index)} className="grid h-10 w-10 place-items-center rounded-md text-zinc-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/40" title="Remover campo">
                  <Trash2 size={15} />
                </button>
                <div className="flex flex-wrap gap-4 text-xs text-zinc-600 dark:text-zinc-300 md:col-span-4">
                  <label className="flex min-h-10 items-center gap-2"><input type="checkbox" checked={field.required} onChange={(e) => updateField(index, { required: e.target.checked })} className="scale-125 sm:scale-100" /> Obrigatório</label>
                  <label className="flex min-h-10 items-center gap-2"><input type="checkbox" checked={field.visibleInPortal} onChange={(e) => updateField(index, { visibleInPortal: e.target.checked })} className="scale-125 sm:scale-100" /> Visível no portal/PDF</label>
                </div>
              </div>
            ))}
          </div>

          <div className="flex flex-col-reverse justify-between gap-2 sm:flex-row">
            <button
              type="button"
              disabled={!selectedId || remove.isPending}
              onClick={() => selectedId && remove.mutate(selectedId)}
              className="min-h-11 rounded-lg px-3 py-2 text-sm text-red-600 hover:bg-red-50 disabled:opacity-50 dark:hover:bg-red-950/40"
            >
              Apagar
            </button>
            <button
              type="button"
              disabled={!draft.nome.trim() || save.isPending}
              onClick={() => save.mutate()}
              className="min-h-11 rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60"
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
  'block min-h-11 w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-100 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-800 dark:bg-zinc-950 dark:focus:ring-brand-900/40';

type BackupLocationFilter = 'all' | 'local' | 'r2';

function ApiKeysSection() {
  const qc = useQueryClient();
  const keys = useQuery({
    queryKey: ['service-keys'],
    queryFn: () => serviceKeysApi.list(),
  });

  const [createOpen, setCreateOpen] = useState(false);
  const [newKeyName, setNewKeyName] = useState('');
  const [newKeyScopes, setNewKeyScopes] = useState<Set<'read' | 'write'>>(new Set(['read', 'write']));
  const [revokedFor, setRevokedFor] = useState<ServiceApiKey | null>(null);
  const [revokeReason, setRevokeReason] = useState('');
  const [plainKey, setPlainKey] = useState<{ value: string; name: string } | null>(null);

  const create = useMutation({
    mutationFn: () => serviceKeysApi.create(
      newKeyName.trim(),
      newKeyScopes.size === 2 ? null : (Array.from(newKeyScopes) as ('read' | 'write')[]),
    ),
    onSuccess: (res) => {
      qc.invalidateQueries({ queryKey: ['service-keys'] });
      setPlainKey({ value: res.plainKey, name: res.key.name });
      setCreateOpen(false);
      setNewKeyName('');
      setNewKeyScopes(new Set(['read', 'write']));
    },
    onError: (err) => toast.fromError(err, 'Não foi possível criar a chave.'),
  });

  const revoke = useMutation({
    mutationFn: (k: ServiceApiKey) => serviceKeysApi.revoke(k.id, revokeReason.trim() || null),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['service-keys'] });
      setRevokedFor(null);
      setRevokeReason('');
      toast.success('Chave revogada');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível revogar.'),
  });

  const active = (keys.data ?? []).filter((k) => k.revokedAt === null);
  const revoked = (keys.data ?? []).filter((k) => k.revokedAt !== null);

  return (
    <div className="space-y-4">
      <div className="flex items-start gap-3 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <Key size={20} strokeWidth={2} className="mt-0.5 flex-none text-zinc-500" />
        <div className="flex-1 text-sm">
          <h3 className="font-semibold">Chaves de API</h3>
          <p className="mt-1 text-xs text-zinc-500">
            Para integrações servidor-a-servidor (loja online, importadores). Cada chave dá acesso total
            aos endpoints da API no contexto deste tenant — guarda como secret. A plain key só é mostrada
            no momento da criação.
          </p>
        </div>
        <button
          type="button"
          onClick={() => setCreateOpen(true)}
          className="inline-flex items-center gap-1 rounded-md bg-brand-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-700"
        >
          <Plus size={13} /> Nova chave
        </button>
      </div>

      {keys.isLoading && <SkeletonCard />}

      {!keys.isLoading && active.length === 0 && revoked.length === 0 && (
        <p className="rounded-lg border border-dashed border-zinc-300 bg-white p-6 text-center text-sm text-zinc-500 dark:border-zinc-700 dark:bg-zinc-900">
          Ainda não há chaves criadas.
        </p>
      )}

      {active.length > 0 && (
        <div className="rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
          <div className="border-b border-zinc-200 px-4 py-2 text-xs font-medium text-zinc-500 dark:border-zinc-800">Activas ({active.length})</div>
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {active.map((k) => (
              <li key={k.id} className="flex items-center justify-between gap-3 px-4 py-3 text-sm">
                <div className="min-w-0 flex-1">
                  <div className="font-medium">{k.name}</div>
                  <div className="font-mono text-[11px] text-zinc-500">{k.keyPrefix}</div>
                  <div className="text-[11px] text-zinc-400">
                    Criada {new Date(k.createdAt).toLocaleDateString('pt-PT')}
                    {k.lastUsedAt
                      ? ` · último uso ${new Date(k.lastUsedAt).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}`
                      : ' · nunca usada'}
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setRevokedFor(k)}
                  className="rounded-md px-2 py-1 text-xs text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/40"
                >
                  Revogar
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {revoked.length > 0 && (
        <details className="rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
          <summary className="cursor-pointer px-4 py-2 text-xs font-medium text-zinc-500">
            Revogadas ({revoked.length})
          </summary>
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {revoked.map((k) => (
              <li key={k.id} className="px-4 py-3 text-sm opacity-70">
                <div className="font-medium line-through">{k.name}</div>
                <div className="font-mono text-[11px] text-zinc-500">{k.keyPrefix}</div>
                <div className="text-[11px] text-zinc-400">
                  Revogada {k.revokedAt && new Date(k.revokedAt).toLocaleDateString('pt-PT')}
                  {k.revokedReason && ` · ${k.revokedReason}`}
                </div>
              </li>
            ))}
          </ul>
        </details>
      )}

      <Modal
        open={createOpen}
        title="Nova chave de API"
        onClose={() => { if (!create.isPending) { setCreateOpen(false); setNewKeyName(''); } }}
        footer={<>
          <button type="button" disabled={create.isPending} onClick={() => { setCreateOpen(false); setNewKeyName(''); }}
            className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 disabled:opacity-60 dark:text-zinc-300">Cancelar</button>
          <button type="button" disabled={!newKeyName.trim() || newKeyScopes.size === 0 || create.isPending} onClick={() => create.mutate()}
            className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
            {create.isPending ? 'A criar…' : 'Gerar chave'}
          </button>
        </>}
      >
        <label className="block text-sm">
          <span className="text-xs font-medium text-zinc-600 dark:text-zinc-300">Nome (descritivo) *</span>
          <input
            value={newKeyName}
            onChange={(e) => setNewKeyName(e.target.value)}
            placeholder="Ex: Loja online produção"
            maxLength={200}
            autoFocus
            className="mt-1 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </label>
        <div className="mt-3">
          <div className="text-xs font-medium text-zinc-600 dark:text-zinc-300">Permissões</div>
          <p className="mt-1 text-[11px] text-zinc-500">
            Princípio menor privilégio. Se a chave só lê dados (ex: dashboard externa), desactiva <strong>write</strong> — atacante que apanhe a chave não consegue criar/cancelar vendas.
          </p>
          <div className="mt-2 grid grid-cols-2 gap-2">
            {(['read', 'write'] as const).map((s) => (
              <label key={s} className={`flex cursor-pointer items-start gap-2 rounded-md border p-2 text-xs ${
                newKeyScopes.has(s)
                  ? 'border-brand-300 bg-brand-50/50 dark:border-brand-700 dark:bg-brand-950/30'
                  : 'border-zinc-200 dark:border-zinc-700'
              }`}>
                <input
                  type="checkbox"
                  checked={newKeyScopes.has(s)}
                  onChange={(e) => {
                    const next = new Set(newKeyScopes);
                    if (e.target.checked) next.add(s); else next.delete(s);
                    setNewKeyScopes(next);
                  }}
                  className="mt-0.5"
                />
                <div>
                  <div className="font-mono font-medium">{s}</div>
                  <div className="text-[10px] text-zinc-500">
                    {s === 'read' ? 'GETs (orders, parts, clientes, garantias)' : 'POSTs (checkout, cancel)'}
                  </div>
                </div>
              </label>
            ))}
          </div>
          {newKeyScopes.size === 0 && (
            <div className="mt-2 text-[11px] text-rose-600">Selecciona pelo menos uma permissão.</div>
          )}
        </div>
      </Modal>

      <Modal
        open={plainKey !== null}
        title="Chave criada"
        onClose={() => setPlainKey(null)}
        footer={<button type="button" onClick={() => setPlainKey(null)}
          className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white">Fechar</button>}
      >
        {plainKey && (
          <div className="space-y-3 text-sm">
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-200">
              <strong>Guarda esta chave agora.</strong> Depois de fechar, só verás o prefix —
              não é possível recuperar o valor completo.
            </div>
            <div>
              <div className="text-xs font-medium text-zinc-600 dark:text-zinc-300">{plainKey.name}</div>
              <div className="mt-1 flex items-center gap-2 rounded-lg border border-zinc-300 bg-zinc-50 p-2 dark:border-zinc-700 dark:bg-zinc-950">
                <code className="flex-1 break-all text-xs">{plainKey.value}</code>
                <button
                  type="button"
                  onClick={() => navigator.clipboard.writeText(plainKey.value).then(() => toast.success('Copiado'))}
                  className="inline-flex items-center gap-1 rounded-md bg-zinc-200 px-2 py-1 text-[11px] hover:bg-zinc-300 dark:bg-zinc-800 dark:hover:bg-zinc-700"
                >
                  <Copy size={12} /> Copiar
                </button>
              </div>
            </div>
            <p className="text-[11px] text-zinc-500">
              Uso: header <code>Authorization: ApiKey {plainKey.value.slice(0, 20)}…</code> ou <code>X-Api-Key</code>.
            </p>
          </div>
        )}
      </Modal>

      <Modal
        open={revokedFor !== null}
        title="Revogar chave"
        onClose={() => { if (!revoke.isPending) { setRevokedFor(null); setRevokeReason(''); } }}
        footer={<>
          <button type="button" disabled={revoke.isPending}
            onClick={() => { setRevokedFor(null); setRevokeReason(''); }}
            className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 disabled:opacity-60 dark:text-zinc-300">Cancelar</button>
          <button type="button" disabled={revoke.isPending}
            onClick={() => revokedFor && revoke.mutate(revokedFor)}
            className="rounded-md bg-rose-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
            {revoke.isPending ? 'A revogar…' : 'Revogar'}
          </button>
        </>}
      >
        {revokedFor && (
          <div className="space-y-3 text-sm">
            <p>
              Revogar <strong>{revokedFor.name}</strong> ({revokedFor.keyPrefix})?
              Requests futuros com esta chave vão falhar com 401.
            </p>
            <label className="block">
              <span className="text-xs font-medium text-zinc-600 dark:text-zinc-300">Motivo (opcional)</span>
              <input
                value={revokeReason}
                onChange={(e) => setRevokeReason(e.target.value)}
                placeholder="Ex: comprometida, rotação, projecto descontinuado"
                maxLength={500}
                className="mt-1 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-rose-500 focus:ring-2 focus:ring-rose-200 dark:border-zinc-700 dark:bg-zinc-950"
              />
            </label>
          </div>
        )}
      </Modal>
    </div>
  );
}

function BackupsSection() {
  const [locationFilter, setLocationFilter] = useState<BackupLocationFilter>('all');
  const [restoreTarget, setRestoreTarget] = useState<BackupFileDto | null>(null);
  const [restoreText, setRestoreText] = useState('');

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['admin-backups'],
    queryFn: () => backupApi.list(),
    staleTime: 30_000,
  });

  const preview = useQuery({
    queryKey: ['admin-backup-restore-preview', restoreTarget?.id],
    queryFn: () => backupApi.restorePreview(restoreTarget!.id),
    enabled: Boolean(restoreTarget),
    retry: false,
  });

  const runNow = useMutation({
    mutationFn: () => backupApi.runNow(),
    onSuccess: (r) => {
      toast.success('Backup concluido', `${r.fileName} (${formatBytes(r.sizeBytes)})${r.uploadedToR2 ? ' - enviado para R2' : ''}`);
      refetch();
    },
    onError: (err) => toast.fromError(err, 'Nao foi possivel correr o backup agora.'),
  });

  const restore = useMutation({
    mutationFn: () => {
      if (!restoreTarget) throw new Error('Sem backup seleccionado.');
      return backupApi.restore(restoreTarget.id, restoreText);
    },
    onSuccess: (r) => {
      toast.success('Restore concluido', `Base de dados restaurada a partir de ${r.restoredBackup.fileName}.`);
      setRestoreTarget(null);
      setRestoreText('');
      refetch();
    },
    onError: (err) => toast.fromError(err, 'Nao foi possivel restaurar este backup.'),
  });

  const allItems = useMemo(() => {
    const items = data?.items?.length ? data.items : [...(data?.local ?? []), ...(data?.r2 ?? [])];
    return items
      .filter((item) => {
        const key = backupLocationKey(item.location);
        return locationFilter === 'all' || key === locationFilter;
      })
      .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());
  }, [data, locationFilter]);

  const health = normalizeBackupHealth(data?.healthStatus);
  const localCount = data?.local.length ?? 0;
  const r2Count = data?.r2.length ?? 0;

  return (
    <div className="space-y-5">
      <div className={`rounded-lg border p-4 text-sm ${health.panelClass}`}>
        <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
          <div className="flex items-start gap-3">
            <health.Icon size={18} strokeWidth={2} className={`mt-0.5 flex-none ${health.iconClass}`} />
            <div className="space-y-1">
              <p className="font-medium">Backups e restore</p>
              <p className="text-xs text-zinc-600 dark:text-zinc-400">
                Restore 1-click cria primeiro um backup de seguranca, regista auditoria e exige permissao especial de owner.
                Os ficheiros nunca sao descarregados directamente pela interface.
              </p>
              <p className={`inline-flex items-center gap-1 text-xs ${health.textClass}`}>
                {health.label}
                {data?.latestBackupAt && (
                  <span className="text-zinc-500 dark:text-zinc-400">
                    - ultimo backup em {new Date(data.latestBackupAt).toLocaleString('pt-PT')}
                  </span>
                )}
              </p>
              {data?.status === 'disabled' && (
                <p className="inline-flex items-center gap-1 text-xs text-amber-700 dark:text-amber-400">
                  <AlertTriangle size={11} strokeWidth={2} /> Agendamento desligado. Usa <code>Backup__Enabled=true</code> em producao.
                </p>
              )}
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => runNow.mutate()}
              disabled={runNow.isPending}
              className="inline-flex min-h-11 items-center gap-1 rounded-lg bg-brand-600 px-3 py-2 text-xs font-medium text-white hover:bg-brand-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-60"
            >
              {runNow.isPending ? <Loader2 size={14} className="animate-spin" /> : <DatabaseBackup size={14} />}
              Forcar backup agora
            </button>
            <button
              type="button"
              onClick={() => refetch()}
              className="inline-flex min-h-11 items-center gap-1 rounded-lg border border-zinc-200 px-3 py-2 text-xs text-zinc-600 hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              <RefreshCw size={14} /> Atualizar
            </button>
          </div>
        </div>
      </div>

      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <BackupMetric icon={HardDrive} label="Backups locais / R2" value={`${localCount} / ${r2Count}`} />
        <BackupMetric icon={Clock3} label="Idade do mais recente" value={formatBackupAge(data?.latestBackupAgeHours)} />
        <BackupMetric icon={DatabaseBackup} label="Espaco local usado" value={formatBytes(data?.localBytesUsed ?? 0)} />
        <BackupMetric icon={Cloud} label="Retention policy" value={`${data?.localRetentionDays ?? 30}d local / ${data?.r2RetentionDays ?? 90}d R2`} />
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="inline-flex rounded-lg border border-zinc-200 p-1 text-xs dark:border-zinc-800">
          {[
            ['all', 'Ambos'],
            ['local', 'So Local'],
            ['r2', 'So R2'],
          ].map(([value, label]) => (
            <button
              key={value}
              type="button"
              onClick={() => setLocationFilter(value as BackupLocationFilter)}
              className={`rounded-md px-3 py-1.5 transition ${
                locationFilter === value
                  ? 'bg-brand-600 text-white'
                  : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800'
              }`}
            >
              {label}
            </button>
          ))}
        </div>
        <p className="text-xs text-zinc-500">{allItems.length} backup(s), ordenados por data desc.</p>
      </div>

      <BackupTable items={allItems} loading={isLoading} error={isError} onRestore={setRestoreTarget} />

      {restoreTarget && (
        <RestoreConfirmation
          backup={restoreTarget}
          preview={preview.data ?? null}
          loading={preview.isLoading}
          error={preview.isError}
          confirmation={restoreText}
          onConfirmationChange={setRestoreText}
          restoring={restore.isPending}
          onCancel={() => {
            if (!restore.isPending) {
              setRestoreTarget(null);
              setRestoreText('');
            }
          }}
          onRestore={() => restore.mutate()}
        />
      )}
    </div>
  );
}

function BackupMetric({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof DatabaseBackup;
  label: string;
  value: string;
}) {
  return (
    <div className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
      <div className="flex items-center gap-2 text-xs text-zinc-500">
        <Icon size={14} strokeWidth={2} />
        {label}
      </div>
      <div className="mt-1 text-lg font-semibold tabular-nums">{value}</div>
    </div>
  );
}

function BackupTable({
  items,
  loading,
  error,
  onRestore,
}: {
  items: BackupFileDto[];
  loading: boolean;
  error: boolean;
  onRestore: (item: BackupFileDto) => void;
}) {
  if (loading) {
    return (
      <div className="rounded-lg border border-zinc-200 dark:border-zinc-800">
        <SkeletonRow columns={2} />
        <SkeletonRow columns={2} />
      </div>
    );
  }
  if (error) return <p className="text-xs text-rose-600">Nao foi possivel obter a lista.</p>;
  if (items.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-zinc-300 p-8 text-center text-sm text-zinc-500 dark:border-zinc-700">
        Sem backups para este filtro. Usa "Forcar backup agora" para criar um snapshot manual.
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
      <table className="w-full min-w-[720px] text-left text-sm">
        <thead className="bg-zinc-50 text-xs text-zinc-500 dark:bg-zinc-950 dark:text-zinc-400">
          <tr>
            <th className="px-3 py-2 font-medium">Data</th>
            <th className="px-3 py-2 font-medium">Tamanho</th>
            <th className="px-3 py-2 font-medium">Localizacao</th>
            <th className="px-3 py-2 font-medium">Status</th>
            <th className="px-3 py-2 font-medium">Idade</th>
            <th className="px-3 py-2 text-right font-medium">Acao</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {items.map((b) => (
            <tr key={b.id} className="align-middle">
              <td className="px-3 py-2">
                <div className="font-mono text-xs">{b.fileName}</div>
                <div className="text-[11px] text-zinc-500">{new Date(b.timestamp).toLocaleString('pt-PT')}</div>
              </td>
              <td className="px-3 py-2 tabular-nums">{formatBytes(b.sizeBytes)}</td>
              <td className="px-3 py-2">
                <span className="inline-flex items-center gap-1 rounded-full bg-zinc-100 px-2 py-0.5 text-[11px] text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">
                  {backupLocationKey(b.location) === 'r2' ? <Cloud size={11} /> : <HardDrive size={11} />}
                  {backupLocationLabel(b.location)}
                </span>
              </td>
              <td className="px-3 py-2">
                <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${backupStatusClass(b.status)}`}>
                  {backupStatusLabel(b.status)}
                </span>
              </td>
              <td className="px-3 py-2 text-zinc-600 dark:text-zinc-300">{formatBackupAge(b.ageHours)}</td>
              <td className="px-3 py-2 text-right">
                <button
                  type="button"
                  onClick={() => onRestore(b)}
                  className="inline-flex min-h-9 items-center gap-1 rounded-lg border border-rose-200 px-2.5 py-1.5 text-xs font-medium text-rose-700 hover:bg-rose-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-400 dark:border-rose-900/70 dark:text-rose-300 dark:hover:bg-rose-950/30"
                >
                  <RotateCcw size={13} /> Restore
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function RestoreConfirmation({
  backup,
  preview,
  loading,
  error,
  confirmation,
  onConfirmationChange,
  restoring,
  onCancel,
  onRestore,
}: {
  backup: BackupFileDto;
  preview: Awaited<ReturnType<typeof backupApi.restorePreview>> | null;
  loading: boolean;
  error: boolean;
  confirmation: string;
  onConfirmationChange: (value: string) => void;
  restoring: boolean;
  onCancel: () => void;
  onRestore: () => void;
}) {
  const canRestore = confirmation.trim() === 'RESTORE' && !loading && !error && !restoring;
  const backupSnapshot = preview?.backupSnapshot ?? backup.snapshot;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-zinc-950/60 p-4">
      <div className="w-full max-w-2xl rounded-xl border border-zinc-200 bg-white p-5 shadow-xl dark:border-zinc-800 dark:bg-zinc-950">
        <div className="flex items-start gap-3">
          <div className="rounded-lg bg-rose-100 p-2 text-rose-700 dark:bg-rose-950/50 dark:text-rose-300">
            <AlertTriangle size={20} strokeWidth={2} />
          </div>
          <div className="flex-1">
            <h3 className="text-base font-semibold">Confirmar restore</h3>
            <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
              Vais SUBSTITUIR os dados actuais. Esta accao NAO PODE ser desfeita.
            </p>
          </div>
        </div>

        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <SnapshotPanel
            title="Estado actual"
            subtitle={preview?.currentSnapshot ? new Date(preview.currentSnapshot.capturedAt).toLocaleString('pt-PT') : 'A calcular...'}
            snapshot={preview?.currentSnapshot ?? null}
            loading={loading}
          />
          <SnapshotPanel
            title={`Backup ${new Date(backup.timestamp).toLocaleString('pt-PT')}`}
            subtitle={`${backupLocationLabel(backup.location)} - ${formatBytes(backup.sizeBytes)}`}
            snapshot={backupSnapshot ?? null}
            loading={loading}
            missingText="Este backup nao tem metadados de contagem. O restore ainda e possivel, mas valida primeiro pelo timestamp."
          />
        </div>

        {error && (
          <p className="mt-3 rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700 dark:border-rose-900/60 dark:bg-rose-950/30 dark:text-rose-300">
            Nao foi possivel preparar a pre-visualizacao. Confirma as permissoes: restore exige Admin + SuperAdmin.
          </p>
        )}

        <label className="mt-4 block">
          <span className="mb-1 block text-xs font-medium text-zinc-600 dark:text-zinc-400">
            Escreve RESTORE para confirmar
          </span>
          <input
            value={confirmation}
            onChange={(e) => onConfirmationChange(e.target.value)}
            className={`${inputCls} font-mono`}
            placeholder="RESTORE"
            autoFocus
          />
        </label>

        <div className="mt-5 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            disabled={restoring}
            className="min-h-11 rounded-lg border border-zinc-200 px-3 py-2 text-sm text-zinc-700 hover:bg-zinc-50 disabled:opacity-60 dark:border-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-900"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={onRestore}
            disabled={!canRestore}
            className="inline-flex min-h-11 items-center gap-1 rounded-lg bg-rose-600 px-3 py-2 text-sm font-medium text-white hover:bg-rose-700 disabled:opacity-50"
          >
            {restoring ? <Loader2 size={15} className="animate-spin" /> : <RotateCcw size={15} />}
            Restaurar backup
          </button>
        </div>
      </div>
    </div>
  );
}

function SnapshotPanel({
  title,
  subtitle,
  snapshot,
  loading,
  missingText = 'Sem dados.',
}: {
  title: string;
  subtitle: string;
  snapshot: BackupSnapshotDto | null;
  loading: boolean;
  missingText?: string;
}) {
  return (
    <div className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
      <p className="text-sm font-medium">{title}</p>
      <p className="text-[11px] text-zinc-500">{subtitle}</p>
      {loading ? (
        <div className="mt-3 rounded-lg border border-zinc-200 dark:border-zinc-800">
          <SkeletonRow columns={2} />
        </div>
      ) : snapshot ? (
        <dl className="mt-3 grid grid-cols-2 gap-2 text-xs">
          <SnapshotStat label="Reparacoes" value={snapshot.reparacoes} />
          <SnapshotStat label="Clientes" value={snapshot.clientes} />
          <SnapshotStat label="Trabalhos" value={snapshot.trabalhos} />
          <SnapshotStat label="Vendas" value={snapshot.vendas} />
          <SnapshotStat label="Despesas" value={snapshot.despesas} />
        </dl>
      ) : (
        <p className="mt-3 text-xs text-amber-700 dark:text-amber-400">{missingText}</p>
      )}
    </div>
  );
}

function SnapshotStat({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <dt className="text-zinc-500">{label}</dt>
      <dd className="font-semibold tabular-nums">{value}</dd>
    </div>
  );
}

function normalizeBackupHealth(value: BackupHealthStatus | undefined) {
  if (value === 'Green' || value === 0) {
    return {
      label: 'Saudavel: ultimo backup recente',
      Icon: ShieldCheck,
      panelClass: 'border-emerald-200 bg-emerald-50/60 dark:border-emerald-900/50 dark:bg-emerald-950/20',
      iconClass: 'text-emerald-600 dark:text-emerald-400',
      textClass: 'text-emerald-700 dark:text-emerald-400',
    };
  }
  if (value === 'Yellow' || value === 1) {
    return {
      label: 'Atencao: backup a aproximar-se do limite',
      Icon: AlertTriangle,
      panelClass: 'border-amber-200 bg-amber-50/60 dark:border-amber-900/50 dark:bg-amber-950/20',
      iconClass: 'text-amber-600 dark:text-amber-400',
      textClass: 'text-amber-700 dark:text-amber-400',
    };
  }
  return {
    label: 'Critico: sem backup recente ou ultimo falhou',
    Icon: AlertTriangle,
    panelClass: 'border-rose-200 bg-rose-50/60 dark:border-rose-900/50 dark:bg-rose-950/20',
    iconClass: 'text-rose-600 dark:text-rose-400',
    textClass: 'text-rose-700 dark:text-rose-400',
  };
}

function backupLocationKey(location: BackupFileDto['location']): 'local' | 'r2' {
  return location === 'R2' || location === 1 ? 'r2' : 'local';
}

function backupLocationLabel(location: BackupFileDto['location']): string {
  return backupLocationKey(location) === 'r2' ? 'R2' : 'Local';
}

function backupStatusLabel(status: string): string {
  return status.toLowerCase() === 'ok' || status.toLowerCase() === 'available' ? 'OK' : 'Falhado';
}

function backupStatusClass(status: string): string {
  return backupStatusLabel(status) === 'OK'
    ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300'
    : 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300';
}

function formatBackupAge(hours: number | null | undefined): string {
  if (hours == null) return 'Sem backups';
  if (hours < 1) return `${Math.max(1, Math.round(hours * 60))} min`;
  if (hours < 48) return `${Math.round(hours)} h`;
  return `${Math.round(hours / 24)} dias`;
}

export function BackupsSectionLegacy() {
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
            className="inline-flex min-h-11 items-center gap-1 rounded-lg bg-brand-600 px-3 py-2 text-xs font-medium text-white hover:bg-brand-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-60"
          >
            {runNow.isPending ? 'A correr…' : 'Correr backup agora'}
          </button>
          <button
            type="button"
            onClick={() => refetch()}
            className="min-h-11 rounded-lg border border-zinc-200 px-3 py-2 text-xs text-zinc-600 hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
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

export function BackupListSection({ title, items, loading, error }: { title: string; items: ReturnType<typeof backupApi.list> extends Promise<infer T> ? T extends { local: infer L } ? L : never : never; loading: boolean; error: boolean }) {
  return (
    <div>
      <h3 className="mb-2 text-sm font-semibold">{title} <span className="text-xs font-normal text-zinc-500">· {items.length}</span></h3>
      {loading ? (
        <div className="space-y-2 rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
          <SkeletonRow columns={2} />
          <SkeletonRow columns={2} />
        </div>
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
