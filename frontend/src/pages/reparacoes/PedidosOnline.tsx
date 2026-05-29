import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, FileText, Flag, Inbox, Mail, MessageCircle, Phone, Plus, Save, StickyNote, Wrench, X } from 'lucide-react';
import {
  repairRequestsApi,
  REPAIR_REQUEST_ESTADO,
  REPAIR_REQUEST_PRIORIDADE,
  REPAIR_REQUEST_ORIGEM,
  REPAIR_REQUEST_ORIGEM_LABEL,
  type RepairRequestDto,
  type RepairRequestEstado,
  type RepairRequestPrioridade,
  type RepairRequestOrigem,
} from '../../lib/repairRequests/api';
import { toast } from '../../lib/toast';
import Modal from '../../components/Modal';
import { formatDate } from '../../lib/money';
import { liveListOptions } from '../../lib/queryOptions';
import { displayPhone } from '../../lib/phone/formatter';

/**
 * Sprint 354 (Doc 83 Pillar 9): backoffice dos pedidos de reparação submetidos
 * via widget público. Converter cria a reparação (lookup-or-create cliente).
 */
export default function PedidosOnline() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [filtro, setFiltro] = useState<RepairRequestEstado>(REPAIR_REQUEST_ESTADO.Pendente);
  // Sprint 438: filtro adicional por canal de entrada. "all" mostra todos.
  const [origemFiltro, setOrigemFiltro] = useState<RepairRequestOrigem | 'all'>('all');
  const [slaFilter, setSlaFilter] = useState<'all' | 'overdue'>('all');

  const list = useQuery({
    queryKey: ['repair-requests', filtro],
    queryFn: () => repairRequestsApi.list(filtro),
    ...liveListOptions,
  });

  const allRequests = useQuery({
    queryKey: ['repair-requests', 'all'],
    queryFn: () => repairRequestsApi.list(),
    ...liveListOptions,
  });

  const converterMut = useMutation({
    mutationFn: (id: string) => repairRequestsApi.converter(id),
    onSuccess: (req) => {
      toast.success('Pedido convertido em reparação.');
      qc.invalidateQueries({ queryKey: ['repair-requests'] });
      qc.invalidateQueries({ queryKey: ['repair-requests-count'] });
      if (req.reparacaoId) navigate(`/reparacoes/${req.reparacaoId}`);
    },
    onError: (err) => toast.fromError(err, 'Erro a converter pedido.'),
  });

  // Sprint 437: segundo caminho — quando o cliente só quer estimativa, criamos
  // um Trabalho (status Orçamento) em vez de uma Reparacao física.
  const converterTrabMut = useMutation({
    mutationFn: (id: string) => repairRequestsApi.converterEmTrabalho(id),
    onSuccess: (req) => {
      toast.success('Pedido convertido em orçamento.');
      qc.invalidateQueries({ queryKey: ['repair-requests'] });
      qc.invalidateQueries({ queryKey: ['repair-requests-count'] });
      if (req.trabalhoId) navigate(`/trabalhos/${req.trabalhoId}`);
    },
    onError: (err) => toast.fromError(err, 'Erro a converter em orçamento.'),
  });

  const rejeitarMut = useMutation({
    mutationFn: ({ id, motivo }: { id: string; motivo?: string }) =>
      repairRequestsApi.rejeitar(id, motivo),
    onSuccess: () => {
      toast.success('Pedido rejeitado.');
      qc.invalidateQueries({ queryKey: ['repair-requests'] });
      qc.invalidateQueries({ queryKey: ['repair-requests-count'] });
    },
    onError: (err) => toast.fromError(err, 'Erro a rejeitar pedido.'),
  });

  const triagemMut = useMutation({
    mutationFn: (vars: { id: string; notasInternas: string | null; prioridade: RepairRequestPrioridade }) =>
      repairRequestsApi.updateTriagem(vars.id, { notasInternas: vars.notasInternas, prioridade: vars.prioridade }),
    onSuccess: () => {
      toast.success('Triagem guardada.');
      qc.invalidateQueries({ queryKey: ['repair-requests'] });
    },
    onError: (err) => toast.fromError(err, 'Erro a guardar triagem.'),
  });

  // Sprint 439: criar pedido manual para leads offline.
  const [showNovo, setShowNovo] = useState(false);
  const [rejectTarget, setRejectTarget] = useState<RepairRequestDto | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const novoMut = useMutation({
    mutationFn: repairRequestsApi.createManual,
    onSuccess: () => {
      toast.success('Pedido registado.');
      setShowNovo(false);
      qc.invalidateQueries({ queryKey: ['repair-requests'] });
      qc.invalidateQueries({ queryKey: ['repair-requests-count'] });
    },
    onError: (err) => toast.fromError(err, 'Erro a registar pedido.'),
  });

  function askRejeitar(request: RepairRequestDto) {
    setRejectTarget(request);
    setRejectReason('');
  }

  function closeRejectModal() {
    setRejectTarget(null);
    setRejectReason('');
  }

  function submitReject() {
    if (!rejectTarget) return;
    rejeitarMut.mutate(
      { id: rejectTarget.id, motivo: rejectReason.trim() || undefined },
      { onSuccess: closeRejectModal },
    );
  }

  const tabs: { label: string; value: RepairRequestEstado }[] = [
    { label: 'Pendentes', value: REPAIR_REQUEST_ESTADO.Pendente },
    { label: 'Convertidos', value: REPAIR_REQUEST_ESTADO.Convertido },
    { label: 'Rejeitados', value: REPAIR_REQUEST_ESTADO.Rejeitado },
  ];
  const counts = {
    pendentes: (allRequests.data ?? []).filter((r) => r.estado === REPAIR_REQUEST_ESTADO.Pendente).length,
    convertidos: (allRequests.data ?? []).filter((r) => r.estado === REPAIR_REQUEST_ESTADO.Convertido).length,
    rejeitados: (allRequests.data ?? []).filter((r) => r.estado === REPAIR_REQUEST_ESTADO.Rejeitado).length,
    atrasados: (allRequests.data ?? []).filter(
      (r) => r.estado === REPAIR_REQUEST_ESTADO.Pendente && isOverdueRequest(r),
    ).length,
    urgentes: (allRequests.data ?? []).filter(
      (r) => r.estado === REPAIR_REQUEST_ESTADO.Pendente && r.prioridade === REPAIR_REQUEST_PRIORIDADE.Urgente,
    ).length,
  };

  // Sprint 442: breakdown por canal nos últimos 30d — derivado de dados já carregados.
  // Mostra ao Bruno qual canal traz mais leads e onde investir esforço de marketing.
  const origemBreakdown = useMemo(() => {
    const cutoff = Date.now() - 30 * 86_400_000;
    const recents = (allRequests.data ?? []).filter((r) => new Date(r.createdAt).getTime() >= cutoff);
    const map = new Map<RepairRequestOrigem, number>();
    for (const r of recents) map.set(r.origem, (map.get(r.origem) ?? 0) + 1);
    return Array.from(map.entries())
      .sort((a, b) => b[1] - a[1])
      .map(([origem, count]) => ({ origem, count }));
  }, [allRequests.data]);

  // Pendentes ordenam por prioridade desc, depois mais antigos primeiro (SLA implícito).
  // Outras tabs mantém ordem natural (server already sorted by date).
  let rows = (list.data ?? []).slice();
  if (origemFiltro !== 'all') {
    rows = rows.filter((r) => r.origem === origemFiltro);
  }
  if (filtro === REPAIR_REQUEST_ESTADO.Pendente && slaFilter === 'overdue') {
    rows = rows.filter(isOverdueRequest);
  }
  if (filtro === REPAIR_REQUEST_ESTADO.Pendente) {
    rows.sort((a, b) => {
      if (a.prioridade !== b.prioridade) return b.prioridade - a.prioridade;
      return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
    });
  }

  return (
    <div className="space-y-4">
      <header className="flex items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">Pedidos online</h1>
          <p className="text-sm text-zinc-500">Inbox unificada: widget público, telefone, email, balcão.</p>
        </div>
        <button
          type="button" onClick={() => setShowNovo(true)}
          className="inline-flex items-center gap-1.5 self-start rounded-lg bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700"
        >
          <Plus size={14} /> Novo pedido
        </button>
      </header>

      {showNovo && (
        <NovoPedidoModal
          isSaving={novoMut.isPending}
          onClose={() => setShowNovo(false)}
          onSave={(payload) => novoMut.mutate(payload)}
        />
      )}

      <Modal
        open={!!rejectTarget}
        title="Rejeitar pedido"
        onClose={closeRejectModal}
        footer={
          <>
            <button
              type="button"
              onClick={closeRejectModal}
              className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              Cancelar
            </button>
            <button
              type="button"
              onClick={submitReject}
              disabled={rejeitarMut.isPending}
              className="rounded-lg bg-rose-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-rose-700 disabled:opacity-50"
            >
              {rejeitarMut.isPending ? 'A rejeitar...' : 'Rejeitar pedido'}
            </button>
          </>
        }
      >
        <div className="space-y-3">
          <p className="text-sm text-zinc-600 dark:text-zinc-400">
            Este pedido fica arquivado como rejeitado e nao cria reparacao nem orcamento.
          </p>
          {rejectTarget && (
            <div className="rounded-lg border border-zinc-200 bg-zinc-50 px-3 py-2 text-sm dark:border-zinc-800 dark:bg-zinc-950/50">
              <div className="font-medium text-zinc-900 dark:text-zinc-100">
                {rejectTarget.nome} · {rejectTarget.equipamento}
              </div>
              <div className="mt-0.5 line-clamp-2 text-xs text-zinc-500">{rejectTarget.descricao}</div>
            </div>
          )}
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-zinc-600 dark:text-zinc-400">
              Motivo interno (opcional)
            </span>
            <textarea
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              rows={4}
              maxLength={500}
              placeholder="Ex.: duplicado, cliente ja resolveu, spam, sem contacto valido..."
              className="w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-900"
            />
          </label>
        </div>
      </Modal>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <SummaryCard
          icon={Inbox}
          label="Por tratar"
          value={counts.pendentes}
          tone="amber"
          helper="Contactar, qualificar e converter."
        />
        <SummaryCard
          icon={CheckCircle2}
          label="Convertidos"
          value={counts.convertidos}
          tone="emerald"
          helper="Ja viraram reparacao."
        />
        <SummaryCard
          icon={AlertTriangle}
          label="Atrasados"
          value={counts.atrasados}
          tone="rose"
          helper="Pendentes ha mais de 48h."
        />
        <SummaryCard
          icon={X}
          label="Rejeitados"
          value={counts.rejeitados}
          tone="zinc"
          helper="Ruido, spam ou sem seguimento."
        />
      </div>

      {origemBreakdown.length > 0 && (
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 rounded-lg border border-zinc-200 bg-white px-3 py-2 text-xs dark:border-zinc-800 dark:bg-zinc-900">
          <span className="text-[10px] font-semibold uppercase tracking-wider text-zinc-500">Por canal · 30d</span>
          {origemBreakdown.map(({ origem, count }) => (
            <span
              key={origem}
              className="inline-flex items-center gap-1 rounded-full border border-zinc-200 bg-zinc-50 px-2 py-0.5 dark:border-zinc-700 dark:bg-zinc-800/60"
            >
              <span className="font-medium text-zinc-700 dark:text-zinc-200">{REPAIR_REQUEST_ORIGEM_LABEL[origem]}</span>
              <span className="tabular-nums text-zinc-500">{count}</span>
            </span>
          ))}
        </div>
      )}

      <div className="flex flex-wrap items-end justify-between gap-2 border-b border-zinc-200 dark:border-zinc-800">
        <div className="flex gap-1">
          {tabs.map((t) => (
            <button
              key={t.value}
              type="button"
              onClick={() => {
                setFiltro(t.value);
                if (t.value !== REPAIR_REQUEST_ESTADO.Pendente) setSlaFilter('all');
              }}
              className={`px-3 py-1.5 text-sm ${filtro === t.value ? 'border-b-2 border-brand-600 font-medium text-brand-700 dark:text-brand-400' : 'text-zinc-500'}`}
            >
              {t.label}
            </button>
          ))}
        </div>
        {filtro === REPAIR_REQUEST_ESTADO.Pendente && (
          <button
            type="button"
            onClick={() => setSlaFilter((value) => (value === 'overdue' ? 'all' : 'overdue'))}
            className={`mb-1 inline-flex items-center gap-1 rounded-full border px-2 py-1 text-[11px] font-medium transition ${
              slaFilter === 'overdue'
                ? 'border-rose-300 bg-rose-50 text-rose-700 dark:border-rose-900/60 dark:bg-rose-950/30 dark:text-rose-300'
                : 'border-zinc-200 bg-white text-zinc-500 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:hover:bg-zinc-800'
            }`}
          >
            <AlertTriangle size={12} />
            Atrasados 48h ({counts.atrasados})
          </button>
        )}
        <label className="flex items-center gap-1.5 pb-1 text-[11px] text-zinc-500">
          Canal
          <select
            value={origemFiltro}
            onChange={(e) => setOrigemFiltro(e.target.value === 'all' ? 'all' : (Number(e.target.value) as RepairRequestOrigem))}
            className="rounded border border-zinc-300 bg-white px-1.5 py-1 text-xs dark:border-zinc-700 dark:bg-zinc-900"
          >
            <option value="all">Todos</option>
            <option value={REPAIR_REQUEST_ORIGEM.Widget}>Widget</option>
            <option value={REPAIR_REQUEST_ORIGEM.Telefone}>Telefone</option>
            <option value={REPAIR_REQUEST_ORIGEM.Email}>Email</option>
            <option value={REPAIR_REQUEST_ORIGEM.WhatsApp}>WhatsApp</option>
            <option value={REPAIR_REQUEST_ORIGEM.BalcaoFisico}>Balcão</option>
            <option value={REPAIR_REQUEST_ORIGEM.Outro}>Outro</option>
          </select>
        </label>
      </div>

      {filtro === REPAIR_REQUEST_ESTADO.Pendente && counts.urgentes > 0 && (
        <div className="flex items-center gap-2 rounded-lg border border-rose-300 bg-rose-50 px-3 py-2 text-xs text-rose-700 dark:border-rose-900/60 dark:bg-rose-950/30 dark:text-rose-300">
          <AlertTriangle size={14} className="flex-none" />
          <span>
            {counts.urgentes === 1
              ? '1 pedido marcado como Urgente — trata primeiro.'
              : `${counts.urgentes} pedidos marcados como Urgentes — trata primeiro.`}
          </span>
        </div>
      )}

      <div className="grid gap-2">
        {list.isLoading && <p className="text-sm text-zinc-500">A carregar…</p>}
        {rows.length === 0 && !list.isLoading && (
          <div className="rounded-xl border border-dashed border-zinc-300 bg-white p-8 text-center text-sm text-zinc-500 dark:border-zinc-800 dark:bg-zinc-900">
            {slaFilter === 'overdue' ? 'Sem pedidos atrasados neste momento.' : 'Sem pedidos nesta categoria.'}
          </div>
        )}
        {rows.map((r) => (
          <PedidoCard
            key={r.id}
            request={r}
            isConverting={converterMut.isPending || converterTrabMut.isPending}
            isSavingTriagem={triagemMut.isPending}
            onConverterReparacao={() => converterMut.mutate(r.id)}
            onConverterTrabalho={() => converterTrabMut.mutate(r.id)}
            onRejeitar={() => askRejeitar(r)}
            onSaveTriagem={(notas, prioridade) =>
              triagemMut.mutate({ id: r.id, notasInternas: notas, prioridade })
            }
            onAbrirReparacao={(repId) => navigate(`/reparacoes/${repId}`)}
            onAbrirTrabalho={(trabId) => navigate(`/trabalhos/${trabId}`)}
          />
        ))}
      </div>
    </div>
  );
}

