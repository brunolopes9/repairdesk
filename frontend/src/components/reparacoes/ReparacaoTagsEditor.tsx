import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, X } from 'lucide-react';
import { reparacaoTagsApi } from '../../lib/reparacaoTags/api';
import { toast } from '../../lib/toast';

interface Props {
  reparacaoId: string;
  currentTagIds: string[];
}

/**
 * Sprint 346: editor inline de tags em ReparacaoDetalhe. Mostra chips das tags
 * atribuídas + dropdown para adicionar/remover. Admins podem criar tag inline.
 */
export default function ReparacaoTagsEditor({ reparacaoId, currentTagIds }: Props) {
  const qc = useQueryClient();
  const [pickerOpen, setPickerOpen] = useState(false);
  const [creating, setCreating] = useState(false);
  const [newNome, setNewNome] = useState('');

  const tagsQuery = useQuery({
    queryKey: ['reparacao-tags-all'],
    queryFn: () => reparacaoTagsApi.list(),
    staleTime: 60_000,
  });
  const all = tagsQuery.data ?? [];

  const selected = new Set(currentTagIds);
  const current = all.filter((t) => selected.has(t.id));
  const available = all.filter((t) => !selected.has(t.id));

  const setTagsMut = useMutation({
    mutationFn: (ids: string[]) => reparacaoTagsApi.setForReparacao(reparacaoId, ids),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reparacao', reparacaoId] });
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'Erro a guardar tags.'),
  });

  const createMut = useMutation({
    mutationFn: (nome: string) => reparacaoTagsApi.create({ nome }),
    onSuccess: (tag) => {
      toast.success(`Tag "${tag.nome}" criada.`);
      qc.invalidateQueries({ queryKey: ['reparacao-tags-all'] });
      setTagsMut.mutate([...currentTagIds, tag.id]);
      setNewNome('');
      setCreating(false);
    },
    onError: (err) => {
      const e = err as { response?: { data?: { code?: string; message?: string } } };
      toast.error(e.response?.data?.message ?? 'Erro ao criar tag.');
    },
  });

  function add(id: string) {
    setTagsMut.mutate([...currentTagIds, id]);
    setPickerOpen(false);
  }
  function remove(id: string) {
    setTagsMut.mutate(currentTagIds.filter((x) => x !== id));
  }

  return (
    <div className="flex flex-wrap items-center gap-1">
      {current.map((t) => (
        <span
          key={t.id}
          className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium text-white"
          style={{ background: t.corHex }}
        >
          {t.nome}
          <button
            type="button"
            onClick={() => remove(t.id)}
            className="hover:opacity-70"
            title="Remover tag"
          >
            <X size={10} />
          </button>
        </span>
      ))}
      <div className="relative">
        <button
          type="button"
          onClick={() => setPickerOpen((v) => !v)}
          className="inline-flex items-center gap-1 rounded-full border border-zinc-300 px-2 py-0.5 text-[11px] hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800"
        >
          <Plus size={11} /> Tag
        </button>
        {pickerOpen && (
          <div className="absolute left-0 top-full z-20 mt-1 w-56 rounded-lg border border-zinc-200 bg-white p-2 shadow-lg dark:border-zinc-700 dark:bg-zinc-900">
            {available.length === 0 && (
              <p className="px-2 py-1 text-xs text-zinc-500">Todas as tags já estão aplicadas.</p>
            )}
            {available.map((t) => (
              <button
                key={t.id}
                type="button"
                onClick={() => add(t.id)}
                className="flex w-full items-center gap-2 rounded px-2 py-1 text-left text-xs hover:bg-zinc-50 dark:hover:bg-zinc-800"
              >
                <span className="inline-block h-3 w-3 rounded-full" style={{ background: t.corHex }} />
                {t.nome}
              </button>
            ))}
            <div className="mt-1 border-t border-zinc-200 pt-1 dark:border-zinc-700">
              {creating ? (
                <div className="flex gap-1">
                  <input
                    type="text"
                    autoFocus
                    value={newNome}
                    onChange={(e) => setNewNome(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' && newNome.trim()) createMut.mutate(newNome.trim());
                      if (e.key === 'Escape') { setCreating(false); setNewNome(''); }
                    }}
                    placeholder="Nova tag…"
                    className="flex-1 rounded border border-zinc-300 px-1.5 py-1 text-xs dark:border-zinc-700 dark:bg-zinc-800"
                  />
                  <button
                    type="button"
                    onClick={() => newNome.trim() && createMut.mutate(newNome.trim())}
                    disabled={!newNome.trim() || createMut.isPending}
                    className="rounded bg-brand-600 px-2 text-xs text-white disabled:opacity-50"
                  >
                    OK
                  </button>
                </div>
              ) : (
                <button
                  type="button"
                  onClick={() => setCreating(true)}
                  className="w-full rounded px-2 py-1 text-left text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800"
                >
                  + Criar nova tag
                </button>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
