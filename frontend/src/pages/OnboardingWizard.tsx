import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, Check, Circle, Store, UserPlus, Wrench, LayoutDashboard, Users, X } from 'lucide-react';
import { Button } from '../components/ui/Button';
import { Popover } from '../components/ui/Popover';
import { StatusBadge } from '../components/ui/StatusBadge';
import { useAuth } from '../lib/auth/AuthContext';
import { clientesApi } from '../lib/clientes/api';
import type { ClienteForm } from '../lib/clientes/types';
import { reparacoesApi } from '../lib/reparacoes/api';
import { REPAIR_STATUS, STATUS_LABEL } from '../lib/reparacoes/types';
import { tenantSettingsApi } from '../lib/tenantSettings/api';
import type { UpdateTenantSettings } from '../lib/tenantSettings/types';
import { toast } from '../lib/toast';

const TOTAL_STEPS = 5;

const steps = [
  { id: 1, title: 'Dados da empresa', icon: Store },
  { id: 2, title: 'Primeiro cliente', icon: UserPlus },
  { id: 3, title: 'Primeira reparação', icon: Wrench },
  { id: 4, title: 'Explorar dashboard', icon: LayoutDashboard },
  { id: 5, title: 'Equipa', icon: Users },
] as const;

const emptySettings: UpdateTenantSettings = {
  name: '',
  legalName: null,
  nif: null,
  address: null,
  postalCode: null,
  locality: null,
  country: 'PT',
  phone: null,
  email: null,
  website: null,
  iban: null,
  caePrincipal: null,
  caeSecundarios: null,
  regimeFiscal: 0,
  termosCondicoes: null,
  logoUrl: null,
  primaryColor: null,
  garantiaDiasDefault: 90,
  garantiaCoberturaDefault: null,
  garantiaExclusoesDefault: null,
  garantiaVendaDiasDefault: 1095,
  garantiaVendaOpenBoxDias: 730,
  garantiaVendaRecondicionadoDias: 540,
  garantiaVendaUsadoDias: 540,
  garantiaVendaCoberturaDefault: null,
  garantiaVendaExclusoesDefault: null,
  googleReviewUrl: null,
  retentionRejectedDays: 15,
  retentionFailedDays: 30,
  retentionApprovedPdfDays: null,
};

const emptyCliente: ClienteForm = {
  nome: '',
  telefone: null,
  email: null,
  nif: null,
  notas: null,
};

