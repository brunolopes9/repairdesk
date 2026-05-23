import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import { AlertTriangle, Check, CheckCircle2, Download, FileText, Mail, ShieldCheck, ShieldOff, ShieldX, ShoppingBag, Wrench } from 'lucide-react';
import { publicPortalApi } from '../lib/publicPortal/api';
import { SkeletonCard } from '../components/ui';
import { formatCents } from '../lib/money';

export default function PortalGarantia() {
  const { slug } = useParams<{ slug: string }>();
  const g = useQuery({
    queryKey: ['public-garantia', slug],
    queryFn: () => publicPortalApi.getGarantia(slug!),
    enabled: !!slug,
    retry: 0,
  });

  if (g.isLoading) {
    return (
      <div className="grid min-h-screen place-items-center bg-gradient-to-b from-zinc-50 to-white p-6 dark:from-zinc-950 dark:to-zinc-900">
        <div className="w-full max-w-md">
          <SkeletonCard />
        </div>
      </div>
    );
  }

  if (g.isError || !g.data) {
    return (
      <div className="grid min-h-screen place-items-center bg-gradient-to-b from-zinc-50 to-white p-6 text-center dark:from-zinc-950 dark:to-zinc-900">
        <div className="max-w-sm">
          <div className="mx-auto grid h-16 w-16 place-items-center rounded-full bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">
            <ShieldOff size={32} strokeWidth={1.75} />
          </div>
          <h1 className="mt-4 text-xl font-semibold">Garantia não encontrada</h1>
          <p className="mt-2 text-sm text-zinc-500">
            O link parece estar partido. Pede ajuda à loja.
          </p>
        </div>
      </div>
    );
  }

  const data = g.data;
  const activa = data.activa;
  const bgGrad = activa
    ? 'from-emerald-500/20 to-emerald-500/5'
    : data.anulada
      ? 'from-rose-500/20 to-rose-500/5'
      : 'from-zinc-500/20 to-zinc-500/5';
  const StatusIcon = data.anulada ? ShieldX : activa ? ShieldCheck : ShieldOff;
  const statusIconCls = data.anulada
    ? 'text-rose-600 dark:text-rose-400'
    : activa
      ? 'text-emerald-600 dark:text-emerald-400'
      : 'text-zinc-500 dark:text-zinc-400';
  const statusLabel = data.anulada
    ? 'Anulada'
    : activa
      ? `Activa · ${data.diasRestantes} ${data.diasRestantes === 1 ? 'dia restante' : 'dias restantes'}`
      : 'Expirada';

  return (
    <div className="min-h-screen bg-gradient-to-b from-zinc-50 to-white pb-12 dark:from-zinc-950 dark:to-zinc-900">
      <div className="mx-auto max-w-2xl px-4 pt-6">
        <header className="flex items-center gap-3">
          {data.logoUrl ? (
            <img src={data.logoUrl} alt={data.loja} className="h-10 w-10 rounded-lg object-cover" onError={(e) => ((e.target as HTMLImageElement).style.opacity = '0')} />
          ) : (
            <div className="grid h-10 w-10 place-items-center rounded-lg bg-brand-100 text-brand-700 dark:bg-brand-900/40 dark:text-brand-300">
              <ShieldCheck size={18} strokeWidth={2} />
            </div>
          )}
          <div>
            <div className="text-sm font-semibold">{data.loja}</div>
            <div className="text-[11px] text-zinc-500">Verificação de garantia</div>
          </div>
        </header>

        <section className={`mt-6 rounded-3xl border border-zinc-200/70 bg-gradient-to-br ${bgGrad} p-6 shadow-sm backdrop-blur dark:border-zinc-800/70`}>
          <div className="flex items-center gap-2 text-[11px] uppercase tracking-wider text-zinc-500">
            <StatusIcon size={14} strokeWidth={2} className={statusIconCls} />
            Garantia {data.origem === 'Venda' ? '· Venda (DL 84/2021)' : '· Reparação'}
          </div>
          <h1 className="mt-1 text-3xl font-semibold tracking-tight">{data.equipamentoPublico}</h1>
          {data.documentoReferencia && (
            <div className="mt-1 inline-flex items-center gap-1.5 text-xs text-zinc-500">
              {data.origem === 'Venda' ? <ShoppingBag size={12} strokeWidth={2} /> : <Wrench size={12} strokeWidth={2} />}
              {data.documentoReferencia}
              {data.numeroFatura && <span>· Fatura {data.numeroFatura}</span>}
            </div>
          )}
          <div className={`mt-2 inline-flex items-center gap-1.5 text-base font-medium ${statusIconCls}`}>
            {activa && <Check size={16} strokeWidth={2.5} />}
            {statusLabel}
          </div>
          <div className="mt-3 grid grid-cols-2 gap-3 text-sm">
            <div>
              <div className="text-[11px] uppercase text-zinc-500">Início</div>
              <div className="font-medium">{new Date(data.dataInicio).toLocaleDateString('pt-PT')}</div>
            </div>
            <div>
              <div className="text-[11px] uppercase text-zinc-500">Fim</div>
              <div className="font-medium">{new Date(data.dataFim).toLocaleDateString('pt-PT')}</div>
            </div>
            <div>
              <div className="text-[11px] uppercase text-zinc-500">Período</div>
              <div className="font-medium">{data.diasGarantia} dias</div>
            </div>
            <div>
              <div className="text-[11px] uppercase text-zinc-500">Código</div>
              <div className="font-mono text-xs">{data.slug}</div>
            </div>
          </div>
          <a
            href={`/api/public/warranty/${encodeURIComponent(data.slug)}/pdf`}
            target="_blank"
            rel="noopener noreferrer"
            className="mt-4 inline-flex items-center gap-2 rounded-lg bg-zinc-900 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-800 dark:bg-white dark:text-zinc-900 dark:hover:bg-zinc-100"
          >
            <Download size={14} strokeWidth={2} /> Descarregar PDF
          </a>
        </section>

        {data.origem === 'Venda' && data.items && data.items.length > 0 && (
          <section className="mt-4 rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
            <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold">
              <FileText size={15} strokeWidth={2} className="text-zinc-500" />
              Artigos abrangidos
            </h2>
            <ul className="divide-y divide-zinc-100 text-sm dark:divide-zinc-800">
              {data.items.map((item, idx) => (
                <li key={idx} className="flex items-center justify-between gap-2 py-2">
                  <div className="min-w-0 flex-1">
                    <div className="truncate">{item.descricao}</div>
                    <div className="text-[11px] text-zinc-500">
                      {item.quantidade}x · {formatCents(item.precoUnitarioCents)}
                      {item.imeiMascarado && (
                        <>
                          {' · '}
                          <span className="font-mono">IMEI {item.imeiMascarado}</span>
                        </>
                      )}
                    </div>
                  </div>
                  <div className="font-medium">{formatCents(item.totalCents)}</div>
                </li>
              ))}
            </ul>
          </section>
        )}

        {data.cobertura && (
          <section className="mt-4 rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
            <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold">
              <CheckCircle2 size={15} strokeWidth={2} className="text-emerald-600 dark:text-emerald-400" />
              Cobertura
            </h2>
            <p className="whitespace-pre-line text-sm text-zinc-700 dark:text-zinc-300">{data.cobertura}</p>
          </section>
        )}
        {data.exclusoes && (
          <section className="mt-4 rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
            <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold">
              <AlertTriangle size={15} strokeWidth={2} className="text-amber-600 dark:text-amber-400" />
              Exclusões
            </h2>
            <p className="whitespace-pre-line text-sm text-zinc-700 dark:text-zinc-300">{data.exclusoes}</p>
          </section>
        )}

        {/* Sprint 94: botão "Reclamar garantia" — só visível quando garantia activa e há email da loja */}
        {data.activa && data.lojaEmail && (
          <section className="mt-4 rounded-2xl border border-brand-200 bg-brand-50/40 p-5 shadow-sm dark:border-brand-900/60 dark:bg-brand-950/30">
            <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold">
              <Mail size={15} strokeWidth={2} className="text-brand-600 dark:text-brand-400" />
              Tens um problema com este equipamento?
            </h2>
            <p className="text-xs text-zinc-600 dark:text-zinc-400">
              Esta garantia está activa. Se queres reclamar uma reparação ao abrigo da garantia,
              contacta a loja com a informação abaixo já preenchida.
            </p>
            <a
              href={(() => {
                const subject = encodeURIComponent(`Reclamação garantia · ${data.documentoReferencia ?? data.slug}`);
                const body = encodeURIComponent(
                  `Olá,\n\n`
                  + `Venho reclamar uma reparação ao abrigo da garantia do seguinte equipamento:\n\n`
                  + `Equipamento: ${data.equipamentoPublico}\n`
                  + `${data.documentoReferencia ? `Documento: ${data.documentoReferencia}\n` : ''}`
                  + `${data.numeroFatura ? `Fatura: ${data.numeroFatura}\n` : ''}`
                  + `Data de compra: ${new Date(data.dataInicio).toLocaleDateString('pt-PT')}\n`
                  + `Garantia válida até: ${new Date(data.dataFim).toLocaleDateString('pt-PT')}\n`
                  + `Código garantia: ${data.slug}\n\n`
                  + `Descrição do problema:\n[descrever aqui]\n\n`
                  + `Aguardo a vossa resposta.\n\nObrigado(a).`
                );
                return `mailto:${data.lojaEmail}?subject=${subject}&body=${body}`;
              })()}
              className="mt-3 inline-flex items-center gap-2 rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
            >
              <Mail size={14} strokeWidth={2} /> Reclamar por email
            </a>
            {data.lojaTelefone && (
              <a
                href={`tel:${data.lojaTelefone.replace(/\s/g, '')}`}
                className="ml-2 inline-flex items-center gap-2 rounded-lg border border-brand-300 bg-white px-4 py-2 text-sm font-medium text-brand-700 hover:bg-brand-50 dark:border-brand-700 dark:bg-zinc-900 dark:text-brand-300"
              >
                📞 {data.lojaTelefone}
              </a>
            )}
          </section>
        )}

        <footer className="mt-10 space-y-2 text-center text-[11px] text-zinc-400">
          <div>
            Verifica esta garantia em qualquer altura — link permanente.<br />
            Gerado pelo Reparo · LopesTech
          </div>
          <div className="flex justify-center gap-3">
            <a href="/privacidade" className="hover:text-zinc-600 dark:hover:text-zinc-300">Privacidade</a>
            <span aria-hidden>·</span>
            <a href="/termos" className="hover:text-zinc-600 dark:hover:text-zinc-300">Termos</a>
            <span aria-hidden>·</span>
            <a href="/cookies" className="hover:text-zinc-600 dark:hover:text-zinc-300">Cookies</a>
          </div>
        </footer>
      </div>
    </div>
  );
}
