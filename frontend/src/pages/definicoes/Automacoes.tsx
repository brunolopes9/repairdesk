import { Workflow, ExternalLink, Mail, Server, Camera, CheckCircle2, AlertCircle, Copy, RefreshCw, Wrench, CalendarCheck, Smartphone } from 'lucide-react';
import { useEffect, useState } from 'react';
import { api } from '../../lib/api';
import { toast } from '../../lib/toast';
import { tacDbApi } from '../../lib/imeiLookup';
import { downloadFile } from '../../lib/downloadPdf';
import { ShieldAlert } from 'lucide-react';

/**
 * Sprint 165: doc page para configurar automações (n8n + ingest IMAP/SFTP).
 *
 * Por agora é descritivo — não integra com API n8n (complica multi-tenant + cada
 * tenant pode ter n8n self-hosted ou central). Bruno usa o painel n8n nativo
 * para gerir workflows; esta página dá-lhe a documentação + URL + status básico.
 */
export default function Automacoes() {
  const n8nUrl = 'http://localhost:5678';
  const [n8nStatus, setN8nStatus] = useState<'unknown' | 'up' | 'down' | 'checking'>('unknown');

  async function checkN8n() {
    setN8nStatus('checking');
    try {
      const res = await api.get<{ up: boolean }>('/automacoes/n8n-status');
      setN8nStatus(res.data.up ? 'up' : 'down');
    } catch {
      setN8nStatus('down');
    }
  }

  // Sprint 173: email forwarding ingest (load on mount).
  const [ingestEmail, setIngestEmail] = useState<{ email: string; slug: string; domain: string } | null>(null);
  useEffect(() => {
    // Sprint 253 (Doc 77): antes era catch silencioso com comentário "só admins" — mas
    // se o endpoint falha por OUTRA razão (DB down, 500, network), user fica sem feedback.
    // Agora: 403 silencioso (não-admin esperado), restos reportam a Sentry sem toast
    // (não-bloqueante — secção mostra placeholder vazio sem este dado).
    api.get<{ email: string; slug: string; domain: string }>('/automacoes/ingest-email')
      .then((r) => setIngestEmail(r.data))
      .catch((err) => {
        if (err?.response?.status === 403 || err?.response?.status === 401) return;
        import('@sentry/react').then((Sentry) =>
          Sentry.captureException(err, { tags: { feature: 'automacoes.ingest-email' } }),
        );
      });
  }, []);

  // Sprint 354: widget de pedido de reparação público.
  const [intake, setIntake] = useState<{ slug: string; publicUrl: string | null } | null>(null);
  useEffect(() => {
    api.get<{ slug: string; publicUrl: string | null }>('/automacoes/intake-widget')
      .then((r) => setIntake(r.data))
      .catch((err) => {
        if (err?.response?.status === 403 || err?.response?.status === 401) return;
        import('@sentry/react').then((Sentry) =>
          Sentry.captureException(err, { tags: { feature: 'automacoes.intake-widget' } }),
        );
      });
  }, []);

  async function copyIntakeUrl() {
    if (!intake?.publicUrl) return;
    await navigator.clipboard.writeText(intake.publicUrl);
    toast.success('Link copiado.');
  }

  async function copyEmail() {
    if (!ingestEmail) return;
    try {
      await navigator.clipboard.writeText(ingestEmail.email);
      toast.success('Email copiado para clipboard.');
    } catch { toast.warning('Selecciona e copia manualmente.'); }
  }

  async function regenerateEmail() {
    if (!confirm('Regenerar o email? O actual deixará de funcionar — vais ter que actualizar os forwards.')) return;
    try {
      const r = await api.post<{ email: string; slug: string }>('/automacoes/ingest-email/regenerate');
      setIngestEmail({ ...(ingestEmail ?? { domain: '' }), ...r.data });
      toast.success('Email regenerado.');
    } catch (e) {
      toast.fromError(e, 'Falhou regenerar.');
    }
  }

  return (
    <div className="mx-auto max-w-5xl space-y-4 px-4 py-6">
      <header className="space-y-2">
        <h1 className="flex items-center gap-2 text-2xl font-semibold">
          <Workflow size={24} strokeWidth={2} />
          Automações
        </h1>
        <p className="text-sm text-zinc-500">
          Workflows de automação que correm fora do Mender (n8n) e alimentam o sistema com dados
          de fornecedores. Configura uma vez, deixa correr em background.
        </p>
      </header>

      {/* Sprint 354: widget público de pedido de reparação. */}
      {intake && (
        <section className="rounded-xl border border-sky-200 bg-sky-50/30 p-4 dark:border-sky-900/40 dark:bg-sky-950/20">
          <h2 className="flex items-center gap-2 text-sm font-semibold">
            <Wrench size={16} />
            Widget de pedido online
          </h2>
          <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
            Partilha este link no teu website ou redes sociais. Os clientes preenchem o problema e o pedido
            aparece em <strong>Pedidos online</strong> para converteres em reparação.
          </p>
          {intake.publicUrl ? (
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <code className="rounded bg-white px-3 py-1.5 text-sm font-mono shadow-sm dark:bg-zinc-900">{intake.publicUrl}</code>
              <button type="button" onClick={copyIntakeUrl} className="inline-flex items-center gap-1 rounded-lg border border-zinc-300 px-2 py-1.5 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800">
                <Copy size={12} /> Copiar
              </button>
              <a href={intake.publicUrl} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 rounded-lg border border-zinc-300 px-2 py-1.5 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800">
                <ExternalLink size={12} /> Abrir
              </a>
            </div>
          ) : (
            <p className="mt-2 text-xs text-amber-600">
              Configura <code>Frontend:PortalBaseUrl</code> no servidor para gerar o link público. Slug: <code>{intake.slug}</code>
            </p>
          )}
        </section>
      )}

      {/* Sprint 389: link público de marcação online (booking). Deriva do mesmo slug do widget. */}
      {intake?.publicUrl && (
        <section className="rounded-xl border border-emerald-200 bg-emerald-50/30 p-4 dark:border-emerald-900/40 dark:bg-emerald-950/20">
          <h2 className="flex items-center gap-2 text-sm font-semibold">
            <CalendarCheck size={16} />
            Marcação online
          </h2>
          <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
            Partilha este link para os clientes marcarem hora. As marcações aparecem em <strong>Agendamentos</strong> e
            recebes notificação no telemóvel.
          </p>
          {(() => {
            const bookingUrl = intake.publicUrl!.replace('/pedido/', '/agendar/');
            return (
              <div className="mt-2 flex flex-wrap items-center gap-2">
                <code className="rounded bg-white px-3 py-1.5 text-sm font-mono shadow-sm dark:bg-zinc-900">{bookingUrl}</code>
                <button type="button" onClick={() => navigator.clipboard?.writeText(bookingUrl)} className="inline-flex items-center gap-1 rounded-lg border border-zinc-300 px-2 py-1.5 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800">
                  <Copy size={12} /> Copiar
                </button>
                <a href={bookingUrl} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 rounded-lg border border-zinc-300 px-2 py-1.5 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800">
                  <ExternalLink size={12} /> Abrir
                </a>
              </div>
            );
          })()}
        </section>
      )}

      {/* Sprint 390: base TAC para auto-detetar modelo a partir do IMEI. */}
      <TacDbCard />

      {/* Sprint 391: export da lista de IMEI de usados para a PSP. */}
      <PspExportCard />

      {/* Sprint 173: email forwarding ingest per-tenant. */}
      {ingestEmail && (
        <section className="rounded-xl border border-brand-200 bg-brand-50/30 p-4 dark:border-brand-900/40 dark:bg-brand-950/20">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="flex-1">
              <h2 className="flex items-center gap-2 text-sm font-semibold">
                <Mail size={16} />
                Email de import automático
                <span className="rounded bg-emerald-600 px-2 py-0.5 text-[10px] uppercase text-white">Recomendado</span>
              </h2>
              <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
                Reencaminha emails de fornecedores para este endereço — vão para "Importações pendentes" automaticamente.
                Mais simples que IMAP, RGPD-clean (controlas o que reencaminhas).
              </p>
              <div className="mt-2 flex items-center gap-2">
                <code className="rounded bg-white px-3 py-1.5 text-sm font-mono shadow-sm dark:bg-zinc-900">
                  {ingestEmail.email}
                </code>
                <button
                  type="button"
                  onClick={copyEmail}
                  className="flex items-center gap-1 rounded-md border border-zinc-300 bg-white px-2 py-1.5 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900"
                  title="Copiar"
                >
                  <Copy size={12} /> Copiar
                </button>
                <button
                  type="button"
                  onClick={regenerateEmail}
                  className="flex items-center gap-1 rounded-md border border-zinc-300 bg-white px-2 py-1.5 text-xs text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900"
                  title="Regenerar — o actual deixa de funcionar"
                >
                  <RefreshCw size={12} /> Regenerar
                </button>
              </div>
              <details className="mt-3">
                <summary className="cursor-pointer text-xs font-medium text-brand-700 dark:text-brand-300">
                  Como configurar no Gmail (passo-a-passo)
                </summary>
                <ol className="mt-2 space-y-1 pl-5 text-xs text-zinc-600 dark:text-zinc-400 list-decimal">
                  <li>Gmail → Definições → "Encaminhamento e POP/IMAP"</li>
                  <li>"Adicionar endereço de encaminhamento" → cola <code>{ingestEmail.email}</code></li>
                  <li>Confirma o email de verificação que Gmail manda</li>
                  <li>Cria filtro: "From contém" <code>@tudo4mobile.pt</code> OR <code>@utopya.com</code> OR <code>@molano</code> → "Reencaminhar para" {ingestEmail.email}</li>
                  <li>Pronto. Próximas faturas aparecem em <a href="/importacoes" className="underline">/importacoes</a></li>
                </ol>
              </details>
            </div>
          </div>
        </section>
      )}

      {/* Estado n8n */}
      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="flex items-center gap-2 text-sm font-semibold">
              <Server size={16} />
              Servidor n8n
            </h2>
            <p className="mt-1 text-xs text-zinc-500">
              n8n é a infra de workflows que processa emails IMAP, SFTP e webhooks. Corre num container
              docker próprio (profile <code className="rounded bg-zinc-100 px-1 dark:bg-zinc-800">automation</code>).
            </p>
            <div className="mt-2 text-xs">
              URL: <a href={n8nUrl} target="_blank" rel="noreferrer" className="font-mono text-brand-600 underline dark:text-brand-400">{n8nUrl}</a>
              <a href={n8nUrl} target="_blank" rel="noreferrer" className="ml-1 inline-flex items-center text-brand-600 dark:text-brand-400">
                <ExternalLink size={11} />
              </a>
            </div>
            <div className="mt-2 flex items-center gap-2 text-xs">
              {n8nStatus === 'up' && <span className="flex items-center gap-1 text-emerald-600"><CheckCircle2 size={14} /> A funcionar</span>}
              {n8nStatus === 'down' && <span className="flex items-center gap-1 text-rose-600"><AlertCircle size={14} /> Não responde — corre <code className="rounded bg-zinc-100 px-1 dark:bg-zinc-800">docker compose --profile automation up -d</code></span>}
              {n8nStatus === 'checking' && <span className="text-zinc-500">A verificar…</span>}
              {n8nStatus === 'unknown' && <span className="text-zinc-500">Estado desconhecido</span>}
            </div>
          </div>
          <button
            type="button"
            onClick={checkN8n}
            className="rounded-md border border-zinc-300 bg-white px-3 py-1.5 text-xs font-medium text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300 dark:hover:bg-zinc-800"
          >
            Verificar estado
          </button>
        </div>
      </section>

      {/* Workflows recomendados */}
      <section className="space-y-3">
        <h2 className="text-sm font-semibold text-zinc-700 dark:text-zinc-300">Workflows recomendados</h2>

        <WorkflowCard
          icon={Mail}
          title="IMAP Ingest — fatura fornecedor"
          description="Lê novos emails da tua caixa, detecta anexos PDF de fornecedores (T4M, Utopya, Molano, …), envia para o endpoint /api/external/supplier-invoices/ingest."
          docPath="Contexto/56-Setup-IMAP-Ingest-Passo-a-Passo.md"
          steps={[
            'Cria workflow novo em n8n com IMAP Trigger',
            'Configura credenciais IMAP do teu Gmail/Outlook',
            'Filtra: from contém "@tudo4mobile" OU "@utopya" OU "@molano" (ou outros)',
            'Extrai anexos + body HTML',
            'HTTP POST com API key Mender (cria em /definicoes → chaves)',
          ]}
        />

        <WorkflowCard
          icon={Camera}
          title="Foto papel mobile → Mender"
          description="Alternativa: usa o telemóvel para fotografar fatura papel directamente em /importacoes. Não precisa de n8n. Claude Vision faz OCR."
          docPath="(Sprint 164 — já disponível em /importacoes → '📷 Foto papel')"
          steps={[
            'Abre Mender no telemóvel',
            'Vai a /importacoes',
            'Clica "📷 Foto papel" — câmara abre directamente',
            'Tira foto da fatura',
            'Claude Vision faz OCR — fica em "Pendentes" para aprovares',
          ]}
        />

        <WorkflowCard
          icon={Server}
          title="SFTP Molano — sync CSV diário"
          description="Quando Molano disponibilizar SFTP, n8n pull do CSV todas as 6h e alimenta importação automática de produtos."
          docPath="Contexto/59-Molano-SFTP-Automation.md"
          steps={[
            'Aguardar credenciais SFTP Molano',
            'n8n: Schedule trigger 6h + SFTP node',
            'Hash SHA256 para dedupe (skip se igual ao último)',
            'POST /api/products/import-molano',
            'Erros visíveis em /importacoes',
          ]}
        />
      </section>

      <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900 dark:border-amber-900/40 dark:bg-amber-950/30 dark:text-amber-200">
        <strong>Multi-tenant SaaS:</strong> hoje n8n é central (1 instância para todos os tenants). Cada
        tenant tem o seu próprio workflow IMAP no mesmo n8n. Para isolamento total, futuro Sprint vai
        permitir n8n self-hosted por tenant.
      </div>
    </div>
  );
}