export default function OnboardingWizard() {
  const navigate = useNavigate();
  const qc = useQueryClient();
  const { user, hasRole } = useAuth();
  const [step, setStep] = useState(1);
  const [bootstrapped, setBootstrapped] = useState(false);
  const [settingsForm, setSettingsForm] = useState<UpdateTenantSettings>(emptySettings);
  const [clienteForm, setClienteForm] = useState<ClienteForm>(emptyCliente);
  const [clienteId, setClienteId] = useState<string | null>(null);
  const [reparacaoId, setReparacaoId] = useState<string | null>(null);
  const [repairForm, setRepairForm] = useState({
    equipamento: '',
    avaria: '',
    imei: '',
    orcamento: '',
    notas: '',
  });
  const [invite, setInvite] = useState({ modo: 'solo' as 'solo' | 'equipa', email: '', role: 'Tecnico' });

  const status = useQuery({
    queryKey: ['onboarding-status'],
    queryFn: () => tenantSettingsApi.onboardingStatus(),
    staleTime: 30_000,
  });

  const settings = useQuery({
    queryKey: ['tenant-settings'],
    queryFn: () => tenantSettingsApi.getMine(),
    staleTime: 60_000,
  });

  const clientes = useQuery({
    queryKey: ['clientes', 'onboarding'],
    queryFn: () => clientesApi.list('', 1, 10),
    staleTime: 30_000,
  });

  const existingClienteId = clienteId ?? clientes.data?.items[0]?.id ?? null;
  const isAdmin = hasRole('Admin');

  useEffect(() => {
    if (status.data?.onboardingCompletado) {
      navigate('/', { replace: true });
    }
  }, [navigate, status.data?.onboardingCompletado]);

  useEffect(() => {
    if (settings.data) {
      const { id: _id, onboardingCompletado: _onboardingCompletado, ...form } = settings.data;
      setSettingsForm(form);
    }
  }, [settings.data]);

  useEffect(() => {
    if (!bootstrapped && status.data) {
      setStep(Math.min(status.data.onboardingCompletado ? TOTAL_STEPS : status.data.currentStep, TOTAL_STEPS));
      setBootstrapped(true);
    }
  }, [bootstrapped, status.data]);

  const updateSettings = useMutation({
    mutationFn: (payload: UpdateTenantSettings) => tenantSettingsApi.updateMine(payload),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['tenant-settings'] });
      qc.invalidateQueries({ queryKey: ['onboarding-status'] });
    },
  });

  const createCliente = useMutation({
    mutationFn: (payload: ClienteForm) => clientesApi.create(payload),
    onSuccess: (cliente) => {
      setClienteId(cliente.id);
      qc.invalidateQueries({ queryKey: ['clientes'] });
      qc.invalidateQueries({ queryKey: ['onboarding-status'] });
    },
  });

  const createRepair = useMutation({
    mutationFn: (payload: { clienteId: string; demo?: boolean }) =>
      reparacoesApi.create({
        clienteId: payload.clienteId,
        equipamento: payload.demo ? 'iPhone 11' : repairForm.equipamento,
        avaria: payload.demo ? 'Ecrã partido' : repairForm.avaria,
        imei: payload.demo ? null : clean(repairForm.imei),
        orcamentoCents: payload.demo ? 8900 : parseEuroToCents(repairForm.orcamento),
        notas: payload.demo
          ? 'Demo criada automaticamente pelo onboarding. Pode ser apagada quando quiseres.'
          : clean(repairForm.notas),
        estadoInicial: REPAIR_STATUS.Recebido,
      }),
    onSuccess: (rep) => {
      setReparacaoId(rep.id);
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      qc.invalidateQueries({ queryKey: ['onboarding-status'] });
    },
  });

  const complete = useMutation({
    mutationFn: () => tenantSettingsApi.completeOnboarding(),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['onboarding-status'] });
      navigate('/', { replace: true });
    },
  });

  async function saveCompany(e: FormEvent) {
    e.preventDefault();
    if (!settingsForm.name.trim()) {
      toast.warning('O nome da loja é obrigatório.');
      return;
    }
    try {
      await updateSettings.mutateAsync({ ...settingsForm, name: settingsForm.name.trim() });
      toast.success('Dados da loja guardados.');
      setStep(2);
    } catch (err) {
      toast.fromError(err, 'Não foi possível guardar os dados da empresa.');
    }
  }

  async function createRealCliente(e: FormEvent) {
    e.preventDefault();
    if (!clienteForm.nome.trim()) {
      toast.warning('O nome do cliente é obrigatório.');
      return;
    }
    try {
      await createCliente.mutateAsync({ ...clienteForm, nome: clienteForm.nome.trim() });
      toast.success('Cliente criado.', 'Agora vamos registar a primeira reparação.');
      setStep(3);
    } catch (err) {
      toast.fromError(err, 'Não foi possível criar o cliente.');
    }
  }

  async function createDemoCliente(): Promise<string | null> {
    try {
      const cliente = await createCliente.mutateAsync({
        nome: 'Cliente Demo',
        telefone: null,
        email: null,
        nif: null,
        notas: 'Criado automaticamente pelo onboarding. Pode ser apagado.',
      });
      toast.success('Cliente demo criado.');
      setStep(3);
      return cliente.id;
    } catch (err) {
      toast.fromError(err, 'Não foi possível criar o cliente demo.');
      return null;
    }
  }

  async function createFirstRepair(e?: FormEvent) {
    e?.preventDefault();
    if (!existingClienteId) {
      toast.warning('Cria ou escolhe um cliente antes da reparação.');
      setStep(2);
      return;
    }
    if (!repairForm.equipamento.trim() || !repairForm.avaria.trim()) {
      toast.warning('Equipamento e avaria são obrigatórios.');
      return;
    }
    try {
      await createRepair.mutateAsync({ clienteId: existingClienteId });
      toast.success('Reparação criada.', 'A loja já deixou de estar vazia.');
      setStep(4);
    } catch (err) {
      toast.fromError(err, 'Não foi possível criar a reparação.');
    }
  }

  async function createDemoRepair() {
    let id = existingClienteId;
    if (!id) id = await createDemoCliente();
    if (!id) return;
    try {
      await createRepair.mutateAsync({ clienteId: id, demo: true });
      toast.success('Reparação demo criada.');
      setStep(4);
    } catch (err) {
      toast.fromError(err, 'Não foi possível criar a reparação demo.');
    }
  }

  function skipCurrentStep() {
    if (step === 2) {
      createDemoCliente();
      return;
    }
    if (step === 3) {
      createDemoRepair();
      return;
    }
    if (step === 5) {
      complete.mutate();
      return;
    }
    setStep((s) => Math.min(s + 1, TOTAL_STEPS));
  }

  const progressPct = useMemo(() => Math.round((step / TOTAL_STEPS) * 100), [step]);

  if (!isAdmin) {
    return (
      <Shell step={step} progressPct={progressPct} onExit={() => complete.mutate()} exiting={complete.isPending}>
        <div className="rounded-xl border border-zinc-200 bg-white p-6 text-center dark:border-zinc-800 dark:bg-zinc-900">
          <h1 className="text-xl font-semibold">O arranque da loja é para admins</h1>
          <p className="mt-2 text-sm text-zinc-500">
            A tua conta está pronta para usar. Pede a um admin para terminar as definições iniciais da loja.
          </p>
          <Button className="mt-5" onClick={() => navigate('/')}>Ir para o dashboard</Button>
        </div>
      </Shell>
    );
  }

  if (status.isLoading || settings.isLoading) {
    return (
      <Shell step={step} progressPct={progressPct} onExit={() => complete.mutate()} exiting={complete.isPending}>
        <div className="rounded-xl border border-zinc-200 bg-white p-6 text-sm text-zinc-500 dark:border-zinc-800 dark:bg-zinc-900">
          A preparar o arranque da loja...
        </div>
      </Shell>
    );
  }

  return (
    <Shell step={step} progressPct={progressPct} onExit={() => complete.mutate()} exiting={complete.isPending}>
      <div className="grid gap-5 lg:grid-cols-[260px_1fr]">
        <aside className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
          <p className="text-xs font-medium uppercase tracking-wide text-zinc-500">Arranque da loja</p>
          <div className="mt-4 space-y-2">
            {steps.map((s) => {
              const Icon = s.icon;
              const done = step > s.id || (s.id === 1 && status.data?.empresaCompleta) || (s.id === 2 && status.data?.clienteCriado) || (s.id === 3 && status.data?.reparacaoCriada);
              return (
                <button
                  key={s.id}
                  type="button"
                  onClick={() => setStep(s.id)}
                  className={`flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm transition ${
                    step === s.id
                      ? 'bg-brand-50 text-brand-700 dark:bg-zinc-800 dark:text-brand-300'
                      : 'text-zinc-600 hover:bg-zinc-50 dark:text-zinc-300 dark:hover:bg-zinc-800'
                  }`}
                >
                  <span className="grid h-7 w-7 place-items-center rounded-full bg-white ring-1 ring-zinc-200 dark:bg-zinc-950 dark:ring-zinc-700">
                    {done ? <Check size={15} /> : <Icon size={15} />}
                  </span>
                  <span>{s.title}</span>
                </button>
              );
            })}
          </div>
        </aside>

        <section className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-900 sm:p-6">
          <div className="mb-5 flex flex-wrap items-center justify-between gap-2">
            <div>
              <StatusBadge tone="blue">Passo {step} de {TOTAL_STEPS}</StatusBadge>
              <h1 className="mt-2 text-2xl font-semibold tracking-tight">{steps[step - 1]?.title}</h1>
            </div>
            <Button variant="ghost" size="sm" onClick={skipCurrentStep} loading={createCliente.isPending || createRepair.isPending || complete.isPending}>
              {step === 5 ? 'Fazer isto depois' : 'Saltar por agora'}
            </Button>
          </div>

          {step === 1 && (
            <CompanyStep
              form={settingsForm}
              setForm={setSettingsForm}
              onSubmit={saveCompany}
              loading={updateSettings.isPending}
            />
          )}
          {step === 2 && (
            <ClienteStep
              form={clienteForm}
              setForm={setClienteForm}
              onSubmit={createRealCliente}
              onDemo={createDemoCliente}
              loading={createCliente.isPending}
            />
          )}
          {step === 3 && (
            <RepairStep
              form={repairForm}
              setForm={setRepairForm}
              clienteNome={clientes.data?.items.find((c) => c.id === existingClienteId)?.nome ?? 'Cliente seleccionado'}
              hasCliente={Boolean(existingClienteId)}
              onSubmit={createFirstRepair}
              onDemo={createDemoRepair}
              loading={createRepair.isPending}
            />
          )}
          {step === 4 && (
            <DashboardTourStep
              reparacaoId={reparacaoId}
              onContinue={() => setStep(5)}
            />
          )}
          {step === 5 && (
            <TeamStep
              invite={invite}
              setInvite={setInvite}
              onFinish={() => complete.mutate()}
              loading={complete.isPending}
              userName={user?.displayName ?? 'Bruno'}
            />
          )}
        </section>
      </div>
    </Shell>
  );
}

