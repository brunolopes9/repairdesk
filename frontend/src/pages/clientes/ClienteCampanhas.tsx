import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  ArrowLeft,
  Ban,
  CheckCircle2,
  Copy,
  Download,
  Mail,
  Megaphone,
  MessageCircle,
  Search,
  ShieldCheck,
  Tags,
  Users,
} from 'lucide-react';
import { Button, EmptyState, PageHeader, SkeletonCard } from '../../components/ui';
import { clienteTagsApi } from '../../lib/clienteTags/api';
import type { Cliente, ClienteTag } from '../../lib/clientes/types';
import { displayPhone } from '../../lib/phone/formatter';

const DEFAULT_TEMPLATE =
  'Ola {nome}, e o Bruno da LopesTech. Tenho uma novidade/oferta que pode fazer sentido para si. Posso enviar-lhe detalhes?';

function primeiroNome(nome: string) {
  return nome.trim().split(/\s+/)[0] || nome;
}

function renderTemplate(template: string, cliente: Cliente) {
  return template.split('{nome}').join(primeiroNome(cliente.nome));
}

function normalizeWaPhone(phone: string | null | undefined) {
  const digits = (phone ?? '').replace(/\D/g, '');
  if (!digits) return null;
  if (digits.startsWith('00')) return digits.slice(2);
  if (digits.startsWith('351')) return digits;
  if (digits.length === 9) return `351${digits}`;
  return digits;
}

function csvEscape(value: unknown) {
  const text = value == null ? '' : String(value);
  return `"${text.replace(/"/g, '""')}"`;
}

function downloadCsv(clientes: Cliente[], template: string) {
  const header = ['nome', 'telefone', 'email', 'contactoPreferido', 'etiquetas', 'mensagem'];
  const rows = clientes.map((c) => [
    c.nome,
    c.telefone ?? '',
    c.email ?? '',
    c.contactoPreferido ?? '',
    c.tags?.map((t) => t.nome).join('; ') ?? '',
    renderTemplate(template, c),
  ]);
  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\r\n');
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `campanha_clientes_${new Date().toISOString().slice(0, 10)}.csv`;
  a.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function toggleId(ids: string[], id: string) {
  return ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id];
}

function tagLabel(tags: ClienteTag[] | undefined) {
  if (!tags || tags.length === 0) return 'Sem etiquetas';
  return tags.map((t) => t.nome).join(', ');
}

