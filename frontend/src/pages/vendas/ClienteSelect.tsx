import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Plus, UserPlus, X } from 'lucide-react';
import { toast } from '../../lib/toast';
import NovoClienteModal from '../../components/NovoClienteModal';
import { Button } from '../../components/ui';
import { inputCls } from '../../components/ui/formClasses';
import { clientesApi } from '../../lib/clientes/api';

export interface ClienteEscolhido {
  id: string;
  nome: string;
}

/** Procura de cliente com "Novo cliente" inline (reutiliza o NovoClienteModal). */
export default function ClienteSelect({
  value,
  onChange,
  disabled,
}: {
  value: ClienteEscolhido | null;
  onChange: (c: ClienteEscolhido | null) => void;
  disabled?: boolean;
}) {
  const [q, setQ] = useState('');
  const [debounced, setDebounced] = useState('');
  const [novoOpen, setNovoOpen] = useState(false);
  const [aCriar, setACriar] = useState(false);

  // Sprint 669: escrever o nome e não escolher perdia o cliente («Consumidor final»). Agora cria-se num clique (ou Enter).
  async function criarRapido() {
    const nome = q.trim();
    if (nome.length < 2 || aCriar) return;
    setACriar(true);
    try {
      const c = await clientesApi.create({ nome, telefone: null, email: null, nif: null, notas: null });
      onChange({ id: c.id, nome: c.nome });
      setQ('');
      toast.success(`Cliente «${c.nome}» criado`);
    } catch (e) {
      toast.fromError(e, 'Não foi possível criar o cliente.');
    } finally {
      setACriar(false);
    }
  }

  useEffect(() => {
    const t = setTimeout(() => setDebounced(q.trim()), 250);
    return () => clearTimeout(t);
  }, [q]);

  const resultados = useQuery({
    queryKey: ['clientes', 'select', debounced],
    queryFn: () => clientesApi.list(debounced, 1, 8),
    enabled: debounced.length >= 2 && !value,
  });

  if (value) {
    return (
      <div className="flex min-h-11 items-center justify-between gap-2 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950">
        <span className="font-medium">{value.nome}</span>
        {!disabled && (
          <button type="button" onClick={() => onChange(null)} aria-label="Trocar cliente" className="rounded p-1 text-zinc-500 hover:bg-zinc-100 dark:hover:bg-zinc-800">
            <X size={14} />
          </button>
        )}
      </div>
    );
  }

  return (
    <div className="relative">
      <div className="flex gap-2">
        <input
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onKeyDown={(e) => {
            if (e.key !== 'Enter') return;
            e.preventDefault();
            const exato = resultados.data?.items.find((c) => c.nome.trim().toLowerCase() === q.trim().toLowerCase());
            if (exato) { onChange({ id: exato.id, nome: exato.nome }); setQ(''); } else void criarRapido();
          }}
          className={inputCls}
          placeholder="Nome, telefone ou NIF…"
          aria-label="Procurar cliente"
          disabled={disabled}
        />
        <Button type="button" variant="secondary" leftIcon={<UserPlus size={15} />} onClick={() => setNovoOpen(true)} disabled={disabled}>
          Novo
        </Button>
      </div>
      {q.trim().length >= 2 && (
        <p className="mt-1 text-xs font-medium text-amber-700 dark:text-amber-300">Escolhe da lista ou cria — só o texto escrito não fica gravado.</p>
      )}
      {debounced.length >= 2 && (
        <ul className="absolute z-20 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border border-zinc-200 bg-white shadow-lg dark:border-zinc-800 dark:bg-zinc-900">
          {!resultados.data?.items.some((c) => c.nome.trim().toLowerCase() === debounced.toLowerCase()) && (
            <li>
              <button type="button" onClick={() => void criarRapido()} disabled={aCriar}
                className="flex min-h-11 w-full items-center gap-2 border-b border-zinc-100 px-3 py-2 text-left text-sm font-semibold text-brand-700 hover:bg-brand-50 dark:border-zinc-800 dark:text-brand-300 dark:hover:bg-brand-950/30">
                <Plus size={15} /> Criar cliente «{q.trim()}»
              </button>
            </li>
          )}
          {(resultados.data?.items ?? []).map((c) => (
            <li key={c.id}>
              <button
                type="button"
                onClick={() => { onChange({ id: c.id, nome: c.nome }); setQ(''); }}
                className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800"
              >
                <span className="font-medium">{c.nome}</span>
                <span className="text-xs text-zinc-500">{c.telefone ?? c.nif ?? ''}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <NovoClienteModal
        open={novoOpen}
        onClose={() => setNovoOpen(false)}
        onCreated={(c) => { onChange(c); setNovoOpen(false); }}
      />
    </div>
  );
}
