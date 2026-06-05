import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, Search } from 'lucide-react';
import Modal from '../Modal';
import { toast } from '../../lib/toast';
import { clientesApi } from '../../lib/clientes/api';
import { vendasApi } from '../../lib/vendas/api';
import { PAYMENT_METHOD, type PaymentMethod } from '../../lib/vendas/types';
import type { Cliente } from '../../lib/clientes/types';
import { formatCents } from '../../lib/money';

/**
 * Sprint 516: "Nova fatura" (documento avulso) — emite uma Fatura/Fatura Simplificada do zero,
 * sem reparação nem venda de balcão (ex.: faturar uma consultoria). Reusa toda a máquina de Vendas:
 * cria uma venda com linhas livres (sem stock) e emite a fatura num passo. Aparece logo em Vendas·Faturas.
 */

const inputCls = 'w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-900';

interface Linha {
  descricao: string;
  precoEur: string;
  qtd: number;
}

const METODOS: Array<[PaymentMethod, string]> = [
  [PAYMENT_METHOD.Dinheiro, 'Numerário'],
  [PAYMENT_METHOD.Multibanco, 'Multibanco'],
  [PAYMENT_METHOD.MBWay, 'MBWay'],
  [PAYMENT_METHOD.TransferenciaBancaria, 'Transferência'],
  [PAYMENT_METHOD.Cartao, 'Cartão'],
];

const eur = (s: string) => parseFloat(s.replace(',', '.')) || 0;

