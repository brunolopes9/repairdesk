import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Calculator } from 'lucide-react';
import { Button, SectionCard } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { apuramentoApi, type ApuramentoEstimativas } from '../../lib/apuramento/api';
import { formatEur, parseDecimal } from '../../lib/compras/format';
import { toast } from '../../lib/toast';

const dataPt = (d: string) => new Date(d).toLocaleDateString('pt-PT');
const pct = (v: number) => `${(v * 100).toLocaleString('pt-PT', { maximumFractionDigits: 1 })}%`;
const txt = (v: number | null | undefined) => (v ? v.toFixed(2).replace('.', ',') : '');

/**
 * Sprint 575/576: estimativas de IRS (regime simplificado) e Segurança Social — para ter noção dos valores
 * ao longo do ano. Os pressupostos (início de atividade, contribuição atual, faturação de antes do Mender) são
 * indicados pelo dono, nunca adivinhados. A liquidação a sério é das Finanças/contabilista.
 */
export default function EstimativasCard({ e, ano, trimestre }: { e: ApuramentoEstimativas; ano: number; trimestre: number }) {
  const qc = useQueryClient();
  const [f, setF] = useState({ inicio: '', ss: '', bens: '', informatica: '', reparacoes: '', trading: '' });
  useEffect(() => {
    setF({
      inicio: e.inicioAtividade?.slice(0, 10) ?? '',
      ss: txt(e.ssContribuicaoAtual),
      bens: txt(e.faturacaoAnterior?.bens),
      informatica: txt(e.faturacaoAnterior?.informatica),
      reparacoes: txt(e.faturacaoAnterior?.reparacoes),
      trading: txt(e.faturacaoAnterior?.trading),
    });
  }, [e]);
  const num = (s: string) => parseDecimal(s) ?? 0;
  const guardar = useMutation({
    mutationFn: () => apuramentoApi.definirPressupostos({
      inicioAtividade: f.inicio || null,
      ssContribuicaoAtual: f.ss.trim() ? num(f.ss) : null,
      ano,
      faturacaoAnterior: { bens: num(f.bens), informatica: num(f.informatica), reparacoes: num(f.reparacoes), trading: num(f.trading) },
    }),
    onSuccess: () => { toast.success('Pressupostos guardados'); qc.invalidateQueries({ queryKey: ['apuramento'] }); },
    onError: (err) => toast.fromError(err, 'Não foi possível guardar.'),
  });
  const reducao = e.fatorInicio < 1 ? ` (reduzido a ${pct(e.fatorInicio)} — ${e.fatorInicio === 0.5 ? '1.º' : '2.º'} ano de atividade)` : '';
  const campo = (k: keyof typeof f, label: string, extra?: string) => (
    <label className="block">
      <span className={labelCls}>{label}</span>
      <input value={f[k]} onChange={(ev) => setF({ ...f, [k]: ev.target.value })} className={inputCls} inputMode="decimal" placeholder={extra ?? '0,00'} />
    </label>
  );

  return (
    <SectionCard title={<span className="flex items-center gap-2 text-sm font-semibold"><Calculator size={15} /> Estimativas de IRS e Segurança Social</span>}>
      {!e.inicioDefinido && (
        <p className="mb-4 flex items-start gap-2 rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
          <AlertTriangle size={16} className="mt-0.5 shrink-0" />
          Indica a data de início de atividade (Portal das Finanças → Dados cadastrais → Atividade). Sem ela não aplico a redução de IRS dos primeiros anos nem a isenção de Segurança Social.
        </p>
      )}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-zinc-500">IRS {ano} · faturação até ao fim do {trimestre}.º trimestre</p>
          <p className="mt-1 text-2xl font-bold tabular-nums">{formatEur(e.irsEstimado)}</p>
          <p className="text-xs text-zinc-500">taxa efetiva ≈ {pct(e.taxaEfetiva)} da faturação · regime simplificado</p>
          <dl className="mt-3 space-y-1.5 text-sm tabular-nums">
            <Linha label="Vendas de bens (peças, artigos) × 0,15" valor={e.receitasBens} />
            <Linha label={`Informática / software × 0,75${reducao}`} valor={e.receitasServicos075} />
            <Linha label={`Reparações (outros serviços) × 0,35${reducao}`} valor={e.receitasServicos035} />
            <Linha label={`Trading × 0,75${reducao} (prudente)`} valor={e.receitasTrading} />
            {e.acrescimoRegra15 > 0 && <Linha label="Acréscimo da regra dos 15% (faltam despesas)" valor={e.acrescimoRegra15} />}
            <Linha label="Rendimento tributável estimado" valor={e.rendimentoTributavel} forte />
          </dl>
          <p className="mt-2 text-xs text-zinc-500">
            Despesas que contam para a regra dos 15%: {formatEur(e.despesasJustificadas)} (inclui os 4.104 € automáticos).
            Não inclui deduções à coleta, outros rendimentos do agregado nem IRS Jovem. Escalões de {e.anoTabelaIrs}.
          </p>
        </div>
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-zinc-500">Segurança Social · {trimestre}.º trimestre</p>
          {e.ssIsento ? (
            <>
              <p className="mt-1 text-2xl font-bold tabular-nums text-emerald-600">Isento</p>
              <p className="text-xs text-zinc-500">primeiros 12 meses de atividade{e.ssIsentoAte ? ` — até ${dataPt(e.ssIsentoAte)}` : ''}</p>
            </>
          ) : (
            <>
              <p className="mt-1 text-2xl font-bold tabular-nums">{formatEur(e.ssContribuicaoMensal)}<span className="text-sm font-normal text-zinc-500"> /mês</span></p>
              <p className="text-xs text-zinc-500">
                a pagar nos 3 meses a seguir à declaração trimestral
                {e.ssContribuicaoAtual != null && <> · hoje pagas {formatEur(e.ssContribuicaoAtual)}/mês ({e.ssContribuicaoMensal >= e.ssContribuicaoAtual ? '+' : ''}{formatEur(e.ssContribuicaoMensal - e.ssContribuicaoAtual)})</>}
              </p>
            </>
          )}
          <dl className="mt-3 space-y-1.5 text-sm tabular-nums">
            <Linha label="Rendimento relevante do trimestre (70% serviços + 20% bens)" valor={e.ssRendimentoRelevante} />
            <Linha label="Base mensal (÷ 3) × 21,4%" valor={e.ssRendimentoRelevante / 3 * 0.214} />
          </dl>
        </div>
      </div>

      <details className="mt-5 rounded-lg border border-zinc-200 p-3 dark:border-zinc-800" open={!e.inicioDefinido}>
        <summary className="cursor-pointer text-sm font-medium">Pressupostos das estimativas</summary>
        <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2">
          <label className="block">
            <span className={labelCls}>Início de atividade nas Finanças</span>
            <input type="date" value={f.inicio} onChange={(ev) => setF({ ...f, inicio: ev.target.value })} className={inputCls} />
          </label>
          {campo('ss', 'Contribuição mensal atual à Segurança Social (€)', 'ex.: 115,87')}
        </div>
        <p className="mt-4 text-xs font-medium text-zinc-600 dark:text-zinc-300">Faturação de {ano} antes de usar o Mender (sem IVA) — ex.: janeiro a junho</p>
        <div className="mt-2 grid grid-cols-2 gap-3 sm:grid-cols-4">
          {campo('bens', 'Bens / peças')}
          {campo('informatica', 'Informática / software')}
          {campo('reparacoes', 'Reparações')}
          {campo('trading', 'Trading')}
        </div>
        <div className="mt-3 flex justify-end">
          <Button type="button" loading={guardar.isPending} onClick={() => guardar.mutate()}>Guardar pressupostos</Button>
        </div>
      </details>
      <p className="mt-4 border-t border-zinc-100 pt-3 text-xs text-zinc-500 dark:border-zinc-800">
        Estimativas para teres noção dos valores — o cálculo final é das Finanças e do contabilista.
      </p>
    </SectionCard>
  );
}

function Linha({ label, valor, forte }: { label: string; valor: number; forte?: boolean }) {
  return (
    <div className={`flex items-baseline justify-between gap-3 ${forte ? 'border-t border-zinc-100 pt-1.5 font-semibold dark:border-zinc-800' : ''}`}>
      <dt className={forte ? '' : 'text-zinc-600 dark:text-zinc-300'}>{label}</dt>
      <dd>{formatEur(valor)}</dd>
    </div>
  );
}