function Shell({
  step,
  progressPct,
  children,
  onExit,
  exiting,
}: {
  step: number;
  progressPct: number;
  children: ReactNode;
  onExit: () => void;
  exiting?: boolean;
}) {
  return (
    <div className="min-h-screen bg-zinc-50 px-4 py-5 text-zinc-900 dark:bg-zinc-950 dark:text-zinc-100 sm:py-8">
      <div className="mx-auto max-w-5xl">
        <header className="mb-5 flex flex-wrap items-center justify-between gap-3">
          <div>
            <div className="flex items-center gap-2 font-semibold">
              <span className="grid h-8 w-8 place-items-center rounded-lg bg-brand-50 text-brand-600 dark:bg-zinc-900">●</span>
              Mender
            </div>
            <p className="mt-1 text-sm text-zinc-500">Em menos de meia hora, ficas com a primeira reparação registada.</p>
          </div>
          <Button variant="ghost" size="sm" onClick={onExit} loading={exiting} leftIcon={<X size={14} />}>
            Sair do wizard
          </Button>
        </header>
        <div className="mb-5">
          <div className="mb-1 flex items-center justify-between text-xs text-zinc-500">
            <span>Passo {step} de {TOTAL_STEPS}</span>
            <span>{progressPct}%</span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-zinc-200 dark:bg-zinc-800">
            <div className="h-full rounded-full bg-brand-600 transition-all duration-300" style={{ width: `${progressPct}%` }} />
          </div>
        </div>
        <div className="transition-opacity duration-150">{children}</div>
      </div>
    </div>
  );
}

