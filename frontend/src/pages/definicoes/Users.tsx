import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Shield, UserCog } from 'lucide-react';
import { toast } from '../../lib/toast';
import { usersApi } from '../../lib/users/api';
import { APP_ROLES, ROLE_DESCRIPTION, ROLE_LABEL, type UserListItem } from '../../lib/users/types';
import { PageHeader, SkeletonTable, Button } from '../../components/ui';

export default function UsersDefinicoes() {
  const qc = useQueryClient();
  const [editing, setEditing] = useState<UserListItem | null>(null);

  const usersQuery = useQuery({
    queryKey: ['users-list'],
    queryFn: () => usersApi.list(),
  });

  return (
    <div className="space-y-4">
      <PageHeader
        title="Utilizadores"
        description="Gestão de roles dos utilizadores do tenant. Sprint 311 (Doc 72 Fase D)."
      />

      {usersQuery.isLoading && <SkeletonTable rows={3} cols={4} />}
      {usersQuery.isError && (
        <div className="rounded-lg border border-red-300 bg-red-50 p-4 text-sm text-red-800 dark:bg-red-950 dark:text-red-200">
          Erro a carregar utilizadores. Provavelmente não tens role <code>Admin</code>.
        </div>
      )}

      {usersQuery.data && (
        <div className="overflow-hidden rounded-lg border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-950">
          <table className="w-full text-sm">
            <thead className="bg-zinc-50 text-left text-zinc-600 dark:bg-zinc-900 dark:text-zinc-300">
              <tr>
                <th className="px-4 py-2 font-medium">Nome</th>
                <th className="px-4 py-2 font-medium">Email</th>
                <th className="px-4 py-2 font-medium">Estado</th>
                <th className="px-4 py-2 font-medium">Roles</th>
                <th className="px-4 py-2 font-medium" />
              </tr>
            </thead>
            <tbody>
              {usersQuery.data.map((u) => (
                <tr key={u.id} className="border-t border-zinc-200 dark:border-zinc-800">
                  <td className="px-4 py-2">{u.displayName || '—'}</td>
                  <td className="px-4 py-2 font-mono text-xs">{u.email}</td>
                  <td className="px-4 py-2">
                    {u.isActive ? (
                      <span className="rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-950 dark:text-green-300">Activo</span>
                    ) : (
                      <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-xs font-medium text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">Inactivo</span>
                    )}
                  </td>
                  <td className="px-4 py-2">
                    {u.roles.length === 0 ? (
                      <span className="text-xs text-zinc-500">(sem roles)</span>
                    ) : (
                      <div className="flex flex-wrap gap-1">
                        {u.roles.map((r) => (
                          <span key={r} className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-800 dark:bg-blue-950 dark:text-blue-300">
                            <Shield size={11} /> {r}
                          </span>
                        ))}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-2 text-right">
                    <button
                      type="button"
                      onClick={() => setEditing(u)}
                      className="inline-flex items-center gap-1 rounded-md border border-zinc-300 px-2 py-1 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800"
                    >
                      <UserCog size={13} /> Editar roles
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {editing && (
        <EditRolesModal
          user={editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            qc.invalidateQueries({ queryKey: ['users-list'] });
          }}
        />
      )}
    </div>
  );
}

function EditRolesModal({ user, onClose, onSaved }: { user: UserListItem; onClose: () => void; onSaved: () => void }) {
  const [roles, setRoles] = useState<Set<string>>(new Set(user.roles));

  const save = useMutation({
    mutationFn: () => usersApi.setRoles(user.id, Array.from(roles)),
    onSuccess: () => {
      toast.success(`Roles actualizadas para ${user.displayName}`);
      onSaved();
    },
    onError: (err) => {
      const e = err as { response?: { data?: { code?: string; message?: string } } };
      toast.error(e.response?.data?.message ?? e.response?.data?.code ?? 'Erro ao actualizar roles.');
    },
  });

  function toggle(role: string) {
    const next = new Set(roles);
    if (next.has(role)) next.delete(role);
    else next.add(role);
    setRoles(next);
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-2xl dark:bg-zinc-900">
        <h2 className="text-lg font-semibold">Roles — {user.displayName}</h2>
        <p className="mt-1 text-xs text-zinc-500">{user.email}</p>

        <div className="mt-4 space-y-3">
          {APP_ROLES.map((role) => (
            <label key={role} className="flex cursor-pointer items-start gap-3 rounded-lg border border-zinc-200 p-3 hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800">
              <input
                type="checkbox"
                checked={roles.has(role)}
                onChange={() => toggle(role)}
                className="mt-0.5"
              />
              <div className="flex-1">
                <div className="text-sm font-medium">{ROLE_LABEL[role]}</div>
                <div className="text-xs text-zinc-500">{ROLE_DESCRIPTION[role]}</div>
              </div>
            </label>
          ))}
        </div>

        <div className="mt-4 flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>Cancelar</Button>
          <Button onClick={() => save.mutate()} disabled={save.isPending}>
            {save.isPending ? 'A guardar…' : 'Guardar'}
          </Button>
        </div>
      </div>
    </div>
  );
}
