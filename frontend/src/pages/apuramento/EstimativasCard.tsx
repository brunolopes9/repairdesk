import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Calculator } from 'lucide-react';
import { Button, SectionCard } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { apuramentoApi, type ApuramentoEstimativas } from '../../lib/apuramento/api';
import { formatEur } from '../../lib/compras/format';
import { toast } from '../../lib/toast';

const dataPt = (d: string) => new Date(d).toLocaleDateString('pt-PT');
const pct = (v: number) => `${(v * 100).toLocaleString('pt-PT', { maximumFractionDigits: 1 })}%`;

/**
 * Sprint 575: estimativas de IRS (regime simplificado) e Segurança Social — para ter noção dos valores ao longo
 * do ano. A liquidação a sério é das Finanças/contabilista; aqui ficam os pressupostos à vista.
 */
export default function EstimativasCard({ e, ano, trimestre }: { e: ApuramentoEstimativas; ano: number; trimestre: number }) {
  const qc = useQueryClient();
  const [inicio, setInicio] = useState(e.inicioAtividade.slice(0, 10));
  useEffect(() => { setInicio(e.inicioAtividade.slice(0, 10)); }, [e.inicioAtividade]);
  const guardar = useMutation({
    mutationFn: () => apuramentoApi.definirInicioAtividade(inicio || null),
    onSuccess: () => { toast.success('Início de atividade guardado'); qc.invalidateQueries({ queryKey: ['apuramento'] }); },
    onError: (err) => toast.fromError(err, 'Não foi possível guardar.'),
  });
  const reducao = e.fatorInicio < 1 ? ` (reduzido a ${pct(e.fatorInicio)} — ${e.fatorInicio === 0.5 ? '1.º' : '2.º'} ano de atividade)` : '';

  return (
    <SectionCard title={<span className="flex items-center gap-2 text-sm font-semibold"><Calculator size={15} /> Estimativas de IRS e Segurança Social</span>}>
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
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
              <p className="text-xs text-zinc-500">primeiros 12 meses de atividade — até {dataPt(e.ssIsentoAte)}</p>
            </>
          ) : (
            <>
              <p className="mt-1 text-2xl font-bold tabular-nums">{formatEur(e.ssContribuicaoMensal)}<span className="text-sm font-normal text-zinc-500"> /mês</span></p>
              <p className="text-xs text-zinc-500">a pagar nos 3 meses seguintes ao trimestre</p>
            </>
          )}
          <dl className="mt-3 space-y-1.5 text-sm tabular-nums">
            <Linha label="Rendimento relevante (70% serviços + 20% bens)" valor={e.ssRendimentoRelevante} />
            <Linha label="Base mensal (÷ 3) × 21,4%" valor={e.ssRendimentoRelevante / 3 * 0.214} />
          </dl>
          <div className="mt-4 grid grid-cols-[1fr_auto] items-end gap-2">
            <label>
              <span className={labelCls}>Início de atividade nas Finanças{e.inicioDefinido ? '' : ' (estimado pela 1.ª fatura)'}</span>
              <input type="date" value={inicio} onChange={(ev) => setInicio(ev.target.value)} className={inputCls} />
            </label>
            <Button type="button" variant="secondary" loading={guardar.isPending} disabled={!inicio || inicio === e.inicioAtividade.slice(0, 10) && e.inicioDefinido}
              onClick={() => guardar.mutate()}>Guardar</Button>
          </div>
        </div>
      </div>
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