/**
 * Sprint 436: card individual com triagem inline (prioridade + notas).
 * Componente próprio para isolar local state do form, evitar re-render do mundo
 * sempre que se escreve uma nota.
 */
function PedidoCard({
  request,
  isConverting,
  isSavingTriagem,
  onConverterReparacao,
  onConverterTrabalho,
  onRejeitar,
  onSaveTriagem,
  onAbrirReparacao,
  onAbrirTrabalho,
}: {
  request: RepairRequestDto;
  isConverting: boolean;
  isSavingTriagem: boolean;
  onConverterReparacao: () => void;
  onConverterTrabalho: () => void;
  onRejeitar: () => void;
  onSaveTriagem: (notas: string | null, prioridade: RepairRequestPrioridade) => void;
  onAbrirReparacao: (repId: string) => void;
  onAbrirTrabalho: (trabId: string) => void;
}) {
  const [notas, setNotas] = useState(request.notasInternas ?? '');
  const [prioridade, setPrioridade] = useState<RepairRequestPrioridade>(request.prioridade);
  const isPendente = request.estado === REPAIR_REQUEST_ESTADO.Pendente;
  const dirty = (notas.trim() || null) !== (request.notasInternas ?? null) || prioridade !== request.prioridade;

  const borderTone = prioridadeBorder(request.prioridade);

  return (
    <div className={`rounded-lg border bg-white p-3 dark:bg-zinc-900 ${borderTone}`}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <div className="font-medium">
              {request.nome} · <span className="font-normal text-zinc-600 dark:text-zinc-400">{request.equipamento}</span>
            </div>
            {isPendente && <PrioridadeBadge prioridade={request.prioridade} />}
          </div>
          <div className="mt-0.5 flex flex-wrap gap-x-3 gap-y-0.5 text-[11px] text-zinc-500">
            {request.telefone && <span className="inline-flex items-center gap-1"><Phone size={10} /> {displayPhone(request.telefone)}</span>}
            {request.email && <span className="inline-flex items-center gap-1"><Mail size={10} /> {request.email}</span>}
            <span>{formatDate(request.createdAt)}</span>
            <span className="text-zinc-400">via {REPAIR_REQUEST_ORIGEM_LABEL[request.origem]}</span>
          </div>
          <p className="mt-1.5 whitespace-pre-line text-sm text-zinc-700 dark:text-zinc-300">{request.descricao}</p>
          {request.motivoRejeicao && <p className="mt-1 text-xs italic text-rose-600">Rejeitado: {request.motivoRejeicao}</p>}
          {!isPendente && request.notasInternas && (
            <p className="mt-1 inline-flex items-start gap-1 text-xs text-zinc-500">
              <StickyNote size={11} className="mt-0.5 flex-none" />
              <span className="whitespace-pre-line">{request.notasInternas}</span>
            </p>
          )}
          <LeadContactActions request={request} />
        </div>
        {isPendente && (
          <div className="flex shrink-0 flex-col gap-1">
            <button
              type="button" disabled={isConverting}
              onClick={onConverterReparacao}
              title="Cliente vai trazer o equipamento — abrir reparação"
              className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-2.5 py-1.5 text-xs font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
            >
              <Wrench size={12} /> Reparação
            </button>
            <button
              type="button" disabled={isConverting}
              onClick={onConverterTrabalho}
              title="Cliente só quer estimativa — abrir orçamento"
              className="inline-flex items-center gap-1 rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1.5 text-xs font-medium text-brand-700 hover:bg-brand-100 disabled:opacity-50 dark:border-brand-900/60 dark:bg-brand-950/30 dark:text-brand-300"
            >
              <FileText size={12} /> Orçamento
            </button>
            <button
              type="button" onClick={onRejeitar}
              className="inline-flex items-center gap-1 rounded-lg border border-zinc-300 px-2.5 py-1.5 text-xs text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-400 dark:hover:bg-zinc-800"
            >
              <X size={12} /> Rejeitar
            </button>
          </div>
        )}
        {!isPendente && request.reparacaoId && (
          <button
            type="button" onClick={() => onAbrirReparacao(request.reparacaoId!)}
            className="shrink-0 text-xs text-brand-600 hover:underline"
          >
            Ver reparação →
          </button>
        )}
        {!isPendente && !request.reparacaoId && request.trabalhoId && (
          <button
            type="button" onClick={() => onAbrirTrabalho(request.trabalhoId!)}
            className="shrink-0 text-xs text-brand-600 hover:underline"
          >
            Ver orçamento →
          </button>
        )}
      </div>

      {isPendente && (
        <div className="mt-3 grid gap-2 border-t border-zinc-100 pt-2 dark:border-zinc-800 sm:grid-cols-[160px_1fr_auto]">
          <label className="flex items-center gap-1.5 text-xs">
            <Flag size={12} className="text-zinc-400" />
            <select
              value={prioridade}
              onChange={(e) => setPrioridade(Number(e.target.value) as RepairRequestPrioridade)}
              className="w-full rounded border border-zinc-300 bg-white px-1.5 py-1 text-xs dark:border-zinc-700 dark:bg-zinc-900"
            >
              <option value={REPAIR_REQUEST_PRIORIDADE.Baixa}>Baixa</option>
              <option value={REPAIR_REQUEST_PRIORIDADE.Normal}>Normal</option>
              <option value={REPAIR_REQUEST_PRIORIDADE.Alta}>Alta</option>
              <option value={REPAIR_REQUEST_PRIORIDADE.Urgente}>Urgente</option>
            </select>
          </label>
          <textarea
            value={notas}
            onChange={(e) => setNotas(e.target.value)}
            placeholder="Notas internas (cliente já ligou, espera confirmação, etc.) — não visíveis ao cliente"
            rows={2}
            maxLength={2000}
            className="w-full rounded border border-zinc-300 bg-white px-2 py-1 text-xs dark:border-zinc-700 dark:bg-zinc-900"
          />
          <button
            type="button"
            disabled={!dirty || isSavingTriagem}
            onClick={() => onSaveTriagem(notas.trim() ? notas.trim() : null, prioridade)}
            className="inline-flex items-center justify-center gap-1 self-start rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1.5 text-xs font-medium text-brand-700 hover:bg-brand-100 disabled:opacity-40 dark:border-brand-900/60 dark:bg-brand-950/30 dark:text-brand-300"
          >
            <Save size={12} /> Guardar
          </button>
        </div>
      )}
    </div>
  );
}

