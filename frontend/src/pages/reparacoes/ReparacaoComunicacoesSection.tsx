import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { MessageCircle, Phone, Mail, MessageSquare, StickyNote, MapPin, Plus, Trash2, ArrowDownLeft, ArrowUpRight, FileText, Send } from 'lucide-react'; // Mail importado para CTA Email S471
import { Button } from '../../components/ui/Button';
import { toast } from '../../lib/toast';
import { apiErrorMessage } from '../../lib/errors';
import {
  comunicacoesApi,
  ComunicacaoDirecao,
  ComunicacaoTipo,
  COMUNICACAO_DIRECAO_LABEL,
  COMUNICACAO_TIPO_LABEL,
  type ReparacaoComunicacao,
} from '../../lib/comunicacoes/api';
import { tenantPreferencesApi } from '../../lib/tenantPreferences/api';
import { tenantSettingsApi } from '../../lib/tenantSettings/api';

/**
 * Sprint 452 (Doc 91 ponto 1 — Conversas omnicanal v1).
 *
 * Caso típico: cliente liga a perguntar "está pronta?". Staff regista a chamada
 * (tipo=Telefone, direção=Recebida, texto="cliente perguntou estado") para deixar
 * rasto. Próxima vez que alguém abrir a reparação vê que já houve contacto e o
 * que se disse — sem precisar de procurar no WhatsApp ou esperar pela memória.
 *
 * Form inline: tipo + direção + texto. Lista cronológica reversa com ícone+chip.
 */
const TIPO_ICON: Record<ComunicacaoTipo, typeof MessageCircle> = {
  [ComunicacaoTipo.Nota]: StickyNote,
  [ComunicacaoTipo.Telefone]: Phone,
  [ComunicacaoTipo.WhatsApp]: MessageCircle,
  [ComunicacaoTipo.Email]: Mail,
  [ComunicacaoTipo.Sms]: MessageSquare,
  [ComunicacaoTipo.Visita]: MapPin,
  // Sprint 480: mensagens recebidas pelo portal cliente.
  [ComunicacaoTipo.PortalCliente]: MessageCircle,
};

const TIPO_COR: Record<ComunicacaoTipo, string> = {
  [ComunicacaoTipo.Nota]: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300',
  [ComunicacaoTipo.Telefone]: 'bg-sky-100 text-sky-800 dark:bg-sky-950/40 dark:text-sky-300',
  [ComunicacaoTipo.WhatsApp]: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300',
  [ComunicacaoTipo.Email]: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-950/40 dark:text-indigo-300',
  [ComunicacaoTipo.Sms]: 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300',
  [ComunicacaoTipo.Visita]: 'bg-purple-100 text-purple-800 dark:bg-purple-950/40 dark:text-purple-300',
  [ComunicacaoTipo.PortalCliente]: 'bg-rose-100 text-rose-800 dark:bg-rose-950/40 dark:text-rose-300',
};

