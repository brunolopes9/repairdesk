import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, FileUp, Plus } from 'lucide-react';
import { Button, PageHeader, SkeletonTable } from '../../components/ui';
import { DespesaFormModal } from '../../components/DespesaFormModal';
import { api } from '../../lib/api';
import { formatEur } from '../../lib/compras/format';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';

interface Linha { nome: string; valor: number }
interface Fixa { descricao: string; valorTipico: number; registadoEsteMes: number; ok: boolean }
interface Devedor { vendaId: string; numero: number; cliente: string; telefone: string | null; data: string; valor: number; dias: number }
interface Cobrancas { recebidoMes: number; porReceberMes: number; porReceberTotal: number; devedores: Devedor[] }
interface Verificacao { codigo: string; mensagem: string; quantidade: number; gravidade: 'aviso' | 'info' }
interface Mes {
  ano: number; mes: number; objetivo: number;
  lucroInformatica: number; payoutsTrading: number; totalLucros: number;
  despesasInformatica: number; despesasTrading: number; despesasGerais: number; despesasPessoais: number; obrigacoes: number;
  totalDespesas: number; impostosPagos: number; resultadoInformatica: number; resultadoTrading: number;
  saldo: number; faltaParaObjetivo: number;
  vendas: number; faturado: number; margemMedia: number; ticketMedio: number; gasto: number; investimento: number;
  maioresDespesas: Linha[]; fixas: Fixa[]; vendasSemFatura: number;
  /** Sprint 650 */
  cobrancas?: Cobrancas | null; verificacoes?: Verificacao[] | null;
}
interface ResumoMes { mes: number; lucroInformatica: number; payoutsTrading: number; despesasNegocio: number; despesasPessoais: number; saldo: number; objetivo: number }

const MESES = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

/**
 * Sprint 633 (Doc 99): o Excel «Gestão Financeira» do Bruno no Mender — quanto ganhei este mês, em que negócio,
 * onde gastei, e estou acima ou abaixo do objetivo. Só o dono vê (inclui despesas pessoais).
 */