function PrioridadeBadge({ prioridade }: { prioridade: RepairRequestPrioridade }) {
  if (prioridade === REPAIR_REQUEST_PRIORIDADE.Normal) return null;
  const map: Record<number, { label: string; cls: string }> = {
    [REPAIR_REQUEST_PRIORIDADE.Baixa]: { label: 'Baixa', cls: 'border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300' },
    [REPAIR_REQUEST_PRIORIDADE.Alta]: { label: 'Alta', cls: 'border-amber-300 bg-amber-50 text-amber-700 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-300' },
    [REPAIR_REQUEST_PRIORIDADE.Urgente]: { label: 'Urgente', cls: 'border-rose-300 bg-rose-50 text-rose-700 dark:border-rose-900/60 dark:bg-rose-950/30 dark:text-rose-300' },
  };
  const tone = map[prioridade];
  return (
    <span className={`inline-flex items-center gap-1 rounded-full border px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide ${tone.cls}`}>
      <Flag size={9} /> {tone.label}
    </span>
  );
}

/**
 * Sprint 439: modal simples para staff registar lead que entrou por canal
 * offline (telefone, balcão). Mantém o mesmo modelo de RepairRequest — depois
 * o pedido aparece na inbox e segue o mesmo fluxo de triagem/conversão.
 */