export function ReparacaoComunicacoesSection({
  reparacaoId,
  reparacaoNumero,
  reparacaoEstado,
  reparacaoEquipamento,
  clienteNome,
  clienteTelefone,
  clienteEmail,
  clienteNaoContactar,
  clienteContactoPreferido,
}: {
  reparacaoId: string;
  reparacaoNumero?: number;
  /** RepairStatus: 4 = Pronto. Quando Pronto, mostra CTA "Avisar pronto" (S456). */
  reparacaoEstado?: number;
  reparacaoEquipamento?: string;
  clienteNome?: string;
  clienteTelefone?: string | null;
  clienteEmail?: string | null;
  /** Sprint 488: consentimento RGPD (S479). Quando true, esconde CTAs proativos de contacto. */
  clienteNaoContactar?: boolean;
  clienteContactoPreferido?: string | null;
}) {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [tipo, setTipo] = useState<ComunicacaoTipo>(ComunicacaoTipo.Telefone);
  const [direcao, setDirecao] = useState<ComunicacaoDirecao>(ComunicacaoDirecao.Inbound);
  const [texto, setTexto] = useState('');

  const list = useQuery({
    queryKey: ['comunicacoes', reparacaoId],
    queryFn: () => comunicacoesApi.list(reparacaoId),
    staleTime: 30_000,
  });

  const create = useMutation({
    mutationFn: () => {
      const t = texto.trim();
      if (t.length < 1) throw new Error('Texto obrigatório.');
      return comunicacoesApi.create(reparacaoId, { tipo, direcao, texto: t });
    },
    onSuccess: () => {
      setTexto('');
      setOpen(false);
      qc.invalidateQueries({ queryKey: ['comunicacoes', reparacaoId] });
      toast.success('Registado.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao registar.'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => comunicacoesApi.remove(reparacaoId, id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['comunicacoes', reparacaoId] }),
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao apagar.'),
  });

  const items = list.data ?? [];

  // Sprint 485 (Doc 91): cliente escreveu pelo portal e aguarda resposta? — a última mensagem
  // PortalCliente é Inbound sem Outbound posterior. Mostra banner no topo (par do badge S484
  // no board) para que, ao abrir a reparação, o staff veja logo que tem de responder.
  const portalMsgs = items.filter((c) => c.tipo === ComunicacaoTipo.PortalCliente);
  const ultimaPortal = portalMsgs[0]; // items vêm desc por createdAt (repo OrderByDescending)
  const clienteAguardaResposta = ultimaPortal?.direcao === ComunicacaoDirecao.Inbound;

  // WhatsApp link rápido (número PT normalizado 351). Útil porque é o canal #1 de comunicação no balcão.
  const waNumber = clienteTelefone ? normalizeWaNumber(clienteTelefone) : null;
  const waLink = waNumber ? `https://wa.me/${waNumber}` : null;

  // Sprint 459: usa os templates configurados em /definicoes (TenantPreferences.Communication.TemplatesByState)
  // em vez de mensagens hardcoded. Bruno editou um template? — passa a usar. Fallback para defaults pt-PT do S456/S457.
  // Os queries são staleTime longo: prefs e tenant mudam pouco, não vale a pena refetch agressivo.
  const prefs = useQuery({
    queryKey: ['tenant-preferences'],
    queryFn: () => tenantPreferencesApi.get(),
    staleTime: 5 * 60_000,
  });
  const tenant = useQuery({
    queryKey: ['tenant-settings'],
    queryFn: () => tenantSettingsApi.getMine(),
    staleTime: 5 * 60_000,
  });

  // Sprint 456+457 (Doc 91 follow-up): CTA contextual "Avisar cliente" por estado.
  // Estados cobertos: 1 (Diagnóstico), 2 (AguardaPeça), 4 (Pronto). Click abre WhatsApp
  // pré-preenchido + regista comunicação outbound automaticamente, fechando o loop S452.
  const aviso = reparacaoEstado != null && waNumber
    ? buildAvisoPorEstado(reparacaoEstado, {
        clienteNome,
        equipamento: reparacaoEquipamento,
        numero: reparacaoNumero,
        lojaNome: tenant.data?.name,
        templateTexto: getTemplateTexto(prefs.data?.communication.templatesByState, reparacaoEstado),
      })
    : null;
  const waAvisoLink = aviso && waNumber ? `https://wa.me/${waNumber}?text=${encodeURIComponent(aviso.mensagem)}` : null;

  const avisarCliente = useMutation({
    mutationFn: () =>
      comunicacoesApi.create(reparacaoId, {
        tipo: ComunicacaoTipo.WhatsApp,
        direcao: ComunicacaoDirecao.Outbound,
        texto: aviso ? `${aviso.notaLog}\n\n[Mensagem enviada]\n${aviso.mensagem}` : 'Avisei via WhatsApp.',
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['comunicacoes', reparacaoId] });
      toast.success('Aviso registado.', 'Abriu o WhatsApp com a mensagem pré-feita.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro a registar aviso.'),
  });

  // Sprint 471: par CTA Email do CTA WhatsApp. Útil para clientes que preferem email
  // ou quando não há telefone. Usa mesmo aviso da reparação (mensagem por estado).
  const subjectByEstado: Record<number, string> = {
    1: `Diagnóstico concluído${reparacaoNumero ? ` — Ref. R-${String(reparacaoNumero).padStart(5, '0')}` : ''}`,
    2: `Peça encomendada${reparacaoNumero ? ` — Ref. R-${String(reparacaoNumero).padStart(5, '0')}` : ''}`,
    4: `Reparação pronta para levantar${reparacaoNumero ? ` — Ref. R-${String(reparacaoNumero).padStart(5, '0')}` : ''}`,
  };
  const emailSubject = reparacaoEstado != null ? subjectByEstado[reparacaoEstado] : undefined;
  const mailtoLink = clienteEmail && aviso && emailSubject
    ? `mailto:${clienteEmail}?subject=${encodeURIComponent(emailSubject)}&body=${encodeURIComponent(aviso.mensagem)}`
    : null;

  const avisarClienteEmail = useMutation({
    mutationFn: () =>
      comunicacoesApi.create(reparacaoId, {
        tipo: ComunicacaoTipo.Email,
        direcao: ComunicacaoDirecao.Outbound,
        texto: aviso ? `${aviso.notaLog.replace('via WhatsApp', 'via Email')}\n\n[Assunto] ${emailSubject}\n[Mensagem]\n${aviso.mensagem}` : 'Avisei via email.',
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['comunicacoes', reparacaoId] });
      toast.success('Aviso registado.', 'Abriu o cliente de email com a mensagem pré-feita.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro a registar aviso.'),
  });

  return (
    <section className="space-y-3 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold">
          <MessageCircle size={16} /> Comunicações
          {items.length > 0 && (
            <span className="rounded-full bg-zinc-100 px-1.5 py-0.5 text-[10px] font-medium text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">
              {items.length}
            </span>
          )}
        </h2>
        <div className="flex items-center gap-1.5">
          {/* Sprint 488: cliente pediu para não ser contactado (RGPD, S479) — esconde CTAs proativos. */}
          {clienteNaoContactar && (
            <span title="O cliente pediu para não ser contactado (RGPD)" className="inline-flex items-center gap-1 rounded-md bg-rose-100 px-2 py-1 text-[11px] font-semibold text-rose-700 dark:bg-rose-950/40 dark:text-rose-300">
              🚫 Não contactar
            </span>
          )}
          {/* Sprint 488: canal preferido do cliente (S479) — dica para o staff escolher por onde contactar. */}
          {!clienteNaoContactar && clienteContactoPreferido && (
            <span title="Canal de contacto preferido pelo cliente" className="inline-flex items-center gap-1 rounded-md bg-zinc-100 px-2 py-1 text-[11px] font-medium text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
              Prefere: {clienteContactoPreferido}
            </span>
          )}
          {/* Sprint 456+457: CTA contextual por estado (Diagnóstico/AguardaPeça/Pronto). */}
          {!clienteNaoContactar && waAvisoLink && aviso && (
            <a
              href={waAvisoLink}
              target="_blank"
              rel="noopener noreferrer"
              onClick={() => avisarCliente.mutate()}
              className={`inline-flex items-center gap-1 rounded-md px-2.5 py-1 text-[11px] font-semibold text-white ${aviso.cor}`}
              title={`Abrir WhatsApp com "${aviso.label}" pré-feito e registar como Outbound`}
            >
              <Send size={12} /> {aviso.label}
            </a>
          )}
          {/* Sprint 471: par Email — só visible quando há email + estado comunicável.
              Sprint 489: se o cliente prefere Email (S479), promove a primário (filled) e order-first. */}
          {!clienteNaoContactar && mailtoLink && aviso && (
            <a
              href={mailtoLink}
              onClick={() => avisarClienteEmail.mutate()}
              className={`inline-flex items-center gap-1 rounded-md px-2 py-1 text-[11px] font-medium ${
                (clienteContactoPreferido ?? '').toLowerCase() === 'email'
                  ? 'order-first bg-indigo-600 text-white hover:bg-indigo-700'
                  : 'border border-indigo-300 text-indigo-700 hover:bg-indigo-50 dark:border-indigo-800/60 dark:text-indigo-300 dark:hover:bg-indigo-950/30'
              }`}
              title="Abrir cliente de email com mensagem pré-feita e registar como Outbound"
            >
              <Mail size={12} /> Email
            </a>
          )}
          {!clienteNaoContactar && waLink && !waAvisoLink && (
            <a
              href={waLink}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1 rounded-md border border-emerald-200 px-2 py-1 text-[11px] font-medium text-emerald-700 hover:bg-emerald-50 dark:border-emerald-900/40 dark:text-emerald-300 dark:hover:bg-emerald-950/30"
              title="Abrir WhatsApp do cliente"
            >
              <MessageCircle size={12} /> WhatsApp
            </a>
          )}
          {/* Sprint 482: responder ao cliente no portal. Pré-define canal=Portal + Enviada,
              que o cliente vê no fio de conversa em /r/{slug}. */}
          {!open && (
            <button
              type="button"
              onClick={() => { setTipo(ComunicacaoTipo.PortalCliente); setDirecao(ComunicacaoDirecao.Outbound); setOpen(true); }}
              className="inline-flex items-center gap-1 rounded-md border border-rose-200 px-2 py-1 text-[11px] font-medium text-rose-700 hover:bg-rose-50 dark:border-rose-900/40 dark:text-rose-300 dark:hover:bg-rose-950/30"
              title="Escrever uma resposta visível para o cliente no portal público"
            >
              <MessageCircle size={12} /> Responder no portal
            </button>
          )}
          {!open && (
            <Button size="sm" onClick={() => setOpen(true)} leftIcon={<Plus size={14} />}>
              Registar
            </Button>
          )}
        </div>
      </div>

      {/* Sprint 485 (Doc 91): cliente escreveu pelo portal e aguarda resposta — banner + CTA. */}
      {clienteAguardaResposta && !open && (
        <button
          type="button"
          onClick={() => { setTipo(ComunicacaoTipo.PortalCliente); setDirecao(ComunicacaoDirecao.Outbound); setOpen(true); }}
          className="flex w-full items-start gap-2.5 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-left transition hover:bg-rose-100 dark:border-rose-900/40 dark:bg-rose-950/30 dark:hover:bg-rose-950/50"
        >
          <MessageCircle size={16} className="mt-0.5 flex-none text-rose-600 dark:text-rose-400" />
          <span className="min-w-0 flex-1">
            <span className="block text-sm font-medium text-rose-900 dark:text-rose-100">O cliente escreveu pelo portal e aguarda resposta</span>
            <span className="mt-0.5 block truncate text-xs text-rose-700/80 dark:text-rose-300/80">“{ultimaPortal.texto}”</span>
            <span className="mt-0.5 block text-[11px] font-medium text-rose-700 dark:text-rose-300">Clica para responder no portal →</span>
          </span>
        </button>
      )}

      {open && (
        <form
          className="space-y-2 rounded-lg border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-950"
          onSubmit={(e) => { e.preventDefault(); create.mutate(); }}
        >
          <div className="flex flex-wrap gap-2">
            <Select
              label="Canal"
              value={tipo}
              onChange={(v) => setTipo(v as ComunicacaoTipo)}
              options={[
                ComunicacaoTipo.Telefone,
                ComunicacaoTipo.WhatsApp,
                ComunicacaoTipo.Email,
                ComunicacaoTipo.Sms,
                ComunicacaoTipo.Visita,
                ComunicacaoTipo.Nota,
                ComunicacaoTipo.PortalCliente,
              ].map((t) => ({ value: t, label: COMUNICACAO_TIPO_LABEL[t] }))}
            />
            <Select
              label="Direção"
              value={direcao}
              onChange={(v) => setDirecao(v as ComunicacaoDirecao)}
              options={[
                { value: ComunicacaoDirecao.Inbound, label: 'Recebida (cliente → nós)' },
                { value: ComunicacaoDirecao.Outbound, label: 'Enviada (nós → cliente)' },
                { value: ComunicacaoDirecao.Interna, label: 'Nota interna (sem cliente)' },
              ]}
            />
          </div>
          <textarea
            className="w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
            placeholder="O que foi falado / enviado / acordado…"
            rows={3}
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            autoFocus
            maxLength={2000}
          />
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => { setOpen(false); setTexto(''); }} className="rounded-md px-3 py-1.5 text-xs text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800">
              Cancelar
            </button>
            <Button type="submit" size="sm" loading={create.isPending} disabled={texto.trim().length < 1}>
              Guardar
            </Button>
          </div>
        </form>
      )}

      {items.length === 0 && !list.isLoading && !open && (
        <p className="flex items-start gap-2 text-xs text-zinc-500">
          <FileText size={12} className="mt-0.5 flex-none" />
          <span>Sem registos. Cada vez que falares com o cliente sobre esta reparação (telefone, WhatsApp, email) regista aqui — fica rasto para a próxima.</span>
        </p>
      )}

      {items.length > 0 && (
        <ul className="space-y-2">
          {items.map((c) => (
            <ComunicacaoRow key={c.id} entry={c} onDelete={() => remove.mutate(c.id)} />
          ))}
        </ul>
      )}
    </section>
  );
}

