import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { CalendarClock, ChevronLeft, ChevronRight, List, Plus, Wrench, X } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { toast } from '../../lib/toast';
import { liveListOptions } from '../../lib/queryOptions';
import {
  appointmentsApi,
  APPOINTMENT_STATUS_LABEL,
  type Appointment,
  type AppointmentStatus,
  type CreateAppointmentRequest,
} from '../../lib/appointments/api';
import { reparacoesApi } from '../../lib/reparacoes/api';
import type { Reparacao } from '../../lib/reparacoes/types';

// Sprint 419: estados em curso (0=Recebido, 1=Diagnóstico, 2=AguardaPeça, 3=EmReparação, 4=Pronto).
// Reparações nestes estados com previstoEntregueEm aparecem como overlay no calendário.
const REPAIR_OVERLAY_STATES: number[] = [0, 1, 2, 3, 4];

const STATUS_STYLE: Record<AppointmentStatus, string> = {
  Agendado: 'bg-sky-100 text-sky-700 dark:bg-sky-900/40 dark:text-sky-300',
  Confirmado: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300',
  Concluido: 'bg-zinc-200 text-zinc-700 dark:bg-zinc-700 dark:text-zinc-200',
  Cancelado: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300',
  NaoCompareceu: 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300',
};

const NEXT_STATUS: Partial<Record<AppointmentStatus, AppointmentStatus[]>> = {
  Agendado: ['Confirmado', 'Cancelado'],
  Confirmado: ['Concluido', 'NaoCompareceu', 'Cancelado'],
};

function dayKey(iso: string) {
  return new Date(iso).toLocaleDateString('pt-PT', { weekday: 'long', day: '2-digit', month: 'long' });
}
function hhmm(iso: string) {
  return new Date(iso).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });
}

