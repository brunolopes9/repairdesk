import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Clapperboard, Download, Trash2, Upload, XCircle } from 'lucide-react';
import { useConfirm } from '../../components/ConfirmDialog';
import { EmptyState, StatusBadge } from '../../components/ui';
import { conteudoApi } from '../../lib/conteudo/api';
import { ConteudoFicheiroTipo, type ConteudoFicheiro } from '../../lib/conteudo/types';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import { bloquearReload } from '../../lib/reloadGuard';
import { duracao, tamanho } from './conteudoUi';

type Envio = {
  chave: string;
  nome: string;
  bytes: number;
  progresso: number;
  estado: 'a-enviar' | 'feito' | 'erro';
  erro?: string;
};

/**
 * Sprint 598 (Doc 95 §6): o dono envia do telemóvel os vídeos e fotos das reparações (sem cabo).
 * Cada ficheiro sobe direto para o armazenamento com barra de progresso; vários em paralelo
 * limitado a 2 para não saturar os dados móveis. Aviso ao sair da página a meio do envio.
 */
export function BibliotecaVideos() {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const [notas, setNotas] = useState('');
  const [envios, setEnvios] = useState<Envio[]>([]);
  const aEnviar = envios.some((e) => e.estado === 'a-enviar');

  const lista = useQuery({
    queryKey: ['conteudo', 'biblioteca'],
    queryFn: () => conteudoApi.biblioteca(false),
  });

  useEffect(() => {
    if (!aEnviar) return;
    const avisar = (e: BeforeUnloadEvent) => {
      e.preventDefault();
    };
    window.addEventListener('beforeunload', avisar);
    return () => window.removeEventListener('beforeunload', avisar);
  }, [aEnviar]);

  const atualizar = (chave: string, patch: Partial<Envio>) =>
    setEnvios((v) => v.map((e) => (e.chave === chave ? { ...e, ...patch } : e)));

  async function enviarTodos(files: File[]) {
    const nota = notas.trim() || null;
    const novos = files.map((f) => ({
      file: f,
      envio: { chave: `${f.name}-${f.size}-${Date.now()}-${Math.random()}`, nome: f.name, bytes: f.size, progresso: 0, estado: 'a-enviar' as const },
    }));
    setEnvios((v) => [...novos.map((n) => n.envio), ...v]);

    const fila = [...novos];
    const trabalhar = async () => {
      for (let item = fila.shift(); item; item = fila.shift()) {
        const { file, envio } = item;
        try {
          await conteudoApi.enviar(file, nota, (p) => atualizar(envio.chave, { progresso: p }));
          atualizar(envio.chave, { estado: 'feito', progresso: 1 });
        } catch (err) {
          atualizar(envio.chave, { estado: 'erro', erro: apiErrorMessage(err) });
        }
      }
    };
    const libertar = bloquearReload();
    try {
      await Promise.all([trabalhar(), trabalhar()]);
    } finally {
      libertar();
    }
    qc.invalidateQueries({ queryKey: ['conteudo'] });
    setNotas('');
  }

  const apagar = useMutation({
    mutationFn: (id: string) => conteudoApi.apagarFicheiro(id),
    onSuccess: () => {
      toast.success('Ficheiro apagado.');
      qc.invalidateQueries({ queryKey: ['conteudo'] });
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Não foi possível apagar.'),
  });

  const ficheiros = lista.data ?? [];

  return (
    <div className="space-y-5">
      <section className="rounded-lg border border-zinc-200 bg-white p-4 shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
        <h3 className="text-base font-semibold text-zinc-950 dark:text-zinc-50">Enviar vídeos e fotos</h3>
        <p className="mt-1 text-sm text-zinc-500">
          Grava a reparação no telemóvel e envia daqui — mesmo vídeos longos. Eu edito e preparo os reels.
        </p>
        <input
          className="mt-3 min-h-11 w-full rounded-md border border-zinc-200 bg-white px-3 text-sm outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 dark:border-zinc-700 dark:bg-zinc-950"
          placeholder="Nota opcional — ex.: troca de ecrã iPhone 12, cliente autorizou filmar"
          maxLength={1000}
          value={notas}
          onChange={(e) => setNotas(e.target.value)}
        />
        {/*
          O toque tem de cair no próprio <input> (dentro do <label>): no Safari do iPhone, abrir o
          seletor por input.click() num input escondido (display:none) às vezes não devolve os
          ficheiros à página. sr-only mantém-no acessível sem o mostrar.
        */}
        <label className="mt-3 inline-flex min-h-11 w-full cursor-pointer items-center justify-center gap-1.5 rounded-lg bg-brand-600 px-4 text-sm font-medium text-white shadow-sm hover:bg-brand-700 active:bg-brand-800 sm:w-auto">
          <Upload size={18} />
          Escolher ficheiros
          <input
            type="file"
            accept="video/*,image/*"
            multiple
            className="sr-only"
            onChange={(e) => {
              const files = Array.from(e.target.files ?? []);
              e.target.value = '';
              if (files.length > 0) void enviarTodos(files);
            }}
          />
        </label>
        <p className="mt-2 text-xs text-zinc-500">
          Vídeos e fotos misturados, quantos quiseres. No iPhone, com muitos vídeos o telemóvel demora a preparar
          antes de começar — se houver «Opções» no topo da fototeca, escolhe «Atual» (é mais rápido).
        </p>

        {envios.length > 0 && (
          <ul className="mt-4 space-y-2">
            {envios.map((e) => (
              <li key={e.chave} className="rounded-md border border-zinc-200 p-3 text-sm dark:border-zinc-800">
                <div className="flex items-center justify-between gap-2">
                  <span className="min-w-0 truncate font-medium">{e.nome}</span>
                  <span className="shrink-0 text-xs text-zinc-500">
                    {e.estado === 'feito' ? (
                      <CheckCircle2 size={16} className="text-emerald-600" />
                    ) : e.estado === 'erro' ? (
                      <XCircle size={16} className="text-rose-600" />
                    ) : (
                      `${Math.round(e.progresso * 100)}% de ${tamanho(e.bytes)}`
                    )}
                  </span>
                </div>
                {e.estado === 'a-enviar' && (
                  <div className="mt-2 h-2 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800">
                    <div className="h-full rounded-full bg-brand-600 transition-all" style={{ width: `${Math.max(2, e.progresso * 100)}%` }} />
                  </div>
                )}
                {e.erro && <p className="mt-1 text-xs text-rose-700 dark:text-rose-300">{e.erro}</p>}
              </li>
            ))}
          </ul>
        )}
        {aEnviar && <p className="mt-3 text-xs text-amber-700 dark:text-amber-300">Não feches esta página até acabar o envio.</p>}
      </section>

      {lista.isLoading ? null : ficheiros.length === 0 ? (
        <EmptyState icon={Clapperboard} title="Ainda não há vídeos" description="Os vídeos que enviares aparecem aqui até eu os usar num reel." />
      ) : (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {ficheiros.map((f) => (
            <FicheiroTile
              key={f.id}
              f={f}
              aApagar={apagar.isPending && apagar.variables === f.id}
              onApagar={async () => {
                const ok = await confirm({
                  title: 'Apagar este ficheiro?',
                  description: `«${f.nomeOriginal}» é apagado do armazenamento e não se recupera.`,
                  confirmLabel: 'Apagar',
                  destructive: true,
                });
                if (ok) apagar.mutate(f.id);
              }}
            />
          ))}
        </ul>
      )}
    </div>
  );
}

function FicheiroTile({ f, onApagar, aApagar }: { f: ConteudoFicheiro; onApagar: () => void; aApagar: boolean }) {
  const dur = duracao(f.duracaoSeg);
  return (
    <li className="overflow-hidden rounded-lg border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
      <div className="relative aspect-[3/4] bg-zinc-900">
        {f.url &&
          (f.tipo === ConteudoFicheiroTipo.Video ? (
            <video src={f.url} controls playsInline preload="metadata" className="h-full w-full object-contain" />
          ) : (
            <img src={f.url} alt={f.nomeOriginal} loading="lazy" className="h-full w-full object-cover" />
          ))}
        <span className="absolute left-2 top-2">
          {f.processadoEm ? <StatusBadge tone="zinc">Usado</StatusBadge> : <StatusBadge tone="emerald">Novo</StatusBadge>}
        </span>
      </div>
      <div className="space-y-1 p-2">
        <p className="truncate text-xs font-medium text-zinc-900 dark:text-zinc-100" title={f.nomeOriginal}>{f.nomeOriginal}</p>
        <p className="text-xs text-zinc-500">
          {tamanho(f.bytes)}
          {dur && ` · ${dur}`}
          {` · ${new Date(f.createdAt).toLocaleDateString('pt-PT')}`}
        </p>
        {f.notas && <p className="line-clamp-2 text-xs text-zinc-600 dark:text-zinc-400">{f.notas}</p>}
        <div className="flex gap-1 pt-1">
          {f.url && (
            <a
              href={f.url}
              download={f.nomeOriginal}
              className="grid h-9 w-9 place-items-center rounded-md text-zinc-500 hover:bg-zinc-100 dark:hover:bg-zinc-800"
              title="Descarregar"
            >
              <Download size={16} />
            </a>
          )}
          <button
            type="button"
            onClick={onApagar}
            disabled={aApagar}
            className="grid h-9 w-9 place-items-center rounded-md text-zinc-500 hover:bg-rose-50 hover:text-rose-700 disabled:opacity-50 dark:hover:bg-rose-950"
            title="Apagar"
          >
            <Trash2 size={16} />
          </button>
        </div>
      </div>
    </li>
  );
}