function ComunicacaoRow({
  entry,
  onDelete,
}: {
  entry: ReparacaoComunicacao;
  onDelete: () => void;
}) {
  const Icon = TIPO_ICON[entry.tipo];
  const isInbound = entry.direcao === ComunicacaoDirecao.Inbound;
  const isInterna = entry.direcao === ComunicacaoDirecao.Interna;
  return (
    <li className="rounded-lg border border-zinc-100 bg-zinc-50/50 p-3 text-sm dark:border-zinc-800 dark:bg-zinc-950/40">
      <div className="flex items-start gap-2.5">
        <span className={`mt-0.5 inline-flex h-7 w-7 flex-none items-center justify-center rounded-full ${TIPO_COR[entry.tipo]}`}>
          <Icon size={14} />
        </span>
        <div className="min-w-0 flex-1">
          <div className="mb-0.5 flex flex-wrap items-center gap-1.5 text-[11px] text-zinc-500">
            <span className="font-semibold text-zinc-700 dark:text-zinc-300">{COMUNICACAO_TIPO_LABEL[entry.tipo]}</span>
            {!isInterna && (
              <span className="inline-flex items-center gap-0.5 text-zinc-500">
                {isInbound ? <ArrowDownLeft size={11} /> : <ArrowUpRight size={11} />}
                {COMUNICACAO_DIRECAO_LABEL[entry.direcao]}
              </span>
            )}
            {isInterna && <span className="rounded bg-zinc-200 px-1 py-0.5 text-[10px] text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">Nota</span>}
            <span>·</span>
            <span>{new Date(entry.createdAt).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}</span>
          </div>
          <p className="whitespace-pre-wrap break-words">{entry.texto}</p>
        </div>
        <button
          type="button"
          onClick={onDelete}
          className="flex-none text-zinc-400 hover:text-rose-600 dark:hover:text-rose-400"
          title="Apagar"
        >
          <Trash2 size={13} />
        </button>
      </div>
    </li>
  );
}