function NovoPedidoModal({
  onClose,
  onSave,
  isSaving,
}: {
  onClose: () => void;
  onSave: (payload: {
    nome: string;
    telefone: string | null;
    email: string | null;
    equipamento: string;
    descricao: string;
    origem: RepairRequestOrigem;
    prioridade?: RepairRequestPrioridade;
    notasInternas?: string | null;
  }) => void;
  isSaving: boolean;
}) {
  const [nome, setNome] = useState('');
  const [telefone, setTelefone] = useState('');
  const [email, setEmail] = useState('');
  const [equipamento, setEquipamento] = useState('');
  const [descricao, setDescricao] = useState('');
  const [origem, setOrigem] = useState<RepairRequestOrigem>(REPAIR_REQUEST_ORIGEM.Telefone);
  const [prioridade, setPrioridade] = useState<RepairRequestPrioridade>(REPAIR_REQUEST_PRIORIDADE.Normal);
  const [notas, setNotas] = useState('');

  const valid =
    nome.trim().length >= 2 &&
    equipamento.trim().length >= 2 &&
    descricao.trim().length >= 5 &&
    (telefone.trim().length > 0 || email.trim().length > 0);

  function handleSave() {
    if (!valid) return;
    onSave({
      nome: nome.trim(),
      telefone: telefone.trim() || null,
      email: email.trim() || null,
      equipamento: equipamento.trim(),
      descricao: descricao.trim(),
      origem,
      prioridade,
      notasInternas: notas.trim() ? notas.trim() : null,
    });
  }

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center bg-black/40 p-4 sm:items-center" onClick={onClose}>
      <div className="w-full max-w-lg rounded-xl border border-zinc-200 bg-white p-4 shadow-xl dark:border-zinc-800 dark:bg-zinc-900" onClick={(e) => e.stopPropagation()}>
        <div className="mb-3 flex items-start justify-between gap-3">
          <div>
            <h2 className="text-base font-semibold">Novo pedido (offline)</h2>
            <p className="text-xs text-zinc-500">Lead recebido por telefone, balcão ou outro canal.</p>
          </div>
          <button type="button" onClick={onClose} className="rounded p-1 text-zinc-500 hover:bg-zinc-100 dark:hover:bg-zinc-800">
            <X size={16} />
          </button>
        </div>

        <div className="grid gap-2.5">
          <Field label="Nome">
            <input value={nome} onChange={(e) => setNome(e.target.value)} maxLength={120} className={inputCls} placeholder="Ex.: João Silva" />
          </Field>
          <div className="grid gap-2.5 sm:grid-cols-2">
            <Field label="Telefone">
              <input value={telefone} onChange={(e) => setTelefone(e.target.value)} maxLength={32} className={inputCls} placeholder="912 345 678" />
            </Field>
            <Field label="Email">
              <input value={email} onChange={(e) => setEmail(e.target.value)} maxLength={120} className={inputCls} placeholder="joao@email.pt" type="email" />
            </Field>
          </div>
          <Field label="Equipamento">
            <input value={equipamento} onChange={(e) => setEquipamento(e.target.value)} maxLength={120} className={inputCls} placeholder="Ex.: iPhone 13" />
          </Field>
          <Field label="Descrição / avaria">
            <textarea value={descricao} onChange={(e) => setDescricao(e.target.value)} maxLength={2000} rows={3} className={inputCls} placeholder="Ex.: ecrã partido, quer estimativa antes de trazer" />
          </Field>
          <div className="grid gap-2.5 sm:grid-cols-2">
            <Field label="Canal">
              <select value={origem} onChange={(e) => setOrigem(Number(e.target.value) as RepairRequestOrigem)} className={inputCls}>
                <option value={REPAIR_REQUEST_ORIGEM.Telefone}>Telefone</option>
                <option value={REPAIR_REQUEST_ORIGEM.Email}>Email</option>
                <option value={REPAIR_REQUEST_ORIGEM.WhatsApp}>WhatsApp</option>
                <option value={REPAIR_REQUEST_ORIGEM.BalcaoFisico}>Balcão</option>
                <option value={REPAIR_REQUEST_ORIGEM.Outro}>Outro</option>
              </select>
            </Field>
            <Field label="Prioridade">
              <select value={prioridade} onChange={(e) => setPrioridade(Number(e.target.value) as RepairRequestPrioridade)} className={inputCls}>
                <option value={REPAIR_REQUEST_PRIORIDADE.Baixa}>Baixa</option>
                <option value={REPAIR_REQUEST_PRIORIDADE.Normal}>Normal</option>
                <option value={REPAIR_REQUEST_PRIORIDADE.Alta}>Alta</option>
                <option value={REPAIR_REQUEST_PRIORIDADE.Urgente}>Urgente</option>
              </select>
            </Field>
          </div>
          <Field label="Notas internas (opcional)">
            <textarea value={notas} onChange={(e) => setNotas(e.target.value)} maxLength={2000} rows={2} className={inputCls} placeholder="Ex.: cliente vai trazer amanhã ao final do dia" />
          </Field>
        </div>

        <div className="mt-4 flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800">
            Cancelar
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={!valid || isSaving}
            className="rounded-lg bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {isSaving ? 'A guardar…' : 'Registar pedido'}
          </button>
        </div>
      </div>
    </div>
  );
}