function CompanyStep({
  form,
  setForm,
  onSubmit,
  loading,
}: {
  form: UpdateTenantSettings;
  setForm: (form: UpdateTenantSettings) => void;
  onSubmit: (e: FormEvent) => void;
  loading: boolean;
}) {
  function update<K extends keyof UpdateTenantSettings>(key: K, value: UpdateTenantSettings[K]) {
    setForm({ ...form, [key]: value });
  }

  return (
    <form onSubmit={onSubmit} className="grid gap-5 lg:grid-cols-[1fr_280px]">
      <div className="space-y-4">
        <p className="text-sm text-zinc-500">Isto aparece nos orçamentos, fichas e mensagens. Só o nome da loja é obrigatório.</p>
        <Field label="Nome da loja *">
          <input className={inputCls} value={form.name} onChange={(e) => update('name', e.target.value)} autoFocus />
        </Field>
        <Field label="Nome legal">
          <input className={inputCls} value={form.legalName ?? ''} onChange={(e) => update('legalName', clean(e.target.value))} />
        </Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="NIF">
            <input className={inputCls} value={form.nif ?? ''} onChange={(e) => update('nif', clean(e.target.value))} inputMode="numeric" />
          </Field>
          <Field label="IBAN">
            <input className={inputCls} value={form.iban ?? ''} onChange={(e) => update('iban', clean(e.target.value))} placeholder="PT50..." />
          </Field>
        </div>
        <Field label="Logo URL">
          <input className={inputCls} value={form.logoUrl ?? ''} onChange={(e) => update('logoUrl', clean(e.target.value))} placeholder="https://..." />
        </Field>
        <div className="flex flex-wrap gap-2">
          <Button type="submit" loading={loading} rightIcon={<ArrowRight size={15} />}>Guardar e continuar</Button>
        </div>
      </div>
      <PreviewCard form={form} />
    </form>
  );
}