export default function Financas() {
  const qc = useQueryClient();
  const hoje = new Date();
  const [ano, setAno] = useState(hoje.getFullYear());
  const [mes, setMes] = useState(hoje.getMonth() + 1);
  const [novaDespesa, setNovaDespesa] = useState(false);
  const [objetivo, setObjetivo] = useState('');

  const dados = useQuery({ queryKey: ['financas', 'mes', ano, mes], queryFn: () => api.get<Mes>('/financas/mes', { params: { ano, mes } }).then((r) => r.data) });
  const anoQ = useQuery({ queryKey: ['financas', 'ano', ano], queryFn: () => api.get<ResumoMes[]>('/financas/ano', { params: { ano } }).then((r) => r.data) });

  const importar = useMutation({
    mutationFn: (f: File) => {
      const form = new FormData();
      form.append('file', f);
      return api.post<{ importadas: number; jaExistiam: number; ignoradas: number; fixas: number; avisos: string[] }>('/financas/importar-excel', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      }).then((r) => r.data);
    },
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ['financas'] });
      toast.success(`${r.importadas} despesa(s) importadas`, `${r.jaExistiam} já existiam · ${r.ignoradas} de fora${r.avisos.length ? ` — ${r.avisos.slice(0, 2).join(' ')}` : ''}`);
    },
    onError: (e) => toast.error('Não foi possível importar', apiErrorMessage(e)),
  });

  const guardarObjetivo = useMutation({
    mutationFn: (v: number) => api.put('/financas/objetivo', { objetivo: v }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['financas'] }); setObjetivo(''); toast.success('Objetivo guardado'); },
    onError: (e) => toast.error('Não ficou guardado', apiErrorMessage(e)),
  });

  function mudarMes(delta: number) {
    const d = new Date(ano, mes - 1 + delta, 1);
    setAno(d.getFullYear());
    setMes(d.getMonth() + 1);
  }

  const d = dados.data;
  return (
    <div className="space-y-5">
      <PageHeader
        title="Finanças"
        description="O teu Excel de gestão, agora aqui: lucro de cada negócio, onde gastaste, e quanto falta para o objetivo."
        actions={(
          <div className="flex flex-wrap gap-2">
            <label className="inline-flex cursor-pointer items-center gap-1.5 rounded-lg border border-zinc-300 px-3 py-2 text-sm font-medium hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800">
              <FileUp size={16} /> {importar.isPending ? 'A importar…' : 'Importar Excel'}
              <input type="file" accept=".xlsx" className="sr-only" aria-label="Importar Excel de gestão"
                onChange={(e) => { const f = e.target.files?.[0]; if (f) importar.mutate(f); e.target.value = ''; }} />
            </label>
            <Button leftIcon={<Plus size={16} />} onClick={() => setNovaDespesa(true)}>Nova despesa</Button>
          </div>
        )}
      />

      <div className="flex items-center gap-2">
        <Button size="sm" variant="ghost" aria-label="Mês anterior" onClick={() => mudarMes(-1)}><ChevronLeft size={16} /></Button>
        <span className="min-w-[10rem] text-center font-semibold capitalize">{MESES[mes - 1]} de {ano}</span>
        <Button size="sm" variant="ghost" aria-label="Mês seguinte" onClick={() => mudarMes(1)}><ChevronRight size={16} /></Button>
      </div>

      {dados.isLoading ? <SkeletonTable columns={4} rows={5} /> : dados.isError ? (
        <p className="text-sm text-rose-600">{apiErrorMessage(dados.error)}</p>
      ) : d && (
        <>
          <section className={`rounded-2xl border p-5 ${d.saldo >= d.objetivo ? 'border-emerald-300 bg-emerald-50 dark:border-emerald-900/50 dark:bg-emerald-950/30' : 'border-amber-300 bg-amber-50 dark:border-amber-900/50 dark:bg-amber-950/30'}`}>
            <p className="text-sm text-zinc-600 dark:text-zinc-300">Saldo do mês</p>
            <p className="mt-1 text-4xl font-bold tabular-nums">{formatEur(d.saldo)}</p>
            <p className="mt-1 text-sm font-medium">
              {d.saldo >= d.objetivo ? `Objetivo de ${formatEur(d.objetivo)} batido por ${formatEur(d.saldo - d.objetivo)} 🎉` : `Faltam ${formatEur(d.faltaParaObjetivo)} para o objetivo de ${formatEur(d.objetivo)}`}
            </p>
            <form className="mt-3 flex flex-wrap items-center gap-2 text-xs" onSubmit={(e) => { e.preventDefault(); const v = Number(objetivo.replace(',', '.')); if (v >= 0) guardarObjetivo.mutate(v); }}>
              <span className="text-zinc-500">Mudar objetivo mensal:</span>
              <input value={objetivo} onChange={(e) => setObjetivo(e.target.value)} inputMode="decimal" placeholder={String(d.objetivo)} aria-label="Objetivo mensal"
                className="w-24 rounded border border-zinc-300 bg-white px-2 py-1 dark:border-zinc-700 dark:bg-zinc-900" />
              <Button type="submit" size="sm" variant="secondary" disabled={!objetivo.trim()} loading={guardarObjetivo.isPending}>Guardar</Button>
            </form>
          </section>

          {d.verificacoes && d.verificacoes.length > 0 && (
            <section className="rounded-2xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900" aria-label="Verificações dos dados">
              <h2 className="mb-2 text-sm font-semibold">A rever</h2>
              <ul className="space-y-1 text-sm">
                {d.verificacoes.map((v) => (
                  <li key={v.codigo} className="flex items-start gap-2">
                    <span className={`mt-0.5 inline-block min-w-6 rounded-full px-1.5 text-center text-xs font-semibold ${v.gravidade === 'aviso' ? 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300' : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'}`}>{v.quantidade}</span>
                    <span>{v.mensagem}</span>
                  </li>
                ))}
              </ul>
            </section>
          )}

          <div className="grid gap-4 lg:grid-cols-2">
            <Bloco titulo="Lucro dos negócios">
              <Linhas linhas={[
                ['Informática (LopesTech)', d.lucroInformatica],
                ['Trading (payouts)', d.payoutsTrading],
              ]} total={['Total de lucros', d.totalLucros]} />
              {d.vendasSemFatura > 0 && <p className="mt-2 text-xs text-zinc-500">Inclui {d.vendasSemFatura} venda(s) sem fatura (registo interno).</p>}
            </Bloco>
            <Bloco titulo="Despesas do mês">
              <Linhas linhas={[
                ['Informática', d.despesasInformatica],
                ['Trading', d.despesasTrading],
                ['Gerais (mistas)', d.despesasGerais],
                ['Pessoais', d.despesasPessoais],
                ['Segurança Social', d.obrigacoes],
              ]} total={['Total de despesas', d.totalDespesas]} />
              {d.impostosPagos > 0 && <p className="mt-2 text-xs text-zinc-500">IVA/IRS pagos ao Estado: {formatEur(d.impostosPagos)} (dinheiro de passagem — fora do saldo).</p>}
            </Bloco>
            <Bloco titulo="Resultado por negócio">
              <Linhas linhas={[
                ['Informática', d.resultadoInformatica],
                ['Trading', d.resultadoTrading],
              ]} />
            </Bloco>
            <Bloco titulo="Atividade da loja">
              <Linhas linhas={[
                ['Vendas/trabalhos', d.vendas],
                ['Faturado (s/ IVA)', d.faturado],
                ['Ticket médio', d.ticketMedio],
              ]} contagem={0} />
              <p className="mt-2 text-sm">Margem média: <strong>{(d.margemMedia * 100).toLocaleString('pt-PT', { maximumFractionDigits: 1 })}%</strong></p>
              <p className="text-sm text-zinc-500">Gasto {formatEur(d.gasto)} · Investimento {formatEur(d.investimento)}</p>
            </Bloco>
            <Bloco titulo="Maiores despesas">
              {d.maioresDespesas.length === 0 ? <p className="text-sm text-zinc-500">Sem despesas este mês.</p> : (
                <Linhas linhas={d.maioresDespesas.map((l) => [l.nome, l.valor])} />
              )}
            </Bloco>
            {d.cobrancas && <BlocoCobrancas c={d.cobrancas} />}
            <Bloco titulo="Despesas fixas">
              {d.fixas.length === 0 ? <p className="text-sm text-zinc-500">Marca as despesas mensais como «recorrente» e aparecem aqui.</p> : (
                <ul className="space-y-1 text-sm">
                  {d.fixas.map((f) => (
                    <li key={f.descricao} className="flex items-center justify-between gap-2">
                      <span className="truncate">{f.descricao}</span>
                      <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${f.ok ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300' : 'bg-rose-100 text-rose-800 dark:bg-rose-950/40 dark:text-rose-300'}`}>
                        {f.ok ? `OK · ${formatEur(f.registadoEsteMes)}` : `EM FALTA · ~${formatEur(f.valorTipico)}`}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </Bloco>
          </div>
        </>
      )}

      {anoQ.data && anoQ.data.length > 0 && <EvolucaoAno ano={ano} meses={anoQ.data} />}

      <DespesaFormModal open={novaDespesa} onClose={() => setNovaDespesa(false)}
        onSaved={() => { setNovaDespesa(false); qc.invalidateQueries({ queryKey: ['financas'] }); qc.invalidateQueries({ queryKey: ['despesas'] }); toast.success('Despesa registada'); }} />
    </div>
  );
}

/** Sprint 650: recebido / por receber (com IVA — o que o cliente paga) e quem deve. */
function BlocoCobrancas({ c }: { c: Cobrancas }) {
  return (
    <Bloco titulo="Cobranças">
      <Linhas linhas={[
        ['Recebido este mês', c.recebidoMes],
        ['Por receber deste mês', c.porReceberMes],
      ]} total={['Por receber (todos os meses)', c.porReceberTotal]} />
      {c.devedores.length === 0 ? (
        <p className="mt-2 text-xs text-zinc-500">Ninguém te deve nada. Numa venda entregue sem pagamento, usa «Entregue, paga depois».</p>
      ) : (
        <ul className="mt-3 space-y-1 text-sm">
          {c.devedores.map((dv) => (
            <li key={dv.vendaId}>
              <Link to={`/vendas/${dv.vendaId}`} className="flex items-center justify-between gap-2 rounded-lg px-2 py-1 hover:bg-zinc-50 dark:hover:bg-zinc-800">
                <span className="min-w-0 truncate">#{dv.numero} · {dv.cliente}</span>
                <span className="flex shrink-0 items-center gap-2 tabular-nums">
                  <span className={`text-xs ${dv.dias > 30 ? 'font-semibold text-rose-600' : 'text-zinc-500'}`}>{dv.dias} dia{dv.dias === 1 ? '' : 's'}</span>
                  <strong>{formatEur(dv.valor)}</strong>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Bloco>
  );
}

function Bloco({ titulo, children }: { titulo: string; children: React.ReactNode }) {
  return (
    <section className="rounded-2xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <h2 className="mb-2 text-sm font-semibold">{titulo}</h2>
      {children}
    </section>
  );
}

function Linhas({ linhas, total, contagem }: { linhas: (readonly [string, number])[]; total?: readonly [string, number]; contagem?: number }) {
  return (
    <dl className="space-y-1 text-sm tabular-nums">
      {linhas.map(([n, v], i) => (
        <div key={n} className="flex justify-between gap-2">
          <dt className="text-zinc-600 dark:text-zinc-300">{n}</dt>
          <dd>{contagem === i ? v : formatEur(v)}</dd>
        </div>
      ))}
      {total && (
        <div className="flex justify-between gap-2 border-t border-zinc-200 pt-1 font-semibold dark:border-zinc-700">
          <dt>{total[0]}</dt>
          <dd>{formatEur(total[1])}</dd>
        </div>
      )}
    </dl>
  );
}

function EvolucaoAno({ ano, meses }: { ano: number; meses: ResumoMes[] }) {
  const max = Math.max(1, ...meses.map((m) => Math.abs(m.saldo)), ...meses.map((m) => m.objetivo));
  return (
    <section className="rounded-2xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <h2 className="mb-3 text-sm font-semibold">Resumo de {ano}</h2>
      <div className="flex h-40 items-end gap-2" role="img" aria-label={`Saldo por mês em ${ano}`}>
        {meses.map((m) => (
          <div key={m.mes} className="flex flex-1 flex-col items-center gap-1">
            <span className="text-[10px] tabular-nums text-zinc-500">{Math.round(m.saldo)}</span>
            <div className={`w-full max-w-10 rounded-t ${m.saldo >= m.objetivo ? 'bg-emerald-500' : m.saldo >= 0 ? 'bg-amber-400' : 'bg-rose-500'}`}
              style={{ height: `${Math.max(3, (Math.abs(m.saldo) / max) * 100)}%` }} title={`${MESES[m.mes - 1]}: ${formatEur(m.saldo)}`} />
            <span className="text-[10px] text-zinc-400">{MESES[m.mes - 1].slice(0, 3)}</span>
          </div>
        ))}
      </div>
      <div className="mt-4 overflow-x-auto">
        <table className="w-full min-w-[36rem] text-sm tabular-nums">
          <thead className="text-left text-xs text-zinc-500">
            <tr><th className="py-1">Mês</th><th>Informática</th><th>Trading</th><th>Desp. negócio</th><th>Desp. pessoais</th><th>Saldo</th><th>vs objetivo</th></tr>
          </thead>
          <tbody>
            {meses.map((m) => (
              <tr key={m.mes} className="border-t border-zinc-100 dark:border-zinc-800">
                <td className="py-1 capitalize">{MESES[m.mes - 1]}</td>
                <td>{formatEur(m.lucroInformatica)}</td>
                <td>{formatEur(m.payoutsTrading)}</td>
                <td>{formatEur(m.despesasNegocio)}</td>
                <td>{formatEur(m.despesasPessoais)}</td>
                <td className="font-semibold">{formatEur(m.saldo)}</td>
                <td className={m.saldo >= m.objetivo ? 'text-emerald-600' : 'text-rose-600'}>{formatEur(m.saldo - m.objetivo)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
