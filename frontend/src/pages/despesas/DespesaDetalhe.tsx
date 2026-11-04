import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Save, Trash2 } from 'lucide-react';
import { BackButton, Button, PageHeader, SectionCard, SkeletonCard } from '../../components/ui';
import { DespesaCampos, useDespesaForm } from '../../components/DespesaFormModal';
import { PdfPreview } from '../../components/PdfPreview';
import { useConfirm } from '../../components/ConfirmDialog';
import { despesasApi } from '../../lib/despesas/api';
import { DESPESA_LABEL } from '../../lib/despesas/types';
import { apiErrorMessage } from '../../lib/errors';
import { formatCents, formatDateOnly } from '../../lib/money';
import { supplierInvoicesApi } from '../../lib/supplierInvoices/api';
import { toast } from '../../lib/toast';
import { FaturaDespesa } from './FaturaDespesa';

/**
 * Sprint 592: a despesa numa página própria (como as compras) — os dados para editar e a fatura à vista ao lado,
 * para conferir e imprimir. A lista abre aqui ao clicar numa linha; "Voltar" regressa à mesma página da lista.
 */
export default function DespesaDetalhe() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const confirm = useConfirm();
  const despesa = useQuery({ queryKey: ['despesa', id], queryFn: () => despesasApi.get(id!) });

  const form = useDespesaForm({
    editing: despesa.data ?? null,
    onSaved: (d) => {
      qc.setQueryData(['despesa', d.id], d);
      qc.invalidateQueries({ queryKey: ['despesas'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      qc.invalidateQueries({ queryKey: ['apuramento'] });
      toast.success('Despesa guardada', `Nº ${d.numero ?? ''} · ${formatCents(d.valorCents)}`);
    },
  });

  const remove = useMutation({
    mutationFn: () => despesasApi.remove(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['despesas'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      toast.success('Despesa apagada');
      navigate('/despesas', { replace: true });
    },
    onError: (err) => toast.fromError(err),
  });

  if (despesa.isLoading) return <SkeletonCard />;
  if (despesa.isError || !despesa.data) return <p className="text-sm text-rose-600">{apiErrorMessage(despesa.error)}</p>;
  const d = form.comFatura ?? despesa.data;

  return (
    <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); if (form.podeGravar) form.save.mutate(); }}>
      <BackButton to="/despesas" label="Despesas" />
      <PageHeader
        title={`Despesa nº ${d.numero ?? ''}${d.numeroEncomenda ? ` · ${d.numeroEncomenda}` : ''}`}
        description={[d.fornecedor, formatDateOnly(d.data), DESPESA_LABEL[d.categoria], formatCents(d.valorCents)].filter(Boolean).join(' · ')}
        actions={
          <>
            <FaturaDespesa despesa={d} onAnexada={form.setComFatura} />
            <Button type="button" variant="ghost" leftIcon={<Trash2 size={15} />} loading={remove.isPending}
              onClick={async () => {
                if (await confirm({ title: 'Apagar esta despesa?', description: `${d.descricao} (${formatCents(d.valorCents)})`, destructive: true })) remove.mutate();
              }}>
              Apagar
            </Button>
            <Button type="submit" leftIcon={<Save size={15} />} disabled={!form.podeGravar} loading={form.save.isPending}>Guardar</Button>
          </>
        }
      />
      <div className={`grid gap-4 ${d.supplierInvoiceImportId ? 'lg:grid-cols-[minmax(0,26rem)_1fr]' : ''}`}>
        <SectionCard title="Dados da despesa">
          <DespesaCampos form={form} mostrarFatura={false} />
        </SectionCard>
        {d.supplierInvoiceImportId ? (
          <SectionCard title="Fatura">
            <PdfPreview path={supplierInvoicesApi.pdfPath(d.supplierInvoiceImportId)} title={`Fatura da despesa nº ${d.numero ?? ''}`} />
          </SectionCard>
        ) : (
          <p className="rounded-lg border border-dashed border-zinc-300 p-4 text-sm text-zinc-500 dark:border-zinc-700">
            Esta despesa ainda não tem a fatura anexada — usa «Anexar PDF da fatura» lá em cima para a guardar aqui e poder imprimir.
          </p>
        )}
      </div>
    </form>
  );
}