// Sprint 418: helpers para vista calendário semanal.
function startOfWeek(d: Date): Date {
  const dt = new Date(d);
  dt.setHours(0, 0, 0, 0);
  const wd = dt.getDay(); // 0=Dom, 1=Seg
  const diff = wd === 0 ? -6 : 1 - wd;
  dt.setDate(dt.getDate() + diff);
  return dt;
}
function addDays(d: Date, n: number): Date {
  const dt = new Date(d);
  dt.setDate(dt.getDate() + n);
  return dt;
}
const HOURS = Array.from({ length: 11 }, (_, i) => i + 9); // 9h–19h
const WEEKDAYS = ['Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'];
function toLocalInput(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export default function Agendamentos() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [showForm, setShowForm] = useState(false);
  const [prefilledAt, setPrefilledAt] = useState<string | null>(null);
  const [view, setView] = useState<'week' | 'list'>('week');
  const [weekStart, setWeekStart] = useState<Date>(() => startOfWeek(new Date()));

  const range = useMemo(() => {
    if (view === 'week') {
      const from = weekStart;
      const to = addDays(weekStart, 7);
      return { from: from.toISOString(), to: to.toISOString() };
    }
    const from = new Date();
    from.setHours(0, 0, 0, 0);
    const to = new Date(from);
    to.setDate(to.getDate() + 30);
    return { from: from.toISOString(), to: to.toISOString() };
  }, [view, weekStart]);

  const list = useQuery({
    queryKey: ['appointments', range.from, range.to],
    queryFn: () => appointmentsApi.list(range.from, range.to),
    ...liveListOptions,
  });

  // Sprint 419: overlay de reparações com ETA. Carrega só estados em-curso e filtra client-side por range visível.
  const reparacoesList = useQuery({
    queryKey: ['reparacoes', 'overlay-eta'],
    queryFn: () => reparacoesApi.list({ pageSize: 100 }),
    staleTime: 30_000,
  });
  const reparacoesEta = useMemo<Reparacao[]>(() => {
    const all = reparacoesList.data?.items ?? [];
    const fromMs = new Date(range.from).getTime();
    const toMs = new Date(range.to).getTime();
    return all.filter((r) => {
      if (!r.previstoEntregueEm) return false;
      if (!REPAIR_OVERLAY_STATES.includes(r.estado)) return false;
      const t = new Date(r.previstoEntregueEm).getTime();
      return t >= fromMs && t < toMs;
    });
  }, [reparacoesList.data, range.from, range.to]);

  const statusMut = useMutation({
    mutationFn: ({ id, status }: { id: string; status: AppointmentStatus }) => appointmentsApi.updateStatus(id, status),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['appointments'] }); },
  });

  const grouped = useMemo(() => {
    const map = new Map<string, Appointment[]>();
    for (const a of list.data ?? []) {
      const k = dayKey(a.scheduledAt);
      (map.get(k) ?? map.set(k, []).get(k)!).push(a);
    }
    return [...map.entries()];
  }, [list.data]);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Agendamentos</h1>
          <p className="text-sm text-zinc-500">
            {view === 'week'
              ? 'Semana visual — clica num slot livre para marcar, ou no cartão para mudar estado.'
              : 'Próximos 30 dias. Marca horas para os clientes deixarem equipamentos.'}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <div className="inline-flex rounded-lg border border-zinc-200 bg-white p-0.5 dark:border-zinc-800 dark:bg-zinc-900">
            <button type="button" onClick={() => setView('week')}
              className={`inline-flex min-h-9 items-center gap-1 rounded-md px-2.5 py-1.5 text-xs font-medium transition ${view === 'week' ? 'bg-zinc-100 text-zinc-900 dark:bg-zinc-800 dark:text-zinc-100' : 'text-zinc-500'}`}>
              <CalendarClock size={14} /> Semana
            </button>
            <button type="button" onClick={() => setView('list')}
              className={`inline-flex min-h-9 items-center gap-1 rounded-md px-2.5 py-1.5 text-xs font-medium transition ${view === 'list' ? 'bg-zinc-100 text-zinc-900 dark:bg-zinc-800 dark:text-zinc-100' : 'text-zinc-500'}`}>
              <List size={14} /> Lista
            </button>
          </div>
          <Button type="button" onClick={() => { setPrefilledAt(null); setShowForm(true); }} leftIcon={<Plus size={16} />}>Novo</Button>
        </div>
      </div>

      {view === 'week' && (
        <WeekGrid
          weekStart={weekStart}
          onPrev={() => setWeekStart((s) => addDays(s, -7))}
          onNext={() => setWeekStart((s) => addDays(s, 7))}
          onToday={() => setWeekStart(startOfWeek(new Date()))}
          appointments={list.data ?? []}
          reparacoesEta={reparacoesEta}
          loading={list.isLoading}
          onSlotClick={(iso) => { setPrefilledAt(iso); setShowForm(true); }}
          onCardClick={(a) => {
            const next = NEXT_STATUS[a.status]?.[0];
            if (next) statusMut.mutate({ id: a.id, status: next });
          }}
          onRepairClick={(r) => navigate(`/reparacoes/${r.id}`)}
        />
      )}

      {view === 'list' && list.isLoading && <p className="text-sm text-zinc-500">A carregar…</p>}
      {view === 'list' && !list.isLoading && grouped.length === 0 && (
        <div className="rounded-xl border border-dashed border-zinc-300 p-8 text-center text-sm text-zinc-500 dark:border-zinc-700">
          <CalendarClock className="mx-auto mb-2 text-zinc-400" size={28} />
          Sem agendamentos nos próximos 30 dias.
        </div>
      )}

      {view === 'list' && grouped.map(([day, items]) => (
        <div key={day}>
          <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-400">{day}</h2>
          <div className="space-y-2">
            {items.map((a) => (
              <div key={a.id} className="flex flex-wrap items-center gap-3 rounded-xl border border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-900">
                <div className="w-14 flex-none text-center">
                  <div className="text-lg font-semibold tabular-nums">{hhmm(a.scheduledAt)}</div>
                  <div className="text-[11px] text-zinc-400">{a.durationMin}min</div>
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <span className="truncate font-medium">{a.nome}</span>
                    {a.source === 'Online' && <span className="rounded bg-brand-50 px-1.5 py-0.5 text-[10px] text-brand-600 dark:bg-zinc-800">online</span>}
                  </div>
                  <div className="truncate text-xs text-zinc-500">
                    {[a.equipamento, a.telefone, a.notas].filter(Boolean).join(' · ') || '—'}
                  </div>
                </div>
                <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_STYLE[a.status]}`}>
                  {APPOINTMENT_STATUS_LABEL[a.status]}
                </span>
                <div className="flex gap-1">
                  {(NEXT_STATUS[a.status] ?? []).map((st) => (
                    <button
                      key={st}
                      type="button"
                      onClick={() => statusMut.mutate({ id: a.id, status: st })}
                      disabled={statusMut.isPending}
                      className="rounded-md border border-zinc-200 px-2 py-1 text-xs text-zinc-600 transition hover:bg-zinc-100 disabled:opacity-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
                    >
                      {APPOINTMENT_STATUS_LABEL[st]}
                    </button>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      ))}

      {showForm && <NovoAgendamentoModal initialIso={prefilledAt} onClose={() => { setShowForm(false); setPrefilledAt(null); }} onSaved={() => { setShowForm(false); setPrefilledAt(null); qc.invalidateQueries({ queryKey: ['appointments'] }); }} />}
    </div>
  );
}

// Sprint 418: vista calendário semanal (grelha 7d × slots horários 9-19).
function WeekGrid({
  weekStart, onPrev, onNext, onToday, appointments, reparacoesEta, loading, onSlotClick, onCardClick, onRepairClick,
}: {
  weekStart: Date;
  onPrev: () => void;
  onNext: () => void;
  onToday: () => void;
  appointments: Appointment[];
  reparacoesEta: Reparacao[];
  loading: boolean;
  onSlotClick: (iso: string) => void;
  onCardClick: (a: Appointment) => void;
  onRepairClick: (r: Reparacao) => void;
}) {
  const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i));
  const weekEnd = addDays(weekStart, 6);
  const sameMonth = weekStart.getMonth() === weekEnd.getMonth();
  const label = sameMonth
    ? `${weekStart.getDate()} – ${weekEnd.getDate()} ${weekEnd.toLocaleDateString('pt-PT', { month: 'long', year: 'numeric' })}`
    : `${weekStart.toLocaleDateString('pt-PT', { day: '2-digit', month: 'short' })} – ${weekEnd.toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' })}`;

  // Index: para cada (dayIndex, hour) qual o appointment colocado (primeiro encontrado).
  const slotMap = useMemo(() => {
    const m = new Map<string, Appointment[]>();
    for (const a of appointments) {
      const dt = new Date(a.scheduledAt);
      const di = (dt.getDay() + 6) % 7; // 0=Seg
      const k = `${di}-${dt.getHours()}`;
      (m.get(k) ?? m.set(k, []).get(k)!).push(a);
    }
    return m;
  }, [appointments]);

  // Sprint 419: index reparações com ETA por slot (mesma estrutura).
  const repairSlotMap = useMemo(() => {
    const m = new Map<string, Reparacao[]>();
    for (const r of reparacoesEta) {
      if (!r.previstoEntregueEm) continue;
      const dt = new Date(r.previstoEntregueEm);
      const di = (dt.getDay() + 6) % 7;
      const k = `${di}-${dt.getHours()}`;
      (m.get(k) ?? m.set(k, []).get(k)!).push(r);
    }
    return m;
  }, [reparacoesEta]);

  const todayKey = new Date().toDateString();

  return (
    <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
      {/* Header: nav semanal */}
      <div className="flex items-center justify-between border-b border-zinc-200 px-3 py-2 dark:border-zinc-800">
        <div className="flex items-center gap-1">
          <button type="button" onClick={onPrev} className="grid h-8 w-8 place-items-center rounded-md text-zinc-500 hover:bg-zinc-100 dark:hover:bg-zinc-800" title="Semana anterior" aria-label="Semana anterior">
            <ChevronLeft size={16} />
          </button>
          <button type="button" onClick={onToday} className="rounded-md border border-zinc-200 px-2.5 py-1 text-xs font-medium hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800">Hoje</button>
          <button type="button" onClick={onNext} className="grid h-8 w-8 place-items-center rounded-md text-zinc-500 hover:bg-zinc-100 dark:hover:bg-zinc-800" title="Semana seguinte" aria-label="Semana seguinte">
            <ChevronRight size={16} />
          </button>
        </div>
        <div className="text-sm font-semibold capitalize">{label}</div>
        <div className="flex items-center gap-3 text-xs text-zinc-500">
          <span>{appointments.length} {appointments.length === 1 ? 'agendamento' : 'agendamentos'}</span>
          {reparacoesEta.length > 0 && (
            <span className="inline-flex items-center gap-1 text-orange-700 dark:text-orange-300">
              <Wrench size={11} /> {reparacoesEta.length} reparaç{reparacoesEta.length === 1 ? 'ão' : 'ões'} com ETA
            </span>
          )}
        </div>
      </div>

      {/* Grelha */}
      <div className="overflow-x-auto">
        <div className="min-w-[840px]">
          {/* Linha dos dias */}
          <div className="grid grid-cols-[60px_repeat(7,1fr)] border-b border-zinc-200 dark:border-zinc-800">
            <div />
            {days.map((d, i) => {
              const isToday = d.toDateString() === todayKey;
              return (
                <div key={i} className={`border-l border-zinc-200 px-2 py-2 text-center text-xs dark:border-zinc-800 ${isToday ? 'bg-brand-50 dark:bg-brand-950/30' : ''}`}>
                  <div className="font-semibold uppercase tracking-wider text-zinc-500">{WEEKDAYS[i]}</div>
                  <div className={`mt-0.5 text-base ${isToday ? 'font-bold text-brand-700 dark:text-brand-300' : 'text-zinc-700 dark:text-zinc-300'}`}>{d.getDate()}</div>
                </div>
              );
            })}
          </div>
          {/* Slots */}
          {HOURS.map((h) => (
            <div key={h} className="grid grid-cols-[60px_repeat(7,1fr)] border-b border-zinc-100 dark:border-zinc-800/50">
              <div className="px-2 py-1 text-right text-[10px] text-zinc-400">{String(h).padStart(2, '0')}:00</div>
              {days.map((d, di) => {
                const apps = slotMap.get(`${di}-${h}`) ?? [];
                const reps = repairSlotMap.get(`${di}-${h}`) ?? [];
                const slotDate = new Date(d);
                slotDate.setHours(h, 0, 0, 0);
                return (
                  <div
                    key={di}
                    className="relative min-h-[56px] cursor-pointer border-l border-zinc-100 transition hover:bg-brand-50/40 dark:border-zinc-800/50 dark:hover:bg-brand-950/20"
                    onClick={(e) => { if (e.target === e.currentTarget) onSlotClick(slotDate.toISOString()); }}
                  >
                    {apps.map((a) => (
                      <button
                        key={a.id}
                        type="button"
                        onClick={(e) => { e.stopPropagation(); onCardClick(a); }}
                        title={`${a.nome} · ${hhmm(a.scheduledAt)} · ${a.durationMin}min — clica para avançar estado`}
                        className={`m-0.5 flex w-[calc(100%-4px)] flex-col items-start rounded-md px-1.5 py-1 text-left text-[11px] leading-tight shadow-sm transition hover:shadow ${STATUS_STYLE[a.status]}`}
                      >
                        <span className="truncate font-semibold w-full">{hhmm(a.scheduledAt)} {a.nome}</span>
                        {a.equipamento && <span className="truncate text-[10px] opacity-75 w-full">{a.equipamento}</span>}
                      </button>
                    ))}
                    {/* Sprint 419: overlay reparações com ETA — cor distinta (laranja), ícone chave-de-fendas. */}
                    {reps.map((r) => (
                      <button
                        key={`r-${r.id}`}
                        type="button"
                        onClick={(e) => { e.stopPropagation(); onRepairClick(r); }}
                        title={`Reparação #${r.numero} · ${r.cliente.nome} · ${r.equipamento} — ETA ${hhmm(r.previstoEntregueEm!)}`}
                        className="m-0.5 flex w-[calc(100%-4px)] items-center gap-1 rounded-md border border-dashed border-orange-400 bg-orange-50 px-1.5 py-1 text-left text-[11px] leading-tight text-orange-800 shadow-sm transition hover:shadow dark:border-orange-500/60 dark:bg-orange-950/40 dark:text-orange-300"
                      >
                        <Wrench size={11} className="flex-none" />
                        <span className="truncate font-semibold">#{r.numero} {r.cliente.nome}</span>
                      </button>
                    ))}
                  </div>
                );
              })}
            </div>
          ))}
        </div>
      </div>

      {loading && <div className="border-t border-zinc-100 px-3 py-2 text-center text-xs text-zinc-400 dark:border-zinc-800">A carregar…</div>}
    </div>
  );
}

function NovoAgendamentoModal({ onClose, onSaved, initialIso }: { onClose: () => void; onSaved: () => void; initialIso?: string | null }) {
  const initLocal = initialIso ? toLocalInput(initialIso) : '';
  const [form, setForm] = useState<CreateAppointmentRequest>({ nome: '', scheduledAt: '', durationMin: 30 });
  const [localDt, setLocalDt] = useState(initLocal);

  const create = useMutation({
    mutationFn: () => {
      if (!form.nome.trim() || !localDt) throw new Error('Nome e data/hora são obrigatórios.');
      return appointmentsApi.create({ ...form, scheduledAt: new Date(localDt).toISOString() });
    },
    onSuccess: () => { toast.success('Agendamento criado.'); onSaved(); },
    onError: (e) => toast.error(e instanceof Error ? e.message : 'Erro ao criar.'),
  });

  const input = 'w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div className="w-full max-w-md rounded-2xl border border-zinc-200 bg-white p-5 shadow-xl dark:border-zinc-800 dark:bg-zinc-900" onClick={(e) => e.stopPropagation()}>
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold">Novo agendamento</h2>
          <button type="button" onClick={onClose} className="rounded-md p-1 text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800"><X size={18} /></button>
        </div>
        <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); create.mutate(); }}>
          <input className={input} placeholder="Nome do cliente *" value={form.nome} onChange={(e) => setForm({ ...form, nome: e.target.value })} autoFocus />
          <input className={input} placeholder="Telefone" value={form.telefone ?? ''} onChange={(e) => setForm({ ...form, telefone: e.target.value })} />
          <input className={input} placeholder="Equipamento (ex: iPhone 13)" value={form.equipamento ?? ''} onChange={(e) => setForm({ ...form, equipamento: e.target.value })} />
          <div className="flex gap-2">
            <input type="datetime-local" className={input} value={localDt} onChange={(e) => setLocalDt(e.target.value)} />
            <input type="number" min={5} step={5} className="w-24 rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950" value={form.durationMin ?? 30} onChange={(e) => setForm({ ...form, durationMin: Number(e.target.value) })} />
          </div>
          <textarea className={input} rows={2} placeholder="Notas" value={form.notas ?? ''} onChange={(e) => setForm({ ...form, notas: e.target.value })} />
          <div className="flex justify-end gap-2 pt-1">
            <Button type="button" variant="secondary" onClick={onClose}>Cancelar</Button>
            <Button type="submit" loading={create.isPending}>Guardar</Button>
          </div>
        </form>
      </div>
    </div>
  );
}
