import { Copy, ExternalLink, FileText, Printer } from 'lucide-react';
import FotosReparacao from '../../components/FotosReparacao';
import GarantiaCard from '../../components/GarantiaCard';
import WhatsAppMenu from '../../components/WhatsAppMenu';
import { Button, SectionCard } from '../../components/ui';
import { openPdfInNewTab } from '../../lib/downloadPdf';
import { formatEur } from '../../lib/compras/format';
import { toast } from '../../lib/toast';
import { vendasApi } from '../../lib/vendas/api';
import { VENDA_ESTADO, type Venda } from '../../lib/vendas/types';
import { AssinaturasVenda } from './AssinaturasVenda';
import { ComunicacoesVenda } from './ComunicacoesVenda';

/**
 * Doc 94 Fase 4c: o que a Reparação antiga tinha e ficou — portal do cliente, PDFs de entrada/entrega
 * com assinatura, etiqueta, fotos, comunicações e garantia — agora pendurado na Venda do tipo Reparação.
 */
export default function ReparacaoExtras({ venda }: { venda: Venda }) {
  const portalUrl = venda.publicSlug ? `${window.location.origin}/r/${venda.publicSlug}` : null;
  const telefone = venda.cliente?.telefone?.replace(/\s/g, '') ?? '';

  async function copiarPortal() {
    if (!portalUrl) return;
    try {
      await navigator.clipboard.writeText(portalUrl);
      toast.success('Link do portal copiado');
    } catch {
      toast.error('Não consegui copiar — seleciona o link manualmente.');
    }
  }

  return (
    <>
      <SectionCard title="Cliente e documentos">
        <div className="space-y-4">
          {portalUrl && (
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
              <span className="text-xs text-zinc-500 sm:w-32">Portal do cliente</span>
              <code className="min-w-0 flex-1 truncate rounded bg-zinc-100 px-2 py-1.5 text-xs dark:bg-zinc-800">{portalUrl}</code>
              <div className="flex gap-2">
                <Button type="button" size="sm" variant="secondary" leftIcon={<Copy size={14} />} onClick={copiarPortal}>Copiar</Button>
                <a href={portalUrl} target="_blank" rel="noreferrer" className="inline-flex min-h-11 items-center gap-1 rounded-lg px-3 text-xs text-brand-700 hover:underline dark:text-brand-300">
                  Abrir <ExternalLink size={12} />
                </a>
              </div>
            </div>
          )}

          <div className="flex flex-wrap gap-2">
            <Button type="button" size="sm" variant="secondary" leftIcon={<FileText size={14} />} onClick={() => openPdfInNewTab(vendasApi.pdfPath(venda.id, 'entrada'))}>
              Comprovativo de entrada
            </Button>
            <Button type="button" size="sm" variant="secondary" leftIcon={<FileText size={14} />} disabled={venda.estado === VENDA_ESTADO.Orcamento} onClick={() => openPdfInNewTab(vendasApi.pdfPath(venda.id, 'entrega'))}>
              Recibo de entrega
            </Button>
            <Button type="button" size="sm" variant="secondary" leftIcon={<Printer size={14} />} onClick={() => openPdfInNewTab(vendasApi.pdfPath(venda.id, 'label'))}>
              Etiqueta
            </Button>
            {telefone && venda.cliente && (
              <WhatsAppMenu
                phone={telefone}
                estado={venda.estado}
                entityId={venda.id}
                entityType="Venda"
                vars={{
                  cliente_nome: venda.cliente.nome.split(' ')[0],
                  equipamento: venda.equipamento ?? 'equipamento',
                  numero_reparacao: venda.numero,
                  valor: venda.totalCents > 0 ? formatEur(venda.totalCents / 100) : undefined,
                  link_aprovacao: portalUrl ?? undefined,
                }}
              />
            )}
          </div>

          <AssinaturasVenda vendaId={venda.id} />
        </div>
      </SectionCard>

      <SectionCard title="Fotos do equipamento">
        <FotosReparacao vendaId={venda.id} />
      </SectionCard>

      <ComunicacoesVenda
        vendaId={venda.id}
        reparacaoNumero={venda.numero}
        reparacaoEstado={venda.estado}
        reparacaoEquipamento={venda.equipamento ?? undefined}
        clienteNome={venda.cliente?.nome}
        clienteTelefone={venda.cliente?.telefone ?? null}
        publicSlug={venda.publicSlug}
      />

      {venda.estado === VENDA_ESTADO.Entregue && <GarantiaCard vendaId={venda.id} />}
    </>
  );
}
