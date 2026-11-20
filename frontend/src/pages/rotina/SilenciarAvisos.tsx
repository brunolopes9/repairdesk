import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BellOff, BellRing } from 'lucide-react';
import { Button } from '../../components/ui';
import { rotinaApi } from '../../lib/rotina/api';
import { toast } from '../../lib/toast';

/** Fim do dia de hoje (23:59 na hora local) em ISO. */
function fimDoDia(): string {
  const d = new Date();
  d.setHours(23, 59, 0, 0);
  return d.toISOString();
}

/**
 * 20/11 (Bruno): «silenciar hoje» os avisos da rotina (comer, ginásio…). Os que forem devidos durante o
 * silêncio não chegam depois em rajada. Alertas importantes (ex.: PS5 em stock) tocam na mesma.
 */
export default function SilenciarAvisos() {
  const qc = useQueryClient();
  const estado = useQuery({ queryKey: ['rotina', 'silencio'], queryFn: rotinaApi.silencio, staleTime: 60_000 });
  const ate = estado.data?.ate ? new Date(estado.data.ate) : null;
  const mudar = useMutation({
    mutationFn: (novo: string | null) => rotinaApi.silenciar(novo),
    onSuccess: (r) => {
      qc.setQueryData(['rotina', 'silencio'], r);
      toast.success(r.ate ? 'Avisos silenciados até ao fim do dia' : 'Avisos da rotina ligados outra vez',
        r.ate ? 'Os alertas importantes (ex.: PS5 em stock) continuam a tocar.' : undefined);
    },
    onError: (err) => toast.fromError(err, 'Não foi possível mudar os avisos.'),
  });

  if (ate) {
    const hora = ate.toLocaleString('pt-PT', { weekday: 'short', hour: '2-digit', minute: '2-digit' });
    return (
      <Button variant="secondary" leftIcon={<BellRing size={16} />} loading={mudar.isPending} onClick={() => mudar.mutate(null)}
        title={`Silenciados até ${hora}`}>
        Silenciados até {hora} · Ligar
      </Button>
    );
  }
  return (
    <Button variant="ghost" leftIcon={<BellOff size={16} />} loading={mudar.isPending} disabled={estado.isLoading} onClick={() => mudar.mutate(fimDoDia())}
      title="Calar os avisos da rotina até ao fim do dia (os alertas importantes tocam na mesma)">
      Silenciar hoje
    </Button>
  );
}
