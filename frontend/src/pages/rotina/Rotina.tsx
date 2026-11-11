import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, Clock, Pencil, Plus, SkipForward, Undo2 } from 'lucide-react';
import { Button, EmptyState, PageHeader, SkeletonTable } from '../../components/ui';
import { StaffPushToggle } from '../../components/StaffPushToggle';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import {
  RotinaEstado, RotinaTipo, TIPO_INFO, horaLocal, linhasDetalhe, rotinaApi,
  type RegistarRotina, type RotinaDia, type RotinaDiaItem, type RotinaItem,
} from '../../lib/rotina/api';
import RotinaItemForm from './RotinaItemForm';
import ImportarRotina from './ImportarRotina';
import PesoHistorico from './PesoHistorico';

/**
 * Sprint 615 (Doc 98): «Agora» — o que é suposto estares a fazer, o que vem a seguir e o plano de hoje.
 * Os avisos do telemóvel abrem esta página com ?acao=feito|adiar&item=&dia= e a ação faz-se aqui.
 */
export default function Rotina() {
  const qc = useQueryClient();
  const [params, setParams] = useSearchParams();
  const [editar, setEditar] = useState<RotinaItem | 'novo' | null>(null);
  const [importar, setImportar] = useState(false);
  const [pedido, setPedido] = useState<{ linha: RotinaDiaItem; tipo: 'adiar' | 'saltar' | 'peso' } | null>(null);
  const [agora, setAgora] = useState(() => Date.now());

  const dia = useQuery({ queryKey: ['rotina', 'dia'], queryFn: () => rotinaApi.dia(), refetchInterval: 60_000 });
  useEffect(() => {
    const t = setInterval(() => setAgora(Date.now()), 15_000);
    return () => clearInterval(t);
  }, []);

  const registar = useMutation({
    mutationFn: ({ linha, req }: { linha: RotinaDiaItem; req: RegistarRotina }) =>
      rotinaApi.registar(linha.item.id, dataDe(dia.data!, linha), req),
    onSuccess: (_r, { req }) => {
      qc.invalidateQueries({ queryKey: ['rotina'] });
      if (req.estado === RotinaEstado.Feito) toast.success('Feito ✓', 'Boa. Agora a próxima coisa.');
      setPedido(null);
    },
    onError: (e) => toast.error('Não ficou registado', apiErrorMessage(e)),
  });

  // Ação vinda do botão da notificação.
  useEffect(() => {
    const acao = params.get('acao');
    const itemId = params.get('item');
    if (!acao || !itemId || !dia.data) return;
    const linha = dia.data.itens.find((l) => l.item.id === itemId);
    setParams({}, { replace: true });
    if (!linha) return;
    if (acao === 'feito') {
      if (linha.item.tipo === RotinaTipo.Peso) setPedido({ linha, tipo: 'peso' });
      else registar.mutate({ linha, req: { estado: RotinaEstado.Feito } });
    } else if (acao === 'adiar') {
      if (linha.item.tipo === RotinaTipo.Limite) setPedido({ linha, tipo: 'adiar' });
      else registar.mutate({ linha, req: { estado: RotinaEstado.Adiado, adiarMin: 15 } });
    }
  }, [params, dia.data]); // eslint-disable-line react-hooks/exhaustive-deps

  const d = dia.data;
  const atual = d?.itens.find((l) => l.item.id === d.atualId) ?? null;
  const proximo = d?.itens.find((l) => l.item.id === d.proximoId) ?? null;
  const percent = d && d.devidos > 0 ? Math.round((d.feitos / d.devidos) * 100) : null;

  function feito(linha: RotinaDiaItem) {
    if (linha.item.tipo === RotinaTipo.Peso) setPedido({ linha, tipo: 'peso' });
    else registar.mutate({ linha, req: { estado: RotinaEstado.Feito } });
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Rotina"
        description="Um plano, avisos no telemóvel e um botão de «feito». Agora só existe uma coisa."
        actions={(
          <div className="flex flex-wrap items-center gap-2">
            <StaffPushToggle />
            <Button variant="ghost" onClick={() => setImportar(true)}>Importar</Button>
            <Button variant="secondary" leftIcon={<Plus size={16} />} onClick={() => setEditar('novo')}>Novo item</Button>
          </div>
        )}
      />

      {dia.isLoading ? (
        <SkeletonTable columns={3} rows={6} />
      ) : dia.isError ? (
        <p className="text-sm text-rose-600">{apiErrorMessage(dia.error)}</p>
      ) : d && d.itens.length === 0 ? (
        <EmptyState
          icon={Clock}
          title="Ainda não tens rotina para hoje"
          description="Cria os teus blocos: refeições, meditação, ginásio, trading e a hora de desligar o PC. O Mender avisa-te no telemóvel."
          action={<Button leftIcon={<Plus size={16} />} onClick={() => setEditar('novo')}>Novo item</Button>}
        />
      ) : d ? (
        <>
          <section className="grid gap-3 lg:grid-cols-3">
            <AgoraCard titulo="Agora" linha={atual} agora={agora} vazio="Sem bloco a decorrer." onFeito={feito} />
            <AgoraCard titulo="A seguir" linha={proximo} agora={agora} vazio="Acabou o plano de hoje." />
            <div className="rounded-2xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
              <p className="text-xs font-medium uppercase tracking-wide text-zinc-500">Hoje</p>
              <p className="mt-1 text-3xl font-bold text-zinc-950 dark:text-zinc-50">
                {d.feitos}/{d.devidos}
                {percent != null && <span className="ml-2 text-base font-medium text-zinc-500">{percent}%</span>}
              </p>
              <p className="text-sm text-zinc-500">cumprido até agora</p>
              {d.meditacoesTotal > 0 && (
                <p className="mt-2 text-sm">🧘 Meditação {d.meditacoesFeitas}/{d.meditacoesTotal}</p>
              )}
            </div>
          </section>

          <ul className="divide-y divide-zinc-100 overflow-hidden rounded-2xl border border-zinc-200 bg-white dark:divide-zinc-800 dark:border-zinc-800 dark:bg-zinc-900">
            {d.itens.map((l) => (
              <LinhaDia
                key={l.item.id}
                linha={l}
                destaque={l.item.id === d.atualId}
                ocupado={registar.isPending}
                onFeito={() => feito(l)}
                onAdiar={() => (l.item.tipo === RotinaTipo.Limite
                  ? setPedido({ linha: l, tipo: 'adiar' })
                  : registar.mutate({ linha: l, req: { estado: RotinaEstado.Adiado, adiarMin: 15 } }))}
                onSaltar={() => setPedido({ linha: l, tipo: 'saltar' })}
                onDesfazer={() => registar.mutate({ linha: l, req: { estado: null } })}
                onEditar={() => setEditar(l.item)}
              />
            ))}
          </ul>

          {d.itens.some((l) => l.item.tipo === RotinaTipo.Peso) && (
            <PesoHistorico itemId={d.itens.find((l) => l.item.tipo === RotinaTipo.Peso)!.item.id} />
          )}
        </>
      ) : null}

      <TodosItens onEditar={setEditar} />

      {importar && <ImportarRotina onClose={() => setImportar(false)} />}
      {editar && <RotinaItemForm item={editar === 'novo' ? null : editar} onClose={() => setEditar(null)} />}
      {pedido && (
        <PedidoDialog
          pedido={pedido}
          ocupado={registar.isPending}
          onClose={() => setPedido(null)}
          onConfirmar={(req) => registar.mutate({ linha: pedido.linha, req })}
        />
      )}
    </div>
  );
}

