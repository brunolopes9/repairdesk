import { useEffect, useRef, useState } from 'react';
import { ChevronDown, Download, FileStack, List, Printer } from 'lucide-react';
import { api } from '../lib/api';
import { guardarBlob, imprimirPdf } from '../lib/downloadPdf';
import { toast } from '../lib/toast';
import { Button } from './ui';

type Conteudo = 'tudo' | 'faturas' | 'lista';

const OPCOES: { conteudo: Conteudo; guardar?: boolean; label: string; hint: string; icon: typeof Printer }[] = [
  { conteudo: 'tudo', label: 'Imprimir lista + faturas', hint: 'Uma página com a lista e os totais, e a seguir cada fatura', icon: Printer },
  { conteudo: 'faturas', label: 'Imprimir só as faturas', hint: 'Todas as faturas seguidas, pela ordem das datas', icon: FileStack },
  { conteudo: 'lista', label: 'Imprimir só a lista', hint: 'Nº, data, documento, IVA e total — para conferir', icon: List },
  { conteudo: 'tudo', guardar: true, label: 'Guardar PDF', hint: 'Lista + faturas num ficheiro (ex.: para enviar à contabilidade)', icon: Download },
];

/**
 * Sprint 593: imprime de uma vez tudo o que a lista está a mostrar (os mesmos filtros: dia, semana, mês, fornecedor,
 * pesquisa…) — um só PDF com a lista e as faturas. `caminho` é o endpoint de /api/imprimir e `params` os filtros.
 */
export function BotaoImprimir({ caminho, params, total, nome }: {
  caminho: string;
  params: Record<string, string | number | boolean | undefined | null>;
  /** Nº de documentos com os filtros atuais (para o botão dizer quantos vai imprimir). */
  total?: number;
  /** Nome do ficheiro ao guardar. */
  nome: string;
}) {
  const [open, setOpen] = useState(false);
  const [aGerar, setAGerar] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const fora = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); };
    const esc = (e: KeyboardEvent) => { if (e.key === 'Escape') setOpen(false); };
    document.addEventListener('mousedown', fora);
    document.addEventListener('keydown', esc);
    return () => { document.removeEventListener('mousedown', fora); document.removeEventListener('keydown', esc); };
  }, [open]);

  async function gerar(conteudo: Conteudo, guardar: boolean) {
    setOpen(false);
    setAGerar(true);
    try {
      const limpos = Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''));
      const r = await api.get<Blob>(caminho, { params: { ...limpos, conteudo }, responseType: 'blob', timeout: 180_000 });
      if (guardar) guardarBlob(r.data, nome);
      else {
        try { await imprimirPdf(r.data, nome); } catch { guardarBlob(r.data, nome); }
      }
    } catch (err) {
      toast.fromError(err, 'Não foi possível preparar a impressão.');
    } finally {
      setAGerar(false);
    }
  }

  const vazio = total === 0;
  return (
    <div className="relative" ref={ref}>
      <Button type="button" variant="secondary" leftIcon={<Printer size={15} />} loading={aGerar} disabled={vazio}
        onClick={() => setOpen((v) => !v)} aria-haspopup="menu" aria-expanded={open}
        title={vazio ? 'Não há nada para imprimir com estes filtros' : 'Imprime tudo o que está nesta lista (com os filtros escolhidos)'}>
        Imprimir{total ? ` (${total})` : ''}
        <ChevronDown size={14} className={`ml-1 inline transition ${open ? 'rotate-180' : ''}`} />
      </Button>
      {open && (
        <div role="menu" className="absolute right-0 z-30 mt-1 w-80 overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-lg dark:border-zinc-700 dark:bg-zinc-900">
          <p className="border-b border-zinc-100 px-3 py-2 text-[11px] text-zinc-500 dark:border-zinc-800">
            Imprime {total != null ? `os ${total} documento(s)` : 'tudo'} desta lista, com os filtros escolhidos (datas, fornecedor, pesquisa…).
          </p>
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {OPCOES.map((o) => (
              <li key={o.label}>
                <button type="button" role="menuitem" onClick={() => gerar(o.conteudo, !!o.guardar)}
                  className="flex w-full items-start gap-2 px-3 py-2 text-left hover:bg-zinc-50 focus:bg-zinc-50 focus:outline-none dark:hover:bg-zinc-800 dark:focus:bg-zinc-800">
                  <o.icon size={15} className="mt-0.5 flex-none text-zinc-500" />
                  <span>
                    <span className="block text-sm font-medium text-zinc-900 dark:text-zinc-100">{o.label}</span>
                    <span className="block text-[11px] text-zinc-500">{o.hint}</span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
