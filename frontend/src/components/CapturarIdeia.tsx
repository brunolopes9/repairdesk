import { useEffect, useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Lightbulb } from 'lucide-react';
import { Button } from './ui';
import { apiErrorMessage } from '../lib/errors';
import { toast } from '../lib/toast';
import { internalTasksApi } from '../lib/internalTasks/api';
import { TarefaFoco } from '../lib/internalTasks/types';

/**
 * Sprint 617 (Doc 98 F2): captura rápida. Uma ideia a meio de um bloco não se executa — guarda-se.
 * Botão 💡 no topo de todas as páginas (atalho: Alt+I). Fica em Tarefas → Ideias.
 */
export function CapturarIdeia() {
  const qc = useQueryClient();
  const [aberto, setAberto] = useState(false);
  const [texto, setTexto] = useState('');
  const input = useRef<HTMLInputElement>(null);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.altKey && (e.key === 'i' || e.key === 'I')) {
        e.preventDefault();
        setAberto(true);
      }
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  useEffect(() => {
    if (aberto) setTimeout(() => input.current?.focus(), 30);
  }, [aberto]);

  const guardar = useMutation({
    mutationFn: () => internalTasksApi.create({ title: texto.trim(), foco: TarefaFoco.Ideia }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['internal-tasks'] });
      toast.success('💡 Guardada nas Ideias', 'Volta ao bloco atual. Decides isto no planeamento.');
      setTexto('');
      setAberto(false);
    },
    onError: (e) => toast.error('Não ficou guardada', apiErrorMessage(e)),
  });

  return (
    <>
      <button
        type="button"
        onClick={() => setAberto(true)}
        title="Guardar uma ideia para mais tarde (Alt+I)"
        aria-label="Guardar uma ideia para mais tarde"
        className="flex h-9 items-center gap-1.5 rounded-lg border border-amber-200 bg-amber-50 px-2.5 text-sm font-medium text-amber-800 transition hover:bg-amber-100 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200"
      >
        <Lightbulb size={16} />
        <span className="hidden md:inline">Ideia</span>
      </button>
      {aberto && (
        <div className="fixed inset-0 z-50 flex items-start justify-center bg-black/40 p-4 pt-[15vh]" onClick={() => setAberto(false)}>
          <form
            role="dialog"
            aria-modal="true"
            aria-labelledby="ideia-titulo"
            className="w-full max-w-lg rounded-2xl border border-zinc-200 bg-white p-4 shadow-xl dark:border-zinc-800 dark:bg-zinc-900"
            onClick={(e) => e.stopPropagation()}
            onSubmit={(e) => {
              e.preventDefault();
              if (texto.trim().length >= 2) guardar.mutate();
            }}
          >
            <h2 id="ideia-titulo" className="flex items-center gap-2 font-semibold"><Lightbulb size={18} className="text-amber-500" /> Guardar ideia</h2>
            <p className="mt-1 text-sm text-zinc-500">Escreve e volta ao que estavas a fazer. Não é para executar agora.</p>
            <input
              ref={input}
              value={texto}
              maxLength={200}
              onChange={(e) => setTexto(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Escape') setAberto(false); }}
              placeholder="ex.: integração financeira no Mender"
              aria-label="Ideia"
              className="mt-3 w-full rounded-lg border border-zinc-300 px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950"
            />
            <div className="mt-3 flex justify-end gap-2">
              <Button type="button" variant="ghost" onClick={() => setAberto(false)}>Cancelar</Button>
              <Button type="submit" loading={guardar.isPending} disabled={texto.trim().length < 2}>Guardar</Button>
            </div>
          </form>
        </div>
      )}
    </>
  );
}