function dataDe(d: RotinaDia, _l: RotinaDiaItem) {
  return d.dia;
}

function AgoraCard({ titulo, linha, agora, vazio, onFeito }: {
  titulo: string; linha: RotinaDiaItem | null; agora: number; vazio: string; onFeito?: (l: RotinaDiaItem) => void;
}) {
  const info = linha ? TIPO_INFO[linha.item.tipo] : null;
  let tempo: string | null = null;
  if (linha) {
    const ini = new Date(linha.inicioUtc).getTime();
    const fim = new Date(linha.fimUtc).getTime();
    if (agora < ini) tempo = `daqui a ${minutos(ini - agora)}`;
    else if (fim > ini && agora < fim) tempo = `faltam ${minutos(fim - agora)}`;
  }
  return (
    <div className={`rounded-2xl border p-4 ${titulo === 'Agora' && linha ? 'border-brand-300 bg-brand-50 dark:border-brand-800 dark:bg-brand-950/30' : 'border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900'}`}>
      <p className="text-xs font-medium uppercase tracking-wide text-zinc-500">{titulo}</p>
      {linha && info ? (
        <>
          <p className="mt-1 text-lg font-semibold text-zinc-950 dark:text-zinc-50">{info.icone} {linha.item.titulo}</p>
          <p className="text-sm text-zinc-500">
            {horaLocal(linha.inicioUtc)}{linha.item.duracaoMin > 0 && `–${horaLocal(linha.fimUtc)}`}{tempo && ` · ${tempo}`}
          </p>
          {onFeito && <Button size="sm" className="mt-3" leftIcon={<Check size={14} />} onClick={() => onFeito(linha)}>Feito</Button>}
        </>
      ) : (
        <p className="mt-1 text-sm text-zinc-500">{vazio}</p>
      )}
    </div>
  );
}