const inputCls =
  'w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm dark:border-zinc-700 dark:bg-zinc-900';

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-0.5 block text-[11px] font-medium text-zinc-600 dark:text-zinc-400">{label}</span>
      {children}
    </label>
  );
}

function prioridadeBorder(prioridade: RepairRequestPrioridade): string {
  if (prioridade === REPAIR_REQUEST_PRIORIDADE.Urgente)
    return 'border-rose-300 dark:border-rose-900/60';
  if (prioridade === REPAIR_REQUEST_PRIORIDADE.Alta)
    return 'border-amber-300 dark:border-amber-900/60';
  return 'border-zinc-200 dark:border-zinc-700';
}

function isOverdueRequest(request: RepairRequestDto): boolean {
  const createdAt = new Date(request.createdAt).getTime();
  if (!Number.isFinite(createdAt)) return false;
  return Date.now() - createdAt >= 48 * 60 * 60 * 1000;
}

function LeadContactActions({ request }: { request: RepairRequestDto }) {
  const waPhone = whatsappPhone(request.telefone);
  const subject = `Pedido de reparacao - ${request.equipamento}`;
  const body = `Ola ${request.nome},\n\nRecebemos o teu pedido sobre ${request.equipamento}. Consegues trazer o equipamento a loja ou enviar mais detalhes?\n\nObrigado.`;
  const whatsappText = `Ola ${request.nome}, e da LopesTech. Recebemos o teu pedido sobre ${request.equipamento}. Consegues trazer o equipamento a loja ou enviar mais detalhes?`;

  if (!request.telefone && !request.email) return null;

  return (
    <div className="mt-3 flex flex-wrap gap-2">
      {request.telefone && (
        <a
          href={`tel:${request.telefone}`}
          className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-zinc-300 px-2.5 py-1.5 text-xs font-medium text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
        >
          <Phone size={12} /> Ligar
        </a>
      )}
      {waPhone && (
        <a
          href={`https://wa.me/${waPhone}?text=${encodeURIComponent(whatsappText)}`}
          target="_blank"
          rel="noreferrer"
          className="inline-flex min-h-9 items-center gap-1.5 rounded-lg bg-emerald-600 px-2.5 py-1.5 text-xs font-medium text-white hover:bg-emerald-700"
        >
          <MessageCircle size={12} /> WhatsApp
        </a>
      )}
      {request.email && (
        <a
          href={`mailto:${request.email}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`}
          className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-brand-200 bg-brand-50 px-2.5 py-1.5 text-xs font-medium text-brand-700 hover:bg-brand-100 dark:border-brand-900/60 dark:bg-brand-950/30 dark:text-brand-300"
        >
          <Mail size={12} /> Email
        </a>
      )}
    </div>
  );
}

