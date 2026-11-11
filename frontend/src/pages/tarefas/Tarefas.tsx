import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { CheckCircle2, Circle, ClipboardList, Plus, Trash2, XCircle } from 'lucide-react';
import { Button, DetailWorkspace, InspectorRail, PageHeader, ViewTabs } from '../../components/ui';
import { useConfirm } from '../../components/ConfirmDialog';
import { toast } from '../../lib/toast';
import { apiErrorMessage } from '../../lib/errors';
import { internalTasksApi } from '../../lib/internalTasks/api';
import { InternalTaskStatus, MAX_HOJE, TarefaFoco, type InternalTask } from '../../lib/internalTasks/types';

type Vista = 'hoje' | 'semana' | 'ideias' | 'todas' | 'concluidas';

const FOCO_LABEL: Record<TarefaFoco, string> = { 0: 'Sem foco', 1: 'Hoje', 2: 'Semana', 3: 'Ideia' };
const FOCO_COR: Record<TarefaFoco, string> = {
  0: 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300',
  1: 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300',
  2: 'bg-sky-100 text-sky-700 dark:bg-sky-950/40 dark:text-sky-300',
  3: 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300',
};

/**
 * Sprint 422 / 617 (Doc 98 F2): tarefas internas com foco.
 * «Hoje» tem no máximo 3 prioridades — escolher, não acumular. «Ideias» é a Inbox: o que aparece a meio
 * de um bloco é guardado aqui (botão 💡 no topo) e decidido depois, no planeamento.
 */
