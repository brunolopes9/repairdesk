import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import { CheckCircle2, ClipboardSignature, Download, PenLine } from 'lucide-react';
import { SignaturePadModal } from '../../components/SignaturePadModal';
import { formatCents } from '../../lib/money';
import { SENHA_TIPO, SENHA_TIPO_LABEL, fichaApi, type PublicFicha } from '../../lib/vendas/ficha';

const RESULTADO: Record<string, { label: string; cls: string }> = {
  ok: { label: 'OK', cls: 'text-emerald-700 dark:text-emerald-400' },
  falha: { label: 'Falha', cls: 'text-rose-700 dark:text-rose-400' },
  nt: { label: 'Não testado', cls: 'text-zinc-500' },
};

/**
 * Sprint 595: a ordem de reparação no link do cliente — vê o que ficou registado (equipamento, avaria, estado,
 * testes, reparação e preço), lê o aviso e assina no telemóvel. Pode descarregar a via dele em PDF.
 */
export default function OrdemReparacaoPublica({ slug }: { slug: string }) {
  const qc = useQueryClient();
  const ficha = useQuery({ queryKey: ['public-ficha', slug], queryFn: () => fichaApi.publica(slug), retry: 0 });
  const [aceito, setAceito] = useState(false);
  const [aAssinar, setAAssinar] = useState(false);
  const assinar = useMutation({
    mutationFn: (dataUrl: string) => fichaApi.assinar(slug, dataUrl),
    onSuccess: (f) => { qc.setQueryData(['public-ficha', slug], f); setAAssinar(false); },
  });

  const f = ficha.data;
  if (!f) return null;
  const assinada = !!f.assinadaEm;

  return (
    <section className={`mt-4 rounded-2xl border p-5 shadow-sm ${assinada ? 'border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900' : 'border-amber-300/60 bg-amber-50/70 dark:border-amber-800/40 dark:bg-amber-950/20'}`}>
      <div className="mb-3 flex items-start justify-between gap-2">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <ClipboardSignature size={16} className={assinada ? 'text-zinc-500' : 'text-amber-600'} aria-hidden />
          Ordem de reparação {f.numero}
        </h3>
        <a href={fichaApi.publicaPdfUrl(slug)} target="_blank" rel="noreferrer"
          className="inline-flex items-center gap-1 text-xs font-medium text-zinc-600 underline hover:text-zinc-900 dark:text-zinc-300">
          <Download size={13} /> PDF
        </a>
      </div>

      <Detalhes f={f} />

      {assinada ? (
        <p className="mt-4 flex items-center gap-2 rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-800 dark:bg-emerald-950/30 dark:text-emerald-300">
          <CheckCircle2 size={16} /> Assinada em {new Date(f.assinadaEm!).toLocaleString('pt-PT')}. Obrigado!
        </p>
      ) : f.podeAssinar ? (
        <div className="mt-4 space-y-3">
          <p className="text-xs italic text-zinc-600 dark:text-zinc-400">{f.aviso}</p>
          {f.termos && <p className="whitespace-pre-line text-xs text-zinc-500">{f.termos}</p>}
          <label className="flex items-start gap-2 text-sm">
            <input type="checkbox" className="mt-0.5 h-5 w-5" checked={aceito} onChange={(e) => setAceito(e.target.checked)} />
            Li a ordem de reparação e aceito os termos.
          </label>
          <button type="button" disabled={!aceito} onClick={() => setAAssinar(true)}
            className="inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-zinc-900 px-4 text-sm font-semibold text-white disabled:opacity-40 dark:bg-white dark:text-zinc-900">
            <PenLine size={16} /> Assinar
          </button>
          {assinar.isError && (
            <p className="text-sm text-rose-600">
              {isAxiosError(assinar.error) && assinar.error.response?.status === 409 ? 'Esta ordem já estava assinada.' : 'Não foi possível guardar a assinatura. Tenta de novo.'}
            </p>
          )}
        </div>
      ) : null}

      {aAssinar && (
        <SignaturePadModal
          title={`Assinar a ordem ${f.numero}`}
          subtitle={`${f.cliente}, assine com o dedo dentro da caixa.`}
          saving={assinar.isPending}
          onClose={() => setAAssinar(false)}
          onSave={(dataUrl) => assinar.mutate(dataUrl)}
        />
      )}
    </section>
  );
}

function Detalhes({ f }: { f: PublicFicha }) {
  const linha = (label: string, valor: string | null | undefined) => valor ? (
    <div className="flex gap-3 py-1 text-sm"><dt className="w-32 flex-none text-zinc-500">{label}</dt><dd className="min-w-0 flex-1">{valor}</dd></div>
  ) : null;
  return (
    <div className="space-y-3">
      <dl className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {linha('Equipamento', f.equipamento)}
        {linha('IMEI / nº série', f.imei)}
        {linha('Avaria', f.avaria)}
        {linha('Entrada', new Date(f.recebidoEm).toLocaleString('pt-PT'))}
        {linha('Previsão', f.previstoPara ? new Date(f.previstoPara).toLocaleDateString('pt-PT') : null)}
        {linha('Senha', f.senhaTipo === SENHA_TIPO.Nenhuma ? 'Sem senha' : `${SENHA_TIPO_LABEL[f.senhaTipo]} (registada na loja)`)}
        {linha('Permite testes', f.permiteTestes == null ? null : f.permiteTestes ? 'Sim' : 'Não')}
        {linha('Estado físico', f.estadoFisico)}
        {linha('Acessórios', f.acessorios)}
      </dl>
      {f.checklist.length > 0 && (
        <div>
          <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-zinc-500">Testes à entrada</p>
          <ul className="grid grid-cols-2 gap-x-4 text-sm">
            {f.checklist.map((c) => (
              <li key={c.chave} className="flex justify-between border-b border-zinc-100 py-1 dark:border-zinc-800">
                <span className="truncate">{c.label}</span>
                <span className={`ml-2 font-medium ${RESULTADO[c.entrada ?? '']?.cls ?? ''}`}>{RESULTADO[c.entrada ?? '']?.label}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
      <div>
        <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-zinc-500">Reparação a realizar</p>
        {f.reparacao.length === 0 ? <p className="text-sm text-zinc-500">A definir após diagnóstico.</p> : (
          <ul className="text-sm">
            {f.reparacao.map((l, i) => <li key={i} className="flex justify-between py-0.5"><span>{l.descricao}</span><span className="tabular-nums">{formatCents(l.totalCents)}</span></li>)}
          </ul>
        )}
        {f.totalCents > 0 && (
          <p className="mt-1 flex justify-between border-t border-zinc-200 pt-1 text-sm font-semibold dark:border-zinc-700">
            <span>Preço (c/ IVA)</span><span className="tabular-nums">{formatCents(f.totalCents)}</span>
          </p>
        )}
      </div>
    </div>
  );
}