function Select({
  label,
  value,
  onChange,
  options,
}: {
  label: string;
  value: number;
  onChange: (v: number) => void;
  options: Array<{ value: number; label: string }>;
}) {
  return (
    <label className="flex flex-col gap-1 text-xs">
      <span className="text-zinc-600 dark:text-zinc-400">{label}</span>
      <select
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        className="rounded-lg border border-zinc-200 bg-white px-2 py-1.5 text-sm outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </label>
  );
}

/**
 * Sprint 456+457+459: mensagens por estado.
 *
 * Estratégia S459: se houver template configurado em /definicoes
 * (TenantPreferences.Communication.TemplatesByState) e estiver enabled, USA-O.
 * Senão fallback para o default hardcoded (Sprint 456/457).
 *
 * Em ambos os casos substitui placeholders {{cliente_nome}}, {{equipamento}},
 * {{loja_nome}}. Outros placeholders (valor, prazo, peca_nome, horario) ficam
 * preservados literais para o Bruno editar no WhatsApp antes de enviar.
 *
 * Devolve null para estados sem CTA específico.
 */
function buildAvisoPorEstado(
  estado: number,
  opts: { clienteNome?: string; equipamento?: string; numero?: number; lojaNome?: string; templateTexto?: string | null },
): { mensagem: string; label: string; cor: string; notaLog: string } | null {
  const meta = STATE_AVISO_META[estado];
  if (!meta) return null;

  // S459: template do tenant tem prioridade.
  const base = opts.templateTexto && opts.templateTexto.trim().length > 0
    ? opts.templateTexto
    : meta.defaultTemplate(opts.numero);

  const mensagem = applyPlaceholders(base, {
    clienteNome: opts.clienteNome,
    equipamento: opts.equipamento,
    lojaNome: opts.lojaNome,
  });

  return { mensagem, label: meta.label, cor: meta.cor, notaLog: meta.notaLog };
}

