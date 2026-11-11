import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, Share2 } from 'lucide-react';
import { EmptyState, SkeletonCard } from '../../components/ui';
import { conteudoApi } from '../../lib/conteudo/api';
import { RedeSocial } from '../../lib/conteudo/types';
import { apiErrorMessage } from '../../lib/errors';
import { PublicacaoCard } from './PublicacaoCard';
import { linkPartilhaFacebook } from './conteudoUi';

/**
 * Sprint 612: aberta pela notificação «saiu no Facebook» — mostra a publicação e um botão grande
 * para a partilhar no perfil pessoal (a API não o permite fazer sozinha).
 */
export default function PartilharPerfil() {
  const { id = '' } = useParams();
  const p = useQuery({ queryKey: ['conteudo', 'publicacao', id], queryFn: () => conteudoApi.obter(id), enabled: Boolean(id) });

  if (p.isLoading) return <SkeletonCard />;
  if (p.isError || !p.data) return <p className="text-sm text-rose-600">{apiErrorMessage(p.error) || 'Publicação não encontrada.'}</p>;

  const fb = p.data.destinos.find((d) => d.rede === RedeSocial.Facebook && d.permalink);

  return (
    <div className="mx-auto max-w-xl space-y-4">
      <Link to="/conteudo?tab=historico" className="inline-flex min-h-10 items-center gap-1 text-sm text-zinc-600 dark:text-zinc-300">
        <ArrowLeft size={16} /> Conteúdo
      </Link>
      {fb?.permalink ? (
        <div className="rounded-xl border border-brand-200 bg-brand-50 p-4 dark:border-brand-900/50 dark:bg-brand-950/30">
          <a
            href={linkPartilhaFacebook(fb.permalink)}
            target="_blank"
            rel="noreferrer"
            className="flex min-h-14 w-full items-center justify-center gap-2 rounded-lg bg-[#1877F2] px-4 text-base font-semibold text-white shadow-sm hover:bg-[#166FE5]"
          >
            <Share2 size={20} /> Partilhar no meu perfil
          </a>
          <ol className="mt-3 list-decimal space-y-1 pl-5 text-sm text-zinc-700 dark:text-zinc-300">
            <li>Abre o Facebook com a publicação da página.</li>
            <li>Em <strong>«Com quem estás?»</strong> escolhe <strong>Lopes Tech</strong>.</li>
            <li>Carrega em <strong>Publicar</strong>.</li>
          </ol>
        </div>
      ) : (
        <EmptyState icon={Share2} title="Ainda não saiu no Facebook" description="Quando sair, voltas a receber a notificação para partilhar." compact />
      )}
      <PublicacaoCard p={p.data} />
    </div>
  );
}
