import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { CalendarClock, Check, CheckCheck, PartyPopper, Send, X } from 'lucide-react';
import Modal from '../../components/Modal';
import { useConfirm } from '../../components/ConfirmDialog';
import { Button, EmptyState } from '../../components/ui';
import { conteudoApi } from '../../lib/conteudo/api';
import type { ConteudoPublicacao, HoraPublicacao } from '../../lib/conteudo/types';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import { PublicacaoCard } from './PublicacaoCard';
import { EscolherHora } from './EscolherHora';
import { useQuery } from '@tanstack/react-query';
import { RedeSocial } from '../../lib/conteudo/types';
import { redesSociaisApi } from '../../lib/redesSociais/api';

const MOTIVOS_RAPIDOS = ['Foto cortada', 'Texto a mudar', 'Outra hora', 'Não gosto', 'Preço errado'];

/**
 * Sprint 598 (Doc 95): fila «Por aprovar» pensada para o telemóvel — cada publicação inteira à
 * vista e dois botões grandes. Rejeitar pede um motivo opcional (com atalhos) para o Claude corrigir.
 * Sprint 610: aprovar também «a outra hora» ou «Publicar já» (sai no próximo minuto).
 */
export function FilaAprovacao({ itens }: { itens: ConteudoPublicacao[] }) {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const [rejeitar, setRejeitar] = useState<ConteudoPublicacao | null>(null);
  const [motivo, setMotivo] = useState('');
  const [outraHora, setOutraHora] = useState<ConteudoPublicacao | null>(null);
  const redes = useQuery({ queryKey: ['redes-sociais'], queryFn: redesSociaisApi.estado, staleTime: 300_000 });
  const ligTikTok = redes.data?.ligacoes.find((l) => l.rede === RedeSocial.TikTok);
  const tiktokLigado = Boolean(ligTikTok?.valida);

  const refrescar = () => qc.invalidateQueries({ queryKey: ['conteudo'] });

  const aprovar = useMutation({
    mutationFn: ({ id, hora }: { id: string; hora?: HoraPublicacao }) => conteudoApi.aprovar(id, hora),
    onSuccess: (p, { hora }) => {
      toast.success(hora?.agora ? `#${p.numero} aprovada — sai dentro de 1 minuto.` : `#${p.numero} aprovada.`);
      setOutraHora(null);
      refrescar();
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Não foi possível aprovar.'),
  });

  const aprovarTodas = useMutation({
    mutationFn: async () => {
      // Em série: se uma falhar (ex.: ficheiro em falta), as outras continuam e diz-se qual foi.
      const falhas: string[] = [];
      for (const p of itens) {
        try {
          await conteudoApi.aprovar(p.id);
        } catch (err) {
          falhas.push(`#${p.numero}: ${apiErrorMessage(err)}`);
        }
      }
      return falhas;
    },
    onSuccess: (falhas) => {
      if (falhas.length === 0) toast.success('Tudo aprovado.');
      else toast.error(`Algumas não foram aprovadas — ${falhas.join(' · ')}`);
      refrescar();
    },
  });

  const rejeitarMut = useMutation({
    mutationFn: ({ id, m }: { id: string; m: string | null }) => conteudoApi.rejeitar(id, m),
    onSuccess: (p) => {
      toast.success(`#${p.numero} rejeitada — vou corrigir.`);
      setRejeitar(null);
      setMotivo('');
      refrescar();
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Não foi possível rejeitar.'),
  });

  if (itens.length === 0) {
    return (
      <EmptyState
        icon={PartyPopper}
        title="Nada para aprovar"
        description="Quando houver publicações novas recebes uma notificação no telemóvel."
      />
    );
  }

  return (
    <div className="space-y-4">
      {itens.length > 1 && (
        <div className="flex justify-end">
          <Button
            variant="secondary"
            leftIcon={<CheckCheck size={16} />}
            loading={aprovarTodas.isPending}
            onClick={async () => {
              const ok = await confirm({
                title: `Aprovar as ${itens.length} publicações?`,
                description: 'Confirma que viste todas — saem às horas marcadas.',
                confirmLabel: 'Aprovar todas',
              });
              if (ok) aprovarTodas.mutate();
            }}
          >
            Aprovar todas ({itens.length})
          </Button>
        </div>
      )}

      <div className="grid items-start gap-4 xl:grid-cols-2">
        {itens.map((p) => (
          <PublicacaoCard
            key={p.id}
            p={p}
            tiktokEditavel={tiktokLigado}
            actions={
              <div className="grid grid-cols-2 gap-2">
                <Button
                  variant="ghost"
                  leftIcon={<CalendarClock size={16} />}
                  onClick={() => setOutraHora(p)}
                >
                  Mudar hora
                </Button>
                <Button
                  variant="secondary"
                  leftIcon={<Send size={16} />}
                  loading={aprovar.isPending && aprovar.variables?.id === p.id && Boolean(aprovar.variables?.hora?.agora)}
                  onClick={async () => {
                    const ok = await confirm({
                      title: `Publicar #${p.numero} já?`,
                      description: 'Fica aprovada e sai em todas as redes dentro de 1 minuto.',
                      confirmLabel: 'Publicar já',
                    });
                    if (ok) aprovar.mutate({ id: p.id, hora: { agendadoPara: null, agora: true } });
                  }}
                >
                  Publicar já
                </Button>
                <Button
                  variant="secondary"
                  size="lg"
                  leftIcon={<X size={18} />}
                  onClick={() => {
                    setMotivo('');
                    setRejeitar(p);
                  }}
                >
                  Rejeitar
                </Button>
                <Button
                  size="lg"
                  leftIcon={<Check size={18} />}
                  loading={aprovar.isPending && aprovar.variables?.id === p.id && !aprovar.variables?.hora}
                  onClick={() => aprovar.mutate({ id: p.id })}
                >
                  Aprovar
                </Button>
              </div>
            }
          />
        ))}
      </div>

      <EscolherHora
        open={outraHora !== null}
        titulo={outraHora ? `Aprovar #${outraHora.numero} para outra hora` : ''}
        inicial={outraHora?.agendadoPara ?? null}
        confirmar="Aprovar a esta hora"
        loading={aprovar.isPending}
        onClose={() => setOutraHora(null)}
        onConfirm={(iso) => outraHora && aprovar.mutate({ id: outraHora.id, hora: { agendadoPara: iso, agora: false } })}
      />

      <Modal
        open={rejeitar !== null}
        title={rejeitar ? `Rejeitar #${rejeitar.numero}` : ''}
        onClose={() => setRejeitar(null)}
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setRejeitar(null)}>
              Voltar
            </Button>
            <Button
              variant="danger"
              loading={rejeitarMut.isPending}
              onClick={() => rejeitar && rejeitarMut.mutate({ id: rejeitar.id, m: motivo.trim() || null })}
            >
              Rejeitar
            </Button>
          </div>
        }
      >
        <p className="text-sm text-zinc-600 dark:text-zinc-400">O que está mal? (opcional — ajuda-me a corrigir)</p>
        <div className="mt-3 flex flex-wrap gap-2">
          {MOTIVOS_RAPIDOS.map((m) => (
            <button
              key={m}
              type="button"
              onClick={() => setMotivo((v) => (v ? `${v}; ${m}` : m))}
              className="min-h-9 rounded-full border border-zinc-200 px-3 text-sm text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800"
            >
              {m}
            </button>
          ))}
        </div>
        <textarea
          className="mt-3 min-h-24 w-full rounded-md border border-zinc-200 bg-white p-3 text-sm outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 dark:border-zinc-700 dark:bg-zinc-950"
          placeholder="Ex.: a foto do 3.º slide está cortada"
          maxLength={1000}
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
        />
      </Modal>
    </div>
  );
}
