import { useState } from 'react';
import { BadgeCheck, UserRound } from 'lucide-react';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { clientesApi } from '../../lib/clientes/api';
import type { MoloniDadosFiscais } from '../../lib/moloni/api';
import { validateNif } from '../../lib/nif/validator';
import type { VendaClienteResumo } from '../../lib/vendas/types';

/** Sprint 571: a quem sai a fatura — consumidor final (sem NIF) ou com NIF e os dados fiscais pedidos no momento. */
export interface EscolhaFatura {
  comNif: boolean;
  dados: MoloniDadosFiscais;
}

/** Com NIF por defeito só quando o cliente já tem NIF (o nome de um cliente sem NIF é só interno). */
export function escolhaInicial(cliente: VendaClienteResumo | null | undefined): EscolhaFatura {
  const temNif = !!cliente?.nif && validateNif(cliente.nif).isValid;
  return {
    comNif: temNif,
    dados: {
      nif: temNif ? cliente!.nif! : '',
      nome: temNif ? cliente!.nomeFaturacao || cliente!.nome : '',
      morada: cliente?.morada ?? '',
      codigoPostal: cliente?.codigoPostal ?? '',
      localidade: cliente?.localidade ?? '',
    },
  };
}

/** Os mesmos requisitos que o servidor valida (MoloniService.ValidarDadosFiscais). Null = pronto. */
export function problemaFatura(e: EscolhaFatura): string | null {
  if (!e.comNif) return null;
  const d = e.dados;
  if (!validateNif(d.nif).isValid) return 'NIF inválido.';
  if (d.nome.trim().length < 2) return 'Falta o nome para a fatura.';
  if (!d.morada.trim()) return 'Falta a morada.';
  if (!/^\d{4}-\d{3}$/.test(d.codigoPostal.trim())) return 'Código postal no formato 0000-000.';
  if (!d.localidade.trim()) return 'Falta a localidade.';
  return null;
}

/** Corpo do pedido de emissão: sem NIF explícito, ou os dados fiscais. */
export function pedidoFatura(e: EscolhaFatura): { semNif: boolean; comNif: MoloniDadosFiscais | null } {
  return e.comNif
    ? { semNif: false, comNif: { ...e.dados, nif: e.dados.nif.replace(/\D/g, ''), codigoPostal: e.dados.codigoPostal.trim() } }
    : { semNif: true, comNif: null };
}

/** "Rua X, 1 3500-001 VISEU" (morada da AT) → morada, código postal e localidade. */
function partirMorada(m: string): Partial<MoloniDadosFiscais> {
  const r = /^(.*?)[,\s]*(\d{4}-\d{3})\s*(.*)$/.exec(m.trim());
  return r ? { morada: r[1].trim(), codigoPostal: r[2], localidade: r[3].trim() } : { morada: m.trim() };
}

interface Props {
  value: EscolhaFatura;
  onChange: (e: EscolhaFatura) => void;
  /** Nome interno do cliente da venda, para lembrar que não vai na fatura sem NIF. */
  nomeInterno?: string | null;
}

export default function DadosFatura({ value, onChange, nomeInterno }: Props) {
  const [aVerificar, setAVerificar] = useState(false);
  const [aviso, setAviso] = useState<string | null>(null);
  const d = value.dados;
  const set = (p: Partial<MoloniDadosFiscais>) => onChange({ ...value, dados: { ...d, ...p } });
  const nif = validateNif(d.nif);

  async function verificar() {
    setAVerificar(true);
    setAviso(null);
    try {
      const r = await clientesApi.lookupAtNif(nif.cleaned);
      onChange({ ...value, dados: { ...d, nome: d.nome.trim() ? d.nome : r.nome, ...(r.morada ? partirMorada(r.morada) : {}) } });
      setAviso(r.morada ? 'Nome e morada preenchidos pela AT — confirma.' : 'NIF verificado, sem morada — preenche à mão.');
    } catch {
      setAviso('Não foi possível verificar o NIF — preenche à mão.');
    } finally {
      setAVerificar(false);
    }
  }

  const opcao = (comNif: boolean, titulo: string, sub: string) => (
    <button type="button" onClick={() => onChange({ ...value, comNif })} aria-pressed={value.comNif === comNif}
      className={`flex-1 rounded-lg border px-3 py-2 text-left text-sm transition ${value.comNif === comNif
        ? 'border-brand-500 bg-brand-50 ring-2 ring-brand-200 dark:border-brand-500 dark:bg-brand-950/40 dark:ring-brand-900'
        : 'border-zinc-200 hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800/50'}`}>
      <span className="flex items-center gap-1.5 font-medium">{comNif ? <BadgeCheck size={15} /> : <UserRound size={15} />} {titulo}</span>
      <span className="block text-xs text-zinc-500">{sub}</span>
    </button>
  );

  return (
    <div className="space-y-3">
      <div className="flex flex-col gap-2 sm:flex-row">
        {opcao(false, 'Sem NIF', 'Consumidor final')}
        {opcao(true, 'Com NIF', 'Nome e morada na fatura')}
      </div>
      {!value.comNif ? (
        nomeInterno ? <p className="text-xs text-zinc-500">"{nomeInterno}" fica só no Mender — a fatura sai para consumidor final.</p> : null
      ) : (
        <div className="grid gap-3 rounded-lg border border-zinc-200 p-3 sm:grid-cols-2 dark:border-zinc-800">
          <label className="block">
            <span className={labelCls}>NIF</span>
            <div className="flex gap-2">
              <input value={d.nif} onChange={(e) => set({ nif: e.target.value.replace(/[^\d]/g, '').slice(0, 9) })} className={inputCls}
                inputMode="numeric" placeholder="9 dígitos" autoFocus={!d.nif} />
              <button type="button" disabled={!nif.isValid || aVerificar} onClick={verificar}
                className="whitespace-nowrap rounded-lg border border-brand-300 bg-brand-50 px-3 text-xs font-medium text-brand-700 hover:bg-brand-100 disabled:opacity-50 dark:border-brand-800/60 dark:bg-brand-950/30 dark:text-brand-200">
                {aVerificar ? 'A verificar…' : 'Verificar'}
              </button>
            </div>
            {d.nif && !nif.isValid && <span className="mt-1 block text-xs text-rose-600">{nif.message || 'NIF inválido.'}</span>}
          </label>
          <label className="block">
            <span className={labelCls}>Nome na fatura</span>
            <input value={d.nome} onChange={(e) => set({ nome: e.target.value })} className={inputCls} placeholder="ex: André Silva" />
          </label>
          <label className="block sm:col-span-2">
            <span className={labelCls}>Morada</span>
            <input value={d.morada} onChange={(e) => set({ morada: e.target.value })} className={inputCls} placeholder="Rua da Igreja, n.º 42" />
          </label>
          <label className="block">
            <span className={labelCls}>Código postal</span>
            <input value={d.codigoPostal} onChange={(e) => set({ codigoPostal: e.target.value })} className={inputCls} placeholder="0000-000" inputMode="numeric" maxLength={8} />
          </label>
          <label className="block">
            <span className={labelCls}>Localidade</span>
            <input value={d.localidade} onChange={(e) => set({ localidade: e.target.value })} className={inputCls} placeholder="Viseu" />
          </label>
          {aviso && <p className="text-xs text-zinc-500 sm:col-span-2">{aviso}</p>}
          <p className="text-xs text-zinc-500 sm:col-span-2">Fica guardado no cliente (o nome interno não muda) e no cliente do Moloni ON.</p>
        </div>
      )}
    </div>
  );
}
