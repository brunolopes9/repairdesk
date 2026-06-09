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
 * Sprint 118: shared component reusado em criar reparação/trabalho/venda — qualquer fluxo onde o
 * utilizador descobre que o cliente ainda não existe.
 * Sprint 538: passa a capturar dados de faturação (NIF + morada + código postal + localidade) com
 * "Verificar NIF" (auto-preenche nome e morada via AT). Necessário porque emitir Fatura com NIF
 * exige LEGALMENTE a morada (CIVA art. 36.º n.º 5) — sem ela o Moloni recusa. Para consumidor final,
 * basta o nome (campos de faturação ficam vazios).
 */
export default function NovoClienteModal({ open, onClose, onCreated }: Props) {
  const [nome, setNome] = useState('');
  const [telefone, setTelefone] = useState('');
  const [nif, setNif] = useState('');
  const [morada, setMorada] = useState('');
  const [codigoPostal, setCodigoPostal] = useState('');
  const [localidade, setLocalidade] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  function reset() {
    setNome(''); setTelefone(''); setNif(''); setMorada(''); setCodigoPostal(''); setLocalidade('');
    setError(null); setAviso(null);
  }

  const create = useMutation({
    mutationFn: () => clientesApi.create({
      nome: nome.trim(),
      telefone: telefone.trim() || null,
      email: null,
      nif: nif.trim() || null,
      notas: null,
      morada: morada.trim() || null,
      codigoPostal: codigoPostal.trim() || null,
      localidade: localidade.trim() || null,
    }),
    onSuccess: (c) => { reset(); onCreated(c); },
    onError: (err) => {
      if (isAxiosError(err)) {
        const data = err.response?.data as { detail?: string; errors?: Record<string, string[]> } | undefined;
        if (data?.errors) setError(Object.values(data.errors).flat().join(' '));
        else setError(data?.detail ?? 'Erro');
      }
    },
  });

  // Sprint 538: auto-preenche nome + morada a partir do NIF (lookup AT), como o "Verificar contribuinte" do Moloni.
  const lookup = useMutation({
    mutationFn: () => clientesApi.lookupAtNif(nif.trim()),
    onSuccess: (r) => {
      if (r.nome && !nome.trim()) setNome(r.nome);
      if (r.morada) setMorada(r.morada);
      setError(null);
      setAviso(r.morada ? 'Dados preenchidos a partir do NIF.' : 'NIF verificado, mas sem morada — preenche à mão.');
    },
    onError: () => { setAviso(null); setError('Não foi possível verificar o NIF. Preenche os dados à mão.'); },
  });

  function handleClose() { reset(); onClose(); }

  const inputCls = 'min-h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950';
  const labelCls = 'text-xs font-medium uppercase tracking-wide text-zinc-500';

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
        {aviso && <div className="rounded-lg bg-emerald-50 px-3 py-2 text-xs text-emerald-700 dark:bg-emerald-950/30 dark:text-emerald-300">{aviso}</div>}
        <div className="space-y-1">
          <label className={labelCls}>Nome <span className="text-red-500">*</span></label>
          <input value={nome} onChange={e => setNome(e.target.value)} className={inputCls} autoFocus />
        </div>
        <div className="space-y-1">
          <label className={labelCls}>Telefone (opcional)</label>
          <input value={telefone} onChange={e => setTelefone(e.target.value)} className={inputCls} placeholder="ou vazio se for via Messenger" />
        </div>

        <div className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
          <p className="mb-2 text-xs font-semibold text-zinc-600 dark:text-zinc-300">Dados de faturação <span className="font-normal text-zinc-400">— para Fatura com NIF</span></p>
          <div className="space-y-1">
            <label className={labelCls}>NIF</label>
            <div className="flex gap-2">
              <input value={nif} onChange={e => setNif(e.target.value)} className={inputCls} placeholder="Contribuinte" inputMode="numeric" />
              <button type="button" disabled={nif.trim().length < 9 || lookup.isPending}
                onClick={() => lookup.mutate()}
                className="whitespace-nowrap rounded-lg border border-brand-300 bg-brand-50 px-3 text-xs font-medium text-brand-700 hover:bg-brand-100 disabled:opacity-50 dark:border-brand-800/60 dark:bg-brand-950/30 dark:text-brand-200">
                {lookup.isPending ? 'A verificar…' : 'Verificar NIF'}
              </button>
            </div>
          </div>
          <div className="mt-2 space-y-1">
            <label className={labelCls}>Morada</label>
            <input value={morada} onChange={e => setMorada(e.target.value)} className={inputCls} placeholder="Rua, número" />
          </div>
          <div className="mt-2 grid grid-cols-2 gap-2">
            <div className="space-y-1">
              <label className={labelCls}>Código postal</label>
              <input value={codigoPostal} onChange={e => setCodigoPostal(e.target.value)} className={inputCls} placeholder="0000-000" />
            </div>
            <div className="space-y-1">
              <label className={labelCls}>Localidade</label>
              <input value={localidade} onChange={e => setLocalidade(e.target.value)} className={inputCls} />
            </div>
          </div>
        </div>
        <p className="text-[11px] text-zinc-500">
          Consumidor final: basta o nome. Empresa / Fatura com NIF: preenche o NIF e carrega <strong>Verificar NIF</strong> para puxar o nome e a morada.
        </p>
      </div>
    </Modal>
  );
}
