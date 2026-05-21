import { Workflow, ExternalLink, Mail, Server, Camera, CheckCircle2, AlertCircle } from 'lucide-react';
import { useState } from 'react';

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
      // n8n healthcheck endpoint
      const res = await fetch(`${n8nUrl}/healthz`, { method: 'GET', mode: 'no-cors' });
      // no-cors devolve opaque response — assume up se não throw
      setN8nStatus(res ? 'up' : 'down');
    } catch {
      setN8nStatus('down');
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
          Workflows de automação que correm fora do RepairDesk (n8n) e alimentam o sistema com dados
          de fornecedores. Configura uma vez, deixa correr em background.
        </p>
      </header>

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
            'HTTP POST com API key RepairDesk (cria em /definicoes → chaves)',
          ]}
        />

        <WorkflowCard
          icon={Camera}
          title="Foto papel mobile → RepairDesk"
          description="Alternativa: usa o telemóvel para fotografar fatura papel directamente em /importacoes. Não precisa de n8n. Claude Vision faz OCR."
          docPath="(Sprint 164 — já disponível em /importacoes → '📷 Foto papel')"
          steps={[
            'Abre RepairDesk no telemóvel',
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