export default function ClienteCampanhas() {
  const [selectedTagIds, setSelectedTagIds] = useState<string[]>([]);
  const [template, setTemplate] = useState(DEFAULT_TEMPLATE);
  const [search, setSearch] = useState('');

  const tagsQuery = useQuery({
    queryKey: ['cliente-tags-all'],
    queryFn: () => clienteTagsApi.list(),
    staleTime: 60_000,
  });
  const allTags = tagsQuery.data ?? [];

  const segmentoQuery = useQuery({
    queryKey: ['cliente-tags-segmento', selectedTagIds],
    queryFn: () => clienteTagsApi.segmento(selectedTagIds),
    enabled: selectedTagIds.length > 0,
  });

  const segmento = segmentoQuery.data;
  const clientes = segmento?.clientes ?? [];
  const filteredClientes = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return clientes;
    return clientes.filter((c) =>
      [c.nome, c.telefone, c.email, c.nif, tagLabel(c.tags)].some((value) => (value ?? '').toLowerCase().includes(term)),
    );
  }, [clientes, search]);

  const excluidos = Math.max(0, (segmento?.totalSegmento ?? 0) - (segmento?.totalElegiveis ?? 0));
  const selectedTags = allTags.filter((t) => selectedTagIds.includes(t.id));

  return (
    <div className="space-y-4">
      <PageHeader
        title="Campanhas"
        description="Outreach manual por etiquetas de cliente, sempre filtrado por consentimento RGPD."
        meta={<span className="text-sm text-zinc-500">Clientes CRM</span>}
        actions={
          <>
            <Link
              to="/clientes"
              className="inline-flex min-h-11 items-center justify-center gap-1.5 rounded-lg border border-zinc-200 bg-white px-3 text-sm font-medium text-zinc-700 shadow-sm transition hover:bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-200 dark:hover:bg-zinc-800"
            >
              <ArrowLeft size={15} />
              Clientes
            </Link>
            <Button
              type="button"
              variant="secondary"
              leftIcon={<Download size={15} />}
              disabled={clientes.length === 0}
              onClick={() => downloadCsv(clientes, template)}
            >
              Exportar CSV
            </Button>
          </>
        }
      />

      <div className="grid gap-4 xl:grid-cols-[360px_minmax(0,1fr)]">
        <aside className="space-y-4">
          <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
            <div className="flex items-center gap-2">
              <div className="grid h-9 w-9 place-items-center rounded-lg bg-brand-50 text-brand-700 dark:bg-brand-950/40 dark:text-brand-300">
                <Tags size={18} />
              </div>
              <div>
                <h2 className="text-sm font-semibold">Segmento</h2>
                <p className="text-xs text-zinc-500">Escolhe uma ou mais etiquetas.</p>
              </div>
            </div>
            <div className="mt-4 flex flex-wrap gap-2">
              {tagsQuery.isLoading && <SkeletonCard />}
              {allTags.map((tag) => {
                const active = selectedTagIds.includes(tag.id);
                return (
                  <button
                    key={tag.id}
                    type="button"
                    aria-pressed={active}
                    onClick={() => setSelectedTagIds((ids) => toggleId(ids, tag.id))}
                    className={`inline-flex min-h-9 items-center gap-2 rounded-full border px-3 text-xs font-medium transition ${
                      active
                        ? 'border-transparent text-white shadow-sm'
                        : 'border-zinc-200 bg-white text-zinc-600 hover:bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300'
                    }`}
                    style={active ? { backgroundColor: tag.corHex } : undefined}
                  >
                    <span className="h-2 w-2 rounded-full" style={{ backgroundColor: active ? '#fff' : tag.corHex }} />
                    {tag.nome}
                  </button>
                );
              })}
            </div>
            {allTags.length === 0 && !tagsQuery.isLoading && (
              <p className="mt-4 rounded-lg border border-dashed border-zinc-200 p-3 text-sm text-zinc-500 dark:border-zinc-800">
                Ainda nao ha etiquetas. Cria etiquetas na pagina de clientes para segmentar campanhas.
              </p>
            )}
          </section>

          <section className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-emerald-950 dark:border-emerald-900/60 dark:bg-emerald-950/30 dark:text-emerald-100">
            <div className="flex gap-3">
              <ShieldCheck size={18} className="mt-0.5 flex-none" />
              <div>
                <h2 className="text-sm font-semibold">RGPD sempre ativo</h2>
                <p className="mt-1 text-sm text-emerald-800 dark:text-emerald-200">
                  O backend so devolve clientes com AceitaMarketing=true e NaoContactar=false. Clientes sem consentimento ficam fora do CSV e dos links.
                </p>
              </div>
            </div>
          </section>

          <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
            <div className="flex items-center justify-between gap-3">
              <div>
                <h2 className="text-sm font-semibold">Template</h2>
                <p className="text-xs text-zinc-500">Usa {'{nome}'} para personalizar.</p>
              </div>
              <Button type="button" variant="ghost" size="sm" onClick={() => setTemplate(DEFAULT_TEMPLATE)}>
                Repor
              </Button>
            </div>
            <textarea
              value={template}
              onChange={(e) => setTemplate(e.target.value)}
              rows={7}
              className="mt-3 w-full resize-none rounded-lg border border-zinc-300 bg-white p-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
            />
            {selectedTags.length > 0 && (
              <p className="mt-3 text-xs text-zinc-500">
                Segmento atual: {selectedTags.map((t) => t.nome).join(', ')}
              </p>
            )}
          </section>
        </aside>

        <section className="min-w-0 space-y-4">
          <div className="grid gap-3 md:grid-cols-3">
            <MetricCard
              icon={Users}
              label="Elegiveis"
              value={`${segmento?.totalElegiveis ?? 0} de ${segmento?.totalSegmento ?? 0}`}
              description="Apenas clientes com consentimento valido."
            />
            <MetricCard
              icon={Ban}
              label="Excluidos RGPD"
              value={String(excluidos)}
              description="Sem marketing opt-in ou marcados como nao contactar."
            />
            <MetricCard
              icon={Megaphone}
              label="Modo de envio"
              value="Manual"
              description="CSV, WhatsApp e email pre-preenchidos. Sem auto-envio."
            />
          </div>

          <div className="rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
            <div className="flex flex-col gap-3 border-b border-zinc-200 p-4 dark:border-zinc-800 lg:flex-row lg:items-center lg:justify-between">
              <div>
                <h2 className="text-sm font-semibold">Clientes do segmento</h2>
                <p className="text-sm text-zinc-500">
                  {selectedTagIds.length === 0
                    ? 'Seleciona etiquetas para ver clientes.'
                    : `${filteredClientes.length} visiveis nesta lista.`}
                </p>
              </div>
              <div className="relative min-w-0 lg:w-80">
                <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
                <input
                  type="search"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Filtrar elegiveis..."
                  className="min-h-11 w-full rounded-lg border border-zinc-300 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
                />
              </div>
            </div>

            <div className="p-4">
              {selectedTagIds.length === 0 ? (
                <EmptyState
                  icon={Tags}
                  title="Escolhe o segmento"
                  description="Seleciona uma ou mais etiquetas para gerar uma lista elegivel para campanha."
                  compact
                />
              ) : segmentoQuery.isLoading ? (
                <div className="grid gap-3">
                  <SkeletonCard />
                  <SkeletonCard />
                </div>
              ) : filteredClientes.length === 0 ? (
                <EmptyState
                  icon={ShieldCheck}
                  title="Sem clientes elegiveis"
                  description="Este segmento nao tem clientes com consentimento de marketing ativo."
                  compact
                />
              ) : (
                <div className="space-y-3">
                  {filteredClientes.map((cliente) => (
                    <ClienteCampanhaRow key={cliente.id} cliente={cliente} message={renderTemplate(template, cliente)} />
                  ))}
                </div>
              )}
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}

