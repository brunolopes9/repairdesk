import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Clapperboard, Video } from 'lucide-react';
import { useConfirm } from '../../components/ConfirmDialog';
import { Button, EmptyState } from '../../components/ui';
import { conteudoApi } from '../../lib/conteudo/api';
import type { ConteudoPublicacao } from '../../lib/conteudo/types';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import { PublicacaoCard } from './PublicacaoCard';

/**
 * Sprint 640: guiões que o estúdio prepara para o dono gravar (título «⚠️ NÃO APROVAR…»). Não se
 * aprovam — ficam fora de «Por aprovar» e do «Aprovar todas». Depois de gravar, tira-se da lista.
 */
export function GuioesGravar({ itens }: { itens: ConteudoPublicacao[] }) {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const gravado = useMutation({
    mutationFn: (id: string) => conteudoApi.cancelar(id),
    onSuccess: (p) => {
      toast.success(`Guião #${p.numero} tirado da lista.`);
      qc.invalidateQueries({ queryKey: ['conteudo'] });
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Não foi possível tirar o guião.'),
  });

  if (itens.length === 0)
    return <EmptyState icon={Clapperboard} title="Sem guiões para gravar" description="Quando o estúdio preparar um guião para gravares, aparece aqui." />;

  return (
    <div className="grid items-start gap-4 xl:grid-cols-2">
      {itens.map((p) => (
        <PublicacaoCard
          key={p.id}
          p={p}
          actions={
            <Button
              variant="secondary"
              leftIcon={<Video size={16} />}
              loading={gravado.isPending && gravado.variables === p.id}
              onClick={async () => {
                const ok = await confirm({
                  title: `Já gravaste o guião #${p.numero}?`,
                  description: 'Sai desta lista. Depois envia o vídeo em Conteúdo → Vídeos.',
                  confirmLabel: 'Já gravei',
                });
                if (ok) gravado.mutate(p.id);
              }}
            >
              Já gravei
            </Button>
          }
        />
      ))}
    </div>
  );
}
