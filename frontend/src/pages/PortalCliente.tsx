import { useState, type ComponentType, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { isAxiosError } from 'axios';
import {
  AlertCircle,
  Bell,
  BellOff,
  Check,
  CheckCircle2,
  Frown,
  Image as ImageIcon,
  MessageCircle,
  Phone,
  Receipt,
  Search,
  ShieldCheck,
  Smartphone,
  Sparkles,
  Star,
  Wrench,
  X,
} from 'lucide-react';
import {
  ESTADO_DESC,
  ESTADO_LABEL,
  PUBLIC_ESTADO,
  STEPS,
  publicPortalApi,
  type AvaliacaoSubmittedDto,
  type PublicEstado,
  type PublicLoja,
  type PublicRepairDto,
  type PublicTimelineEntry,
} from '../lib/publicPortal/api';
import { usePushSubscription } from '../lib/publicPortal/usePushSubscription';
import { formatCents } from '../lib/money';

type IconCmp = ComponentType<{ className?: string; size?: number; strokeWidth?: number }>;

export default function PortalCliente() {
  const { slug } = useParams<{ slug: string }>();
  const qc = useQueryClient();

  const repair = useQuery({
    queryKey: ['public-repair', slug],
    queryFn: () => publicPortalApi.get(slug!),
    enabled: !!slug,
    retry: 0,
  });

  const decidir = useMutation({
    mutationFn: (aceitar: boolean) => publicPortalApi.aprovarOrcamento(slug!, aceitar),
    onSuccess: (data) => qc.setQueryData(['public-repair', slug], data),
  });

  if (repair.isLoading) {
    return (
      <div className="grid min-h-screen place-items-center bg-gradient-to-b from-zinc-50 to-white p-6 dark:from-zinc-950 dark:to-zinc-900">
        <div className="text-sm text-zinc-500">A carregar…</div>
      </div>
    );
  }

  if (repair.isError || !repair.data) {
    const status = isAxiosError(repair.error) ? repair.error.response?.status : undefined;
    const ErrorIcon = status === 429 ? AlertCircle : Frown;
    return (
      <div className="grid min-h-screen place-items-center bg-gradient-to-b from-zinc-50 to-white p-6 text-center dark:from-zinc-950 dark:to-zinc-900">
        <div className="max-w-sm">
          <div className="mx-auto grid h-16 w-16 place-items-center rounded-full bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">
            <ErrorIcon size={32} strokeWidth={1.75} />
          </div>
          <h1 className="mt-4 text-xl font-semibold">
            {status === 429 ? 'Demasiados pedidos' : 'Reparação não encontrada'}
          </h1>
          <p className="mt-2 text-sm text-zinc-500">
            {status === 429
              ? 'Tenta de novo dentro de uns minutos.'
              : 'O link parece estar partido ou expirou. Pede um novo à loja.'}
          </p>
        </div>
      </div>
    );
  }

  const data = repair.data;
  return (
    <div className="min-h-screen bg-gradient-to-b from-zinc-50 to-white pb-12 dark:from-zinc-950 dark:to-zinc-900">
      <div className="mx-auto max-w-2xl px-4">
        <Header loja={data.loja} />

        <Greeting nome={data.clientePrimeiroNome} estado={data.estado} />

        <EstadoCard data={data} />

        <PushNotificationsCard slug={data.slug} />

        <Timeline timeline={data.timeline} estadoActual={data.estado} />

        {data.estado === PUBLIC_ESTADO.Orcamento && data.orcamentoCents != null && (
          <OrcamentoCard
            valorCents={data.orcamentoCents}
            equipamento={data.equipamentoPublico}
            diagnostico={data.diagnostico}
            aprovado={data.orcamentoAprovado}
            decidir={decidir.mutate}
            pending={decidir.isPending}
          />
        )}

        {data.diagnostico && data.estado !== PUBLIC_ESTADO.Orcamento && (
          <Card titulo="Diagnóstico" icon={Search}>
            <p className="text-sm text-zinc-700 dark:text-zinc-300">{data.diagnostico}</p>
          </Card>
        )}

        <Card titulo="O teu equipamento" icon={Smartphone}>
          <div className="text-sm">
            <div className="font-medium">{data.equipamentoPublico}</div>
            <div className="mt-1 text-zinc-500">{data.avariaPublica}</div>
          </div>
          {data.camposEquipamento?.length > 0 && (
            <dl className="mt-3 grid grid-cols-1 gap-2 rounded-xl bg-zinc-50 p-3 text-sm dark:bg-zinc-950 sm:grid-cols-2">
              {data.camposEquipamento.map((field) => (
                <div key={`${field.label}-${field.ordem}`}>
                  <dt className="text-[11px] uppercase tracking-wide text-zinc-500">{field.label}</dt>
                  <dd className="font-medium text-zinc-800 dark:text-zinc-100">{field.value}</dd>
                </div>
              ))}
            </dl>
          )}
        </Card>

        {data.healthScore != null && (
          <HealthScoreCard score={data.healthScore} destaques={data.diagnosticoDestaques} />
        )}

        {data.fotos && data.fotos.length > 0 && (
          <FotosPublicCard fotos={data.fotos} />
        )}

        {data.estado === PUBLIC_ESTADO.Entregue && data.garantiaSlug && (
          <GarantiaCard slug={data.garantiaSlug} />
        )}

        {data.estado === PUBLIC_ESTADO.Entregue && !data.jaAvaliado && (
          <AvaliacaoCard slug={data.slug} />
        )}

        <ContactoLoja loja={data.loja} mensagemBase={`Olá! Sou ${data.clientePrimeiroNome}, queria saber novidades sobre o ${data.equipamentoPublico}.`} />

        <footer className="mt-10 space-y-1 text-center text-[11px] text-zinc-400">
          <div>Gerado pelo RepairDesk · LopesTech</div>
          <div className="flex justify-center gap-3">
            <a href="/privacidade" className="hover:text-zinc-600 dark:hover:text-zinc-300">Privacidade</a>
            <span aria-hidden>·</span>
            <a href="/termos" className="hover:text-zinc-600 dark:hover:text-zinc-300">Termos</a>
            <span aria-hidden>·</span>
            <a href="/cookies" className="hover:text-zinc-600 dark:hover:text-zinc-300">Cookies</a>
          </div>
        </footer>
      </div>
    </div>
  );
}

function PushNotificationsCard({ slug }: { slug: string }) {
  const push = usePushSubscription(slug);

  if (!push.supported || push.status === 'unsupported') return null;

  const isBusy = push.status === 'busy' || push.status === 'checking';
  const isSubscribed = push.status === 'subscribed';
  const denied = push.status === 'denied';

  return (
    <Card titulo="Notificações" icon={isSubscribed ? BellOff : Bell} tone={isSubscribed ? 'emerald' : undefined}>
      <p className="text-sm text-zinc-600 dark:text-zinc-300">
        {isSubscribed
          ? 'Avisamos-te quando a loja mudar o estado desta reparação.'
          : denied
            ? 'As notificações estão bloqueadas neste browser. Podes alterar isso nas definições do browser.'
            : 'Recebe um aviso quando houver novidades, sem precisares de actualizar a página.'}
      </p>
      {push.error && <p className="mt-2 text-xs text-red-600 dark:text-red-400">{push.error}</p>}
      {!denied && (
        <button
          type="button"
          onClick={isSubscribed ? push.unsubscribe : push.subscribe}
          disabled={isBusy}
          className="mt-3 inline-flex h-9 items-center justify-center rounded-xl bg-zinc-900 px-3 text-sm font-medium text-white transition hover:bg-zinc-700 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-white dark:text-zinc-900 dark:hover:bg-zinc-200"
        >
          {isBusy ? 'A tratar...' : isSubscribed ? 'Deixar de receber' : 'Receber notificações'}
        </button>
      )}
    </Card>
  );
}

function Header({ loja }: { loja: PublicLoja }) {
  return (
    <header className="flex items-center justify-between pt-6">
      <div className="flex items-center gap-3">
        {loja.logoUrl ? (
          <img src={loja.logoUrl} alt={loja.nome} className="h-10 w-10 rounded-lg object-cover" onError={(e) => ((e.target as HTMLImageElement).style.opacity = '0')} />
        ) : (
          <div className="grid h-10 w-10 place-items-center rounded-lg bg-brand-100 text-brand-700 dark:bg-brand-900/40 dark:text-brand-300">
            <Wrench size={18} strokeWidth={2} />
          </div>
        )}
        <div>
          <div className="text-sm font-semibold">{loja.nome}</div>
          {loja.website && (
            <a href={loja.website.startsWith('http') ? loja.website : `https://${loja.website}`} target="_blank" rel="noopener noreferrer" className="text-[11px] text-zinc-500 hover:underline">
              {loja.website.replace(/^https?:\/\//, '')}
            </a>
          )}
        </div>
      </div>
    </header>
  );
}

function Greeting({ nome, estado }: { nome: string; estado: PublicEstado }) {
  const celebrate = estado === PUBLIC_ESTADO.Pronto || estado === PUBLIC_ESTADO.Entregue;
  return (
    <section className="mt-6">
      <h1 className="flex items-center gap-2 text-3xl font-semibold tracking-tight">
        Olá {nome}!
        {celebrate && <Sparkles size={22} strokeWidth={2} className="text-amber-500" aria-hidden />}
      </h1>
      <p className="mt-1 text-sm text-zinc-500">Acompanha o estado da tua reparação aqui.</p>
    </section>
  );
}

function EstadoCard({ data }: { data: PublicRepairDto }) {
  const accentByEstado: Record<PublicEstado, string> = {
    0: 'from-amber-500/20 to-amber-500/5',
    1: 'from-blue-500/20 to-blue-500/5',
    2: 'from-blue-500/20 to-blue-500/5',
    3: 'from-blue-500/20 to-blue-500/5',
    4: 'from-amber-500/20 to-amber-500/5',
    5: 'from-emerald-500/30 to-emerald-500/5',
    6: 'from-emerald-500/30 to-emerald-500/5',
    7: 'from-zinc-400/30 to-zinc-400/10',
  };
  return (
    <section className={`mt-6 rounded-3xl border border-zinc-200/70 bg-gradient-to-br ${accentByEstado[data.estado]} p-6 shadow-sm backdrop-blur dark:border-zinc-800/70`}>
      <div className="text-[11px] uppercase tracking-wider text-zinc-500">Estado actual</div>
      <h2 className="mt-1 text-2xl font-semibold tracking-tight">{ESTADO_LABEL[data.estado]}</h2>
      <p className="mt-2 text-sm text-zinc-700 dark:text-zinc-300">{ESTADO_DESC[data.estado]}</p>
      {data.estado === PUBLIC_ESTADO.Pronto && data.temPrecoFinal && data.precoFinalCents != null && (
        <div className="mt-3 text-sm">
          <span className="text-zinc-500">Valor a pagar no levantamento: </span>
          <span className="font-semibold">{formatCents(data.precoFinalCents)}</span>
        </div>
      )}
    </section>
  );
}

function Timeline({ timeline, estadoActual }: { timeline: PublicTimelineEntry[]; estadoActual: PublicEstado }) {
  // Para cada step na progressão visual, encontrar se já passou (último log com esse estado)
  const stepDates = new Map<PublicEstado, string>();
  for (const entry of timeline) {
    if (!stepDates.has(entry.estado)) {
      stepDates.set(entry.estado, entry.mudouEm);
    }
  }
  // Estado actual define quão longe vai a barra
  const currentIndex = STEPS.indexOf(estadoActual);
  const isCancelado = estadoActual === PUBLIC_ESTADO.Cancelado;
  if (isCancelado) return null;

  return (
    <section className="mt-6">
      <h3 className="mb-3 text-sm font-semibold">Linha do tempo</h3>
      <ol className="relative space-y-3 border-l-2 border-zinc-200 pl-5 dark:border-zinc-800">
        {STEPS.map((step, i) => {
          const completed = currentIndex >= 0 && i <= currentIndex;
          const isCurrent = step === estadoActual;
          const date = stepDates.get(step);
          return (
            <li key={step} className="relative">
              <span
                className={`absolute -left-[26px] grid h-5 w-5 place-items-center rounded-full ring-4 ring-white dark:ring-zinc-900 ${
                  completed
                    ? isCurrent
                      ? 'bg-brand-600 text-white'
                      : 'bg-emerald-500 text-white'
                    : 'bg-zinc-300 dark:bg-zinc-700'
                }`}
              >
                {completed && (isCurrent ? <span className="h-1.5 w-1.5 rounded-full bg-white" /> : <Check size={11} strokeWidth={3} />)}
              </span>
              <div className={isCurrent ? 'font-semibold' : ''}>
                {ESTADO_LABEL[step]}
              </div>
              {date && (
                <div className="text-[11px] text-zinc-500">
                  {new Date(date).toLocaleString('pt-PT', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })}
                </div>
              )}
            </li>
          );
        })}
      </ol>
    </section>
  );
}

function OrcamentoCard({
  valorCents,
  equipamento,
  diagnostico,
  aprovado,
  decidir,
  pending,
}: {
  valorCents: number;
  equipamento: string;
  diagnostico: string | null;
  aprovado: boolean;
  decidir: (aceitar: boolean) => void;
  pending: boolean;
}) {
  if (aprovado) {
    return (
      <Card titulo="Orçamento aprovado" icon={CheckCircle2} tone="emerald">
        <p className="text-sm">Obrigado por aprovares! A loja vai começar a reparação.</p>
      </Card>
    );
  }
  return (
    <Card titulo="Orçamento" icon={Receipt} tone="amber">
      <p className="text-sm text-zinc-600 dark:text-zinc-300">
        Para reparar o teu <strong>{equipamento}</strong>, a loja propõe:
      </p>
      <div className="mt-2 text-3xl font-semibold tracking-tight">{formatCents(valorCents)}</div>
      {diagnostico && (
        <p className="mt-3 rounded-lg bg-white/60 p-3 text-sm text-zinc-700 dark:bg-zinc-900/60 dark:text-zinc-300">
          <span className="font-medium">Diagnóstico:</span> {diagnostico}
        </p>
      )}
      <div className="mt-4 grid grid-cols-2 gap-2">
        <button
          type="button"
          disabled={pending}
          onClick={() => decidir(true)}
          className="inline-flex items-center justify-center gap-1.5 rounded-xl bg-emerald-600 px-4 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-emerald-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-400 disabled:opacity-60"
        >
          {pending ? '…' : <><Check size={16} strokeWidth={2.5} /> Aceitar orçamento</>}
        </button>
        <button
          type="button"
          disabled={pending}
          onClick={() => decidir(false)}
          className="inline-flex items-center justify-center gap-1.5 rounded-xl border border-zinc-300 bg-white px-4 py-3 text-sm font-medium text-zinc-700 transition hover:bg-zinc-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-60 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-300 dark:hover:bg-zinc-900"
        >
          <X size={16} strokeWidth={2.5} /> Recusar
        </button>
      </div>
    </Card>
  );
}

function ContactoLoja({ loja, mensagemBase }: { loja: PublicLoja; mensagemBase: string }) {
  const tel = loja.telefone?.replace(/\s/g, '') ?? '';
  const wa = tel.startsWith('+') ? tel.slice(1) : tel;
  return (
    <Card titulo="Precisas de algo?" icon={MessageCircle}>
      <p className="text-sm text-zinc-600 dark:text-zinc-400">Fala directamente com a loja.</p>
      <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
        {wa && (
          <a
            href={`https://wa.me/${wa}?text=${encodeURIComponent(mensagemBase)}`}
            target="_blank" rel="noopener noreferrer"
            className="inline-flex items-center justify-center gap-2 rounded-xl bg-green-500 px-4 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-green-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-green-300"
          >
            <MessageCircle size={16} strokeWidth={2} /> WhatsApp
          </a>
        )}
        {tel && (
          <a
            href={`tel:${tel}`}
            className="inline-flex items-center justify-center gap-2 rounded-xl bg-emerald-600 px-4 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-emerald-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-300"
          >
            <Phone size={16} strokeWidth={2} /> Ligar
          </a>
        )}
      </div>
      {loja.email && (
        <a href={`mailto:${loja.email}`} className="mt-2 block text-center text-xs text-zinc-500 hover:underline">
          {loja.email}
        </a>
      )}
    </Card>
  );
}

function FotosPublicCard({ fotos }: { fotos: { id: string; tipo: number; legenda: string | null; criadaEm: string }[] }) {
  const [lightboxId, setLightboxId] = useState<string | null>(null);
  const groups: Array<{ tipo: number; label: string; items: typeof fotos }> = [
    { tipo: 0, label: 'Antes', items: fotos.filter((f) => f.tipo === 0) },
    { tipo: 2, label: 'Depois', items: fotos.filter((f) => f.tipo === 2) },
    { tipo: 1, label: 'Durante', items: fotos.filter((f) => f.tipo === 1) },
  ].filter((g) => g.items.length > 0);

  return (
    <Card titulo="Fotos da reparação" icon={ImageIcon}>
      <p className="mb-3 text-xs text-zinc-500">Para tu veres com transparência. Clica para ampliar.</p>
      {groups.map((g) => (
        <div key={g.tipo} className="mb-3">
          <h4 className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-zinc-500">{g.label} · {g.items.length}</h4>
          <ul className="grid grid-cols-3 gap-2">
            {g.items.map((f) => (
              <li key={f.id}>
                <button
                  type="button"
                  onClick={() => setLightboxId(f.id)}
                  className="block aspect-square w-full overflow-hidden rounded-lg border border-zinc-200 bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-950"
                >
                  <img
                    src={`/api/public/repair-photo/${f.id}`}
                    alt={f.legenda ?? g.label}
                    className="h-full w-full object-cover"
                    loading="lazy"
                  />
                </button>
                {f.legenda && <div className="mt-1 text-[10px] text-zinc-500 line-clamp-1">{f.legenda}</div>}
              </li>
            ))}
          </ul>
        </div>
      ))}
      {lightboxId && (
        <div
          onClick={() => setLightboxId(null)}
          className="fixed inset-0 z-50 grid place-items-center bg-black/85 p-4"
        >
          <img
            src={`/api/public/repair-photo/${lightboxId}`}
            alt="Foto ampliada"
            className="max-h-[85vh] max-w-full rounded-lg object-contain"
            onClick={(e) => e.stopPropagation()}
          />
          <button
            type="button"
            onClick={() => setLightboxId(null)}
            aria-label="Fechar"
            className="absolute right-4 top-4 grid h-9 w-9 place-items-center rounded-full bg-white/90 text-zinc-700 transition hover:bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-white"
          >
            <X size={18} strokeWidth={2} />
          </button>
        </div>
      )}
    </Card>
  );
}

function GarantiaCard({ slug }: { slug: string }) {
  return (
    <Card titulo="Garantia digital" icon={ShieldCheck} tone="emerald">
      <p className="text-sm text-zinc-600 dark:text-zinc-300">
        A tua reparação tem uma garantia digital. Podes verificá-la em qualquer momento:
      </p>
      <a
        href={`/g/${slug}`}
        target="_blank"
        rel="noopener noreferrer"
        className="mt-3 inline-flex items-center gap-2 rounded-xl bg-emerald-600 px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-emerald-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-300"
      >
        <ShieldCheck size={15} strokeWidth={2} /> Ver garantia
      </a>
      <p className="mt-2 text-[11px] text-zinc-500">Guarda este link — é a tua prova de garantia.</p>
    </Card>
  );
}

function AvaliacaoCard({ slug }: { slug: string }) {
  const [score, setScore] = useState<number>(0);
  const [comentario, setComentario] = useState('');
  const [resultado, setResultado] = useState<AvaliacaoSubmittedDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const submit = useMutation({
    mutationFn: () => publicPortalApi.submeterAvaliacao(slug, score, comentario.trim() || null, false),
    onSuccess: (r) => setResultado(r),
    onError: (err) => {
      if (isAxiosError(err)) setError(err.response?.data?.detail ?? 'Erro a submeter.');
    },
  });

  if (resultado) {
    return (
      <Card titulo="Obrigado pela tua avaliação!" icon={CheckCircle2} tone="emerald">
        <p className="text-sm text-zinc-700 dark:text-zinc-300">
          {resultado.score >= 4
            ? 'Ficamos contentes que tenhas gostado!'
            : 'Lamentamos não ter atingido as tuas expectativas. A loja vai contactar-te para perceber o que melhorar.'}
        </p>
        {resultado.googleReviewUrl && (
          <a
            href={resultado.googleReviewUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="mt-3 inline-flex items-center gap-2 rounded-xl bg-blue-600 px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-blue-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-300"
          >
            <Star size={15} strokeWidth={2} /> Deixa uma review no Google
          </a>
        )}
      </Card>
    );
  }

  return (
    <Card titulo="Como correu?" icon={Star} tone="amber">
      <p className="text-sm text-zinc-600 dark:text-zinc-300">
        A tua opinião ajuda a loja a melhorar e outros clientes a decidir.
      </p>
      <div className="mt-3 flex items-center justify-center gap-2 text-3xl">
        {[1, 2, 3, 4, 5].map((n) => (
          <button
            key={n}
            type="button"
            onClick={() => setScore(n)}
            className={`transition ${n <= score ? 'text-amber-500' : 'text-zinc-300 hover:text-amber-300 dark:text-zinc-600'}`}
            aria-label={`${n} estrelas`}
          >
            ★
          </button>
        ))}
      </div>
      {score > 0 && (
        <>
          <textarea
            rows={3}
            value={comentario}
            onChange={(e) => setComentario(e.target.value)}
            placeholder={score >= 4 ? 'O que correu bem? (opcional)' : 'O que podemos melhorar? (opcional)'}
            className="mt-3 w-full resize-none rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950"
          />
          {error && <div className="mt-2 text-xs text-rose-600">{error}</div>}
          <button
            type="button"
            disabled={submit.isPending}
            onClick={() => submit.mutate()}
            className="mt-3 w-full rounded-xl bg-brand-600 px-4 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-brand-700 disabled:opacity-60"
          >
            {submit.isPending ? 'A enviar…' : 'Enviar avaliação'}
          </button>
        </>
      )}
    </Card>
  );
}

function HealthScoreCard({ score, destaques }: { score: number; destaques: string[] }) {
  const tone = score >= 80 ? 'emerald' : score >= 50 ? 'amber' : 'rose';
  const toneCls =
    tone === 'emerald'
      ? 'from-emerald-500/25 to-emerald-500/5 text-emerald-700 dark:text-emerald-300'
      : tone === 'amber'
        ? 'from-amber-500/25 to-amber-500/5 text-amber-700 dark:text-amber-300'
        : 'from-rose-500/25 to-rose-500/5 text-rose-700 dark:text-rose-300';
  const msg = score >= 80
    ? 'Equipamento em bom estado geral.'
    : score >= 50
      ? 'Equipamento com alguns pontos a verificar.'
      : 'Equipamento precisa de atenção em vários pontos.';
  return (
    <section className={`mt-4 rounded-2xl border border-zinc-200/70 bg-gradient-to-br ${toneCls.split(' text')[0]} p-5 shadow-sm dark:border-zinc-800/70`}>
      <div className="flex items-center justify-between gap-3">
        <div>
          <div className="text-[11px] uppercase tracking-wider text-zinc-500">Diagnóstico</div>
          <h3 className="text-sm font-semibold mt-0.5">Health Score</h3>
          <p className="mt-1 text-xs text-zinc-700 dark:text-zinc-300">{msg}</p>
        </div>
        <div className="text-right">
          <div className={`text-5xl font-semibold tabular-nums ${toneCls.split(' ').slice(2).join(' ')}`}>
            {score}
            <span className="text-base font-normal text-zinc-500">/100</span>
          </div>
        </div>
      </div>
      {destaques.length > 0 && (
        <div className="mt-4 rounded-xl bg-white/70 p-3 dark:bg-zinc-900/70">
          <div className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-zinc-500">Pontos a destacar</div>
          <ul className="space-y-0.5 text-xs text-zinc-700 dark:text-zinc-300">
            {destaques.map((d, i) => (
              <li key={i}>{d}</li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

function Card({ titulo, icon: Icon, tone, children }: { titulo: string; icon: IconCmp; tone?: 'amber' | 'emerald'; children: ReactNode }) {
  const toneCls =
    tone === 'amber'
      ? 'border-amber-300/60 bg-amber-50/70 dark:border-amber-800/40 dark:bg-amber-950/20'
      : tone === 'emerald'
        ? 'border-emerald-300/60 bg-emerald-50/70 dark:border-emerald-800/40 dark:bg-emerald-950/20'
        : 'border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900';
  const iconCls =
    tone === 'amber'
      ? 'text-amber-600 dark:text-amber-400'
      : tone === 'emerald'
        ? 'text-emerald-600 dark:text-emerald-400'
        : 'text-zinc-500 dark:text-zinc-400';
  return (
    <section className={`mt-4 rounded-2xl border p-5 shadow-sm ${toneCls}`}>
      <h3 className="mb-2 flex items-center gap-2 text-sm font-semibold">
        <Icon size={16} strokeWidth={2} className={iconCls} aria-hidden /> {titulo}
      </h3>
      {children}
    </section>
  );
}
