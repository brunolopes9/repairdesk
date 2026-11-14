import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Copy, Mail } from 'lucide-react';
import { Button, EmptyState, PageHeader, SkeletonTable } from '../../components/ui';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import { ESTADO_SUBSCRITOR_LABEL, newsletterApi, type EstadoSubscritor } from '../../lib/loja/newsletter';

const COR: Record<EstadoSubscritor, string> = {
  ativo: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300',
  por_confirmar: 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300',
  cancelado: 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400',
};

const data = (iso: string | null) => (iso ? new Date(iso).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short', year: 'numeric' }) : '—');

/**
 * Sprint 661: quem subscreveu a newsletter no site. Só os «Ativos» (confirmaram pelo email e não cancelaram)
 * podem receber campanhas — é isso que o RGPD exige.
 */
export default function Newsletter() {
  const [todos, setTodos] = useState(false);
  const q = useQuery({ queryKey: ['loja', 'newsletter'], queryFn: newsletterApi.resumo });
  const r = q.data;
  const lista = (r?.subscritores ?? []).filter((s) => todos || s.estado === 'ativo');

  const copiarAtivos = async () => {
    const emails = (r?.subscritores ?? []).filter((s) => s.estado === 'ativo').map((s) => s.email);
    try {
      await navigator.clipboard.writeText(emails.join(', '));
      toast.success(`${emails.length} email(s) copiados`, 'Cola no campo Bcc — nunca em «Para».');
    } catch {
      toast.error('Não consegui copiar', 'O browser bloqueou o acesso à área de transferência.');
    }
  };

  return (
    <div className="space-y-4">
      <PageHeader
        title="Newsletter"
        description={r ? `${r.ativos} ativos · ${r.porConfirmar} por confirmar · ${r.cancelados} cancelaram` : 'Subscritores da loja online.'}
        actions={
          <div className="flex items-center gap-3">
            <Link to="/loja" className="text-sm font-medium text-brand-600 hover:underline">Produtos</Link>
            <Button variant="secondary" leftIcon={<Copy size={14} />} onClick={copiarAtivos} disabled={!r || r.ativos === 0}>Copiar ativos</Button>
          </div>
        }
      />
      <label className="flex items-center gap-2 text-sm text-zinc-600 dark:text-zinc-400">
        <input type="checkbox" checked={todos} onChange={(e) => setTodos(e.target.checked)} />
        Mostrar também os por confirmar e os que cancelaram
      </label>
      {q.isLoading ? (
        <SkeletonTable columns={4} rows={4} />
      ) : q.isError ? (
        <p className="text-sm text-rose-600">{apiErrorMessage(q.error)}</p>
      ) : lista.length === 0 ? (
        <EmptyState icon={Mail} title="Ainda sem subscritores" description="Quem se inscrever no site e confirmar pelo email aparece aqui." />
      ) : (
        <div className="overflow-x-auto rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
          <table className="w-full text-sm">
            <thead className="border-b border-zinc-200 text-left text-xs uppercase text-zinc-500 dark:border-zinc-800">
              <tr><th className="px-4 py-2">Email</th><th className="px-4 py-2">Estado</th><th className="px-4 py-2">Onde</th><th className="px-4 py-2">Desde</th></tr>
            </thead>
            <tbody>
              {lista.map((s) => (
                <tr key={s.email} className="border-b border-zinc-100 last:border-0 dark:border-zinc-800">
                  <td className="px-4 py-2 font-medium">{s.email}</td>
                  <td className="px-4 py-2"><span className={`rounded-full px-2 py-0.5 text-xs font-medium ${COR[s.estado]}`}>{ESTADO_SUBSCRITOR_LABEL[s.estado]}</span></td>
                  <td className="px-4 py-2 text-zinc-500">{s.origem ?? '—'}</td>
                  <td className="px-4 py-2 tabular-nums text-zinc-500">{data(s.confirmadoEm ?? s.criadoEm)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
