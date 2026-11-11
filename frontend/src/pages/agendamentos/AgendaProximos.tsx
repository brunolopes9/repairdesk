import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarClock, Plus } from 'lucide-react';
import { Button, EmptyState, SectionCard } from '../../components/ui';
import { appointmentsApi, type Appointment } from '../../lib/appointments/api';
import { MarcarRapidoModal } from './MarcarRapido';

const ATIVOS = new Set(['Agendado', 'Confirmado']);

function rotuloDia(d: Date): string {
  const hoje = new Date();
  const amanha = new Date(hoje);
  amanha.setDate(hoje.getDate() + 1);
  if (d.toDateString() === hoje.toDateString()) return 'Hoje';
  if (d.toDateString() === amanha.toDateString()) return 'Amanhã';
  return d.toLocaleDateString('pt-PT', { weekday: 'short', day: 'numeric', month: 'short' });
}

/**
 * Sprint 611: cartão do Dashboard — o que está marcado hoje e nos próximos 7 dias, e um botão para
 * marcar sem sair do Dashboard.
 */
export function AgendaProximos() {
  const qc = useQueryClient();
  const [marcar, setMarcar] = useState(false);
  const de = new Date();
  de.setHours(0, 0, 0, 0);
  const ate = new Date(de);
  ate.setDate(de.getDate() + 8);
  const lista = useQuery({
    queryKey: ['appointments', 'proximos', de.toISOString()],
    queryFn: () => appointmentsApi.list(de.toISOString(), ate.toISOString()),
    refetchInterval: 60_000,
  });

  const itens = (lista.data ?? []).filter((a: Appointment) => ATIVOS.has(a.status) && new Date(a.scheduledAt).getTime() > Date.now() - 60 * 60_000);
  const hoje = itens.filter((a) => rotuloDia(new Date(a.scheduledAt)) === 'Hoje').length;

  return (
    <>
      <SectionCard
        title={
          <span className="flex items-center gap-2 text-sm font-semibold">
            <CalendarClock size={15} /> Agenda{hoje > 0 && <span className="rounded-full bg-brand-600 px-2 py-0.5 text-xs text-white">{hoje} hoje</span>}
          </span>
        }
        action={<Button size="sm" variant="secondary" leftIcon={<Plus size={14} />} onClick={() => setMarcar(true)}>Marcar</Button>}
        bodyClassName="p-0"
      >
        {lista.isLoading ? (
          <p className="p-4 text-sm text-zinc-500">A carregar…</p>
        ) : itens.length === 0 ? (
          <div className="p-4">
            <EmptyState icon={CalendarClock} title="Nada marcado" description="Os próximos 7 dias estão livres." compact />
          </div>
        ) : (
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {itens.slice(0, 6).map((a) => {
              const d = new Date(a.scheduledAt);
              return (
                <li key={a.id}>
                  <Link to="/agendamentos" className="flex min-h-12 items-center gap-3 px-4 py-2 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                    <span className="w-16 shrink-0">
                      <span className="block text-xs text-zinc-500">{rotuloDia(d)}</span>
                      <span className="block font-semibold tabular-nums">{d.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' })}</span>
                    </span>
                    <span className="min-w-0">
                      <span className="block truncate font-medium">{a.nome}</span>
                      <span className="block truncate text-xs text-zinc-500">{[a.equipamento, a.notas].filter(Boolean).join(' · ') || '—'}</span>
                    </span>
                  </Link>
                </li>
              );
            })}
          </ul>
        )}
        {itens.length > 6 && (
          <Link to="/agendamentos" className="block border-t border-zinc-100 px-4 py-2 text-sm text-brand-700 hover:underline dark:border-zinc-800 dark:text-brand-300">
            Ver os {itens.length} agendamentos
          </Link>
        )}
      </SectionCard>
      {marcar && (
        <MarcarRapidoModal
          onClose={() => setMarcar(false)}
          onSaved={() => {
            setMarcar(false);
            qc.invalidateQueries({ queryKey: ['appointments'] });
          }}
        />
      )}
    </>
  );
}