function MetricCard({
  icon: Icon,
  label,
  value,
  description,
}: {
  icon: typeof Users;
  label: string;
  value: string;
  description: string;
}) {
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-start gap-3">
        <div className="grid h-9 w-9 place-items-center rounded-lg bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
          <Icon size={18} />
        </div>
        <div className="min-w-0">
          <p className="text-xs font-medium uppercase text-zinc-500">{label}</p>
          <p className="mt-1 text-xl font-semibold tracking-tight">{value}</p>
          <p className="mt-1 text-xs text-zinc-500">{description}</p>
        </div>
      </div>
    </div>
  );
}

function ClienteCampanhaRow({ cliente, message }: { cliente: Cliente; message: string }) {
  const waPhone = normalizeWaPhone(cliente.telefone);
  const waHref = waPhone ? `https://wa.me/${waPhone}?text=${encodeURIComponent(message)}` : null;
  const mailHref = cliente.email
    ? `mailto:${cliente.email}?subject=${encodeURIComponent('Mensagem da LopesTech')}&body=${encodeURIComponent(message)}`
    : null;

  return (
    <article className="rounded-xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-950">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="truncate text-sm font-semibold">{cliente.nome}</h3>
            <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-[11px] font-medium text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300">
              <CheckCircle2 size={12} />
              Marketing OK
            </span>
          </div>
          <p className="mt-1 text-sm text-zinc-500">
            {[cliente.telefone ? displayPhone(cliente.telefone) : null, cliente.email, cliente.contactoPreferido ? `Prefere ${cliente.contactoPreferido}` : null]
              .filter(Boolean)
              .join(' · ') || 'Sem contactos diretos'}
          </p>
          <div className="mt-3 flex flex-wrap gap-1.5">
            {(cliente.tags ?? []).map((tag) => (
              <span
                key={tag.id}
                className="inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium text-white"
                style={{ backgroundColor: tag.corHex }}
              >
                {tag.nome}
              </span>
            ))}
          </div>
          <p className="mt-3 rounded-lg bg-zinc-50 p-3 text-sm text-zinc-600 dark:bg-zinc-900 dark:text-zinc-300">{message}</p>
        </div>

        <div className="flex flex-wrap gap-2 lg:justify-end">
          {waHref ? (
            <a
              href={waHref}
              target="_blank"
              rel="noreferrer"
              className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-lg border border-emerald-200 bg-emerald-50 px-3 text-sm font-medium text-emerald-700 transition hover:bg-emerald-100 dark:border-emerald-900/60 dark:bg-emerald-950/30 dark:text-emerald-300"
            >
              <MessageCircle size={15} />
              WhatsApp
            </a>
          ) : (
            <span className="inline-flex min-h-10 items-center rounded-lg border border-zinc-200 px-3 text-sm text-zinc-400 dark:border-zinc-800">
              Sem telefone
            </span>
          )}
          {mailHref ? (
            <a
              href={mailHref}
              className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-lg border border-zinc-200 bg-white px-3 text-sm font-medium text-zinc-700 transition hover:bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-200 dark:hover:bg-zinc-800"
            >
              <Mail size={15} />
              Email
            </a>
          ) : null}
          <button
            type="button"
            onClick={() => void navigator.clipboard?.writeText(message)}
            className="inline-flex min-h-10 items-center justify-center gap-1.5 rounded-lg border border-zinc-200 bg-white px-3 text-sm font-medium text-zinc-700 transition hover:bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-200 dark:hover:bg-zinc-800"
          >
            <Copy size={15} />
            Copiar
          </button>
        </div>
      </div>
    </article>
  );
}
