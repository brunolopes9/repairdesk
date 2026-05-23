import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { AlertTriangle, Lock, Phone, Search, Snowflake } from 'lucide-react';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import GarantiaCard from '../../components/GarantiaCard';
import { isAxiosError } from 'axios';
import PecasReparacao from '../../components/PecasReparacao';
import DiagnosticoGuiado from '../../components/DiagnosticoGuiado';
import EquipmentFieldsForm, {
  buildEquipmentFieldValues,
  initEquipmentFieldValues,
  missingRequiredEquipmentFields,
  type EquipmentFieldValuesMap,
} from '../../components/EquipmentFieldsForm';
import FotosReparacao from '../../components/FotosReparacao';
import Modal from '../../components/Modal';
import WhatsAppMenu from '../../components/WhatsAppMenu';
import { Breadcrumb, SkeletonCard } from '../../components/ui';
import { tenantSettingsApi } from '../../lib/tenantSettings/api';
import { tenantPreferencesApi } from '../../lib/tenantPreferences/api';
import { displayPhone } from '../../lib/phone/formatter';
import { clientesApi } from '../../lib/clientes/api';
import { equipmentFieldTemplatesApi } from '../../lib/equipmentFields/api';
import type { EquipmentFieldTemplate } from '../../lib/equipmentFields/types';
import { reparacoesApi } from '../../lib/reparacoes/api';
import type { ReparacaoVendaOrigem } from '../../lib/reparacoes/types';
import { toast } from '../../lib/toast';
import {
  PAYMENT_STATUS,
  PRIMARY_STATUSES,
  STATUS_COLOR,
  STATUS_LABEL,
  VALID_TRANSITIONS,
  type RepairStatus,
} from '../../lib/reparacoes/types';
import { formatCents, formatDate, parseEuros } from '../../lib/money';