function PreviewCard({ form }: { form: UpdateTenantSettings }) {
  return (
    <div className="rounded-xl border border-zinc-200 bg-zinc-50 p-4 dark:border-zinc-800 dark:bg-zinc-950">
      <p className="text-xs font-medium uppercase tracking-wide text-zinc-500">Preview do documento</p>
      <div className="mt-4 rounded-lg border border-zinc-200 bg-white p-4 text-sm shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
        <div className="font-semibold">{form.name || 'Nome da loja'}</div>
        <div className="mt-1 text-xs text-zinc-500">{form.nif ? `NIF ${form.nif}` : 'NIF por preencher'}</div>
        <div className="mt-4 rounded-md bg-zinc-100 p-3 text-xs text-zinc-500 dark:bg-zinc-800">
          Orçamento #001<br />
          {form.iban ? `IBAN ${form.iban}` : 'IBAN opcional'}
        </div>
      </div>
      <p className="mt-3 text-xs text-zinc-500">Podes alterar tudo mais tarde em Definições.</p>
    </div>
  );
}

function ClienteStep({
  form,
  setForm,
  onSubmit,
  onDemo,
  loading,
}: {
  form: ClienteForm;
  setForm: (form: ClienteForm) => void;
  onSubmit: (e: FormEvent) => void;
  onDemo: () => void;
  loading: boolean;
}) {
  function update<K extends keyof ClienteForm>(key: K, value: ClienteForm[K]) {
    setForm({ ...form, [key]: value });
  }

  return (
    <form onSubmit={onSubmit} className="space-y-4">
      <p className="text-sm text-zinc-500">
        Tens uma reparação em mãos? Usa um cliente real. Se só queres testar, criamos uma demo que podes apagar.
      </p>
      <Field label="Nome *">
        <input className={inputCls} value={form.nome} onChange={(e) => update('nome', e.target.value)} autoFocus />
      </Field>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Telefone">
          <input className={inputCls} value={form.telefone ?? ''} onChange={(e) => update('telefone', clean(e.target.value))} />
        </Field>
        <Field label="NIF">
          <input className={inputCls} value={form.nif ?? ''} onChange={(e) => update('nif', clean(e.target.value))} />
        </Field>
      </div>
      <Field label="Email">
        <input className={inputCls} type="email" value={form.email ?? ''} onChange={(e) => update('email', clean(e.target.value))} />
      </Field>
      <div className="flex flex-wrap gap-2">
        <Button type="submit" loading={loading}>Criar cliente e continuar</Button>
        <Button type="button" variant="secondary" onClick={onDemo} loading={loading}>Criar demo</Button>
      </div>
    </form>
  );
}

