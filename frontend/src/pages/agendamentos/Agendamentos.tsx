import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarClock, Plus, X } from 'lucide-react';
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

export default function Agendamentos() {
  const qc = useQueryClient();
  const [showForm, setShowForm] = useState(false);

  const range = useMemo(() => {
    const from = new Date();
    from.setHours(0, 0, 0, 0);
    const to = new Date(from);
    to.setDate(to.getDate() + 30);
    return { from: from.toISOString(), to: to.toISOString() };
  }, []);

  const list = useQuery({
    queryKey: ['appointments', range.from, range.to],
    queryFn: () => appointmentsApi.list(range.from, range.to),
    ...liveListOptions,
  });

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
      <div className="flex items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Agendamentos</h1>
          <p className="text-sm text-zinc-500">Próximos 30 dias. Marca horas para os clientes deixarem equipamentos.</p>
        </div>
        <Button type="button" onClick={() => setShowForm(true)} leftIcon={<Plus size={16} />}>Novo</Button>
      </div>

      {list.isLoading && <p className="text-sm text-zinc-500">A carregar…</p>}
      {!list.isLoading && grouped.length === 0 && (
        <div className="rounded-xl border border-dashed border-zinc-300 p-8 text-center text-sm text-zinc-500 dark:border-zinc-700">
          <CalendarClock className="mx-auto mb-2 text-zinc-400" size={28} />
          Sem agendamentos nos próximos 30 dias.
        </div>
      )}

      {grouped.map(([day, items]) => (
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

      {showForm && <NovoAgendamentoModal onClose={() => setShowForm(false)} onSaved={() => { setShowForm(false); qc.invalidateQueries({ queryKey: ['appointments'] }); }} />}
    </div>
  );
}

function NovoAgendamentoModal({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<CreateAppointmentRequest>({ nome: '', scheduledAt: '', durationMin: 30 });
  const [localDt, setLocalDt] = useState('');

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