function minutos(ms: number) {
  const m = Math.max(1, Math.round(ms / 60000));
  return m >= 60 ? `${Math.floor(m / 60)} h ${m % 60 ? `${m % 60} min` : ''}`.trim() : `${m} min`;
}

function LinhaDia({ linha, destaque, ocupado, onFeito, onAdiar, onSaltar, onDesfazer, onEditar }: {
  linha: RotinaDiaItem; destaque: boolean; ocupado: boolean;
  onFeito: () => void; onAdiar: () => void; onSaltar: () => void; onDesfazer: () => void; onEditar: () => void;
}) {
  const { item, estado } = linha;
  const info = TIPO_INFO[item.tipo];
  const passos = linhasDetalhe(item.detalhe);
  const fechado = estado === RotinaEstado.Feito || estado === RotinaEstado.Saltado;
  return (
    <li className={`flex flex-col gap-2 p-3 sm:flex-row sm:items-start ${destaque ? 'bg-brand-50/60 dark:bg-brand-950/20' : ''} ${fechado ? 'opacity-60' : ''}`}>
      <div className="w-24 shrink-0 text-sm tabular-nums text-zinc-600 dark:text-zinc-300">
        {horaLocal(linha.inicioUtc)}{item.duracaoMin > 0 && <span className="text-zinc-400">–{horaLocal(linha.fimUtc)}</span>}
      </div>
      <div className="min-w-0 flex-1">
        <p className="flex flex-wrap items-center gap-2 font-medium text-zinc-950 dark:text-zinc-50">
          <span className={`rounded-full px-2 py-0.5 text-xs ${info.cor}`}>{info.icone} {info.label}</span>
          <span className={estado === RotinaEstado.Feito ? 'line-through' : ''}>{item.titulo}</span>
          {estado === RotinaEstado.Feito && <span className="text-xs text-emerald-600">✓ feito{linha.valor != null && ` · ${linha.valor} kg`}</span>}
          {estado === RotinaEstado.Saltado && <span className="text-xs text-zinc-500">saltado{linha.motivo && ` — ${linha.motivo}`}</span>}
          {estado === RotinaEstado.Adiado && linha.adiadoAte && <span className="text-xs text-amber-600">adiado para {horaLocal(linha.adiadoAte)}{linha.motivo && ` — ${linha.motivo}`}</span>}
          {linha.atrasado && !fechado && <span className="text-xs font-semibold text-rose-600">atrasado</span>}
        </p>
        {passos.length > 0 && (
          <ul className="mt-1 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-zinc-500">
            {passos.map((p) => <li key={p}>• {p}</li>)}
          </ul>
        )}
      </div>
      <div className="flex shrink-0 flex-wrap gap-1">
        {fechado ? (
          <Button size="sm" variant="ghost" leftIcon={<Undo2 size={14} />} disabled={ocupado} onClick={onDesfazer}>Desfazer</Button>
        ) : (
          <>
            <Button size="sm" leftIcon={<Check size={14} />} disabled={ocupado} onClick={onFeito}>Feito</Button>
            <Button size="sm" variant="secondary" leftIcon={<Clock size={14} />} disabled={ocupado} onClick={onAdiar}>+15</Button>
            <Button size="sm" variant="ghost" leftIcon={<SkipForward size={14} />} disabled={ocupado} onClick={onSaltar}>Saltar</Button>
          </>
        )}
        <Button size="sm" variant="ghost" aria-label={`Editar ${item.titulo}`} onClick={onEditar}><Pencil size={14} /></Button>
      </div>
    </li>
  );
}