/**
 * Sprint 459: substitui {{cliente_nome}}, {{equipamento}}, {{loja_nome}} num template.
 * Outros placeholders ({{valor}}, {{prazo_estimado}}, {{peca_nome}}, etc) ficam intactos —
 * o staff edita-os no WhatsApp antes de enviar (wa.me/?text= só pré-preenche).
 */
function applyPlaceholders(template: string, opts: { clienteNome?: string; equipamento?: string; lojaNome?: string }): string {
  return template
    .replace(/\{\{\s*cliente_nome\s*\}\}/gi, opts.clienteNome?.split(' ')[0] ?? 'cliente')
    .replace(/\{\{\s*equipamento\s*\}\}/gi, opts.equipamento ?? 'equipamento')
    .replace(/\{\{\s*loja_nome\s*\}\}/gi, opts.lojaNome ?? 'loja');
}

/** Sprint 459: meta por estado — label/cor para o botão, notaLog para o histórico, default template. */
const STATE_AVISO_META: Record<number, { label: string; cor: string; notaLog: string; defaultTemplate: (numero?: number) => string }> = {
  1: {
    label: 'Avisar diagnóstico',
    cor: 'bg-sky-600 hover:bg-sky-700',
    notaLog: 'Avisei cliente via WhatsApp que o diagnóstico está concluído.',
    defaultTemplate: (n) => `Olá {{cliente_nome}}, terminámos o diagnóstico do seu {{equipamento}}${refSuffix(n)}. Vamos enviar o orçamento em breve para aprovação. Obrigado!`,
  },
  2: {
    label: 'Avisar peça',
    cor: 'bg-amber-600 hover:bg-amber-700',
    notaLog: 'Avisei cliente via WhatsApp que a peça foi encomendada.',
    defaultTemplate: (n) => `Olá {{cliente_nome}}, a peça para a reparação do seu {{equipamento}} foi encomendada${refSuffix(n)}. Estimativa de chegada: 3 a 5 dias úteis. Damos novidades assim que receber. Obrigado pela paciência!`,
  },
  4: {
    label: 'Avisar pronto',
    cor: 'bg-emerald-600 hover:bg-emerald-700',
    notaLog: 'Avisei cliente via WhatsApp que a reparação está pronta para levantar.',
    defaultTemplate: (n) => `Olá {{cliente_nome}}, a reparação do seu {{equipamento}} está pronta para levantar na {{loja_nome}}${refSuffix(n)}. Aguardamos a sua visita. Obrigado!`,
  },
};