export default function NovaFaturaModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const qc = useQueryClient();
  const [cliente, setCliente] = useState<Cliente | null>(null);
  const [busca, setBusca] = useState('');
  const [linhas, setLinhas] = useState<Linha[]>([{ descricao: '', precoEur: '', qtd: 1 }]);
  const [metodo, setMetodo] = useState<PaymentMethod>(PAYMENT_METHOD.Dinheiro);
  const [morada, setMorada] = useState('');
  const [cp, setCp] = useState('');
  const [localidade, setLocalidade] = useState('');

  const resultados = useQuery({
    queryKey: ['clientes-busca-fatura', busca],
    queryFn: () => clientesApi.list(busca, 1, 6),
    enabled: open && !cliente && busca.trim().length >= 2,
    staleTime: 30_000,
  });

  const comNif = !!cliente?.nif;
  const precisaMorada = comNif && !morada.trim();
  const totalCents = useMemo(
    () => linhas.reduce((s, l) => s + Math.round(eur(l.precoEur) * 100) * (l.qtd || 1), 0),
    [linhas],
  );
  const linhasValidas = linhas.filter((l) => l.descricao.trim() && eur(l.precoEur) > 0);

  function reset() {
    setCliente(null);
    setBusca('');
    setLinhas([{ descricao: '', precoEur: '', qtd: 1 }]);
    setMetodo(PAYMENT_METHOD.Dinheiro);
    setMorada('');
    setCp('');
    setLocalidade('');
  }
  function fechar() {
    reset();
    onClose();
  }
  function pick(c: Cliente) {
    setCliente(c);
    setBusca('');
    setMorada(c.morada ?? '');
    setCp(c.codigoPostal ?? '');
    setLocalidade(c.localidade ?? '');
  }
  function setLinha(i: number, patch: Partial<Linha>) {
    setLinhas((prev) => prev.map((x, j) => (j === i ? { ...x, ...patch } : x)));
  }

  const emitir = useMutation({
    mutationFn: async () => {
      // Com NIF: garante a morada na ficha do cliente (o backend recusa Fatura com NIF sem morada — S511/S516).
      if (cliente && comNif && morada.trim() !== (cliente.morada ?? '')) {
        const full = await clientesApi.get(cliente.id);
        await clientesApi.update(cliente.id, {
          nome: full.nome,
          telefone: full.telefone,
          email: full.email,
          nif: full.nif,
          notas: full.notas,
          morada: morada.trim() || null,
          codigoPostal: cp.trim() || null,
          localidade: localidade.trim() || null,
        });
      }
      const venda = await vendasApi.create({
        clienteId: cliente?.id ?? null,
        notas: null,
        items: linhasValidas.map((l) => ({
          partId: null,
          descricao: l.descricao.trim(),
          quantidade: l.qtd || 1,
          precoUnitarioCents: Math.round(eur(l.precoEur) * 100),
          descontoCents: 0,
          ivaRate: 23,
        })),
      });
      // Marca paga + emite a fatura num só passo.
      return vendasApi.marcarPaga(venda.id, metodo, true);
    },
    onSuccess: (res) => {
      qc.invalidateQueries({ queryKey: ['documentos-vendas'] });
      qc.invalidateQueries({ queryKey: ['documentos-vendas-mes'] });
      toast.success(
        res.invoice?.number ? `Fatura ${res.invoice.number} emitida` : 'Fatura emitida',
        'Já aparece na lista de Vendas.',
      );
      fechar();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível emitir a fatura.'),
  });

  const podeEmitir = linhasValidas.length > 0 && !precisaMorada && !emitir.isPending;

  return (
    <Modal
      open={open}
      title="Nova fatura"
      onClose={fechar}
      footer={<>
        <button type="button" onClick={fechar} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
        <button
          type="button"
          disabled={!podeEmitir}
          onClick={() => emitir.mutate()}
          className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
        >
          {emitir.isPending ? 'A emitir…' : `Emitir · ${formatCents(totalCents)}`}
        </button>
      </>}
    >
      <div className="space-y-4 text-sm">
        {/* Cliente */}
        <div>
          <label className="block text-xs font-medium uppercase text-zinc-500">Cliente</label>
          {cliente ? (
            <div className="mt-1 flex items-center justify-between rounded-lg border border-zinc-200 p-2.5 dark:border-zinc-800">
              <div>
                <div className="font-medium">{cliente.nome}</div>
                <div className="text-xs text-zinc-500">{cliente.nif ? `NIF ${cliente.nif} · Fatura com NIF` : 'Sem NIF · Fatura Simplificada'}</div>
              </div>
              <button type="button" onClick={() => { setCliente(null); setMorada(''); setCp(''); setLocalidade(''); }} className="text-xs text-zinc-500 hover:underline">trocar</button>
            </div>
          ) : (
            <div className="relative mt-1">
              <Search size={15} className="absolute left-2.5 top-2.5 text-zinc-400" />
              <input
                value={busca}
                onChange={(e) => setBusca(e.target.value)}
                placeholder="Procurar por nome ou NIF… (vazio = Consumidor final)"
                className={`${inputCls} pl-8`}
              />
              {busca.trim().length >= 2 && (resultados.data?.items.length ?? 0) > 0 && (
                <div className="absolute z-10 mt-1 max-h-48 w-full overflow-auto rounded-lg border border-zinc-200 bg-white shadow-lg dark:border-zinc-700 dark:bg-zinc-900">
                  {resultados.data!.items.map((c) => (
                    <button key={c.id} type="button" onClick={() => pick(c)} className="block w-full px-3 py-2 text-left text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800">
                      <span className="font-medium">{c.nome}</span>
                      {c.nif && <span className="ml-2 text-xs text-zinc-500">NIF {c.nif}</span>}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Morada (só quando há NIF) */}
        {comNif && (
          <div className="space-y-2">
            <label className="block text-xs font-medium uppercase text-zinc-500">Morada do cliente <span className="text-red-500">*</span></label>
            <input value={morada} onChange={(e) => setMorada(e.target.value)} placeholder="Rua / Av., n.º" className={inputCls} />
            <div className="flex gap-2">
              <input value={cp} onChange={(e) => setCp(e.target.value)} placeholder="0000-000" className={`${inputCls} max-w-[8rem]`} />
              <input value={localidade} onChange={(e) => setLocalidade(e.target.value)} placeholder="Localidade" className={inputCls} />
            </div>
            {precisaMorada && <p className="text-xs text-amber-600 dark:text-amber-400">Uma Fatura com NIF exige a morada do adquirente.</p>}
          </div>
        )}

        {/* Linhas */}
        <div className="space-y-2">
          <label className="block text-xs font-medium uppercase text-zinc-500">Linhas</label>
          {linhas.map((l, i) => (
            <div key={i} className="flex gap-2">
              <input value={l.descricao} onChange={(e) => setLinha(i, { descricao: e.target.value })} placeholder="Descrição (ex.: Consultoria informática)" className={`${inputCls} flex-1`} />
              <input value={l.qtd} onChange={(e) => setLinha(i, { qtd: Math.max(1, parseInt(e.target.value, 10) || 1) })} type="number" min={1} className={`${inputCls} w-16`} title="Quantidade" />
              <div className="relative w-28">
                <input value={l.precoEur} onChange={(e) => setLinha(i, { precoEur: e.target.value })} inputMode="decimal" placeholder="0.00" className={`${inputCls} pr-6 text-right`} />
                <span className="pointer-events-none absolute right-2 top-2 text-xs text-zinc-400">€</span>
              </div>
              {linhas.length > 1 && (
                <button type="button" onClick={() => setLinhas((p) => p.filter((_, j) => j !== i))} className="self-center text-zinc-400 hover:text-red-500" title="Remover linha"><Trash2 size={16} /></button>
              )}
            </div>
          ))}
          <button type="button" onClick={() => setLinhas((p) => [...p, { descricao: '', precoEur: '', qtd: 1 }])} className="inline-flex items-center gap-1 text-xs font-medium text-brand-600 hover:underline dark:text-brand-400">
            <Plus size={14} /> Adicionar linha
          </button>
        </div>

        {/* Pagamento */}
        <div>
          <label className="block text-xs font-medium uppercase text-zinc-500">Pagamento</label>
          <div className="mt-1 flex flex-wrap gap-1.5">
            {METODOS.map(([m, label]) => (
              <button key={m} type="button" onClick={() => setMetodo(m)} className={`rounded-lg px-2.5 py-1 text-xs font-medium transition ${metodo === m ? 'bg-brand-600 text-white' : 'border border-zinc-200 text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800'}`}>{label}</button>
            ))}
          </div>
        </div>

        <p className="text-[11px] text-zinc-400">
          IVA 23%. {cliente?.nif ? 'Sai Fatura com NIF.' : 'Sem NIF → Fatura Simplificada.'} A fatura é comunicada à AT em tempo real e fica guardada em Vendas · Faturas.
        </p>
      </div>
    </Modal>
  );
}