function RepairStep({
  form,
  setForm,
  clienteNome,
  hasCliente,
  onSubmit,
  onDemo,
  loading,
}: {
  form: { equipamento: string; avaria: string; imei: string; orcamento: string; notas: string };
  setForm: (form: { equipamento: string; avaria: string; imei: string; orcamento: string; notas: string }) => void;
  clienteNome: string;
  hasCliente: boolean;
  onSubmit: (e: FormEvent) => void;
  onDemo: () => void;
  loading: boolean;
}) {
  function update(key: keyof typeof form, value: string) {
    setForm({ ...form, [key]: value });
  }

  return (
    <form onSubmit={onSubmit} className="grid gap-5 lg:grid-cols-[1fr_280px]">
      <div className="space-y-4">
        {!hasCliente && (
          <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-200">
            Ainda não há cliente. Cria um cliente no passo anterior ou usa a demo.
          </div>
        )}
        <Field label="Cliente">
          <input className={inputCls} value={clienteNome} disabled />
        </Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Equipamento *">
            <input className={inputCls} value={form.equipamento} onChange={(e) => update('equipamento', e.target.value)} autoFocus />
          </Field>
          <Field label="IMEI/Serial">
            <input className={inputCls} value={form.imei} onChange={(e) => update('imei', e.target.value)} />
          </Field>
        </div>
        <Field label="Avaria *">
          <textarea className={`${inputCls} min-h-24 py-2`} value={form.avaria} onChange={(e) => update('avaria', e.target.value)} />
        </Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Orçamento estimado">
            <input className={inputCls} value={form.orcamento} onChange={(e) => update('orcamento', e.target.value)} placeholder="89,00" inputMode="decimal" />
          </Field>
          <Field label="Estado inicial">
            <input className={inputCls} value={STATUS_LABEL[REPAIR_STATUS.Recebido]} disabled />
          </Field>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="submit" loading={loading}>Criar reparação</Button>
          <Button type="button" variant="secondary" onClick={onDemo} loading={loading}>Usar reparação demo</Button>
        </div>
      </div>
      <div className="rounded-xl border border-zinc-200 bg-zinc-50 p-4 dark:border-zinc-800 dark:bg-zinc-950">
        <p className="text-xs font-medium uppercase tracking-wide text-zinc-500">Preview</p>
        <div className="mt-4 rounded-lg bg-white p-4 text-sm shadow-sm ring-1 ring-zinc-200 dark:bg-zinc-900 dark:ring-zinc-800">
          <div className="flex items-center gap-2">
            <StatusBadge tone="amber">Recebido</StatusBadge>
            <span className="font-mono text-xs text-zinc-500">#001</span>
          </div>
          <div className="mt-3 font-semibold">{form.equipamento || 'iPhone 12'}</div>
          <div className="mt-1 text-xs text-zinc-500">{clienteNome}</div>
          <p className="mt-3 text-xs text-zinc-600 dark:text-zinc-300">{form.avaria || 'Ecrã partido e touch falha'}</p>
        </div>
      </div>
    </form>
  );
}

function DashboardTourStep({ reparacaoId, onContinue }: { reparacaoId: string | null; onContinue: () => void }) {
  const [tip, setTip] = useState(0);
  const tips = [
    'No dashboard real, aqui aparecem as reparações que ainda precisam de atenção. Quando o balcão estiver cheio, começa sempre por aqui.',
    'Ajuda-te a não esquecer trabalhos já concluídos que ainda não foram pagos. O dashboard separa receita realizada de receita pendente — sem auto-engano.',
    'Cada reparação fica acessível pela lista, pelo kanban e pelo dashboard. Clica para entrar, mudar de estado, acrescentar fotos ou imprimir o orçamento.',
  ];

  return (
    <div className="space-y-4">
      <p className="text-sm text-zinc-500">
        Os números abaixo são <strong>apenas exemplos</strong> para te mostrar como o dashboard se comporta.
        Clica em cada card para entenderes o que ali vai aparecer no dia-a-dia.
      </p>
      <div className="relative rounded-xl border border-zinc-200 bg-zinc-50 p-4 pb-28 dark:border-zinc-800 dark:bg-zinc-950 sm:pb-24">
        <div className="grid gap-3 sm:grid-cols-3">
          <TourCard active={tip === 0} title="Em curso (exemplo)" value="3 reparações" tip={tips[0]} index={1} onClick={() => setTip(0)} />
          <TourCard active={tip === 1} title="Receita pendente (exemplo)" value="240,00 €" tip={tips[1]} index={2} onClick={() => setTip(1)} />
          <TourCard active={tip === 2} title="Acesso rápido" value="Abrir reparação" tip={tips[2]} index={3} onClick={() => setTip(2)} />
        </div>
      </div>
      <div className="flex flex-wrap gap-2">
        {reparacaoId && (
          <Link to={`/reparacoes/${reparacaoId}`}>
            <Button variant="secondary" type="button">Ver a minha reparação</Button>
          </Link>
        )}
        <Button onClick={onContinue} rightIcon={<ArrowRight size={15} />}>Continuar</Button>
      </div>
    </div>
  );
}

