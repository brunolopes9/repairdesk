import { Workflow, ExternalLink, Copy, Wrench, CalendarCheck, Smartphone } from 'lucide-react';
import { useEffect, useState } from 'react';
import { api } from '../../lib/api';
import { toast } from '../../lib/toast';
import { tacDbApi } from '../../lib/imeiLookup';
import { downloadFile } from '../../lib/downloadPdf';
import { ShieldAlert } from 'lucide-react';
import { BackButton } from '../../components/ui';

/** Sprint 663: links públicos (pedido e marcação online), IMEI/TAC e export para a PSP. O n8n saiu — não é usado. */
export default function Automacoes() {
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

  return (
    <div className="mx-auto max-w-5xl space-y-4 px-4 py-6">
      <BackButton to="/definicoes" label="Voltar a Definições" />
      <header className="space-y-2">
        <h1 className="flex items-center gap-2 text-2xl font-semibold">
          <Workflow size={24} strokeWidth={2} />
          Automações
        </h1>
        <p className="text-sm text-zinc-500">
          Links para os clientes pedirem reparações e marcarem hora, deteção do modelo pelo IMEI e a lista de IMEI para a PSP.
          As faturas por email configuram-se em Definições → Faturas por email.
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

    </div>
  );
}

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