export default function Tarefas() {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const [vista, setVista] = useState<Vista>('hoje');
  const [newTitle, setNewTitle] = useState('');
  const [newDue, setNewDue] = useState('');
  const [newFoco, setNewFoco] = useState<TarefaFoco>(TarefaFoco.Hoje);

  const list = useQuery({ queryKey: ['internal-tasks', 'todas'], queryFn: () => internalTasksApi.list() });
  const todas = useMemo(() => list.data ?? [], [list.data]);
  const pendentes = todas.filter((t) => t.status === InternalTaskStatus.Pendente);
  const hoje = pendentes.filter((t) => t.foco === TarefaFoco.Hoje);
  const semana = pendentes.filter((t) => t.foco === TarefaFoco.Semana);
  const ideias = pendentes.filter((t) => t.foco === TarefaFoco.Ideia);
  const concluidas = todas.filter((t) => t.status !== InternalTaskStatus.Pendente);
  const tasks = { hoje, semana, ideias, todas: pendentes, concluidas }[vista];
  const overdueCount = pendentes.filter((t) => t.dueAt && new Date(t.dueAt).getTime() < Date.now()).length;

  const invalidar = () => qc.invalidateQueries({ queryKey: ['internal-tasks'] });

  const create = useMutation({
    mutationFn: () =>
      internalTasksApi.create({
        title: newTitle.trim(),
        dueAt: newDue ? new Date(newDue).toISOString() : null,
        foco: newFoco,
      }),
    onSuccess: () => {
      setNewTitle('');
      setNewDue('');
      invalidar();
      toast.success('Tarefa criada.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao criar tarefa.'),
  });

  const changeStatus = useMutation({
    mutationFn: ({ id, status }: { id: string; status: InternalTaskStatus }) => internalTasksApi.changeStatus(id, status),
    onSuccess: invalidar,
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao mudar estado.'),
  });

  const mudarFoco = useMutation({
    mutationFn: ({ id, foco }: { id: string; foco: TarefaFoco }) => internalTasksApi.mudarFoco(id, foco),
    onSuccess: invalidar,
    onError: (err) => toast.error('Não mudou', apiErrorMessage(err)),
  });

  const remove = useMutation({
    mutationFn: (id: string) => internalTasksApi.remove(id),
    onSuccess: () => {
      invalidar();
      toast.success('Tarefa eliminada.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao eliminar.'),
  });

  const rail = (
    <InspectorRail>
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-brand-600 dark:text-brand-300">Foco</p>
        <h2 className="mt-1 text-base font-semibold text-zinc-950 dark:text-zinc-50">Menos coisas, terminadas</h2>
        <p className="mt-1 text-sm text-zinc-500">
          Hoje: no máximo {MAX_HOJE}. O resto vai para «Semana». Ideias novas vão para «Ideias» e decidem-se no planeamento de domingo.
        </p>
      </div>
      <div className="grid grid-cols-3 gap-2">
        <TaskRailStat label="Hoje" value={hoje.length} tone={hoje.length >= MAX_HOJE ? 'danger' : 'default'} />
        <TaskRailStat label="Ideias" value={ideias.length} />
        <TaskRailStat label="Atraso" value={overdueCount} tone={overdueCount > 0 ? 'danger' : 'default'} />
      </div>
      <div className="space-y-2 text-xs text-zinc-500">
        <p className="font-medium uppercase tracking-wide text-zinc-400">Boas tarefas</p>
        <p>Encomendar peça, ligar a cliente, backtesting de 1 h, gravar 1 reel, preparar entrega.</p>
      </div>
    </InspectorRail>
  );

  return (
    <div className="space-y-5">
      <PageHeader
        title="Tarefas"
        description="Hoje (máx. 3), esta semana e a Inbox de ideias. Tira as coisas da cabeça sem largar o que estás a fazer."
        meta={<span className="text-sm font-normal text-zinc-500">{pendentes.length} pendentes</span>}
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
          <div className="grid grid-cols-1 gap-2 lg:grid-cols-[minmax(0,1fr)_130px_200px_auto]">
            <input
              aria-label="Nova tarefa"
              className="min-h-10 min-w-0 rounded-md border border-zinc-200 bg-white px-3 text-sm outline-none transition focus:border-brand-400 focus:ring-2 focus:ring-brand-100 dark:border-zinc-700 dark:bg-zinc-950 dark:focus:ring-brand-950/50"
              placeholder="Nova tarefa... ex: pedir bateria iPhone 13 ao fornecedor"
              value={newTitle}
              onChange={(e) => setNewTitle(e.target.value)}
            />
            <select
              aria-label="Foco"
              value={newFoco}
              onChange={(e) => setNewFoco(Number(e.target.value) as TarefaFoco)}
              className="min-h-10 rounded-md border border-zinc-200 bg-white px-2 text-sm dark:border-zinc-700 dark:bg-zinc-950"
            >
              <option value={TarefaFoco.Hoje} disabled={hoje.length >= MAX_HOJE}>Hoje{hoje.length >= MAX_HOJE ? ' (cheio)' : ''}</option>
              <option value={TarefaFoco.Semana}>Esta semana</option>
              <option value={TarefaFoco.Ideia}>Ideia</option>
              <option value={TarefaFoco.Nenhum}>Sem foco</option>
            </select>
            <input
              type="datetime-local"
              aria-label="Prazo"
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
          value={vista}
          onChange={(next) => setVista(next as Vista)}
          tabs={[
            { key: 'hoje', label: 'Hoje', meta: `${hoje.length}/${MAX_HOJE}` },
            { key: 'semana', label: 'Esta semana', meta: semana.length },
            { key: 'ideias', label: 'Ideias', meta: ideias.length },
            { key: 'todas', label: 'Todas pendentes', meta: pendentes.length },
            { key: 'concluidas', label: 'Concluídas', meta: concluidas.length },
          ]}
        />

        <section className="overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
          {list.isLoading && <p className="p-4 text-sm text-zinc-500">A carregar...</p>}
          {list.isError && <p className="p-4 text-sm text-rose-600">{apiErrorMessage(list.error)}</p>}
          {!list.isLoading && tasks.length === 0 && (
            <div className="p-10 text-center text-sm text-zinc-500">
              <ClipboardList className="mx-auto mb-2 text-zinc-400" size={28} />
              {vista === 'hoje' ? `Escolhe até ${MAX_HOJE} prioridades para hoje.` : vista === 'ideias' ? 'Sem ideias guardadas. Usa o botão 💡 no topo quando aparecer uma.' : 'Nada aqui.'}
            </div>
          )}
          {tasks.length > 0 && (
            <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {tasks.map((t) => (
                <TaskRow
                  key={t.id}
                  task={t}
                  hojeCheio={hoje.length >= MAX_HOJE}
                  onFoco={(foco) => mudarFoco.mutate({ id: t.id, foco })}
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

function TaskRow({ task, hojeCheio, onFoco, onToggleDone, onCancel, onDelete }: {
  task: InternalTask;
  hojeCheio: boolean;
  onFoco: (foco: TarefaFoco) => void;
  onToggleDone: () => void;
  onCancel: () => void;
  onDelete: () => void;
}) {
  const isDone = task.status === InternalTaskStatus.Concluida;
  const isCancelled = task.status === InternalTaskStatus.Cancelada;
  const pendente = task.status === InternalTaskStatus.Pendente;
  const overdue = Boolean(pendente && task.dueAt && new Date(task.dueAt).getTime() < Date.now());

  return (
    <li className={`flex items-start gap-3 p-4 ${isCancelled ? 'opacity-60' : ''}`}>
      <button
        type="button"
        onClick={onToggleDone}
        disabled={isCancelled}
        className="mt-0.5 flex-none text-zinc-400 transition hover:text-emerald-600 disabled:hover:text-zinc-400 dark:hover:text-emerald-400"
        title={isDone ? 'Marcar como pendente' : 'Marcar concluída'}
        aria-label={isDone ? `Marcar «${task.title}» como pendente` : `Concluir «${task.title}»`}
      >
        {isDone ? <CheckCircle2 size={20} className="text-emerald-600 dark:text-emerald-400" /> : <Circle size={20} />}
      </button>

      <div className="min-w-0 flex-1">
        <div className={`text-sm font-medium ${isDone ? 'line-through text-zinc-500' : 'text-zinc-950 dark:text-zinc-50'}`}>{task.title}</div>
        {task.description && <div className="mt-0.5 whitespace-pre-wrap text-xs text-zinc-500">{task.description}</div>}
        <div className="mt-2 flex flex-wrap items-center gap-2 text-[11px] text-zinc-500">
          <span className={`rounded px-1.5 py-0.5 font-medium ${FOCO_COR[task.foco]}`}>{FOCO_LABEL[task.foco]}</span>
          {task.dueAt && (
            <span className={`rounded px-1.5 py-0.5 ${overdue ? 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-300' : 'bg-zinc-100 dark:bg-zinc-800'}`}>
              {overdue ? 'Atrasada · ' : 'Prazo: '}
              {new Date(task.dueAt).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}
            </span>
          )}
          {task.assignedToDisplayName && <span className="rounded bg-zinc-100 px-1.5 py-0.5 dark:bg-zinc-800">@{task.assignedToDisplayName}</span>}
          {task.vendaNumero && task.vendaId && (
            <Link to={`/vendas/${task.vendaId}`} className="rounded bg-brand-50 px-1.5 py-0.5 text-brand-700 hover:underline dark:bg-brand-950/40 dark:text-brand-300">
              Venda #{task.vendaNumero}
            </Link>
          )}
        </div>
        {pendente && (
          <div className="mt-2 flex flex-wrap gap-1" role="group" aria-label="Mudar foco">
            {([TarefaFoco.Hoje, TarefaFoco.Semana, TarefaFoco.Ideia] as const).filter((f) => f !== task.foco).map((f) => (
              <button
                key={f}
                type="button"
                disabled={f === TarefaFoco.Hoje && hojeCheio}
                title={f === TarefaFoco.Hoje && hojeCheio ? `«Hoje» já tem ${MAX_HOJE}` : undefined}
                onClick={() => onFoco(f)}
                className="rounded-full border border-zinc-200 px-2 py-0.5 text-[11px] text-zinc-600 transition hover:border-brand-300 hover:text-brand-700 disabled:cursor-not-allowed disabled:opacity-40 dark:border-zinc-700 dark:text-zinc-300"
              >
                → {FOCO_LABEL[f]}
              </button>
            ))}
          </div>
        )}
      </div>

      {pendente && (
        <button
          type="button"
          onClick={onCancel}
          className="flex-none rounded-md p-1 text-zinc-400 transition hover:bg-zinc-100 hover:text-amber-600 dark:hover:bg-zinc-800"
          title="Cancelar"
          aria-label={`Cancelar «${task.title}»`}
        >
          <XCircle size={16} />
        </button>
      )}
      <button
        type="button"
        onClick={onDelete}
        className="flex-none rounded-md p-1 text-zinc-400 transition hover:bg-zinc-100 hover:text-red-600 dark:hover:bg-zinc-800"
        title="Eliminar"
        aria-label={`Eliminar «${task.title}»`}
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