function TourCard({
  active,
  title,
  value,
  tip,
  index,
  onClick,
}: {
  active: boolean;
  title: string;
  value: string;
  tip: string;
  index: number;
  onClick: () => void;
}) {
  return (
    <Popover
      open={active}
      content={(
        <div className="flex items-start gap-3">
          <Circle className="mt-0.5 flex-none text-brand-500" size={14} fill="currentColor" />
          <div>
            <p className="font-medium">Dica {index} de 3</p>
            <p className="mt-1 text-zinc-600 dark:text-zinc-300">{tip}</p>
          </div>
        </div>
      )}
    >
      <button
        type="button"
        onClick={onClick}
        className={`w-full rounded-lg border p-3 text-left transition ${
          active
            ? 'border-brand-300 bg-brand-50 dark:border-brand-800 dark:bg-brand-950/30'
            : 'border-zinc-200 bg-white hover:bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900 dark:hover:bg-zinc-800'
        }`}
      >
        <div className="text-xs uppercase tracking-wide text-zinc-500">{title}</div>
        <div className="mt-1 text-lg font-semibold">{value}</div>
      </button>
    </Popover>
  );
}

function TeamStep({
  invite,
  setInvite,
  onFinish,
  loading,
  userName,
}: {
  invite: { modo: 'solo' | 'equipa'; email: string; role: string };
  setInvite: (invite: { modo: 'solo' | 'equipa'; email: string; role: string }) => void;
  onFinish: () => void;
  loading: boolean;
  userName: string;
}) {
  function finish() {
    if (invite.modo === 'equipa' && invite.email.trim()) {
      toast.success('Email guardado.', 'Avisamos-te assim que o convite puder ser enviado.');
    }
    onFinish();
  }

  return (
    <div className="space-y-4">
      <p className="text-sm text-zinc-500">Trabalhas sozinho ou queres preparar a loja para mais alguém?</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <button
          type="button"
          onClick={() => setInvite({ ...invite, modo: 'solo' })}
          className={`rounded-xl border p-4 text-left transition ${invite.modo === 'solo' ? selectedCls : unselectedCls}`}
        >
          <div className="font-semibold">Trabalho sozinho</div>
          <p className="mt-1 text-sm text-zinc-500">Perfeito, {userName}. Podes adicionar funcionários a qualquer altura nas definições.</p>
        </button>
        <button
          type="button"
          onClick={() => setInvite({ ...invite, modo: 'equipa' })}
          className={`rounded-xl border p-4 text-left transition ${invite.modo === 'equipa' ? selectedCls : unselectedCls}`}
        >
          <div className="font-semibold">Vou trabalhar acompanhado</div>
          <p className="mt-1 text-sm text-zinc-500">Deixa o email apontado e damos-te sinal quando o convite ficar pronto.</p>
        </button>
      </div>
      {invite.modo === 'equipa' && (
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Email do colega">
            <input className={inputCls} type="email" placeholder="ex: joana@oficina.pt" value={invite.email} onChange={(e) => setInvite({ ...invite, email: e.target.value })} />
          </Field>
          <Field label="Permissão prevista">
            <select className={inputCls} value={invite.role} onChange={(e) => setInvite({ ...invite, role: e.target.value })}>
              <option value="Tecnico">Técnico</option>
              <option value="Admin">Admin</option>
            </select>
          </Field>
        </div>
      )}
      <Button onClick={finish} loading={loading} rightIcon={<Check size={15} />}>Terminar arranque</Button>
    </div>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium text-zinc-600 dark:text-zinc-300">{label}</span>
      {children}
    </label>
  );
}

function clean(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

function parseEuroToCents(value: string): number | null {
  const cleanValue = value.trim().replace(/\s/g, '').replace(',', '.');
  if (!cleanValue) return null;
  const parsed = Number(cleanValue);
  if (!Number.isFinite(parsed) || parsed < 0) return null;
  return Math.round(parsed * 100);
}

const inputCls =
  'h-10 w-full rounded-lg border border-zinc-200 bg-white px-3 text-sm text-zinc-900 outline-none transition placeholder:text-zinc-400 focus:border-brand-400 focus:ring-2 focus:ring-brand-100 disabled:bg-zinc-100 disabled:text-zinc-500 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-100 dark:focus:ring-brand-950';

const selectedCls = 'border-brand-300 bg-brand-50 dark:border-brand-800 dark:bg-brand-950/30';
const unselectedCls = 'border-zinc-200 bg-white hover:bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900 dark:hover:bg-zinc-800';
