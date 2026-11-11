import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Circle } from 'lucide-react';
import { internalTasksApi } from '../../lib/internalTasks/api';
import { InternalTaskStatus, MAX_HOJE, TarefaFoco } from '../../lib/internalTasks/types';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';

/** Sprint 617: as (até) 3 prioridades de hoje, no ecrã «Agora». */
export default function PrioridadesHoje() {
  const qc = useQueryClient();
  const lista = useQuery({ queryKey: ['internal-tasks', 'todas'], queryFn: () => internalTasksApi.list() });
  const hoje = (lista.data ?? []).filter((t) => t.foco === TarefaFoco.Hoje && t.status === InternalTaskStatus.Pendente);
  const ideias = (lista.data ?? []).filter((t) => t.foco === TarefaFoco.Ideia && t.status === InternalTaskStatus.Pendente).length;

  const concluir = useMutation({
    mutationFn: (id: string) => internalTasksApi.changeStatus(id, InternalTaskStatus.Concluida),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['internal-tasks'] });
      toast.success('Prioridade concluída ✓');
    },
    onError: (e) => toast.error('Não ficou concluída', apiErrorMessage(e)),
  });

  if (lista.isLoading) return null;
  return (
    <section className="rounded-2xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="font-semibold">🎯 Prioridades de hoje <span className="text-sm font-normal text-zinc-500">{hoje.length}/{MAX_HOJE}</span></h2>
        <Link to="/tarefas" className="text-sm text-brand-700 hover:underline dark:text-brand-300">
          {ideias > 0 ? `${ideias} ideia(s) guardada(s) →` : 'Tarefas →'}
        </Link>
      </div>
      {hoje.length === 0 ? (
        <p className="mt-2 text-sm text-zinc-500">Ainda não escolheste. Em Tarefas, põe até {MAX_HOJE} em «Hoje».</p>
      ) : (
        <ul className="mt-2 space-y-1">
          {hoje.map((t) => (
            <li key={t.id} className="flex items-center gap-2 text-sm">
              <button
                type="button"
                aria-label={`Concluir «${t.title}»`}
                disabled={concluir.isPending}
                onClick={() => concluir.mutate(t.id)}
                className="text-zinc-400 transition hover:text-emerald-600"
              >
                <Circle size={18} />
              </button>
              <span>{t.title}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
