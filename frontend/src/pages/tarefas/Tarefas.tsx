import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { CheckCircle2, Circle, ClipboardList, Plus, Trash2, XCircle } from 'lucide-react';
import { Button, DetailWorkspace, InspectorRail, PageHeader, ViewTabs } from '../../components/ui';
import { useConfirm } from '../../components/ConfirmDialog';
import { toast } from '../../lib/toast';
import { apiErrorMessage } from '../../lib/errors';
import { internalTasksApi } from '../../lib/internalTasks/api';
import { InternalTaskStatus, type InternalTask } from '../../lib/internalTasks/types';

/**
 * Sprint 422 (Doc 90 Tier 2 #7): tarefas internas.
 *
 * UX: tabs por estado, criação inline e toggle de conclusão com 1 clique. Cada tarefa
 * pode ligar opcionalmente a uma reparação para manter o contexto operacional perto.
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

  const tasks = list.data ?? [];
  const overdueCount = tasks.filter((t) => t.status === InternalTaskStatus.Pendente && t.dueAt && new Date(t.dueAt).getTime() < Date.now()).length;
  const linkedCount = tasks.filter((t) => t.reparacaoId).length;
  const upcomingCount = tasks.filter((t) => t.dueAt && new Date(t.dueAt).getTime() >= Date.now()).length;

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

  const rail = (
    <InspectorRail>
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-brand-600 dark:text-brand-300">Produtividade</p>
        <h2 className="mt-1 text-base font-semibold text-zinc-950 dark:text-zinc-50">Fila interna</h2>
        <p className="mt-1 text-sm text-zinc-500">
          Lembretes pequenos ficam aqui, perto da operação e longe das notas soltas nas reparações.
        </p>
      </div>

      <div className="grid grid-cols-3 gap-2">
        <TaskRailStat label="Vista" value={tasks.length} />
        <TaskRailStat label="Atraso" value={overdueCount} tone={overdueCount > 0 ? 'danger' : 'default'} />
        <TaskRailStat label="Ligadas" value={linkedCount} />
      </div>

      <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-3 text-sm dark:border-zinc-800 dark:bg-zinc-950">
        <div className="flex items-center justify-between gap-3">
          <span className="font-medium text-zinc-900 dark:text-zinc-100">Próximos prazos</span>
          <span className="rounded-full bg-white px-2 py-0.5 text-xs text-zinc-500 ring-1 ring-zinc-200 dark:bg-zinc-900 dark:ring-zinc-800">
            {upcomingCount}
          </span>
        </div>
        <p className="mt-2 text-xs leading-5 text-zinc-500">
          Usa prazos só quando a tarefa precisa mesmo de aparecer no radar operacional.
        </p>
      </div>

      <div className="space-y-2 text-xs text-zinc-500">
        <p className="font-medium uppercase tracking-wide text-zinc-400">Boas tarefas</p>
        <p>Encomendar peça, confirmar pagamento, ligar a cliente, rever garantia, preparar entrega.</p>
      </div>
    </InspectorRail>
  );

  return (
    <div className="space-y-5">
      <PageHeader
        title="Tarefas"
        description="Follow-ups, encomendas e lembretes internos. Separado das reparações para manter a oficina limpa."
        meta={<span className="text-sm font-normal text-zinc-500">{tasks.length} {tasks.length === 1 ? 'tarefa' : 'tarefas'}</span>}
      />

      <DetailWorkspace rail={rail}>
        <form
          className="rounded-lg border border-zinc-200 bg-white p-3 shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900"
          onSubmit={(e) => {
            e.preventDefault();
            if (newTitle.trim().length < 2) return;
            create.mutate();
          }}
        >
          <div className="grid gap-2 lg:grid-cols-[minmax(0,1fr)_220px_auto]">
            <input
              className="min-h-10 min-w-0 rounded-md border border-zinc-200 bg-white px-3 text-sm outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100 dark:border-zinc-700 dark:bg-zinc-950 dark:focus:ring-brand-950/50"
              placeholder="Nova tarefa... ex: pedir bateria iPhone 13 ao fornecedor"
              value={newTitle}
              onChange={(e) => setNewTitle(e.target.value)}
            />
            <input
              type="datetime-local"
              className="min-h-10 rounded-md border border-zinc-200 bg-white px-3 text-sm outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100 dark:border-zinc-700 dark:bg-zinc-950 dark:focus:ring-brand-950/50"
              value={newDue}
              onChange={(e) => setNewDue(e.target.value)}
              title="Prazo (opcional)"
            />
            <Button type="submit" loading={create.isPending} disabled={newTitle.trim().length < 2} leftIcon={<Plus size={16} />}>
              Adicionar
            </Button>
          </div>
        </form>

        <ViewTabs
          value={String(tab)}
          onChange={(next) => setTab(Number(next) as InternalTaskStatus)}
          tabs={[
            { key: String(InternalTaskStatus.Pendente), label: 'Pendentes', meta: tab === InternalTaskStatus.Pendente ? tasks.length : undefined },
            { key: String(InternalTaskStatus.Concluida), label: 'Concluídas', meta: tab === InternalTaskStatus.Concluida ? tasks.length : undefined },
            { key: String(InternalTaskStatus.Cancelada), label: 'Canceladas', meta: tab === InternalTaskStatus.Cancelada ? tasks.length : undefined },
          ]}
        />

        <section className="overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
          <div className="flex items-center justify-between gap-3 border-b border-zinc-100 px-4 py-3 dark:border-zinc-800">
            <div>
              <h2 className="text-sm font-semibold text-zinc-950 dark:text-zinc-50">{labelStatus(tab)}</h2>
              <p className="text-xs text-zinc-500">Fila de trabalho interno desta vista.</p>
            </div>
            <span className="rounded-full bg-zinc-100 px-2.5 py-1 text-xs font-medium text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
              {tasks.length}
            </span>
          </div>

          {list.isLoading && <p className="p-4 text-sm text-zinc-500">A carregar...</p>}
          {!list.isLoading && tasks.length === 0 && (
            <div className="p-10 text-center text-sm text-zinc-500">
              <ClipboardList className="mx-auto mb-2 text-zinc-400" size={28} />
              Nenhuma tarefa {labelStatus(tab).toLowerCase()}.
            </div>
          )}
          {tasks.length > 0 && (
            <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {tasks.map((t) => (
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
                    if (await confirm({ title: 'Eliminar tarefa?', description: 'Esta ação não pode ser desfeita.', destructive: true, confirmLabel: 'Eliminar' })) {
                      remove.mutate(t.id);
                    }
                  }}
                />
              ))}
            </ul>
          )}
        </section>
      </DetailWorkspace>
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
  const overdue = Boolean(task.status === InternalTaskStatus.Pendente && task.dueAt && new Date(task.dueAt).getTime() < Date.now());

  return (
    <li className={`flex items-start gap-3 p-4 ${isCancelled ? 'opacity-60' : ''}`}>
      <button
        type="button"
        onClick={onToggleDone}
        disabled={isCancelled}
        className="mt-0.5 flex-none text-zinc-400 transition hover:text-emerald-600 disabled:hover:text-zinc-400 dark:hover:text-emerald-400"
        title={isDone ? 'Marcar como pendente' : 'Marcar concluída'}
      >
        {isDone ? <CheckCircle2 size={20} className="text-emerald-600 dark:text-emerald-400" /> : <Circle size={20} />}
      </button>

      <div className="min-w-0 flex-1">
        <div className={`text-sm font-medium ${isDone ? 'line-through text-zinc-500' : 'text-zinc-950 dark:text-zinc-50'}`}>{task.title}</div>
        {task.description && (
          <div className="mt-0.5 whitespace-pre-wrap text-xs text-zinc-500">{task.description}</div>
        )}
        <div className="mt-2 flex flex-wrap items-center gap-2 text-[11px] text-zinc-500">
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
          className="flex-none rounded-md p-1 text-zinc-400 transition hover:bg-zinc-100 hover:text-amber-600 dark:hover:bg-zinc-800"
          title="Cancelar"
        >
          <XCircle size={16} />
        </button>
      )}
      <button
        type="button"
        onClick={onDelete}
        className="flex-none rounded-md p-1 text-zinc-400 transition hover:bg-zinc-100 hover:text-red-600 dark:hover:bg-zinc-800"
        title="Eliminar"
      >
        <Trash2 size={16} />
      </button>
    </li>
  );
}

function TaskRailStat({ label, value, tone = 'default' }: { label: string; value: number; tone?: 'default' | 'danger' }) {
  return (
    <div className={`rounded-lg border p-2 ${tone === 'danger' ? 'border-red-200 bg-red-50 dark:border-red-900/60 dark:bg-red-950/30' : 'border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900'}`}>
      <div className={`text-lg font-semibold ${tone === 'danger' ? 'text-red-700 dark:text-red-300' : 'text-zinc-950 dark:text-zinc-50'}`}>{value}</div>
      <div className="text-[11px] text-zinc-500">{label}</div>
    </div>
  );
}

function labelStatus(s: InternalTaskStatus): string {
  switch (s) {
    case InternalTaskStatus.Pendente: return 'Pendentes';
    case InternalTaskStatus.Concluida: return 'Concluídas';
    case InternalTaskStatus.Cancelada: return 'Canceladas';
  }
}
