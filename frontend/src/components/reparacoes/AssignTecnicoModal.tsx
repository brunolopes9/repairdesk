import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { reparacoesApi } from '../../lib/reparacoes/api';
import { usersApi } from '../../lib/users/api';
import { toast } from '../../lib/toast';
import Modal from '../Modal';

interface Props {
  open: boolean;
  reparacaoId: string;
  currentUserId: string | null;
  onClose: () => void;
}

/**
 * Sprint 343: modal para Admin atribuir/desatribuir técnico a uma reparação.
 * Lista users do tenant com role Tech ou Admin (quem pode trabalhar em reparações).
 */
export default function AssignTecnicoModal({ open, reparacaoId, currentUserId, onClose }: Props) {
  const qc = useQueryClient();
  const [selected, setSelected] = useState<string | null>(currentUserId);

  const usersQuery = useQuery({
    queryKey: ['users-list'],
    queryFn: () => usersApi.list(),
    enabled: open,
  });

  const candidates = (usersQuery.data ?? [])
    .filter((u) => u.isActive && (u.roles.includes('Tech') || u.roles.includes('Admin')))
    .sort((a, b) => a.displayName.localeCompare(b.displayName));

  const save = useMutation({
    mutationFn: () => reparacoesApi.assign(reparacaoId, selected),
    onSuccess: () => {
      toast.success(selected ? 'Técnico atribuído.' : 'Atribuição removida.');
      qc.invalidateQueries({ queryKey: ['reparacao', reparacaoId] });
      onClose();
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'Erro ao atribuir.'),
  });

  return (
    <Modal
      open={open}
      title="Atribuir técnico"
      onClose={onClose}
      footer={<>
        <button
          type="button"
          onClick={onClose}
          className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
        >
          Cancelar
        </button>
        <button
          type="button"
          disabled={save.isPending || selected === currentUserId}
          onClick={() => save.mutate()}
          className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
        >
          {save.isPending ? 'A guardar…' : 'Guardar'}
        </button>
      </>}
    >
      <div className="space-y-2">
        <label className="flex cursor-pointer items-center gap-3 rounded-lg border border-zinc-200 p-3 hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800">
          <input
            type="radio"
            name="tecnico"
            checked={selected === null}
            onChange={() => setSelected(null)}
          />
          <span className="text-sm text-zinc-500">— Sem atribuição —</span>
        </label>
        {usersQuery.isLoading && <p className="text-xs text-zinc-500">A carregar técnicos…</p>}
        {candidates.length === 0 && !usersQuery.isLoading && (
          <p className="text-xs text-zinc-500">
            Não há técnicos disponíveis. Atribui role <code>Tech</code> em Definições → Utilizadores.
          </p>
        )}
        {candidates.map((u) => (
          <label
            key={u.id}
            className="flex cursor-pointer items-start gap-3 rounded-lg border border-zinc-200 p-3 hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800"
          >
            <input
              type="radio"
              name="tecnico"
              checked={selected === u.id}
              onChange={() => setSelected(u.id)}
              className="mt-0.5"
            />
            <div className="flex-1">
              <div className="text-sm font-medium">{u.displayName}</div>
              <div className="text-xs text-zinc-500">{u.email} · {u.roles.join(', ')}</div>
            </div>
          </label>
        ))}
      </div>
    </Modal>
  );
}