function refSuffix(numero?: number): string {
  return numero != null ? ` (Ref. R-${String(numero).padStart(5, '0')})` : '';
}

/** Sprint 459: lookup do texto de template para o estado da reparação. Devolve null se não enabled ou não existe. */
function getTemplateTexto(
  templates: Record<string, { enabled: boolean; texto: string }> | undefined,
  estado: number,
): string | null {
  if (!templates) return null;
  const key = STATE_KEY_BY_INT[estado];
  if (!key) return null;
  const t = templates[key];
  return t && t.enabled ? t.texto : null;
}

const STATE_KEY_BY_INT: Record<number, string> = {
  0: 'Recebido',
  1: 'Diagnostico',
  2: 'AguardaPeca',
  3: 'EmReparacao',
  4: 'Pronto',
  5: 'Entregue',
  6: 'Cancelado',
  7: 'Orcamento',
};

/** Normaliza um telefone PT para `wa.me/351XXXXXXXXX`. Aceita "+351 9X X XX XX" etc. */
function normalizeWaNumber(raw: string): string | null {
  const digits = raw.replace(/\D/g, '');
  if (!digits) return null;
  if (digits.startsWith('351')) return digits;
  if (digits.length === 9 && digits[0] === '9') return `351${digits}`;
  return digits;
}
