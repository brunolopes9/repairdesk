import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ArrowRight, CheckCircle2, Circle, ListTodo, Plus } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { toast } from '../../lib/toast';
import { apiErrorMessage } from '../../lib/errors';
import { internalTasksApi } from '../../lib/internalTasks/api';
import { InternalTaskStatus } from '../../lib/internalTasks/types';

/**
 * Sprint 424 (Doc 90 follow-up): tarefas ligadas a uma reparação, inline no detalhe.
 *
 * Caso típico do dogfooding Bruno: ao mudar reparação para AguardaPeça, criar
 * tarefa "pedir bateria iPhone 13 ao Tudo4Mobile" para não esquecer. Tarefas
 * ficam visíveis no detalhe da reparação E na lista global /tarefas E no widget
 * "Tarefas pendentes" do Dashboard.
 *
 * Form inline minimal: título + DueAt (opcional). Toggle conclusão por click.
 * Para edição completa o utilizador navega para /tarefas.
 */
export function ReparacaoTarefasSection({
  reparacaoId,
  reparacaoNumero,
  reparacaoEquipamento,
}: {
  reparacaoId: string;
  reparacaoNumero: number;
  reparacaoEquipamento: string;
}) {
  const qc = useQueryClient();
  const [newTitle, setNewTitle] = useState('');
  const [newDue, setNewDue] = useState('');

  const list = useQuery({
    queryKey: ['internal-tasks', 'by-reparacao', reparacaoId],
    queryFn: () => internalTasksApi.list({ reparacaoId }),
    staleTime: 30_000,
  });

  const create = useMutation({
    mutationFn: () => {
      const title = newTitle.trim();
      if (title.length < 2) throw new Error('Título obrigatório.');
      return internalTasksApi.create({
        title,
        reparacaoId,
        dueAt: newDue ? new Date(newDue).toISOString() : null,
      });
    },
    onSuccess: () => {
      setNewTitle('');
      setNewDue('');
      qc.invalidateQueries({ queryKey: ['internal-tasks'] });
      toast.success('Tarefa criada.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao criar tarefa.'),
  });

  const toggle = useMutation({
    mutationFn: ({ id, next }: { id: string; next: InternalTaskStatus }) => internalTasksApi.changeStatus(id, next),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['internal-tasks'] }),
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao actualizar.'),
  });

  const items = list.data ?? [];
  const pendentes = items.filter((t) => t.status === InternalTaskStatus.Pendente);
  const concluidas = items.filter((t) => t.status === InternalTaskStatus.Concluida);

  const input =
    'w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950';

  return (
    <section className="space-y-3 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold">
          <ListTodo size={16} /> Tarefas ligadas
          {pendentes.length > 0 && (
            <span className="rounded-full bg-amber-100 px-1.5 py-0.5 text-[10px] font-medium text-amber-800 dark:bg-amber-950/40 dark:text-amber-300">
              {pendentes.length} pendente{pendentes.length === 1 ? '' : 's'}
            </span>
          )}
        </h2>
        <Link
          to="/tarefas"
          className="inline-flex items-center gap-1 text-[11px] text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300"
        >
          ver todas <ArrowRight size={12} />
        </Link>
      </div>

      {/* Form inline */}
      <form
        className="flex flex-wrap items-center gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          create.mutate();
        }}
      >
        <input
          className={`${input} flex-1 min-w-[180px]`}
          placeholder={`Nova tarefa para #${reparacaoNumero} ${reparacaoEquipamento}…`}
          value={newTitle}
          onChange={(e) => setNewTitle(e.target.value)}
        />
        <input
          type="datetime-local"
          className={`${input} w-auto`}
          value={newDue}
          onChange={(e) => setNewDue(e.target.value)}
          title="Prazo (opcional)"
        />
        <Button type="submit" loading={create.isPending} disabled={newTitle.trim().length < 2} leftIcon={<Plus size={14} />}>
          Adicionar
        </Button>
      </form>

      {/* Pendentes */}
      {pendentes.length > 0 && (
        <ul className="space-y-1.5">
          {pendentes.map((t) => (
            <Row
              key={t.id}
              title={t.title}
              dueAt={t.dueAt}
              done={false}
              onToggle={() => toggle.mutate({ id: t.id, next: InternalTaskStatus.Concluida })}
            />
          ))}
        </ul>
      )}

      {/* Concluídas em colapso */}
      {concluidas.length > 0 && (
        <details className="text-xs">
          <summary className="cursor-pointer text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300">
            {concluidas.length} concluída{concluidas.length === 1 ? '' : 's'}
          </summary>
          <ul className="mt-2 space-y-1.5">
            {concluidas.map((t) => (
              <Row
                key={t.id}
                title={t.title}
                dueAt={t.dueAt}
                done
                onToggle={() => toggle.mutate({ id: t.id, next: InternalTaskStatus.Pendente })}
              />
            ))}
          </ul>
        </details>
      )}

      {items.length === 0 && !list.isLoading && (
        <p className="text-xs text-zinc-500">
          Sem tarefas ligadas. Cria uma para não esquecer de coisas tipo "pedir peça", "ligar ao cliente", "testar carregamento".
        </p>
      )}
    </section>
  );
}

function Row({
  title,
  dueAt,
  done,
  onToggle,
}: {
  title: string;
  dueAt: string | null;
  done: boolean;
  onToggle: () => void;
}) {
  const overdue = !done && dueAt && new Date(dueAt) < new Date();
  return (
    <li className="flex items-start gap-2 rounded-md border border-zinc-100 px-2 py-1.5 text-sm dark:border-zinc-800">
      <button
        type="button"
        onClick={onToggle}
        className={`mt-0.5 flex-none ${done ? 'text-emerald-600 dark:text-emerald-400' : 'text-zinc-400 hover:text-emerald-600 dark:hover:text-emerald-400'}`}
        title={done ? 'Marcar como pendente' : 'Marcar concluída'}
      >
        {done ? <CheckCircle2 size={16} /> : <Circle size={16} />}
      </button>
      <div className="min-w-0 flex-1">
        <div className={`truncate ${done ? 'line-through text-zinc-500' : ''}`}>{title}</div>
        {dueAt && (
          <div className={`text-[11px] ${overdue ? 'text-rose-600 dark:text-rose-400' : 'text-zinc-500'}`}>
            {overdue ? 'Atrasada · ' : 'Prazo: '}
            {new Date(dueAt).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}
          </div>
        )}
      </div>
    </li>
  );
}
