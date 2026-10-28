import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CalendarClock } from 'lucide-react';
import { Button } from '../../components/ui';
import { apuramentoApi } from '../../lib/apuramento/api';
import { useAuth } from '../../lib/auth/AuthContext';
import { toast } from '../../lib/toast';

/**
 * Sprint 580: lembretes fiscais com prazo (ex.: em janeiro, rever o código de atividade do trading). Ficam visíveis
 * no Dashboard e em "IVA e resultados" até o dono confirmar que tratou. Só para o administrador (endpoint Admin).
 */
export default function LembretesFiscais() {
  const qc = useQueryClient();
  const isAdmin = useAuth().hasRole('Admin');
  const q = useQuery({ queryKey: ['apuramento', 'lembretes'], queryFn: apuramentoApi.lembretes, staleTime: 5 * 60_000, retry: false, enabled: isAdmin });
  const feito = useMutation({
    mutationFn: () => apuramentoApi.marcarCodigoTradingRevisto(true),
    onSuccess: () => { toast.success('Lembrete arrumado.'); qc.invalidateQueries({ queryKey: ['apuramento'] }); },
    onError: (err) => toast.fromError(err, 'Não foi possível guardar.'),
  });
  if (!q.data?.length) return null;

  return (
    <div className="space-y-2">
      {q.data.map((l) => (
        <div key={l.codigo} role="status"
          className={`flex flex-col gap-3 rounded-xl border p-4 text-sm sm:flex-row sm:items-start sm:justify-between ${l.urgente
            ? 'border-rose-300 bg-rose-50 text-rose-900 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-200'
            : 'border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200'}`}>
          <div className="flex items-start gap-2">
            {l.urgente ? <AlertTriangle size={18} className="mt-0.5 shrink-0" /> : <CalendarClock size={18} className="mt-0.5 shrink-0" />}
            <div>
              <p className="font-semibold">{l.titulo}</p>
              <p className="mt-1 leading-5">{l.texto}</p>
            </div>
          </div>
          {l.codigo === 'codigo_trading' && (
            <Button type="button" size="sm" variant="secondary" className="shrink-0 self-start" loading={feito.isPending} onClick={() => feito.mutate()}>
              Já fiz a alteração
            </Button>
          )}
        </div>
      ))}
    </div>
  );
}
