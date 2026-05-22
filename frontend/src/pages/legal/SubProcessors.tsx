import LegalLayout, { Section } from './LegalLayout';

interface Sub {
  name: string;
  purpose: string;
  dataCategories: string;
  location: string;
  url: string;
  optional?: boolean;
}

const subs: Sub[] = [
  {
    name: 'Anthropic, PBC',
    purpose: 'Processamento de linguagem natural (Claude) — parsing de facturas de fornecedor + OCR de fotografias.',
    dataCategories: 'Texto extraído de PDFs (com PII redacted), imagens de facturas.',
    location: 'EUA (Cláusulas Contratuais-Tipo)',
    url: 'https://www.anthropic.com/legal',
    optional: true,
  },
  {
    name: 'Cloudflare, Inc.',
    purpose: 'Storage de PDFs/imagens de facturas (R2) + CDN + Email Routing (forwarding faturas-{slug}@ingest.repairdesk.app).',
    dataCategories: 'Ficheiros (facturas, fotos, garantias), emails reencaminhados.',
    location: 'Global (zona EU disponível), Cláusulas Contratuais-Tipo',
    url: 'https://www.cloudflare.com/privacypolicy/',
  },
  {
    name: 'Moloni (DigitalSign Lda)',
    purpose: 'Facturação electrónica certificada AT — apenas quando tenant liga conta Moloni.',
    dataCategories: 'Dados de facturação (cliente, NIF, valor, items).',
    location: 'Portugal (UE)',
    url: 'https://www.moloni.com/termos-e-condicoes',
    optional: true,
  },
  {
    name: 'InvoiceXpress (IOL)',
    purpose: 'Alternativa a Moloni — facturação electrónica certificada. Apenas quando tenant escolhe.',
    dataCategories: 'Dados de facturação.',
    location: 'Portugal (UE)',
    url: 'https://invoicexpress.com/termos',
    optional: true,
  },
  {
    name: 'Sentry.io (Functional Software, Inc.)',
    purpose: 'Captura de erros aplicacionais para debugging.',
    dataCategories: 'Stack traces, tenant_id, user_id (sem PII de clientes finais; PII scrubbing activo).',
    location: 'EUA (Cláusulas Contratuais-Tipo)',
    url: 'https://sentry.io/legal/dpa/',
  },
  {
    name: 'Stripe, Inc.',
    purpose: 'Processamento de pagamentos das subscrições SaaS — apenas quando aplicável.',
    dataCategories: 'Dados de cartão (tokenizado), nome do facturador, NIF, morada.',
    location: 'EUA (Cláusulas Contratuais-Tipo)',
    url: 'https://stripe.com/privacy',
    optional: true,
  },
];

/**
 * Sprint 174: lista de sub-processadores RGPD-compliant.
 * Tenants são notificados com 30 dias de antecedência antes de adicionar novo sub-processador.
 */
export default function SubProcessors() {
  return (
    <LegalLayout title="Sub-processadores" lastUpdated="22 Maio 2026">
      <p>
        Esta página lista todos os terceiros que a LopesTech utiliza para processar Dados Pessoais
        em nome dos seus Clientes (tenants RepairDesk). É actualizada com 30 dias de antecedência
        sempre que adicionarmos novo sub-processador.
      </p>

      <Section title="Lista actual">
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-xs">
            <thead>
              <tr className="border-b border-zinc-200 dark:border-zinc-700">
                <th className="px-2 py-2 text-left font-medium">Sub-processador</th>
                <th className="px-2 py-2 text-left font-medium">Finalidade</th>
                <th className="px-2 py-2 text-left font-medium">Localização</th>
                <th className="px-2 py-2 text-left font-medium">Tipo</th>
              </tr>
            </thead>
            <tbody>
              {subs.map((s) => (
                <tr key={s.name} className="border-b border-zinc-100 align-top dark:border-zinc-800">
                  <td className="px-2 py-3">
                    <a href={s.url} target="_blank" rel="noreferrer" className="font-medium text-brand-600 hover:underline dark:text-brand-400">
                      {s.name}
                    </a>
                    <div className="mt-1 text-[11px] text-zinc-500">{s.dataCategories}</div>
                  </td>
                  <td className="px-2 py-3 text-zinc-600 dark:text-zinc-400">{s.purpose}</td>
                  <td className="px-2 py-3 whitespace-nowrap text-zinc-600 dark:text-zinc-400">{s.location}</td>
                  <td className="px-2 py-3">
                    {s.optional ? (
                      <span className="rounded bg-zinc-100 px-1.5 py-0.5 text-[10px] uppercase text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400">
                        Opt-in
                      </span>
                    ) : (
                      <span className="rounded bg-blue-100 px-1.5 py-0.5 text-[10px] uppercase text-blue-700 dark:bg-blue-900/40 dark:text-blue-300">
                        Core
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Section>

      <Section title="Notas">
        <ul className="list-disc pl-6">
          <li><strong>Core</strong>: sub-processador usado por todos os tenants (infra obrigatória).</li>
          <li><strong>Opt-in</strong>: usado apenas quando o tenant activa a feature (ex: Moloni só se ligar conta).</li>
          <li>
            <strong>BYOK Anthropic</strong>: tenants podem opt-out do nosso uso de Anthropic, configurando
            a sua própria API key em <code>/definicoes/uso-de-ia</code>. Nesse caso, a Anthropic
            processa dados directamente para o tenant.
          </li>
        </ul>
      </Section>

      <Section title="Alterações">
        <p>
          Comunicamos novos sub-processadores via email aos admins de todos os tenants, com 30 dias
          de antecedência. Tenants podem opor-se; se a oposição não puder ser resolvida, podem
          rescindir o contrato com reembolso pro-rata.
        </p>
      </Section>

      <Section title="Contacto">
        <p>
          Dúvidas sobre sub-processadores: <a className="text-brand-600 dark:text-brand-400 hover:underline" href="mailto:privacidade@lopestech.pt">privacidade@lopestech.pt</a>.
        </p>
      </Section>
    </LegalLayout>
  );
}