export default function ReparacaoDetalhe() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const detail = useQuery({
    queryKey: ['reparacao', id],
    queryFn: () => reparacoesApi.get(id!),
    enabled: !!id,
  });

  const tenant = useQuery({
    queryKey: ['tenant-settings'],
    queryFn: () => tenantSettingsApi.getMine(),
    staleTime: 5 * 60_000,
  });

  const billing = useQuery({
    queryKey: ['tenant-billing-settings'],
    queryFn: () => tenantSettingsApi.getBilling(),
    staleTime: 5 * 60_000,
  });

  const preferences = useQuery({
    queryKey: ['tenant-preferences'],
    queryFn: () => tenantPreferencesApi.get(),
    staleTime: 60_000,
  });

  const fieldTemplates = useQuery({
    queryKey: ['equipment-field-templates-active'],
    queryFn: () => equipmentFieldTemplatesApi.active(),
    enabled: !!id,
    staleTime: 60_000,
  });

  const [equipamento, setEquipamento] = useState('');
  const [avaria, setAvaria] = useState('');
  const [imei, setImei] = useState('');
  const [diagnostico, setDiagnostico] = useState('');
  const [precoFinal, setPrecoFinal] = useState('');
  const [pago, setPago] = useState(false);
  const [notas, setNotas] = useState('');
  const [historicoModal, setHistoricoModal] = useState(false);
  const [estadoNotas, setEstadoNotas] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [changeClienteOpen, setChangeClienteOpen] = useState(false);
  const [pagamentoPrompt, setPagamentoPrompt] = useState<RepairStatus | null>(null);
  const [savedAt, setSavedAt] = useState<Date | null>(null);
  // Sprint 140: modal para escolher Simplificada vs Com NIF antes de emitir fatura.
  const [emitFaturaOpen, setEmitFaturaOpen] = useState(false);
  const [emitTipo, setEmitTipo] = useState<'simplificada' | 'com-nif'>('simplificada');
  const [emitNif, setEmitNif] = useState('');
  const [emitLookup, setEmitLookup] = useState<import('../../lib/clientes/types').AtNifLookup | null>(null);
  const [emitLookupErr, setEmitLookupErr] = useState<string | null>(null);
  const [emitLookupPending, setEmitLookupPending] = useState(false);
  // Sprint 141: modal rápido para adicionar telefone ao cliente quando ainda não tem.
  const [telefoneOpen, setTelefoneOpen] = useState(false);
  const [telefoneInput, setTelefoneInput] = useState('');
  const [telefonePending, setTelefonePending] = useState(false);
  const [fieldTemplateId, setFieldTemplateId] = useState<string | null>(null);
  const [fieldValues, setFieldValues] = useState<EquipmentFieldValuesMap>({});
  const hydratedRef = useRef(false);

  useEffect(() => {
    if (!detail.data) return;
    const r = detail.data.reparacao;
    setEquipamento(r.equipamento);
    setAvaria(r.avaria);
    setImei(r.imei ?? '');
    setDiagnostico(r.diagnostico ?? '');
    setPrecoFinal(r.precoFinalCents != null ? (r.precoFinalCents / 100).toFixed(2) : '');
    setPago(r.estadoPagamento === PAYMENT_STATUS.Pago);
    setNotas(r.notas ?? '');
    setFieldTemplateId(r.equipmentFieldTemplateId ?? null);
    setFieldValues(Object.fromEntries((r.fields ?? []).map((field) => [field.fieldDefinitionId, field.value ?? ''])));
    hydratedRef.current = true;
  }, [detail.data]);

  const activeFieldTemplate = fieldTemplateId
    ? fieldTemplates.data?.find((template) => template.id === fieldTemplateId) ?? null
    : null;
  const archivedFieldTemplate: EquipmentFieldTemplate | null =
    !activeFieldTemplate && fieldTemplateId && detail.data?.reparacao.fields.length
      ? {
          id: fieldTemplateId,
          nome: detail.data.reparacao.equipmentFieldTemplateNome ?? 'Template arquivado',
          categoria: 99,
          isActive: false,
          ordem: 999,
          fields: detail.data.reparacao.fields.map((field) => ({
            id: field.fieldDefinitionId,
            label: field.label,
            type: field.type,
            options: [],
            required: field.required,
            ordem: field.ordem,
            visibleInPortal: field.visibleInPortal,
          })),
        }
      : null;
  const selectedFieldTemplate = activeFieldTemplate ?? archivedFieldTemplate;
  const requiredMissing = missingRequiredEquipmentFields(selectedFieldTemplate, fieldValues);

  const update = useMutation({
    mutationFn: () => {
      const r = detail.data!.reparacao;
      const imeiClean = imei.replace(/\D/g, '');
      return reparacoesApi.update(r.id, {
        equipamento: equipamento.trim(),
        avaria: avaria.trim(),
        imei: imeiClean || null,
        diagnostico: diagnostico.trim() || null,
        orcamentoCents: r.orcamentoCents,
        orcamentoAprovado: true,
        precoFinalCents: parseEuros(precoFinal),
        custoPecasCents: r.custoPecasCents,
        horasGastas: 0,
        notas: notas.trim() || null,
        estadoPagamento: pago ? PAYMENT_STATUS.Pago : PAYMENT_STATUS.NaoPago,
      });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reparacao', id] });
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      setSavedAt(new Date());
      setError(null);
    },
    onError: (err) => {
      if (isAxiosError(err)) {
        setError((err.response?.data as { detail?: string } | undefined)?.detail ?? 'Erro ao guardar.');
      }
    },
  });

  const saveEquipmentFields = useMutation({
    mutationFn: () => {
      const r = detail.data!.reparacao;
      return reparacoesApi.setFields(
        r.id,
        selectedFieldTemplate?.id ?? null,
        selectedFieldTemplate ? buildEquipmentFieldValues(selectedFieldTemplate, fieldValues) : [],
      );
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reparacao', id] });
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      qc.invalidateQueries({ queryKey: ['public-repair'] });
      setSavedAt(new Date());
      setError(null);
    },
    onError: (err) => {
      if (isAxiosError(err)) {
        setError((err.response?.data as { detail?: string } | undefined)?.detail ?? 'Erro ao guardar campos personalizados.');
      }
    },
  });

  // Auto-save com debounce de 1.2s sempre que algum campo editável muda
  useEffect(() => {
    if (!hydratedRef.current || !detail.data) return;
    const r = detail.data.reparacao;
    // Frozen (Entregue): só permite alterar pagamento ou reabrir, não auto-save de outros campos
    if (r.estado === 5) {
      // ...mas se só o pagamento mudou, permite gravar
      const wantsPago = pago ? PAYMENT_STATUS.Pago : PAYMENT_STATUS.NaoPago;
      if (wantsPago !== r.estadoPagamento) {
        const t = setTimeout(() => update.mutate(), 300);
        return () => clearTimeout(t);
      }
      return;
    }

    // Skip se nada mudou em relação ao server
    const samePreco = parseEuros(precoFinal) === r.precoFinalCents;
    const wantsPago = pago ? PAYMENT_STATUS.Pago : PAYMENT_STATUS.NaoPago;
    const samePagamento = wantsPago === r.estadoPagamento;
    const imeiClean = imei.replace(/\D/g, '') || null;
    const sameImei = imeiClean === (r.imei ?? null);
    const same =
      equipamento.trim() === r.equipamento &&
      avaria.trim() === r.avaria &&
      sameImei &&
      (diagnostico.trim() || null) === (r.diagnostico ?? null) &&
      (notas.trim() || null) === (r.notas ?? null) &&
      samePreco &&
      samePagamento;
    if (same) return;

    const t = setTimeout(() => update.mutate(), 1200);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [equipamento, avaria, imei, diagnostico, notas, precoFinal, pago, detail.data]);

  // Histórico por IMEI dentro do detalhe (exclui esta reparação)
  const imeiClean = imei.replace(/\D/g, '');
  const historicoImei = useQuery({
    queryKey: ['historico-imei', imeiClean, id],
    queryFn: () => reparacoesApi.historicoImei(imeiClean, id),
    enabled: !!id && imeiClean.length >= 6,
    staleTime: 30_000,
  });

  const changeEstado = useMutation({
    mutationFn: (estado: RepairStatus) => reparacoesApi.changeEstado(id!, estado, estadoNotas || undefined),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reparacao', id] });
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      qc.invalidateQueries({ queryKey: ['dashboard-alertas'] });
      qc.invalidateQueries({ queryKey: ['dashboard-financeiro'] });
      setEstadoNotas('');
      setError(null);
    },
    onError: (err) => {
      if (isAxiosError(err)) {
        setError((err.response?.data as { detail?: string } | undefined)?.detail ?? 'Não foi possível mudar de estado.');
      }
    },
  });

  function tryChangeEstado(target: RepairStatus) {
    const r = detail.data?.reparacao;
    const entregarMarcaPago = preferences.data?.repairs.entregarMarcaPago ?? 0;
    // Ao Entregar (estado 5) se ainda não está pago, perguntar primeiro
    if (target === 5 && r && r.estadoPagamento === PAYMENT_STATUS.NaoPago && entregarMarcaPago === 1) {
      setPagamentoPrompt(target);
      return;
    }
    changeEstado.mutate(target);
  }

  function confirmarEntregaComPagamento(pagamento: 0 | 1 | 2) {
    const r = detail.data?.reparacao;
    if (!r) return;
    setPago(pagamento === PAYMENT_STATUS.Pago);
    // Update do pagamento via PUT — depois encadeia a mudança de estado
    reparacoesApi
      .update(r.id, {
        equipamento: r.equipamento,
        avaria: r.avaria,
        imei: r.imei,
        diagnostico: r.diagnostico,
        orcamentoCents: r.orcamentoCents,
        orcamentoAprovado: true,
        precoFinalCents: r.precoFinalCents,
        custoPecasCents: r.custoPecasCents,
        horasGastas: 0,
        notas: r.notas,
        estadoPagamento: pagamento,
      })
      .then(() => {
        changeEstado.mutate(5);
        setPagamentoPrompt(null);
      })
      .catch((err) => {
        if (isAxiosError(err)) {
          setError((err.response?.data as { detail?: string } | undefined)?.detail ?? 'Erro ao atualizar pagamento.');
        }
        setPagamentoPrompt(null);
      });
  }

  function handleFieldTemplateChange(templateId: string) {
    const nextTemplate = fieldTemplates.data?.find((template) => template.id === templateId) ?? null;
    setFieldTemplateId(nextTemplate?.id ?? null);
    setFieldValues(nextTemplate ? initEquipmentFieldValues(nextTemplate, fieldValues) : {});
  }

  const changeCliente = useMutation({
    mutationFn: (newClienteId: string) => {
      const r = detail.data!.reparacao;
      return reparacoesApi.update(r.id, {
        clienteId: newClienteId,
        equipamento: r.equipamento,
        avaria: r.avaria,
        imei: r.imei,
        diagnostico: r.diagnostico,
        orcamentoCents: r.orcamentoCents,
        orcamentoAprovado: true,
        precoFinalCents: r.precoFinalCents,
        custoPecasCents: r.custoPecasCents,
        horasGastas: 0,
        notas: r.notas,
        estadoPagamento: r.estadoPagamento,
      });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reparacao', id] });
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      setChangeClienteOpen(false);
    },
  });

  const remove = useMutation({
    mutationFn: () => reparacoesApi.remove(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      navigate('/reparacoes');
    },
  });

  const emitirFatura = useMutation({
    mutationFn: () => reparacoesApi.emitirFatura(id!),
    onSuccess: (invoice) => {
      qc.invalidateQueries({ queryKey: ['reparacao', id] });
      toast.success(`Fatura ${invoice.number} emitida`, invoice.pdfUrl ? 'PDF disponível na ficha.' : undefined);
    },
    onError: (err) => toast.fromError(err, 'Não foi possível emitir a fatura.'),
  });

  const anularFatura = useMutation({
    mutationFn: () => reparacoesApi.anularFatura(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reparacao', id] });
      qc.invalidateQueries({ queryKey: ['reparacoes-pagas-sem-fatura'] });
      toast.success('Fatura anulada', 'Nota de Crédito emitida no Moloni. A AT actualiza saldo IVA para zero.');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível anular a fatura.'),
  });

  const emitirOrcamentoMoloni = useMutation({
    mutationFn: () => reparacoesApi.emitirOrcamentoMoloni(id!),
    onSuccess: (updated) => {
      qc.invalidateQueries({ queryKey: ['reparacao', id] });
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      toast.success(`Orcamento ${updated.estimateNumber ?? updated.estimateExternalId} emitido`, updated.estimatePdfUrl ? 'PDF Moloni disponivel na ficha.' : undefined);
    },
    onError: (err) => toast.fromError(err, 'Nao foi possivel emitir o orcamento Moloni.'),
  });

  // Sprint 143: re-emitir orçamento Moloni quando preço mudou (best-effort cancel velho).
  const reemitirOrcamentoMoloni = useMutation({
    mutationFn: () => reparacoesApi.reemitirOrcamentoMoloni(id!),
    onSuccess: (updated) => {
      qc.invalidateQueries({ queryKey: ['reparacao', id] });
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      toast.success(`Orçamento ${updated.estimateNumber} re-emitido`, 'O velho foi anulado no Moloni.');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível re-emitir o orçamento.'),
  });

  // Reabrir: volta para estado Pronto + desmarca Pago via endpoint dedicado.
  const reabrir = useMutation({
    mutationFn: () => reparacoesApi.reabrir(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reparacao', id] });
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
    },
    onError: (err) => {
      if (isAxiosError(err)) {
        setError((err.response?.data as { detail?: string } | undefined)?.detail ?? 'Não foi possível reabrir.');
      }
    },
  });

  if (detail.isLoading) {
    return (
      <div className="space-y-4">
        <SkeletonCard />
        <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <SkeletonCard />
          <SkeletonCard />
          <SkeletonCard />
          <SkeletonCard />
        </section>
        <SkeletonCard />
        <SkeletonCard />
      </div>
    );
  }
  if (detail.isError || !detail.data) return <div className="text-sm text-red-600">Não encontrado.</div>;

  const r = detail.data.reparacao;
  const possibleNext = VALID_TRANSITIONS[r.estado];
  const receitaCents = parseEuros(precoFinal) ?? 0;
  const lucroBrutoLive = receitaCents - r.custoPecasCents - r.custoDespesasCents;
  // Sprint 138: IVA líquido a entregar à AT (só em Regime Normal IVA).
  // O custo das peças/despesas já inclui IVA pago a fornecedores (dedutível);
  // a receita já inclui IVA cobrado ao cliente. Líquido = cobrado − dedutível.
  // RegimeFiscal: 0=IsentoArt53 (sem IVA), 1=Normal (23%), 2=Simplificado.
  const ivaRate = tenant.data?.regimeFiscal === 1 ? 23 : 0;
  const ivaDivisor = 1 + ivaRate / 100;
  const ivaCobradoCents = ivaRate > 0 ? Math.round(receitaCents - receitaCents / ivaDivisor) : 0;
  const ivaDeducivelCents = ivaRate > 0
    ? Math.round((r.custoPecasCents + r.custoDespesasCents) - (r.custoPecasCents + r.custoDespesasCents) / ivaDivisor)
    : 0;
  const ivaAEntregarCents = Math.max(0, ivaCobradoCents - ivaDeducivelCents);
  const lucroLiquidoLive = lucroBrutoLive - ivaAEntregarCents;
  const cleanPhone = r.cliente.telefone?.replace(/\s/g, '') ?? '';
  const showPagamento = r.estado === 4 || r.estado === 5; // Pronto ou Entregue
  // 3 tiers de bloqueio:
  //  - Aberto: tudo editável
  //  - Frozen (Entregue, ainda NãoPago): campos congelados; só pode marcar Pago ou Reabrir
  //  - Locked (Entregue + Pago): encerrado; só Reabrir desbloqueia
  const isFrozen = r.estado === 5;
  const isLocked = isFrozen && r.estadoPagamento === PAYMENT_STATUS.Pago;
  const canEmitMoloniInvoice = r.estadoPagamento === PAYMENT_STATUS.Pago
    && billing.data?.provider === 1
    && !r.invoiceExternalId;
  const canEmitMoloniEstimate = billing.data?.provider === 1 && !r.estimateExternalId;

  // Dias parado no estado actual (usado para escolher template WhatsApp p.ex. "Lembrete levantamento" se >7d em Pronto)
  const staleDays = Math.floor((Date.now() - new Date(r.estadoSince).getTime()) / 86_400_000);
  const valorParaCobrar = r.precoFinalCents ?? r.orcamentoCents ?? null;
  const waVars = {
    cliente_nome: r.cliente.nome?.split(' ')[0] ?? 'olá',
    equipamento: r.equipamento,
    loja_nome: tenant.data?.name ?? undefined,
    numero_reparacao: r.numero,
    valor: valorParaCobrar != null ? formatCents(valorParaCobrar) : undefined,
    link_review_google: tenant.data?.googleReviewUrl ?? undefined,
    data_pronto: r.estado === 4 ? formatDate(r.estadoSince) : undefined,
  };

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 text-sm sm:flex-row sm:items-center sm:justify-between">
        <Breadcrumb
          items={[
            { label: 'Reparações', to: '/reparacoes' },
            { label: `#${r.numero} · ${r.equipamento}` },
          ]}
        />
        <div className="flex flex-wrap items-center gap-2">
          {isFrozen && (
            <button
              type="button"
              disabled={reabrir.isPending}
              onClick={() => reabrir.mutate()}
              className="min-h-10 rounded-md bg-amber-100 px-3 py-2 text-xs font-medium text-amber-900 hover:bg-amber-200 disabled:opacity-60 dark:bg-amber-950/40 dark:text-amber-300"
            >
              {reabrir.isPending ? 'A reabrir…' : '🔓 Reabrir'}
            </button>
          )}
          <button
            type="button"
            onClick={() => setConfirmDelete(true)}
            className="min-h-10 rounded-md px-3 py-2 text-xs text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40"
          >
            Apagar
          </button>
        </div>
      </div>

      {isLocked && (
        <div className="flex items-start gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300">
          <Lock size={15} strokeWidth={2} className="mt-0.5 flex-none" />
          <span>Encerrada — entregue e paga. Campos bloqueados para preservar histórico. Clica em "Reabrir" se precisares de corrigir algo.</span>
        </div>
      )}
      {isFrozen && !isLocked && (
        <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300">
          <Snowflake size={15} strokeWidth={2} className="mt-0.5 flex-none" />
          <span>Entregue mas ainda não paga. Para evitar erros, só podes marcar como Pago ou Reabrir. Para alterar mais alguma coisa, clica "Reabrir" no topo.</span>
        </div>
      )}

      <header className="space-y-2">
        <div className="flex items-center gap-2">
          <span className="text-xs font-mono text-zinc-500">#{r.numero}</span>
          <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${STATUS_COLOR[r.estado]}`}>
            {STATUS_LABEL[r.estado]}
          </span>
        </div>
        <h1 className="text-2xl font-semibold tracking-tight">{r.equipamento}</h1>
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <Link to={`/clientes/${r.cliente.id}`} className="text-zinc-700 hover:underline dark:text-zinc-300">
            {r.cliente.nome}
          </Link>
          {!isFrozen && (
            <button
              type="button"
              onClick={() => setChangeClienteOpen(true)}
              className="text-xs text-zinc-500 hover:underline"
            >
              (trocar)
            </button>
          )}
          {r.cliente.nif ? (
            <span className="rounded-md bg-zinc-100 px-1.5 py-0.5 text-[11px] font-mono text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">
              NIF {r.cliente.nif}
            </span>
          ) : (
            <Link
              to={`/clientes/${r.cliente.id}`}
              className="rounded-md bg-amber-100 px-1.5 py-0.5 text-[11px] font-medium text-amber-800 hover:bg-amber-200 dark:bg-amber-950/40 dark:text-amber-300"
              title="Cliente sem NIF — fatura sairá como Simplificada (válida até €1000). Clica para adicionar NIF."
            >
              Sem NIF · Simplificada
            </Link>
          )}
        </div>
        {/* Sprint 141: se cliente tem telefone, mostra Ligar+WhatsApp; senão mostra botão para adicionar. */}
        {cleanPhone ? (
          <div className="flex gap-2">
            <a
              href={`tel:${cleanPhone}`}
              className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-medium text-white transition hover:bg-emerald-700"
            >
              <Phone size={12} strokeWidth={2} /> Ligar
            </a>
            <WhatsAppMenu phone={cleanPhone} vars={waVars} estado={r.estado} staleDays={staleDays} entityId={r.id} />
            <span className="self-center text-xs text-zinc-500">{displayPhone(r.cliente.telefone)}</span>
          </div>
        ) : (
          <button
            type="button"
            onClick={() => { setTelefoneInput(''); setTelefoneOpen(true); }}
            className="inline-flex items-center gap-1 rounded-lg border border-dashed border-emerald-400 bg-emerald-50/50 px-3 py-1.5 text-xs font-medium text-emerald-700 hover:bg-emerald-50 dark:border-emerald-800/60 dark:bg-emerald-950/30 dark:text-emerald-300"
            title="Adicionar telefone — desbloqueia botões Ligar e WhatsApp"
          >
            <Phone size={12} strokeWidth={2} /> + Telefone
          </button>
        )}
        <p className="text-xs text-zinc-500">recebido {formatDate(r.recebidoEm)}</p>
        {/* Sprint 229: copiar link público para cliente (partilhar via WhatsApp/SMS). */}
        {r.publicSlug && (
          <button
            type="button"
            onClick={async () => {
              const url = `${window.location.origin}/r/${r.publicSlug}`;
              try {
                await navigator.clipboard.writeText(url);
                toast.success('Link copiado!', url);
              } catch {
                toast.fromError(new Error(url), 'Copia manualmente');
              }
            }}
            className="inline-flex items-center gap-1.5 rounded-lg border border-brand-300 bg-brand-50/50 px-3 py-1.5 text-xs font-medium text-brand-700 hover:bg-brand-50 dark:border-brand-800/60 dark:bg-brand-950/30 dark:text-brand-300"
            title="Copiar link público para enviar ao cliente"
          >
            📋 Copiar link cliente
          </button>
        )}
        <div className="flex flex-wrap gap-2 pt-1">
          {/* Sprint 141: o orçamento informativo do Mender foi descontinuado.
              Usa-se Moloni como fonte de orçamento oficial (botão "Emitir Orçamento Moloni" /
              "Orcamento OR ..." abaixo). O endpoint /orcamento.pdf permanece para retro-compat
              mas não está exposto na UI. */}
          {r.estimateExternalId ? (
            <>
              <a
                href={r.estimatePdfUrl ?? '#'}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1 rounded-lg border border-blue-300 bg-blue-50 px-3 py-1.5 text-xs font-medium text-blue-800 hover:bg-blue-100 dark:border-blue-800/60 dark:bg-blue-950/30 dark:text-blue-200"
              >
                Orcamento {r.estimateNumber ?? r.estimateExternalId}
              </a>
              {/* Sprint 143: botão "Re-emitir orçamento" — quando o preço mudou desde a primeira emissão.
                  Anula o velho no Moloni (best-effort) + emite um novo com o preço actual. */}
              {!r.invoiceExternalId && (
                <button
                  type="button"
                  disabled={reemitirOrcamentoMoloni.isPending}
                  onClick={() => {
                    const ok = confirm(
                      'Re-emitir orçamento Moloni com o preço actual?\n\n' +
                      `O orçamento ${r.estimateNumber ?? r.estimateExternalId} vai ser anulado no Moloni ` +
                      `e um novo é emitido com o valor de ${formatCents(r.precoFinalCents ?? r.orcamentoCents ?? 0)}.\n\n` +
                      'Continuar?'
                    );
                    if (ok) reemitirOrcamentoMoloni.mutate();
                  }}
                  className="inline-flex items-center gap-1 rounded-lg border border-blue-300 bg-white px-3 py-1.5 text-xs font-medium text-blue-700 hover:bg-blue-50 disabled:opacity-60 dark:border-blue-800/60 dark:bg-zinc-900 dark:text-blue-200"
                  title="Anula o orçamento Moloni actual e emite um novo com o preço actualizado. Útil quando mudaste o preço final ou adicionaste peças depois do orçamento."
                >
                  {reemitirOrcamentoMoloni.isPending ? 'A re-emitir...' : 'Re-emitir orçamento'}
                </button>
              )}
              {/* Sprint 143: "Converter em Fatura" removido — Moloni não tem endpoint API para
                  esta operação (retornava 404). Em vez disso, o botão "Emitir fatura via Moloni"
                  (no header de pagamento) cria uma fatura nova directamente com os items do Sprint 136. */}
            </>
          ) : canEmitMoloniEstimate && (
            <button
              type="button"
              disabled={emitirOrcamentoMoloni.isPending}
              onClick={() => {
                const valor = r.orcamentoCents ?? r.precoFinalCents ?? 0;
                const ok = confirm(
                  (billing.data?.sandboxMode
                    ? 'MODO SANDBOX - orcamento Moloni de teste\n\n'
                    : 'ATENCAO: Vai emitir um orcamento Moloni certificado\n\n') +
                  `Reparacao #${r.numero} - ${r.equipamento}\n` +
                  `Cliente: ${r.cliente.nome}\n` +
                  `Total: ${formatCents(valor)}\n\nContinuar?`
                );
                if (ok) emitirOrcamentoMoloni.mutate();
              }}
              className="inline-flex items-center gap-1 rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-60"
            >
              {emitirOrcamentoMoloni.isPending ? 'A emitir...' : 'Emitir Orcamento Moloni'}
            </button>
          )}
          {r.invoiceExternalId ? (
            <>
              <a
                href={r.invoicePdfUrl ?? '#'}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1 rounded-lg border border-emerald-300 bg-emerald-50 px-3 py-1.5 text-xs font-medium text-emerald-800 hover:bg-emerald-100 dark:border-emerald-800/60 dark:bg-emerald-950/30 dark:text-emerald-200"
              >
                Fatura {r.invoiceNumber ?? r.invoiceExternalId}
              </a>
              <button
                type="button"
                disabled={anularFatura.isPending}
                onClick={() => {
                  const ok = confirm(
                    'ATENÇÃO: Vai emitir Nota de Crédito Moloni para ANULAR a fatura ' +
                    `${r.invoiceNumber ?? r.invoiceExternalId}.\n\n` +
                    'A AT vai actualizar o saldo IVA para zero.\n\n' +
                    'Continuar?'
                  );
                  if (ok) anularFatura.mutate();
                }}
                className="inline-flex items-center gap-1 rounded-lg border border-red-300 bg-red-50 px-3 py-1.5 text-xs font-medium text-red-700 hover:bg-red-100 disabled:opacity-60 dark:border-red-900/40 dark:bg-red-950/30 dark:text-red-300"
                title="Emite NC Moloni que anula esta fatura — útil se emitiste por engano"
              >
                {anularFatura.isPending ? 'A anular…' : 'Anular fatura (NC)'}
              </button>
            </>
          ) : canEmitMoloniInvoice && (
            <button
              type="button"
              disabled={emitirFatura.isPending}
              onClick={() => {
                // Sprint 140: abre modal de escolha Simplificada vs Com NIF.
                setEmitTipo(r.cliente.nif ? 'com-nif' : 'simplificada');
                setEmitNif(r.cliente.nif ?? '');
                setEmitLookup(null);
                setEmitLookupErr(null);
                setEmitFaturaOpen(true);
              }}
              className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-emerald-700 disabled:opacity-60"
            >
              {emitirFatura.isPending ? 'A emitir…' : 'Emitir fatura via Moloni'}
            </button>
          )}
          {/* Sprint 143: removido botão "Emitir factura no Portal AT" — Bruno pediu (usa Moloni). */}
        </div>
      </header>

      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">{error}</div>}

      {detail.data?.vendaOrigem && (
        <VendaOrigemBanner
          venda={detail.data.vendaOrigem}
          reparacaoId={detail.data.reparacao.id}
          jaMarcadaGarantia={detail.data.reparacao.orcamentoCents === 0}
          notasAtuais={detail.data.reparacao.notas ?? ''}
        />
      )}

      <WorkflowStepper
        estado={r.estado}
        possibleNext={isFrozen ? [] : possibleNext}
        notas={estadoNotas}
        onNotasChange={setEstadoNotas}
        onChange={tryChangeEstado}
        pending={changeEstado.isPending}
      />

      {/* Garantia digital — só visível quando reparação Entregue */}
      {r.estado === 5 && <GarantiaCard kind="reparacao" reparacaoId={r.id} />}

      <Modal
        open={pagamentoPrompt !== null}
        title="Foi pago?"
        onClose={() => setPagamentoPrompt(null)}
      >
        <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-400">
          Antes de marcar como <strong>Entregue</strong>, regista o estado do pagamento.
          Esta resposta entra no Lucro Realizado do dashboard.
        </p>
        <div className="grid grid-cols-3 gap-2">
          <button
            type="button"
            onClick={() => confirmarEntregaComPagamento(PAYMENT_STATUS.Pago)}
            className="rounded-lg border border-emerald-300 bg-emerald-50 px-3 py-3 text-sm font-medium text-emerald-800 transition hover:bg-emerald-100 dark:border-emerald-800/60 dark:bg-emerald-950/30 dark:text-emerald-200 dark:hover:bg-emerald-950/50"
          >
            ✓ Pago
          </button>
          <button
            type="button"
            onClick={() => confirmarEntregaComPagamento(PAYMENT_STATUS.PagoParcial)}
            className="rounded-lg border border-amber-300 bg-amber-50 px-3 py-3 text-sm font-medium text-amber-800 transition hover:bg-amber-100 dark:border-amber-800/60 dark:bg-amber-950/30 dark:text-amber-200 dark:hover:bg-amber-950/50"
          >
            ◐ Parcial
          </button>
          <button
            type="button"
            onClick={() => confirmarEntregaComPagamento(PAYMENT_STATUS.NaoPago)}
            className="rounded-lg border border-zinc-300 bg-white px-3 py-3 text-sm font-medium text-zinc-700 transition hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-300 dark:hover:bg-zinc-900"
          >
            ✕ Ainda não
          </button>
        </div>
        <p className="mt-3 text-xs text-zinc-500">
          Podes sempre actualizar depois no campo "Preço final" da reparação.
        </p>
      </Modal>


      <section className="space-y-3 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <h2 className="text-sm font-semibold">Detalhes</h2>
        <Field label="Equipamento">
          <input disabled={isFrozen} value={equipamento} onChange={(e) => setEquipamento(e.target.value)} className={inputCls} />
        </Field>
        <Field label="Avaria reportada">
          <textarea
            disabled={isFrozen}
            rows={2}
            value={avaria}
            onChange={(e) => setAvaria(e.target.value)}
            className={inputCls + ' resize-none'}
          />
        </Field>
        <Field label="IMEI / Serial">
          <input
            disabled={isFrozen}
            value={imei}
            onChange={(e) => setImei(e.target.value)}
            inputMode="numeric"
            placeholder="ex: 359123456789012"
            className={inputCls + ' font-mono'}
          />
          {imeiClean.length >= 6 && historicoImei.data && (
            <div className="mt-1 flex flex-wrap items-center gap-2 text-xs">
              {historicoImei.data.luhnValido && <span className="text-emerald-700 dark:text-emerald-400">✓ Luhn válido</span>}
              {!historicoImei.data.luhnValido && imeiClean.length === 15 && (
                <span className="inline-flex items-center gap-0.5 text-amber-700 dark:text-amber-400">
                  <AlertTriangle size={11} strokeWidth={2} /> Luhn inválido
                </span>
              )}
              {historicoImei.data.total > 0 && (
                <button
                  type="button"
                  onClick={() => setHistoricoModal(true)}
                  className="rounded-full border border-amber-300 bg-amber-50 px-2 py-0.5 text-amber-800 hover:bg-amber-100 dark:border-amber-800/60 dark:bg-amber-950/30 dark:text-amber-200 dark:hover:bg-amber-950/50"
                >
                  📚 {historicoImei.data.total} {historicoImei.data.total === 1 ? 'reparação anterior' : 'reparações anteriores'} com este IMEI →
                </button>
              )}
            </div>
          )}
        </Field>
        <Field label="Diagnóstico técnico">
          <textarea
            disabled={isFrozen}
            rows={3}
            value={diagnostico}
            onChange={(e) => setDiagnostico(e.target.value)}
            className={inputCls + ' resize-none'}
            placeholder="Conclusão depois de inspecionar…"
          />
        </Field>
        <Field label="Notas internas">
          <textarea
            disabled={isFrozen}
            rows={2}
            value={notas}
            onChange={(e) => setNotas(e.target.value)}
            className={inputCls + ' resize-none'}
            placeholder="Lembretes, contactos com cliente, etc."
          />
        </Field>
      </section>

      {/* Sprint 142: collapse por defeito — campos personalizados são raros (só laptops/desktops/IT).
          Para telemóveis (95% do trabalho da LopesTech) é só ruído. Abre se já tem template aplicado. */}
      <details className="space-y-3 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900" open={!!fieldTemplateId}>
        <summary className="cursor-pointer list-none">
          <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h2 className="text-sm font-semibold">Campos personalizados {fieldTemplateId && <span className="ml-1 rounded bg-brand-100 px-1.5 py-0.5 text-[10px] font-medium text-brand-700 dark:bg-brand-900/30 dark:text-brand-300">activo</span>}</h2>
              <p className="text-xs text-zinc-500">Para laptops, desktops, IT repair. Clica para abrir/fechar.</p>
            </div>
            <button
              type="button"
              disabled={isFrozen || requiredMissing || saveEquipmentFields.isPending}
              onClick={(e) => { e.preventDefault(); saveEquipmentFields.mutate(); }}
              className="rounded-md bg-brand-600 px-3 py-1.5 text-xs font-medium text-white transition hover:bg-brand-700 disabled:opacity-60"
            >
              {saveEquipmentFields.isPending ? 'A guardar...' : 'Guardar campos'}
            </button>
          </div>
        </summary>
        <Field label="Template">
          <select
            disabled={isFrozen}
            value={fieldTemplateId ?? ''}
            onChange={(e) => handleFieldTemplateChange(e.target.value)}
            className={inputCls}
          >
            <option value="">Sem template</option>
            {archivedFieldTemplate && (
              <option value={archivedFieldTemplate.id}>{archivedFieldTemplate.nome} (arquivado)</option>
            )}
            {fieldTemplates.data?.map((template) => (
              <option key={template.id} value={template.id}>{template.nome}</option>
            ))}
          </select>
        </Field>
        <EquipmentFieldsForm
          template={selectedFieldTemplate}
          values={fieldValues}
          disabled={isFrozen}
          onChange={(fieldId, value) => setFieldValues((current) => ({ ...current, [fieldId]: value }))}
        />
        {requiredMissing && (
          <p className="text-xs text-red-600 dark:text-red-400">Preenche os campos obrigatorios antes de guardar.</p>
        )}
      </details>

      <DiagnosticoGuiado reparacaoId={r.id} readOnly={isFrozen} />

      <FotosReparacao reparacaoId={r.id} readOnly={isFrozen} />

      {/* Sprint 198: usar isLocked (Entregue+Pago) em vez de isFrozen (só Entregue) —
          Bruno reportou: conseguia eliminar peças mesmo após reparação fechada. */}
      <PecasReparacao reparacaoId={r.id} readOnly={isLocked} />


      <section className="space-y-3 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <h2 className="text-sm font-semibold">Preço & lucro</h2>
        <Field label="Preço final ao cliente (€)">
          <input
            disabled={isFrozen}
            inputMode="decimal"
            value={precoFinal}
            onChange={(e) => setPrecoFinal(e.target.value)}
            placeholder="0,00"
            className={inputCls}
          />
        </Field>
        <div className="rounded-lg bg-zinc-50 p-3 text-sm dark:bg-zinc-950 space-y-1">
          <div className="flex justify-between">
            <span className="text-zinc-500">Receita:</span>
            <span>{formatCents(receitaCents)}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-zinc-500">Peças em stock:</span>
            <span>−{formatCents(r.custoPecasCents)}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-zinc-500">Outras despesas:</span>
            <span>−{formatCents(r.custoDespesasCents)}</span>
          </div>
          <div className={`flex justify-between ${ivaRate > 0 ? '' : 'border-t border-zinc-200 pt-1 font-semibold dark:border-zinc-800'}`}>
            <span className={ivaRate > 0 ? 'text-zinc-500' : ''}>{ivaRate > 0 ? 'Lucro bruto:' : 'Lucro:'}</span>
            <span className={ivaRate > 0
              ? ''
              : (lucroBrutoLive >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-700 dark:text-red-400')}>
              {formatCents(lucroBrutoLive)}
            </span>
          </div>
          {/* Sprint 138: discriminação IVA só em Regime Normal. Em Isento Art. 53 o "Lucro" acima já é o líquido. */}
          {ivaRate > 0 && (
            <>
              <div className="mt-2 flex justify-between border-t border-zinc-200 pt-2 text-xs text-zinc-500 dark:border-zinc-800">
                <span>IVA cobrado ao cliente ({ivaRate}%):</span>
                <span>+{formatCents(ivaCobradoCents)}</span>
              </div>
              <div className="flex justify-between text-xs text-zinc-500">
                <span>IVA dedutível (peças + despesas):</span>
                <span>−{formatCents(ivaDeducivelCents)}</span>
              </div>
              <div className="flex justify-between text-xs">
                <span className="text-zinc-500">IVA a entregar à AT:</span>
                <span className="text-amber-700 dark:text-amber-400">−{formatCents(ivaAEntregarCents)}</span>
              </div>
              <div className="flex justify-between border-t border-zinc-200 pt-1 font-semibold dark:border-zinc-800">
                <span>Lucro líquido:</span>
                <span className={lucroLiquidoLive >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-700 dark:text-red-400'}>
                  {formatCents(lucroLiquidoLive)}
                </span>
              </div>
              <p className="pt-1 text-[10px] text-zinc-400">
                IVA líquido entregue trimestralmente (cobrado − dedutível). Lucro real após IVA.
              </p>
            </>
          )}
        </div>
        {showPagamento ? (
          <label className={`flex items-center gap-2 rounded-lg border border-zinc-200 bg-zinc-50 px-3 py-2 text-sm dark:border-zinc-800 dark:bg-zinc-950 ${isLocked ? '' : 'cursor-pointer'}`}>
            <input disabled={isLocked} type="checkbox" checked={pago} onChange={(e) => setPago(e.target.checked)} />
            <span>Pago pelo cliente</span>
          </label>
        ) : (
          <p className="text-xs text-zinc-500">Pagamento marcado quando a reparação estiver Reparada ou Entregue.</p>
        )}
      </section>

      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <h2 className="mb-3 text-sm font-semibold">Timeline</h2>
        <ol className="space-y-3">
          {detail.data.timeline.map((t) => (
            <li key={t.id} className="flex gap-3">
              <div className="mt-1 h-2 w-2 shrink-0 rounded-full bg-brand-500" />
              <div className="flex-1">
                <div className="text-sm">
                  {t.estadoFrom != null && <span className="text-zinc-500">{STATUS_LABEL[t.estadoFrom]} → </span>}
                  <span className="font-medium">{STATUS_LABEL[t.estadoTo]}</span>
                </div>
                {t.notas && <div className="text-xs text-zinc-500">{t.notas}</div>}
                <div className="text-[11px] text-zinc-400">{formatDate(t.mudouEm)}</div>
              </div>
            </li>
          ))}
        </ol>
      </section>

      {!isLocked && (
        <div className="flex items-center justify-end gap-2 text-xs text-zinc-500">
          {update.isPending ? (
            <span>A guardar…</span>
          ) : savedAt ? (
            <span className="text-emerald-600 dark:text-emerald-400">
              ✓ Guardado às {savedAt.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' })}
            </span>
          ) : (
            <span>Alterações guardam automaticamente.</span>
          )}
        </div>
      )}

      <ChangeClienteModal
        open={changeClienteOpen}
        onClose={() => setChangeClienteOpen(false)}
        onPick={(c) => changeCliente.mutate(c)}
      />

      <Modal
        open={historicoModal}
        title={`Histórico do IMEI ${imeiClean}`}
        onClose={() => setHistoricoModal(false)}
      >
        {historicoImei.data && historicoImei.data.items.length > 0 ? (
          <ul className="space-y-2">
            {historicoImei.data.items.map((it) => (
              <li key={it.id}>
                <Link
                  to={`/reparacoes/${it.id}`}
                  onClick={() => setHistoricoModal(false)}
                  className="block rounded-lg border border-zinc-200 p-3 transition hover:border-brand-300 hover:bg-brand-50 dark:border-zinc-800 dark:hover:border-brand-700 dark:hover:bg-zinc-800"
                >
                  <div className="flex items-center justify-between text-sm">
                    <div>
                      <span className="font-mono text-xs text-zinc-500">#{it.numero}</span>{' '}
                      <span className="font-medium">{it.equipamento}</span>
                    </div>
                    <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${STATUS_COLOR[it.estado]}`}>
                      {STATUS_LABEL[it.estado]}
                    </span>
                  </div>
                  <div className="mt-1 text-[11px] text-zinc-500">
                    {it.cliente.nome} · entrou em {new Date(it.recebidoEm).toLocaleDateString('pt-PT')}
                    {it.precoFinalCents != null && <> · {formatCents(it.precoFinalCents)}</>}
                  </div>
                  {it.diagnostico && (
                    <div className="mt-1 flex items-center gap-1 truncate text-xs text-zinc-600 dark:text-zinc-400">
                      <Search size={11} strokeWidth={2} className="flex-none" /> {it.diagnostico}
                    </div>
                  )}
                </Link>
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-sm text-zinc-500">Sem reparações anteriores com este IMEI.</p>
        )}
      </Modal>

      <Modal
        open={confirmDelete}
        title="Apagar reparação"
        onClose={() => setConfirmDelete(false)}
        footer={<>
          <button type="button" onClick={() => setConfirmDelete(false)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button
            type="button"
            disabled={remove.isPending}
            onClick={() => remove.mutate()}
            className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
          >
            {remove.isPending ? 'A apagar…' : 'Apagar'}
          </button>
        </>}
      >
        <p className="text-sm">Apagar a reparação <strong>#{r.numero} {r.equipamento}</strong>? Vai ser ocultada (soft delete) mas pode ser recuperada por mim.</p>
      </Modal>

      {/* Sprint 141: modal rápido para adicionar telefone ao cliente. Desbloqueia Ligar+WhatsApp. */}
      <Modal
        open={telefoneOpen}
        title={`Telefone de ${r.cliente.nome}`}
        onClose={() => setTelefoneOpen(false)}
        footer={<>
          <button type="button" onClick={() => setTelefoneOpen(false)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button
            type="button"
            disabled={telefonePending || telefoneInput.replace(/\D/g, '').length < 9}
            onClick={async () => {
              setTelefonePending(true);
              try {
                const full = await clientesApi.get(r.cliente.id);
                await clientesApi.update(r.cliente.id, {
                  nome: full.nome,
                  telefone: telefoneInput.trim(),
                  email: full.email,
                  nif: full.nif,
                  notas: full.notas,
                });
                qc.invalidateQueries({ queryKey: ['reparacao', id] });
                qc.invalidateQueries({ queryKey: ['cliente', r.cliente.id] });
                toast.success('Telefone guardado', 'Botões Ligar e WhatsApp já disponíveis.');
                setTelefoneOpen(false);
              } catch (err) {
                toast.fromError(err, 'Não foi possível guardar o telefone.');
              } finally {
                setTelefonePending(false);
              }
            }}
            className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
          >
            {telefonePending ? 'A guardar…' : 'Guardar'}
          </button>
        </>}
      >
        <div className="space-y-3 text-sm">
          <p className="text-xs text-zinc-500">
            Sem telefone, não consegues ligar nem enviar mensagens WhatsApp ao cliente.
            Adiciona aqui para desbloquear esses botões.
          </p>
          <input
            inputMode="tel"
            value={telefoneInput}
            onChange={(e) => setTelefoneInput(e.target.value)}
            placeholder="912 345 678"
            autoFocus
            className={inputCls}
          />
        </div>
      </Modal>

      {/* Sprint 140: modal "Emitir fatura" — Simplificada vs Com NIF + AT lookup */}
      <Modal
        open={emitFaturaOpen}
        title="Emitir fatura Moloni"
        onClose={() => setEmitFaturaOpen(false)}
        footer={<>
          <button type="button" onClick={() => setEmitFaturaOpen(false)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button
            type="button"
            disabled={emitirFatura.isPending || (emitTipo === 'com-nif' && emitNif.length !== 9)}
            onClick={async () => {
              try {
                // ClienteResumo na reparação só tem id/nome/telefone/nif — para fazer update
                // preciso de fetch completo (inclui email + notas) antes de PUT.
                const wantsNif = emitTipo === 'com-nif' && emitNif !== (r.cliente.nif ?? '');
                const wantsClear = emitTipo === 'simplificada' && !!r.cliente.nif;
                if (wantsNif || wantsClear) {
                  const full = await clientesApi.get(r.cliente.id);
                  await clientesApi.update(r.cliente.id, {
                    nome: wantsNif && emitLookup?.nome ? emitLookup.nome : full.nome,
                    telefone: full.telefone,
                    email: full.email,
                    nif: wantsClear ? null : emitNif,
                    notas: full.notas,
                  });
                }
                qc.invalidateQueries({ queryKey: ['reparacao', id] });
                qc.invalidateQueries({ queryKey: ['cliente', r.cliente.id] });
                setEmitFaturaOpen(false);
                emitirFatura.mutate();
              } catch (err) {
                toast.fromError(err, 'Não foi possível actualizar o cliente.');
              }
            }}
            className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
          >
            {emitirFatura.isPending ? 'A emitir…' : 'Emitir'}
          </button>
        </>}
      >
        <div className="space-y-4 text-sm">
          <div className="rounded-md border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-950">
            <div className="font-medium">{r.cliente.nome}</div>
            <div className="text-xs text-zinc-500">
              Reparação #{r.numero} · {r.equipamento} · {formatCents(r.precoFinalCents ?? r.orcamentoCents ?? 0)}
            </div>
            {billing.data?.sandboxMode && <div className="mt-1 text-xs text-amber-600">⚠️ MODO SANDBOX — fatura de teste</div>}
          </div>

          <fieldset className="space-y-2">
            <legend className="text-xs font-semibold uppercase text-zinc-500">Tipo de documento</legend>
            <label className="flex cursor-pointer items-start gap-2 rounded-md border border-zinc-200 p-3 has-[:checked]:border-emerald-500 has-[:checked]:bg-emerald-50 dark:border-zinc-800 dark:has-[:checked]:bg-emerald-950/30">
              <input
                type="radio"
                name="emit-tipo"
                checked={emitTipo === 'simplificada'}
                onChange={() => setEmitTipo('simplificada')}
                className="mt-0.5"
              />
              <div>
                <div className="font-medium">Fatura Simplificada (Consumidor Final)</div>
                <div className="text-xs text-zinc-500">Sem NIF. Válida até €1000. O cliente NÃO precisa de identificar-se.</div>
              </div>
            </label>
            <label className="flex cursor-pointer items-start gap-2 rounded-md border border-zinc-200 p-3 has-[:checked]:border-emerald-500 has-[:checked]:bg-emerald-50 dark:border-zinc-800 dark:has-[:checked]:bg-emerald-950/30">
              <input
                type="radio"
                name="emit-tipo"
                checked={emitTipo === 'com-nif'}
                onChange={() => setEmitTipo('com-nif')}
                className="mt-0.5"
              />
              <div className="flex-1">
                <div className="font-medium">Fatura com NIF</div>
                <div className="text-xs text-zinc-500">Cliente quer NIF na fatura (dedução IRS/IVA).</div>
              </div>
            </label>
          </fieldset>

          {emitTipo === 'com-nif' && (
            <div className="space-y-2">
              <label className="block text-xs font-medium text-zinc-500">NIF (9 dígitos)</label>
              <div className="flex gap-2">
                <input
                  inputMode="numeric"
                  maxLength={9}
                  value={emitNif}
                  onChange={(e) => {
                    setEmitNif(e.target.value.replace(/\D/g, '').slice(0, 9));
                    setEmitLookup(null);
                    setEmitLookupErr(null);
                  }}
                  placeholder="123456789"
                  className={inputCls}
                />
                <button
                  type="button"
                  disabled={emitNif.length !== 9 || emitLookupPending}
                  onClick={async () => {
                    setEmitLookupPending(true);
                    setEmitLookupErr(null);
                    try {
                      const res = await clientesApi.lookupAtNif(emitNif);
                      setEmitLookup(res);
                    } catch (err) {
                      const code = isAxiosError(err) ? (err.response?.data as { code?: string } | undefined)?.code : undefined;
                      if (code === 'at_nif_not_found') setEmitLookupErr('NIF não encontrado na AT.');
                      else if (code === 'nif_invalid') setEmitLookupErr('NIF inválido (check-digit).');
                      else if (code === 'at_rate_limit_exceeded') setEmitLookupErr('Limite diário de consultas AT atingido.');
                      else if (code === 'at_unavailable') setEmitLookupErr('AT indisponível. Continua manual.');
                      else setEmitLookupErr('Falha na consulta AT.');
                    } finally {
                      setEmitLookupPending(false);
                    }
                  }}
                  className="inline-flex items-center gap-1 rounded-md border border-zinc-300 bg-white px-3 py-2 text-xs font-medium text-zinc-700 hover:bg-zinc-50 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300"
                >
                  {emitLookupPending ? 'A consultar…' : 'Procurar AT'}
                </button>
              </div>
              {emitLookup && (
                <div className="rounded-md border border-emerald-200 bg-emerald-50 p-2 text-xs dark:border-emerald-900/40 dark:bg-emerald-950/30">
                  <div className="font-medium text-emerald-900 dark:text-emerald-200">✓ AT: {emitLookup.nome}</div>
                  {emitLookup.morada && <div className="text-emerald-800 dark:text-emerald-300">{emitLookup.morada}</div>}
                  <div className="text-[10px] text-emerald-700 dark:text-emerald-400">Status: {emitLookup.status}</div>
                </div>
              )}
              {emitLookupErr && (
                <div className="rounded-md border border-red-200 bg-red-50 p-2 text-xs text-red-700 dark:border-red-900/40 dark:bg-red-950/30 dark:text-red-300">
                  {emitLookupErr}
                </div>
              )}
              {r.cliente.nif && r.cliente.nif === emitNif && (
                <div className="text-xs text-zinc-500">NIF actual do cliente — não vai ser alterado.</div>
              )}
            </div>
          )}

          <p className="text-xs text-zinc-500">
            {billing.data?.sandboxMode
              ? 'Documento criado na sandbox Moloni. Não comunicado à AT real.'
              : 'Ao confirmar, a fatura é comunicada à Autoridade Tributária em tempo real.'}
          </p>
        </div>
      </Modal>
    </div>
  );
}

function ChangeClienteModal({ open, onClose, onPick }: { open: boolean; onClose: () => void; onPick: (id: string) => void }) {
  const [search, setSearch] = useState('');
  const list = useQuery({
    queryKey: ['clientes-lookup', search],
    queryFn: () => clientesApi.list(search, 1, 10),
    enabled: open,
    placeholderData: keepPreviousData,
  });
  return (
    <Modal open={open} title="Trocar cliente" onClose={onClose} footer={<button type="button" onClick={onClose} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100">Cancelar</button>}>
      <input placeholder="Pesquisar…" value={search} onChange={e => setSearch(e.target.value)} className={inputCls} autoFocus />
      {list.data && (
        <ul className="mt-2 max-h-64 overflow-y-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
          {list.data.items.map(c => (
            <li key={c.id}>
              <button type="button" onClick={() => onPick(c.id)} className="block w-full px-3 py-2 text-left text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800">
                <div className="font-medium">{c.nome}</div>
                {c.telefone && <div className="text-xs text-zinc-500">{displayPhone(c.telefone)}</div>}
              </button>
            </li>
          ))}
        </ul>
      )}
    </Modal>
  );
}

const inputCls = 'w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950';

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <label className="text-xs font-medium uppercase tracking-wide text-zinc-500">{label}</label>
      {children}
    </div>
  );
}

function WorkflowStepper({
  estado,
  possibleNext,
  notas,
  onNotasChange,
  onChange,
  pending,
}: {
  estado: RepairStatus;
  possibleNext: RepairStatus[];
  notas: string;
  onNotasChange: (v: string) => void;
  onChange: (st: RepairStatus) => void;
  pending: boolean;
}) {
  // Onde estamos no fluxo principal de 4 estados (Recebido → Diag → Reparado → Entregue)
  const currentIdx = PRIMARY_STATUSES.indexOf(estado);
  const isCancelado = estado === 6;
  const isOrcamento = estado === 7;
  const nextPrimary = possibleNext.find((s) => PRIMARY_STATUSES.includes(s));
  const canCancel = possibleNext.includes(6);

  const isEntregar = nextPrimary === 5;

  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      {/* Stepper visual */}
      <ol className="-mx-1 flex items-center overflow-x-auto pb-1">
        {PRIMARY_STATUSES.map((s, i) => {
          const idx = i;
          const isCurrent = s === estado;
          const isDone = currentIdx > -1 && idx < currentIdx;
          const isFuture = currentIdx > -1 && idx > currentIdx;
          const dotColor = isCancelado
            ? 'bg-zinc-300 dark:bg-zinc-700'
            : isCurrent
              ? 'bg-brand-600 ring-4 ring-brand-200 dark:ring-brand-900'
              : isDone
                ? 'bg-emerald-500'
                : 'bg-zinc-300 dark:bg-zinc-700';
          const labelColor = isCurrent
            ? 'font-semibold text-zinc-900 dark:text-zinc-100'
            : isDone
              ? 'text-emerald-700 dark:text-emerald-400'
              : 'text-zinc-400 dark:text-zinc-600';
          return (
            <li key={s} className="flex flex-1 min-w-[80px] items-center">
              <div className="flex flex-1 flex-col items-center gap-1 px-1">
                <div className={`h-3 w-3 rounded-full transition ${dotColor}`} />
                <span className={`text-center text-[10px] sm:text-xs ${labelColor}`}>
                  {STATUS_LABEL[s]}
                </span>
              </div>
              {idx < PRIMARY_STATUSES.length - 1 && (
                <div
                  className={`mb-4 h-0.5 flex-1 ${
                    isCancelado || isFuture || idx >= currentIdx
                      ? 'bg-zinc-200 dark:bg-zinc-800'
                      : 'bg-emerald-500'
                  }`}
                />
              )}
            </li>
          );
        })}
      </ol>

      {isOrcamento && (
        <p className="mt-3 rounded-lg bg-yellow-50 px-3 py-2 text-xs text-yellow-800 dark:bg-yellow-950/40 dark:text-yellow-300">
          Em orçamento — equipamento ainda não chegou à loja.
        </p>
      )}
      {isCancelado && (
        <p className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-xs text-red-700 dark:bg-red-950/40 dark:text-red-300">
          Reparação cancelada.
        </p>
      )}

      {/* Action buttons */}
      {possibleNext.length > 0 && (
        <div className="mt-4 space-y-2">
          <input
            type="text"
            placeholder="Notas (opcional)"
            value={notas}
            onChange={(e) => onNotasChange(e.target.value)}
            className="min-h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950"
          />
          <div className="flex flex-col gap-2 sm:flex-row">
            {nextPrimary != null && (
              <button
                type="button"
                disabled={pending}
                onClick={() => onChange(nextPrimary)}
                className="flex-1 rounded-lg bg-brand-600 px-4 py-3 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:opacity-60"
              >
                {pending ? 'A guardar…' : isEntregar ? `✓ Marcar Entregue (Pago)` : `→ ${STATUS_LABEL[nextPrimary]}`}
              </button>
            )}
            {/* botões secundários (reabrir, etc) */}
            {possibleNext.filter((s) => s !== nextPrimary && s !== 6).map((st) => (
              <button
                key={st}
                type="button"
                disabled={pending}
                onClick={() => onChange(st)}
                className="rounded-lg border border-zinc-300 bg-white px-3 py-2 text-xs font-medium text-zinc-700 transition hover:bg-zinc-50 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300 dark:hover:bg-zinc-800"
              >
                ← {STATUS_LABEL[st]}
              </button>
            ))}
            {canCancel && (
              <button
                type="button"
                disabled={pending}
                onClick={() => onChange(6)}
                className="rounded-lg border border-red-200 bg-white px-3 py-2 text-xs font-medium text-red-700 transition hover:bg-red-50 disabled:opacity-50 dark:border-red-900 dark:bg-zinc-900 dark:text-red-400"
              >
                Cancelar
              </button>
            )}
          </div>
          {isEntregar && (
            <p className="text-[11px] text-zinc-500">
              Ao marcar Entregue, fica automaticamente <strong>Pago</strong>. Desmarca em "Preço & lucro" se for caso de não pagamento.
            </p>
          )}
        </div>
      )}
    </section>
  );
}

/** Sprint 87+92: banner mostra venda original e permite marcar reparação em garantia (0€). */
function VendaOrigemBanner({
  venda,
  reparacaoId,
  jaMarcadaGarantia,
  notasAtuais,
}: {
  venda: ReparacaoVendaOrigem;
  reparacaoId: string;
  jaMarcadaGarantia: boolean;
  notasAtuais: string;
}) {
  const qc = useQueryClient();
  const marcarGarantia = useMutation({
    mutationFn: async () => {
      const notaGarantia = `Reparação em garantia (vendido aqui em ${new Date(venda.vendaData).toLocaleDateString('pt-PT')} · Venda #${String(venda.vendaNumero).padStart(5, '0')})`;
      const novasNotas = notasAtuais.includes('em garantia')
        ? notasAtuais
        : notasAtuais.trim()
          ? `${notasAtuais.trim()}\n${notaGarantia}`
          : notaGarantia;
      // Patch minimal: apenas orçamento + notas. Outros campos preservados pelo back-end com os valores atuais (PUT padrão).
      const current = await reparacoesApi.get(reparacaoId);
      return reparacoesApi.update(reparacaoId, {
        equipamento: current.reparacao.equipamento,
        avaria: current.reparacao.avaria,
        imei: current.reparacao.imei ?? null,
        diagnostico: current.reparacao.diagnostico ?? null,
        orcamentoCents: 0,
        orcamentoAprovado: true,
        precoFinalCents: current.reparacao.precoFinalCents ?? 0,
        custoPecasCents: current.reparacao.custoPecasCents,
        horasGastas: current.reparacao.horasGastas,
        notas: novasNotas,
        // Sprint 95 fix: reparação em garantia tem cobrança 0€ = imediatamente "paga".
        estadoPagamento: PAYMENT_STATUS.Pago,
        equipmentFieldTemplateId: current.reparacao.equipmentFieldTemplateId ?? null,
        fields: null,
      });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reparacao', reparacaoId] });
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      toast.success('Reparação marcada em garantia · orçamento 0€');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível marcar em garantia.'),
  });

  return (
    <div className={`rounded-xl border p-4 ${
      venda.garantiaActiva
        ? 'border-emerald-300 bg-emerald-50/60 dark:border-emerald-900/60 dark:bg-emerald-950/30'
        : 'border-zinc-300 bg-zinc-50/60 dark:border-zinc-700 dark:bg-zinc-900/60'
    }`}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <div className={`text-[10px] uppercase tracking-wider ${
            venda.garantiaActiva
              ? 'text-emerald-700 dark:text-emerald-300'
              : 'text-zinc-500'
          }`}>
            {venda.garantiaActiva ? '✓ Em garantia interna' : 'Equipamento vendido aqui'}
          </div>
          <div className="mt-1 text-sm">
            Vendido na <Link to={`/vendas`} className="font-medium text-brand-600 hover:underline">
              Venda #{String(venda.vendaNumero).padStart(5, '0')}
            </Link>{' '}
            em <strong>{new Date(venda.vendaData).toLocaleDateString('pt-PT')}</strong>{' '}
            ({venda.diasEntreVendaEReparacao} dias antes).
          </div>
          {venda.garantiaActiva && (
            <div className="mt-1 text-xs text-emerald-700 dark:text-emerald-300">
              Garantia activa — expira em <strong>{venda.diasRestantesGarantia} dias</strong>.
              {' '}Reparação coberta — não cobrar ao cliente.
            </div>
          )}
          {!venda.garantiaActiva && venda.garantiaSlug && (
            <div className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
              Garantia já não está activa.
            </div>
          )}
          {venda.garantiaActiva && !jaMarcadaGarantia && (
            <button
              type="button"
              onClick={() => marcarGarantia.mutate()}
              disabled={marcarGarantia.isPending}
              className="mt-2 inline-flex items-center gap-1 rounded-md bg-emerald-600 px-2.5 py-1 text-[11px] font-medium text-white hover:bg-emerald-700 disabled:opacity-60"
            >
              {marcarGarantia.isPending ? 'A definir…' : 'Definir orçamento 0€ (em garantia)'}
            </button>
          )}
          {jaMarcadaGarantia && (
            <div className="mt-2 text-[11px] text-emerald-700 dark:text-emerald-300">
              ✓ Já marcada com orçamento 0€
            </div>
          )}
        </div>
        {venda.garantiaSlug && (
          <a
            href={`/g/${venda.garantiaSlug}`}
            target="_blank"
            rel="noopener noreferrer"
            className="shrink-0 text-xs text-brand-600 hover:underline"
          >
            Ver garantia →
          </a>
        )}
      </div>
      <FornecedorCoberturaBanner venda={venda} />
    </div>
  );
}

/**
 * Sprint 108: banner que mostra estado da garantia B2B do fornecedor.
 * Verde = ainda coberto pelo fornecedor (RMA possível, 0€ a teu cargo).
 * Amarelo = expirou no fornecedor (custo a teu cargo, mas legal mantém-se).
 * Não renderiza nada se não há info de fornecedor.
 */
function FornecedorCoberturaBanner({ venda }: { venda: ReparacaoVendaOrigem }) {
  if (!venda.fornecedorNome && !venda.garantiaFornecedorAteAo) return null;

  const ateAo = venda.garantiaFornecedorAteAo ? new Date(venda.garantiaFornecedorAteAo) : null;
  const hoje = new Date();
  const ainda = ateAo ? ateAo >= hoje : false;
  const diasResta = ateAo ? Math.ceil((ateAo.getTime() - hoje.getTime()) / (1000 * 60 * 60 * 24)) : null;
  const diasPassou = ateAo && !ainda ? Math.ceil((hoje.getTime() - ateAo.getTime()) / (1000 * 60 * 60 * 24)) : null;

  return (
    <div className={`mt-3 rounded-md border p-2.5 text-xs ${
      ainda
        ? 'border-emerald-300 bg-emerald-50/60 dark:border-emerald-900/60 dark:bg-emerald-950/30'
        : ateAo
          ? 'border-amber-300 bg-amber-50/60 dark:border-amber-900/60 dark:bg-amber-950/30'
          : 'border-zinc-300 bg-zinc-50/60 dark:border-zinc-700 dark:bg-zinc-900/60'
    }`}>
      {ainda && (
        <>
          <div className="font-medium text-emerald-800 dark:text-emerald-200">
            🟢 Coberta por {venda.fornecedorNome ?? 'fornecedor'} até{' '}
            {ateAo!.toLocaleDateString('pt-PT')} (faltam {diasResta}d)
          </div>
          <div className="mt-1 text-emerald-700 dark:text-emerald-300">
            Contactar fornecedor para RMA · custo €0 a teu cargo · cliente paga 0€ (DL 84/2021 art. 17.º n.º 4)
          </div>
        </>
      )}
      {!ainda && ateAo && (
        <>
          <div className="font-medium text-amber-800 dark:text-amber-200">
            🟡 Garantia {venda.fornecedorNome ?? 'fornecedor'} expirou há {diasPassou}d
          </div>
          <div className="mt-1 text-amber-700 dark:text-amber-300">
            Custo a teu cargo (peça + mão-de-obra) · cliente paga 0€ (garantia legal mantém-se)
          </div>
        </>
      )}
      {!ateAo && venda.fornecedorNome && (
        <div className="text-zinc-600 dark:text-zinc-400">
          Fornecedor: {venda.fornecedorNome} · sem data de garantia B2B registada
        </div>
      )}
    </div>
  );
}
