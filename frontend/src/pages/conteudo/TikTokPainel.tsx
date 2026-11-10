import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check } from 'lucide-react';
import { Button } from '../../components/ui';
import { conteudoApi } from '../../lib/conteudo/api';
import { ConteudoFicheiroTipo, RedeSocial, type ConteudoPublicacao, type GuardarOpcoesTikTok } from '../../lib/conteudo/types';
import { redesSociaisApi, TIKTOK_PRIVACIDADE_LABEL, type TikTokPrivacidade } from '../../lib/redesSociais/api';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';

const MUSICA = 'https://www.tiktok.com/legal/page/global/music-usage-confirmation/en';
const MARCA = 'https://www.tiktok.com/legal/page/global/bc-policy/en';

/**
 * Sprint 603: «Publicar no TikTok» dentro do cartão de aprovação. Segue as regras do TikTok para
 * apps que publicam por conta do utilizador: conta lida na hora (creator_info), privacidade sem
 * valor por omissão, interações desligadas até as ligar (e bloqueadas se a conta as desligou),
 * divulgação comercial explícita, consentimento visível antes de aprovar.
 */
export function TikTokPainel({ p }: { p: ConteudoPublicacao }) {
  const qc = useQueryClient();
  const destino = p.destinos.find((d) => d.rede === RedeSocial.TikTok);
  const guardado = destino?.tikTok ?? null;
  const criador = useQuery({
    queryKey: ['tiktok-criador'],
    queryFn: redesSociaisApi.criadorTikTok,
    staleTime: 0,
    refetchOnMount: 'always',
    retry: false,
  });

  const [titulo, setTitulo] = useState(destino?.legendaPropria ?? p.legenda ?? '');
  const [privacidade, setPrivacidade] = useState(guardado?.privacidade ?? '');
  const [comentarios, setComentarios] = useState(guardado?.permitirComentarios ?? false);
  const [dueto, setDueto] = useState(guardado?.permitirDueto ?? false);
  const [stitch, setStitch] = useState(guardado?.permitirStitch ?? false);
  const [divulgacao, setDivulgacao] = useState(guardado?.divulgacao ?? false);
  const [suaMarca, setSuaMarca] = useState(guardado?.suaMarca ?? false);
  const [patrocinado, setPatrocinado] = useState(guardado?.conteudoPatrocinado ?? false);

  const form: GuardarOpcoesTikTok = {
    titulo: titulo.trim() || null,
    privacidade: privacidade || null,
    permitirComentarios: comentarios,
    permitirDueto: dueto,
    permitirStitch: stitch,
    divulgacao,
    suaMarca,
    conteudoPatrocinado: patrocinado,
  };
  const divulgacaoIncompleta = divulgacao && !suaMarca && !patrocinado;
  const igualAoGuardado = guardado !== null
    && guardado.privacidade === privacidade
    && guardado.permitirComentarios === comentarios
    && guardado.permitirDueto === dueto
    && guardado.permitirStitch === stitch
    && guardado.divulgacao === divulgacao
    && guardado.suaMarca === (divulgacao && suaMarca)
    && guardado.conteudoPatrocinado === (divulgacao && patrocinado)
    && (destino?.legendaPropria ?? p.legenda ?? '') === titulo;

  const guardar = useMutation({
    mutationFn: () => conteudoApi.guardarTikTok(p.id, form),
    onSuccess: () => {
      toast.success('Escolhas do TikTok guardadas.');
      qc.invalidateQueries({ queryKey: ['conteudo'] });
    },
    onError: (err) => toast.error(apiErrorMessage(err)),
  });

  if (!destino) return null;
  if (criador.isError) {
    return (
      <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-200">
        TikTok: não foi possível ler a conta ({apiErrorMessage(criador.error)}). Liga outra vez em Definições → Redes sociais.
      </p>
    );
  }

  const c = criador.data;
  const video = p.ficheiros.find((f) => f.tipo === ConteudoFicheiroTipo.Video);
  const longoDemais = c && c.maxDuracaoSeg > 0 && video?.duracaoSeg != null && video.duracaoSeg > c.maxDuracaoSeg;
  const rotulo = !divulgacao ? null
    : patrocinado ? 'O vídeo fica marcado como «Parceria paga».'
    : suaMarca ? 'O vídeo fica marcado como «Conteúdo promocional».'
    : null;

  return (
    <section className="space-y-3 rounded-lg border border-zinc-200 p-3 dark:border-zinc-800" aria-label="Publicar no TikTok">
      <header className="flex items-center gap-2">
        {c?.avatarUrl && <img src={c.avatarUrl} alt="" className="size-8 rounded-full" />}
        <div className="min-w-0 text-sm">
          <p className="font-semibold text-zinc-900 dark:text-zinc-100">Publicar no TikTok</p>
          <p className="truncate text-xs text-zinc-500">
            {c ? <>Conta: <b>{c.nickname}</b>{c.username && ` (@${c.username})`}</> : 'A ler a conta…'}
          </p>
        </div>
        {igualAoGuardado && (
          <span className="ml-auto inline-flex items-center gap-1 text-xs font-medium text-emerald-700 dark:text-emerald-400">
            <Check size={14} /> guardado
          </span>
        )}
      </header>

      {longoDemais && (
        <p className="rounded-md bg-rose-50 px-3 py-2 text-xs text-rose-800 dark:bg-rose-950 dark:text-rose-200">
          O vídeo tem {Math.round(video!.duracaoSeg!)} s e esta conta só aceita até {c!.maxDuracaoSeg} s.
        </p>
      )}

      <label className="block text-xs font-medium text-zinc-600 dark:text-zinc-400">
        Texto do post
        <textarea
          className="mt-1 min-h-20 w-full rounded-md border border-zinc-200 bg-white p-2 text-sm text-zinc-900 outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
          maxLength={2200}
          value={titulo}
          onChange={(e) => setTitulo(e.target.value)}
        />
        <span className="block text-right text-[11px] text-zinc-400">{titulo.length}/2200</span>
      </label>

      <label className="block text-xs font-medium text-zinc-600 dark:text-zinc-400">
        Quem pode ver este vídeo
        <select
          className="mt-1 min-h-10 w-full rounded-md border border-zinc-200 bg-white px-2 text-sm text-zinc-900 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-100"
          value={privacidade}
          onChange={(e) => setPrivacidade(e.target.value)}
          disabled={!c}
        >
          <option value="" disabled>Escolhe…</option>
          {(c?.privacidades ?? []).map((o) => {
            const bloqueada = o === 'SELF_ONLY' && divulgacao && patrocinado;
            return (
              <option key={o} value={o} disabled={bloqueada} title={bloqueada ? 'Conteúdo de marca não pode ser privado.' : undefined}>
                {TIKTOK_PRIVACIDADE_LABEL[o as TikTokPrivacidade] ?? o}
                {bloqueada ? ' — indisponível com conteúdo de marca' : ''}
              </option>
            );
          })}
        </select>
      </label>

      <fieldset className="space-y-1.5">
        <legend className="text-xs font-medium text-zinc-600 dark:text-zinc-400">Permitir aos outros</legend>
        <Caixa label="Comentar" checked={comentarios} onChange={setComentarios} bloqueada={c?.comentariosDesligados} />
        <Caixa label="Fazer dueto" checked={dueto} onChange={setDueto} bloqueada={c?.duetoDesligado} />
        <Caixa label="Fazer stitch" checked={stitch} onChange={setStitch} bloqueada={c?.stitchDesligado} />
      </fieldset>

      <fieldset className="space-y-1.5">
        <label className="flex min-h-9 items-center justify-between gap-3 text-sm text-zinc-800 dark:text-zinc-200">
          <span>Divulgar conteúdo comercial</span>
          <input
            type="checkbox"
            role="switch"
            className="size-4"
            checked={divulgacao}
            onChange={(e) => {
              setDivulgacao(e.target.checked);
              if (!e.target.checked) {
                setSuaMarca(false);
                setPatrocinado(false);
              }
            }}
          />
        </label>
        {divulgacao && (
          <div className="space-y-1.5 border-l-2 border-zinc-200 pl-3 dark:border-zinc-700">
            <Caixa label="A tua marca (promoves o teu negócio)" checked={suaMarca} onChange={setSuaMarca} />
            <Caixa
              label="Conteúdo de marca (parceria paga com outra marca)"
              checked={patrocinado}
              onChange={(v) => {
                setPatrocinado(v);
                if (v && privacidade === 'SELF_ONLY') {
                  setPrivacidade('');
                  toast.info('Conteúdo de marca não pode ser privado — escolhe outra privacidade.');
                }
              }}
            />
            {divulgacaoIncompleta && (
              <p className="text-xs text-rose-700 dark:text-rose-300">Indica se o conteúdo promove a tua marca, uma marca de terceiros, ou ambas.</p>
            )}
            {rotulo && <p className="text-xs text-zinc-500">{rotulo}</p>}
          </div>
        )}
      </fieldset>

      <p className="text-xs leading-5 text-zinc-600 dark:text-zinc-400">
        Ao publicar, aceitas a{' '}
        {divulgacao && patrocinado && (
          <>
            <a className="underline" href={MARCA} target="_blank" rel="noreferrer">Branded Content Policy</a> e a{' '}
          </>
        )}
        <a className="underline" href={MUSICA} target="_blank" rel="noreferrer">Music Usage Confirmation</a> do TikTok.
        Depois de sair, o TikTok pode demorar alguns minutos a mostrar o vídeo no perfil.
      </p>

      <Button
        size="sm"
        variant="secondary"
        className="w-full"
        disabled={!privacidade || divulgacaoIncompleta || igualAoGuardado || !c}
        title={divulgacaoIncompleta ? 'Indica se o conteúdo promove a tua marca, uma marca de terceiros, ou ambas.' : undefined}
        loading={guardar.isPending}
        onClick={() => guardar.mutate()}
      >
        {igualAoGuardado ? 'Escolhas guardadas' : 'Guardar escolhas do TikTok'}
      </Button>
    </section>
  );
}

function Caixa({ label, checked, onChange, bloqueada }: { label: string; checked: boolean; onChange: (v: boolean) => void; bloqueada?: boolean }) {
  return (
    <label className={`flex min-h-9 items-center gap-2 text-sm ${bloqueada ? 'text-zinc-400' : 'text-zinc-800 dark:text-zinc-200'}`}>
      <input type="checkbox" className="size-4" checked={checked && !bloqueada} disabled={bloqueada} onChange={(e) => onChange(e.target.checked)} />
      {label}
      {bloqueada && <span className="text-xs">(desligado na tua conta TikTok)</span>}
    </label>
  );
}
