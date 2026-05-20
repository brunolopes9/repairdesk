import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import Modal from './Modal';
import { clientesApi } from '../lib/clientes/api';

interface Props {
  open: boolean;
  onClose: () => void;
  onCreated: (c: { id: string; nome: string }) => void;
}

/**
 * Sprint 118: extraído de Reparacoes.tsx para shared component. Reusado no modal
 * de criar trabalho, criar reparação, criar venda — qualquer fluxo onde o utilizador
 * pode descobrir que o cliente ainda não existe.
 *
 * Form mínimo: nome obrigatório, telefone opcional. NIF, email, notas ficam para
 * edição posterior em /clientes/{id}. Suficiente para "consumidor final" workflow.
 */
export default function NovoClienteModal({ open, onClose, onCreated }: Props) {
  const [nome, setNome] = useState('');
  const [telefone, setTelefone] = useState('');
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () => clientesApi.create({
      nome: nome.trim(),
      telefone: telefone.trim() || null,
      email: null,
      nif: null,
      notas: null,
    }),
    onSuccess: (c) => { setNome(''); setTelefone(''); setError(null); onCreated(c); },
    onError: (err) => {
      if (isAxiosError(err)) {
        const data = err.response?.data as { detail?: string; errors?: Record<string, string[]> } | undefined;
        if (data?.errors) setError(Object.values(data.errors).flat().join(' '));
        else setError(data?.detail ?? 'Erro');
      }
    },
  });

  function handleClose() {
    setNome(''); setTelefone(''); setError(null);
    onClose();
  }

  return (
    <Modal open={open} title="Novo cliente" onClose={handleClose}
      footer={<>
        <button type="button" onClick={handleClose} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
        <button type="button" disabled={!nome || create.isPending}
          onClick={() => create.mutate()}
          className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
          {create.isPending ? 'A criar…' : 'Criar e selecionar'}
        </button>
      </>}
    >
      <div className="space-y-3">
        {error && <div className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950/40 dark:text-red-300">{error}</div>}
        <div className="space-y-1">
          <label className="text-xs font-medium uppercase tracking-wide text-zinc-500">
            Nome <span className="text-red-500">*</span>
          </label>
          <input
            value={nome}
            onChange={e => setNome(e.target.value)}
            className="min-h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
            autoFocus
          />
        </div>
        <div className="space-y-1">
          <label className="text-xs font-medium uppercase tracking-wide text-zinc-500">
            Telefone (opcional)
          </label>
          <input
            value={telefone}
            onChange={e => setTelefone(e.target.value)}
            className="min-h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
            placeholder="ou vazio se for via Messenger"
          />
        </div>
        <p className="text-[11px] text-zinc-500">
          NIF e mais campos podem ser adicionados depois em <code>Clientes</code> → editar.
        </p>
      </div>
    </Modal>
  );
}
