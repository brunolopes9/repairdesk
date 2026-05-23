import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Copy, History, Plus, RefreshCw, ShieldCheck, Trash2, Webhook } from 'lucide-react';
import Modal from '../../components/Modal';
import JsonViewer from '../../components/JsonViewer';
import { Button, EmptyState, PageHeader, SkeletonRow, StatusBadge } from '../../components/ui';
import { toast } from '../../lib/toast';
import { webhooksApi, type CreateWebhookSubscriptionResponse, type WebhookDelivery, type WebhookSubscription } from '../../lib/webhooks/api';
import { formatDate } from '../../lib/money';

interface FormState {
  name: string;
  url: string;
  events: Set<string>;
  active: boolean;
}

const emptyForm: FormState = { name: '', url: 'https://', events: new Set(), active: true };

export default function Webhooks() {
  const qc = useQueryClient();
  const list = useQuery({ queryKey: ['webhooks'], queryFn: () => webhooksApi.list() });
  const eventsQuery = useQuery({ queryKey: ['webhook-events'], queryFn: () => webhooksApi.events(), staleTime: 60_000 });

  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<WebhookSubscription | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [createdSecret, setCreatedSecret] = useState<CreateWebhookSubscriptionResponse | null>(null);
  const [deliveriesOf, setDeliveriesOf] = useState<WebhookSubscription | null>(null);
  const [inspectingDelivery, setInspectingDelivery] = useState<WebhookDelivery | null>(null);

  const deliveriesQuery = useQuery({
    queryKey: ['webhook-deliveries', deliveriesOf?.id],
    queryFn: () => webhooksApi.deliveries(deliveriesOf!.id, 50),
    enabled: !!deliveriesOf,
  });

  const retryMut = useMutation({
    mutationFn: (deliveryId: string) => webhooksApi.retryDelivery(deliveryId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['webhook-deliveries', deliveriesOf?.id] });
      qc.invalidateQueries({ queryKey: ['webhooks'] });
      toast.success('Delivery reagendada para retry imediato.');
    },
    onError: (e) => toast.fromError(e, 'Erro ao reagendar.'),
  });

  function openCreate() {
    setEditing(null);
    setForm(emptyForm);
    setOpen(true);
  }

  function openEdit(s: WebhookSubscription) {
    setEditing(s);
    setForm({ name: s.name, url: s.url, events: new Set(s.events), active: s.active });
    setOpen(true);
  }

  const saveMut = useMutation({
    mutationFn: async () => {
      const payload = { name: form.name, url: form.url, events: Array.from(form.events) };
      if (editing) return webhooksApi.update(editing.id, { ...payload, active: form.active });
      return webhooksApi.create(payload);
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ['webhooks'] });
      setOpen(false);
      if (!editing && 'secret' in data) {
        setCreatedSecret(data as CreateWebhookSubscriptionResponse);
      } else {
        toast.success('Webhook atualizado.');
      }
    },
    onError: (e) => toast.fromError(e, 'Erro ao guardar webhook.'),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => webhooksApi.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['webhooks'] });
      toast.success('Webhook removido.');
    },
    onError: (e) => toast.fromError(e, 'Erro ao remover.'),
  });

  const allEvents = eventsQuery.data ?? [];
  const items = list.data ?? [];

  const canSubmit = useMemo(
    () => form.name.trim().length > 0 && form.url.trim().length > 0 && form.events.size > 0,
    [form],
  );

  return (
    <div className="space-y-5">
      <PageHeader
        title="Webhooks"
        description="Recebe eventos do Reparo no teu servidor (loja online, automações). Cada delivery é assinada com HMAC-SHA256 para verificares autenticidade."
        actions={<Button leftIcon={<Plus size={15} />} onClick={openCreate}>Novo webhook</Button>}
      />

      <section className="overflow-hidden rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <table className="w-full text-sm">
          <thead className="bg-zinc-50 text-left text-xs uppercase tracking-wider text-zinc-500 dark:bg-zinc-800/60">
            <tr>
              <th className="px-4 py-2.5">Nome</th>
              <th className="px-4 py-2.5">URL</th>
              <th className="px-4 py-2.5">Eventos</th>
              <th className="px-4 py-2.5">Estado</th>
              <th className="px-4 py-2.5">Última entrega</th>
              <th className="px-4 py-2.5" />
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {list.isLoading && Array.from({ length: 3 }).map((_, i) => <tr key={i}><td colSpan={6}><SkeletonRow columns={6} /></td></tr>)}
            {!list.isLoading && items.map((s) => (
              <tr key={s.id} onClick={() => openEdit(s)} className="cursor-pointer hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                <td className="px-4 py-3 font-medium">{s.name}</td>
                <td className="px-4 py-3 font-mono text-xs text-zinc-600 dark:text-zinc-300">{s.url}</td>
                <td className="px-4 py-3 text-xs">
                  <div className="flex flex-wrap gap-1">
                    {s.events.map((e) => (
                      <span key={e} className="rounded-full bg-zinc-100 px-2 py-0.5 text-[11px] dark:bg-zinc-800">{e}</span>
                    ))}
                  </div>
                </td>
                <td className="px-4 py-3">
                  {s.disabledAt
                    ? <StatusBadge tone="rose">Desactivada</StatusBadge>
                    : s.active
                      ? <StatusBadge tone="emerald">Activa</StatusBadge>
                      : <StatusBadge tone="zinc">Pausada</StatusBadge>}
                  {s.failureCount > 0 && (
                    <div className="mt-1 flex items-center gap-1 text-[11px] text-amber-600">
                      <AlertTriangle size={11} /> {s.failureCount} falhas
                    </div>
                  )}
                </td>
                <td className="px-4 py-3 text-xs text-zinc-500">
                  {s.lastDeliveryAt ? formatDate(s.lastDeliveryAt) : '—'}
                </td>
                <td className="px-4 py-3 text-right">
                  <div className="flex items-center justify-end gap-1">
                    <button
                      type="button"
                      onClick={(e) => { e.stopPropagation(); setDeliveriesOf(s); }}
                      className="rounded-md p-1 text-zinc-500 hover:bg-zinc-100 hover:text-brand-600 dark:hover:bg-zinc-800"
                      title="Ver últimas entregas"
                    >
                      <History size={15} />
                    </button>
                    <button
                      type="button"
                      onClick={(e) => { e.stopPropagation(); if (confirm(`Remover webhook "${s.name}"?`)) deleteMut.mutate(s.id); }}
                      className="rounded-md p-1 text-zinc-500 hover:bg-rose-50 hover:text-rose-600 dark:hover:bg-rose-950/40"
                      aria-label="Remover"
                    >
                      <Trash2 size={15} />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {!list.isLoading && items.length === 0 && (
              <tr><td colSpan={6} className="p-6">
                <EmptyState
                  icon={Webhook}
                  title="Sem webhooks configurados"
                  description="Configura um endpoint para receber eventos. Útil para sincronizar com a loja online ou outras integrações."
                />
              </td></tr>
            )}
          </tbody>
        </table>
      </section>

      <Modal open={open} title={editing ? 'Editar webhook' : 'Novo webhook'} onClose={() => setOpen(false)}>
        <form
          onSubmit={(e) => { e.preventDefault(); if (canSubmit) saveMut.mutate(); }}
          className="space-y-4"
        >
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-zinc-500">Nome</span>
            <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className={inputCls} placeholder="Loja online — eventos garantia" />
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-zinc-500">URL (HTTPS)</span>
            <input value={form.url} onChange={(e) => setForm({ ...form, url: e.target.value })} className={`${inputCls} font-mono text-xs`} placeholder="https://shop.example.com/api/repairdesk-events" />
          </label>
          <fieldset>
            <legend className="mb-2 text-xs font-medium text-zinc-500">Eventos subscritos</legend>
            <div className="grid grid-cols-2 gap-1.5">
              {allEvents.map((ev) => (
                <label key={ev} className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1 text-xs hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                  <input
                    type="checkbox"
                    checked={form.events.has(ev)}
                    onChange={(e) => {
                      const next = new Set(form.events);
                      if (e.target.checked) next.add(ev); else next.delete(ev);
                      setForm({ ...form, events: next });
                    }}
                  />
                  <span className="font-mono">{ev}</span>
                </label>
              ))}
            </div>
          </fieldset>
          {editing && (
            <label className="flex items-center gap-2 text-xs">
              <input type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} />
              Webhook activo (recebe entregas)
            </label>
          )}
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="ghost" onClick={() => setOpen(false)}>Cancelar</Button>
            <Button type="submit" disabled={!canSubmit || saveMut.isPending}>
              {editing ? 'Guardar' : 'Criar webhook'}
            </Button>
          </div>
        </form>
      </Modal>

      <Modal
        open={!!deliveriesOf}
        title={`Últimas entregas · ${deliveriesOf?.name ?? ''}`}
        onClose={() => setDeliveriesOf(null)}
      >
        <div className="max-h-[60vh] overflow-y-auto">
          {deliveriesQuery.isLoading && <SkeletonRow columns={4} />}
          {!deliveriesQuery.isLoading && (deliveriesQuery.data?.length ?? 0) === 0 && (
            <EmptyState icon={History} title="Sem entregas ainda" description="Quando o Reparo publicar um evento que esta subscription ouve, aparece aqui." />
          )}
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {deliveriesQuery.data?.map((d) => (
              <li key={d.id} className="py-2.5">
                <div className="flex items-center justify-between gap-2">
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <span className="font-mono text-xs">{d.eventType}</span>
                      <DeliveryStatusBadge status={d.status} code={d.lastResponseCode} />
                      <span className="text-[11px] text-zinc-500">tent. {d.attempts}</span>
                    </div>
                    <div className="mt-0.5 text-[11px] text-zinc-500">
                      {formatDate(d.createdAt)}
                      {d.nextRetryAt && d.status === 'Pending' && <span> · retry: {formatDate(d.nextRetryAt)}</span>}
                      {d.lastError && <span className="text-rose-600"> · {d.lastError}</span>}
                    </div>
                  </div>
                  <div className="flex items-center gap-1">
                    <button
                      type="button"
                      onClick={() => setInspectingDelivery(d)}
                      className="rounded-md px-2 py-1 text-[11px] text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
                    >
                      Payload
                    </button>
                    {d.status === 'Failed' && (
                      <button
                        type="button"
                        onClick={() => retryMut.mutate(d.id)}
                        disabled={retryMut.isPending}
                        className="flex items-center gap-1 rounded-md border border-brand-300 bg-brand-50 px-2 py-1 text-[11px] text-brand-700 hover:bg-brand-100 dark:border-brand-700 dark:bg-brand-950/40 dark:text-brand-300"
                      >
                        <RefreshCw size={11} /> Retry
                      </button>
                    )}
                  </div>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </Modal>

      <Modal
        open={!!inspectingDelivery}
        title={`Payload · ${inspectingDelivery?.eventType ?? ''}`}
        onClose={() => setInspectingDelivery(null)}
      >
        {inspectingDelivery && (
          <div className="max-h-[60vh] overflow-y-auto">
            <JsonViewer value={inspectingDelivery.payloadJson} />
          </div>
        )}
      </Modal>

      <Modal
        open={!!createdSecret}
        title="Webhook criado — guarda o secret"
        onClose={() => setCreatedSecret(null)}
      >
        <div className="space-y-3">
          <div className="rounded-md bg-amber-50 p-3 text-xs text-amber-900 dark:bg-amber-950/40 dark:text-amber-200">
            <div className="flex items-start gap-2">
              <ShieldCheck size={15} className="mt-0.5 flex-shrink-0" />
              <div>
                <strong>Este secret só é mostrado uma vez.</strong> Copia-o agora para o teu servidor.
                Usa-o para verificar HMAC-SHA256 do payload no header <code className="font-mono">X-Reparo-Signature</code>.
              </div>
            </div>
          </div>
          <div className="rounded-md border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-700 dark:bg-zinc-900">
            <div className="flex items-center justify-between gap-2">
              <code className="break-all font-mono text-xs">{createdSecret?.secret}</code>
              <button
                type="button"
                onClick={async () => {
                  if (createdSecret) {
                    await navigator.clipboard.writeText(createdSecret.secret);
                    toast.success('Copiado.');
                  }
                }}
                className="flex items-center gap-1 rounded-md border border-zinc-300 bg-white px-2 py-1 text-xs hover:bg-zinc-50 dark:border-zinc-600 dark:bg-zinc-800"
              >
                <Copy size={12} /> Copiar
              </button>
            </div>
          </div>
          <div className="flex items-center gap-2 text-xs text-zinc-500">
            <CheckCircle2 size={14} className="text-emerald-600" />
            Endpoint <code className="font-mono">{createdSecret?.subscription.url}</code> a ouvir {createdSecret?.subscription.events.length} eventos.
          </div>
          <div className="flex justify-end">
            <Button onClick={() => setCreatedSecret(null)}>Já guardei</Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}

const inputCls =
  'w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-zinc-700 dark:bg-zinc-900';

function DeliveryStatusBadge({ status, code }: { status: WebhookDelivery['status']; code: number | null }) {
  if (status === 'Delivered') return <StatusBadge tone="emerald">200 {code && code !== 200 ? code : ''}</StatusBadge>;
  if (status === 'Failed') return <StatusBadge tone="rose">{code ?? 'Falhou'}</StatusBadge>;
  return <StatusBadge tone="amber">Pendente</StatusBadge>;
}
