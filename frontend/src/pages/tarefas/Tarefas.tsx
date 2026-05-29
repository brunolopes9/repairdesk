import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { CheckCircle2, Circle, ClipboardList, Plus, Trash2, XCircle } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { useConfirm } from '../../components/ConfirmDialog';
import { toast } from '../../lib/toast';
import { apiErrorMessage } from '../../lib/errors';
import { internalTasksApi } from '../../lib/internalTasks/api';
import { InternalTaskStatus, type InternalTask } from '../../lib/internalTasks/types';

/**
 * Sprint 422 (Doc 90 Tier 2 #7): tarefas internas — TODO list.
 *
 * UX: tabs por status (Pendentes / Concluídas / Canceladas) + criar inline + toggle de
 * conclusão com 1 click. Cada tarefa pode ligar opcionalmente a uma reparação (atalho
 * para o detalhe).
 */
export default function Tarefas() {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const [tab, setTab] = useState<InternalTaskStatus>(InternalTaskStatus.Pendente);
  const [newTitle, setNewTitle] = useState('');
  const [newDue, setNewDue] = useState('');

  const list = useQuery({
    queryKey: ['internal-tasks', tab],
    queryFn: () => internalTasksApi.list({ status: tab }),
  });

  const create = useMutation({
    mutationFn: () =>
      internalTasksApi.create({
        title: newTitle.trim(),
        dueAt: newDue ? new Date(newDue).toISOString() : null,
      }),
    onSuccess: () => {
      setNewTitle('');
      setNewDue('');
      qc.invalidateQueries({ queryKey: ['internal-tasks'] });
      toast.success('Tarefa criada.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao criar tarefa.'),
  });

  const changeStatus = useMutation({
    mutationFn: ({ id, status }: { id: string; status: InternalTaskStatus }) =>
      internalTasksApi.changeStatus(id, status),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['internal-tasks'] }),
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao mudar estado.'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => internalTasksApi.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['internal-tasks'] });
      toast.success('Tarefa eliminada.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao eliminar.'),
  });

  return (
    <div className="space-y-5">
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-semibold tracking-tight">
          <ClipboardList size={24} /> Tarefas
        </h1>
        <p className="text-sm text-zinc-500">Lembretes internos — follow-ups, encomendas, manutenção. Não confundir com reparações.</p>
      </div>

      {/* Form inline para criar */}
      <form
        className="flex flex-wrap items-center gap-2 rounded-xl border border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-900"
        onSubmit={(e) => {
          e.preventDefault();
          if (newTitle.trim().length < 2) return;
          create.mutate();
        }}
      >
        <input
          className="flex-1 min-w-[240px] rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
          placeholder="Nova tarefa… (ex: pedir bateria iPhone 13 ao fornecedor)"
          value={newTitle}
          onChange={(e) => setNewTitle(e.target.value)}
        />
        <input
          type="datetime-local"
          className="rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
          value={newDue}
          onChange={(e) => setNewDue(e.target.value)}
          title="Prazo (opcional)"
        />
        <Button type="submit" loading={create.isPending} disabled={newTitle.trim().length < 2} leftIcon={<Plus size={16} />}>
          Adicionar
        </Button>
      </form>

      {/* Tabs por status */}
      <div className="flex flex-wrap items-center gap-1 border-b border-zinc-200 dark:border-zinc-800">
        {[
          { key: InternalTaskStatus.Pendente, label: 'Pendentes' },
          { key: InternalTaskStatus.Concluida, label: 'Concluídas' },
          { key: InternalTaskStatus.Cancelada, label: 'Canceladas' },
        ].map(({ key, label }) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key)}
            className={`relative -mb-px border-b-2 px-3 py-2 text-sm font-medium transition ${
              tab === key
                ? 'border-brand-500 text-brand-700 dark:text-brand-300'
                : 'border-transparent text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      {/* Lista */}
      {list.isLoading && <p className="text-sm text-zinc-500">A carregar…</p>}
      {!list.isLoading && (list.data?.length ?? 0) === 0 && (
        <div className="rounded-xl border border-dashed border-zinc-300 p-10 text-center text-sm text-zinc-500 dark:border-zinc-700">
          <ClipboardList className="mx-auto mb-2 text-zinc-400" size={28} />
          Nenhuma tarefa {labelStatus(tab).toLowerCase()}.
        </div>
      )}
      <ul className="space-y-2">
        {list.data?.map((t) => (
          <TaskRow
            key={t.id}
            task={t}
            onToggleDone={() =>
              changeStatus.mutate({
                id: t.id,
                status: t.status === InternalTaskStatus.Concluida ? InternalTaskStatus.Pendente : InternalTaskStatus.Concluida,
              })
            }
            onCancel={() => changeStatus.mutate({ id: t.id, status: InternalTaskStatus.Cancelada })}
            onDelete={async () => {
              if (await confirm({ title: 'Eliminar tarefa?', description: 'Esta acção não pode ser desfeita.', destructive: true, confirmLabel: 'Eliminar' })) {
                remove.mutate(t.id);
              }
            }}
          />
        ))}
      </ul>
    </div>
  );
}

function TaskRow({
  task,
  onToggleDone,
  onCancel,
  onDelete,
}: {
  task: InternalTask;
  onToggleDone: () => void;
  onCancel: () => void;
  onDelete: () => void;
}) {
  const isDone = task.status === InternalTaskStatus.Concluida;
  const isCancelled = task.status === InternalTaskStatus.Cancelada;
  const overdue = task.status === InternalTaskStatus.Pendente && task.dueAt && new Date(task.dueAt) < new Date();

  return (
    <li className={`flex items-start gap-3 rounded-xl border border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-900 ${isCancelled ? 'opacity-60' : ''}`}>
      <button
        type="button"
        onClick={onToggleDone}
        disabled={isCancelled}
        className="mt-0.5 flex-none text-zinc-400 hover:text-emerald-600 disabled:hover:text-zinc-400 dark:hover:text-emerald-400"
        title={isDone ? 'Marcar como pendente' : 'Marcar concluída'}
      >
        {isDone ? <CheckCircle2 size={20} className="text-emerald-600 dark:text-emerald-400" /> : <Circle size={20} />}
      </button>

      <div className="min-w-0 flex-1">
        <div className={`text-sm font-medium ${isDone ? 'line-through text-zinc-500' : ''}`}>{task.title}</div>
        {task.description && (
          <div className="mt-0.5 text-xs text-zinc-500 whitespace-pre-wrap">{task.description}</div>
        )}
        <div className="mt-1 flex flex-wrap items-center gap-2 text-[11px] text-zinc-500">
          {task.dueAt && (
            <span className={`rounded px-1.5 py-0.5 ${overdue ? 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-300' : 'bg-zinc-100 dark:bg-zinc-800'}`}>
              {overdue ? 'Atrasada · ' : 'Prazo: '}
              {new Date(task.dueAt).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}
            </span>
          )}
          {task.assignedToDisplayName && (
            <span className="rounded bg-zinc-100 px-1.5 py-0.5 dark:bg-zinc-800">@{task.assignedToDisplayName}</span>
          )}
          {task.reparacaoNumero && task.reparacaoId && (
            <Link to={`/reparacoes/${task.reparacaoId}`} className="rounded bg-brand-50 px-1.5 py-0.5 text-brand-700 hover:underline dark:bg-brand-950/40 dark:text-brand-300">
              Reparação #{task.reparacaoNumero}
            </Link>
          )}
        </div>
      </div>

      {!isDone && !isCancelled && (
        <button
          type="button"
          onClick={onCancel}
          className="flex-none rounded-md p-1 text-zinc-400 hover:bg-zinc-100 hover:text-amber-600 dark:hover:bg-zinc-800"
          title="Cancelar"
        >
          <XCircle size={16} />
        </button>
      )}
      <button
        type="button"
        onClick={onDelete}
        className="flex-none rounded-md p-1 text-zinc-400 hover:bg-zinc-100 hover:text-red-600 dark:hover:bg-zinc-800"
        title="Eliminar"
      >
        <Trash2 size={16} />
      </button>
    </li>
  );
}

function labelStatus(s: InternalTaskStatus): string {
  switch (s) {
    case InternalTaskStatus.Pendente: return 'Pendente';
    case InternalTaskStatus.Concluida: return 'Concluída';
    case InternalTaskStatus.Cancelada: return 'Cancelada';
  }
}