function whatsappPhone(raw: string | null): string | null {
  if (!raw) return null;
  const digits = raw.replace(/\D/g, '');
  if (digits.length === 9 && digits.startsWith('9')) return `351${digits}`;
  if (digits.length === 12 && digits.startsWith('351')) return digits;
  return digits.length >= 9 ? digits : null;
}

function SummaryCard({
  icon: Icon,
  label,
  value,
  helper,
  tone,
}: {
  icon: typeof Inbox;
  label: string;
  value: number;
  helper: string;
  tone: 'amber' | 'emerald' | 'rose' | 'zinc';
}) {
  const toneClass = {
    amber: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-300',
    emerald: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/50 dark:bg-emerald-950/30 dark:text-emerald-300',
    rose: 'border-rose-200 bg-rose-50 text-rose-700 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-300',
    zinc: 'border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300',
  }[tone];

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-4 shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-[11px] font-semibold uppercase tracking-wide text-zinc-500">{label}</div>
          <div className="mt-1 text-2xl font-semibold tabular-nums">{value}</div>
          <div className="mt-1 text-xs text-zinc-500">{helper}</div>
        </div>
        <div className={`rounded-lg border p-2 ${toneClass}`}>
          <Icon size={16} />
        </div>
      </div>
    </div>
  );
}