function PedidoDialog({ pedido, ocupado, onClose, onConfirmar }: {
  pedido: { linha: RotinaDiaItem; tipo: 'adiar' | 'saltar' | 'peso' }; ocupado: boolean;
  onClose: () => void; onConfirmar: (req: RegistarRotina) => void;
}) {
  const [texto, setTexto] = useState('');
  const [kg, setKg] = useState('');
  const { linha, tipo } = pedido;
  const limite = linha.item.tipo === RotinaTipo.Limite;
  const titulo = tipo === 'peso' ? '⚖️ Registar peso' : tipo === 'adiar' ? '⏳ Mais 15 minutos?' : 'Saltar este item';
  const motivos = limite ? ['Terminar tarefa importante', 'Cliente / emergência'] : [];
  const valido = tipo === 'peso' ? Number(kg.replace(',', '.')) >= 20 : tipo === 'adiar' && limite ? texto.trim().length >= 3 : true;

  function confirmar() {
    if (tipo === 'peso') onConfirmar({ estado: RotinaEstado.Feito, valor: Number(kg.replace(',', '.')) });
    else if (tipo === 'adiar') onConfirmar({ estado: RotinaEstado.Adiado, adiarMin: 15, motivo: texto || null });
    else onConfirmar({ estado: RotinaEstado.Saltado, motivo: texto || null });
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 sm:items-center sm:p-4" onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-labelledby="pedido-titulo"
        className="w-full rounded-t-2xl border border-zinc-200 bg-white p-5 shadow-xl dark:border-zinc-800 dark:bg-zinc-900 sm:max-w-md sm:rounded-2xl"
        onClick={(e) => e.stopPropagation()}>
        <h2 id="pedido-titulo" className="text-lg font-semibold">{titulo}</h2>
        <p className="mt-1 text-sm text-zinc-500">{linha.item.titulo}</p>
        {tipo === 'adiar' && limite && (
          <p className="mt-3 rounded-lg bg-rose-50 p-2 text-sm text-rose-800 dark:bg-rose-950/40 dark:text-rose-200">
            Passar do limite fica registado. Diz porquê — assim vês o padrão no fim da semana.
          </p>
        )}
        {tipo === 'peso' ? (
          <label className="mt-4 block text-sm">
            Peso (kg)
            <input autoFocus inputMode="decimal" value={kg} onChange={(e) => setKg(e.target.value)}
              className="mt-1 w-full rounded-lg border border-zinc-300 px-3 py-2 dark:border-zinc-700 dark:bg-zinc-950" placeholder="ex.: 78,4" />
          </label>
        ) : (
          <>
            {motivos.length > 0 && (
              <div className="mt-3 flex flex-wrap gap-2">
                {motivos.map((m) => (
                  <button key={m} type="button" onClick={() => setTexto(m)}
                    className={`rounded-full border px-3 py-1 text-sm ${texto === m ? 'border-brand-500 bg-brand-50 dark:bg-brand-950/40' : 'border-zinc-300 dark:border-zinc-700'}`}>{m}</button>
                ))}
              </div>
            )}
            <label className="mt-3 block text-sm">
              {tipo === 'adiar' && limite ? 'Porquê?' : 'Motivo (opcional)'}
              <input autoFocus value={texto} maxLength={200} onChange={(e) => setTexto(e.target.value)}
                className="mt-1 w-full rounded-lg border border-zinc-300 px-3 py-2 dark:border-zinc-700 dark:bg-zinc-950" />
            </label>
          </>
        )}
        <div className="mt-5 flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>Cancelar</Button>
          <Button disabled={!valido || ocupado} onClick={confirmar}>Confirmar</Button>
        </div>
      </div>
    </div>
  );
}

function TodosItens({ onEditar }: { onEditar: (i: RotinaItem) => void }) {
  const itens = useQuery({ queryKey: ['rotina', 'itens'], queryFn: rotinaApi.itens });
  const [aberto, setAberto] = useState(false);
  const lista = useMemo(() => itens.data ?? [], [itens.data]);
  if (lista.length === 0) return null;
  return (
    <section className="rounded-2xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
      <button type="button" onClick={() => setAberto((a) => !a)} className="flex w-full items-center justify-between p-4 text-left">
        <span className="font-semibold">Toda a rotina ({lista.length} itens)</span>
        <span className="text-sm text-zinc-500">{aberto ? 'Esconder' : 'Ver e editar'}</span>
      </button>
      {aberto && (
        <ul className="divide-y divide-zinc-100 border-t border-zinc-100 dark:divide-zinc-800 dark:border-zinc-800">
          {lista.map((i) => (
            <li key={i.id} className={`flex items-center gap-3 px-4 py-2 text-sm ${i.ativo ? '' : 'opacity-50'}`}>
              <span className="w-12 tabular-nums">{i.hora}</span>
              <span className="flex-1 truncate">{TIPO_INFO[i.tipo].icone} {i.titulo}</span>
              <span className="hidden text-xs text-zinc-500 sm:inline">{resumoDias(i)}</span>
              <Button size="sm" variant="ghost" aria-label={`Editar ${i.titulo}`} onClick={() => onEditar(i)}><Pencil size={14} /></Button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function resumoDias(i: RotinaItem) {
  if (i.intervaloDias) return `de ${i.intervaloDias} em ${i.intervaloDias} dias`;
  if (i.dias === 127) return 'todos os dias';
  const nomes = [[2, 'Seg'], [4, 'Ter'], [8, 'Qua'], [16, 'Qui'], [32, 'Sex'], [64, 'Sáb'], [1, 'Dom']] as const;
  return nomes.filter(([b]) => (i.dias & b) !== 0).map(([, n]) => n).join(' ');
}