function WorkflowCard({
  icon: Icon, title, description, docPath, steps,
}: {
  icon: React.ComponentType<{ size?: number; strokeWidth?: number }>;
  title: string;
  description: string;
  docPath: string;
  steps: string[];
}) {
  const [expanded, setExpanded] = useState(false);
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <button
        type="button"
        onClick={() => setExpanded(!expanded)}
        className="flex w-full items-start justify-between gap-3 text-left"
      >
        <div className="flex flex-1 items-start gap-3">
          <Icon size={20} strokeWidth={2} />
          <div>
            <div className="text-sm font-semibold">{title}</div>
            <div className="mt-1 text-xs text-zinc-500">{description}</div>
            <div className="mt-1 text-[11px] text-zinc-400">
              📄 {docPath}
            </div>
          </div>
        </div>
        <span className="text-zinc-400">{expanded ? '▾' : '▸'}</span>
      </button>
      {expanded && (
        <ol className="mt-3 space-y-1.5 border-t border-zinc-100 pt-3 text-xs dark:border-zinc-800">
          {steps.map((s, i) => (
            <li key={i} className="flex gap-2">
              <span className="font-mono text-zinc-400">{i + 1}.</span>
              <span>{s}</span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

/**
 * Sprint 390 (Doc 04): card admin para importar a base TAC (auto-detetar modelo do IMEI) e ver
 * quantas entradas estão carregadas. Importa um CSV "tac;marca;modelo" (dump aberto Osmocom/MoazEb).
 */
function TacDbCard() {
  const [count, setCount] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    tacDbApi.status().then((s) => setCount(s.count)).catch(() => { /* não-admin ou erro: ignora */ });
  }, []);

  async function onFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    setBusy(true);
    try {
      const res = await tacDbApi.import(file);
      setCount(res.count);
      toast.success(`Base TAC importada — ${res.count} modelos`);
    } catch (err) {
      toast.fromError(err, 'Não foi possível importar a base TAC.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="rounded-xl border border-violet-200 bg-violet-50/30 p-4 dark:border-violet-900/40 dark:bg-violet-950/20">
      <h2 className="flex items-center gap-2 text-sm font-semibold">
        <Smartphone size={16} />
        Auto-detetar modelo por IMEI (base TAC)
        {count != null && <span className="rounded bg-violet-600 px-2 py-0.5 text-[10px] uppercase text-white">{count} modelos</span>}
      </h2>
      <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
        Ao escrever um IMEI na venda, o Mender deteta a marca e modelo automaticamente — offline, sem custos.
        Importa uma vez um <strong>dump aberto TAC</strong> em CSV <code>tac;marca;modelo</code>
        {' '}(<a className="underline" href="http://tacdb.osmocom.org/" target="_blank" rel="noopener noreferrer">Osmocom CC-BY-SA</a>
        {' '}ou <a className="underline" href="https://github.com/MoazEb/tac-database" target="_blank" rel="noopener noreferrer">MoazEb</a>).
      </p>
      <label className="mt-3 inline-flex cursor-pointer items-center gap-1.5 rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-xs font-medium hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900">
        <ExternalLink size={12} /> {busy ? 'A importar…' : 'Importar CSV TAC'}
        <input type="file" accept=".csv,text/csv" className="hidden" onChange={onFile} disabled={busy} />
      </label>
    </section>
  );
}

/**
 * Sprint 391 (Doc 04): export da lista de IMEI de equipamentos vendidos num período, para o
 * retalhista enviar à PSP (em PT não há API — é envio periódico manual). Inclui link-out CheckMEND
 * para verificação manual de roubados, já que não existe verificação automática gratuita.
 */
function PspExportCard() {
  const [from, setFrom] = useState(() => {
    const d = new Date();
    d.setMonth(d.getMonth() - 1);
    return d.toISOString().slice(0, 10);
  });
  const [to, setTo] = useState(() => new Date().toISOString().slice(0, 10));
  const [busy, setBusy] = useState(false);

  async function baixar() {
    setBusy(true);
    try {
      await downloadFile(`/imei/psp-export.csv?from=${from}T00:00:00Z&to=${to}T23:59:59Z`, `psp-imei_${from}_${to}.csv`);
    } catch (err) {
      toast.fromError(err, 'Não foi possível gerar a lista.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="rounded-xl border border-amber-200 bg-amber-50/30 p-4 dark:border-amber-900/40 dark:bg-amber-950/20">
      <h2 className="flex items-center gap-2 text-sm font-semibold">
        <ShieldAlert size={16} />
        Lista de IMEI para a PSP
      </h2>
      <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
        Em Portugal não há base de dados nacional de roubados com API — os retalhistas de usados enviam
        periodicamente à PSP a lista de IMEI que transaccionaram. Exporta aqui essa lista (a partir das vendas
        com IMEI) e envia. Para uma verificação manual de roubados podes consultar o{' '}
        <a className="underline" href="https://www.checkmend.com/" target="_blank" rel="noopener noreferrer">CheckMEND</a>.
      </p>
      <div className="mt-3 flex flex-wrap items-center gap-2 text-xs">
        <span className="text-zinc-500">De</span>
        <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="rounded-md border border-zinc-300 px-2 py-1.5 dark:border-zinc-700 dark:bg-zinc-900" />
        <span className="text-zinc-500">até</span>
        <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="rounded-md border border-zinc-300 px-2 py-1.5 dark:border-zinc-700 dark:bg-zinc-900" />
        <button type="button" onClick={baixar} disabled={busy} className="inline-flex items-center gap-1.5 rounded-lg bg-zinc-900 px-3 py-1.5 font-medium text-white hover:bg-zinc-800 disabled:opacity-50 dark:bg-zinc-100 dark:text-zinc-900">
          <ExternalLink size={12} /> {busy ? 'A gerar…' : 'Exportar CSV'}
        </button>
      </div>
    </section>
  );
}
