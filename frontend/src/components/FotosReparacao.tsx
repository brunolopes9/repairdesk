import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import { Camera } from 'lucide-react';
import Modal from './Modal';
import {
  FOTO_TIPO,
  FOTO_TIPO_COLOR,
  FOTO_TIPO_LABEL,
  fotosApi,
  type Foto,
  type FotoTipo,
} from '../lib/fotos/api';

interface Props {
  reparacaoId: string;
  readOnly?: boolean;
}

export default function FotosReparacao({ reparacaoId, readOnly = false }: Props) {
  const qc = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);
  const [uploadTipo, setUploadTipo] = useState<FotoTipo>(FOTO_TIPO.Antes);
  const [editing, setEditing] = useState<Foto | null>(null);
  const [lightbox, setLightbox] = useState<Foto | null>(null);

  const list = useQuery({
    queryKey: ['fotos', reparacaoId],
    queryFn: () => fotosApi.list(reparacaoId),
  });

  const upload = useMutation({
    mutationFn: ({ file, tipo }: { file: File; tipo: FotoTipo }) => fotosApi.upload(reparacaoId, file, tipo),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['fotos', reparacaoId] });
      setError(null);
    },
    onError: (err) => {
      if (isAxiosError(err)) setError(err.response?.data?.detail ?? 'Erro ao carregar foto.');
    },
  });

  const remove = useMutation({
    mutationFn: (id: string) => fotosApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['fotos', reparacaoId] }),
  });

  function handleFiles(files: FileList | null) {
    if (!files || readOnly) return;
    Array.from(files).forEach((f) => upload.mutate({ file: f, tipo: uploadTipo }));
  }

  const items = list.data ?? [];
  const grouped = {
    [FOTO_TIPO.Antes]: items.filter((f) => f.tipo === FOTO_TIPO.Antes),
    [FOTO_TIPO.Durante]: items.filter((f) => f.tipo === FOTO_TIPO.Durante),
    [FOTO_TIPO.Depois]: items.filter((f) => f.tipo === FOTO_TIPO.Depois),
  };

  return (
    <section className="space-y-3 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="flex items-center gap-2 text-sm font-semibold">
            <Camera size={15} strokeWidth={2} className="text-zinc-500" />
            Fotos antes / depois
          </h2>
          <p className="text-[11px] text-zinc-500">
            Prova visual para o cliente. Fotos "Antes" e "Depois" aparecem automaticamente no portal cliente.
          </p>
        </div>
        {!readOnly && (
          <div className="flex items-center gap-1 text-xs">
            <span className="text-zinc-500">Próxima foto:</span>
            <div className="inline-flex rounded-lg border border-zinc-200 bg-white p-0.5 dark:border-zinc-800 dark:bg-zinc-950">
              {([FOTO_TIPO.Antes, FOTO_TIPO.Durante, FOTO_TIPO.Depois] as FotoTipo[]).map((t) => (
                <button
                  key={t}
                  type="button"
                  onClick={() => setUploadTipo(t)}
                  className={`rounded-md px-2 py-1 transition ${uploadTipo === t ? FOTO_TIPO_COLOR[t] : 'text-zinc-500 hover:bg-zinc-100 dark:hover:bg-zinc-800'}`}
                >
                  {FOTO_TIPO_LABEL[t]}
                </button>
              ))}
            </div>
          </div>
        )}
      </div>

      {error && (
        <div className="rounded-lg bg-rose-50 px-3 py-2 text-xs text-rose-700 dark:bg-rose-950/30 dark:text-rose-300">
          {error}
        </div>
      )}

      {!readOnly && (
        <div
          onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
          onDragLeave={() => setDragging(false)}
          onDrop={(e) => { e.preventDefault(); setDragging(false); handleFiles(e.dataTransfer.files); }}
          className={`rounded-xl border-2 border-dashed p-4 text-center text-xs transition ${
            dragging
              ? 'border-brand-500 bg-brand-50 dark:border-brand-400 dark:bg-brand-950/30'
              : 'border-zinc-300 bg-zinc-50/30 dark:border-zinc-700 dark:bg-zinc-950/50'
          }`}
        >
          <div className="text-zinc-500">
            Arrasta fotos para aqui (até 10 MB · JPEG/PNG/WebP) — tag: <strong>{FOTO_TIPO_LABEL[uploadTipo]}</strong>
          </div>
          <label className="mt-2 inline-block cursor-pointer rounded-md bg-brand-600 px-3 py-1 text-[11px] font-medium text-white hover:bg-brand-700">
            {upload.isPending ? 'A carregar…' : 'Selecionar'}
            <input
              type="file"
              accept="image/jpeg,image/png,image/webp"
              multiple
              className="hidden"
              onChange={(e) => handleFiles(e.target.files)}
            />
          </label>
        </div>
      )}

      {list.isLoading ? (
        <div className="text-xs text-zinc-500">A carregar…</div>
      ) : items.length === 0 ? (
        <div className="rounded-lg border border-dashed border-zinc-300 p-4 text-center text-xs text-zinc-500 dark:border-zinc-700">
          Sem fotos ainda.
        </div>
      ) : (
        <div className="space-y-3">
          {([FOTO_TIPO.Antes, FOTO_TIPO.Durante, FOTO_TIPO.Depois] as FotoTipo[]).map((t) => {
            const fotos = grouped[t];
            if (fotos.length === 0) return null;
            return (
              <div key={t}>
                <h3 className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-zinc-500">
                  {FOTO_TIPO_LABEL[t]} · {fotos.length}
                </h3>
                <ul className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4">
                  {fotos.map((foto) => (
                    <li key={foto.id} className="group relative">
                      <FotoThumb foto={foto} onClick={() => setLightbox(foto)} />
                      <div className="mt-1 flex items-center justify-between gap-1 text-[10px]">
                        <span className={`rounded-full px-1.5 py-0.5 ${FOTO_TIPO_COLOR[foto.tipo]}`}>{FOTO_TIPO_LABEL[foto.tipo]}</span>
                        {foto.visivelNoPortal && <span className="text-emerald-600 dark:text-emerald-400" title="Visível no portal cliente">👁</span>}
                      </div>
                      {foto.legenda && <div className="truncate text-[10px] text-zinc-500">{foto.legenda}</div>}
                      {!readOnly && (
                        <div className="absolute right-1 top-1 flex gap-0.5 opacity-0 transition group-hover:opacity-100">
                          <button
                            type="button"
                            onClick={() => setEditing(foto)}
                            className="rounded bg-white/90 px-1 text-[10px] text-zinc-700 hover:bg-white dark:bg-zinc-900/90 dark:text-zinc-200"
                          >✎</button>
                          <button
                            type="button"
                            onClick={() => { if (confirm('Apagar foto?')) remove.mutate(foto.id); }}
                            className="rounded bg-white/90 px-1 text-[10px] text-rose-600 hover:bg-white dark:bg-zinc-900/90"
                          >✕</button>
                        </div>
                      )}
                    </li>
                  ))}
                </ul>
              </div>
            );
          })}
        </div>
      )}

      {lightbox && <LightboxModal foto={lightbox} onClose={() => setLightbox(null)} />}
      {editing && (
        <EditFotoModal
          foto={editing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); qc.invalidateQueries({ queryKey: ['fotos', reparacaoId] }); }}
        />
      )}
    </section>
  );
}

function FotoThumb({ foto, onClick }: { foto: Foto; onClick: () => void }) {
  const [src, setSrc] = useState<string | null>(null);

  useEffect(() => {
    let url: string | null = null;
    let cancelled = false;
    fotosApi.contentUrl(foto.id).then((u) => {
      if (cancelled) {
        URL.revokeObjectURL(u);
      } else {
        url = u;
        setSrc(u);
      }
    }).catch(() => {});
    return () => {
      cancelled = true;
      if (url) URL.revokeObjectURL(url);
    };
  }, [foto.id]);

  return (
    <button
      type="button"
      onClick={onClick}
      className="block aspect-square w-full overflow-hidden rounded-lg border border-zinc-200 bg-zinc-100 dark:border-zinc-800 dark:bg-zinc-950"
    >
      {src ? (
        <img src={src} alt={foto.legenda ?? foto.fileName} className="h-full w-full object-cover transition group-hover:scale-105" />
      ) : (
        <div className="grid h-full w-full place-items-center text-zinc-400">⋯</div>
      )}
    </button>
  );
}

function LightboxModal({ foto, onClose }: { foto: Foto; onClose: () => void }) {
  const [src, setSrc] = useState<string | null>(null);

  useEffect(() => {
    let url: string | null = null;
    let cancelled = false;
    fotosApi.contentUrl(foto.id).then((u) => {
      if (cancelled) {
        URL.revokeObjectURL(u);
      } else {
        url = u;
        setSrc(u);
      }
    }).catch(() => {});
    return () => {
      cancelled = true;
      if (url) URL.revokeObjectURL(url);
    };
  }, [foto.id]);

  return (
    <div
      onClick={onClose}
      className="fixed inset-0 z-50 grid place-items-center bg-black/85 p-4"
    >
      <div className="max-h-full max-w-5xl" onClick={(e) => e.stopPropagation()}>
        {src ? (
          <img src={src} alt={foto.fileName} className="max-h-[85vh] max-w-full rounded-lg object-contain" />
        ) : (
          <div className="text-white">A carregar…</div>
        )}
        {foto.legenda && <p className="mt-2 text-center text-sm text-zinc-200">{foto.legenda}</p>}
        <button
          type="button"
          onClick={onClose}
          className="absolute right-4 top-4 rounded-full bg-white/90 px-3 py-1 text-sm hover:bg-white"
        >Fechar ✕</button>
      </div>
    </div>
  );
}

function EditFotoModal({ foto, onClose, onSaved }: { foto: Foto; onClose: () => void; onSaved: () => void }) {
  const [tipo, setTipo] = useState<FotoTipo>(foto.tipo);
  const [legenda, setLegenda] = useState(foto.legenda ?? '');
  const [visivel, setVisivel] = useState(foto.visivelNoPortal);

  const save = useMutation({
    mutationFn: () => fotosApi.update(foto.id, {
      tipo,
      ordem: foto.ordem,
      legenda: legenda.trim() || null,
      visivelNoPortal: visivel,
    }),
    onSuccess: onSaved,
  });

  return (
    <Modal
      open
      title="Editar foto"
      onClose={onClose}
      footer={<>
        <button type="button" onClick={onClose} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
        <button type="button" disabled={save.isPending} onClick={() => save.mutate()} className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
          {save.isPending ? 'A guardar…' : 'Guardar'}
        </button>
      </>}
    >
      <div className="space-y-3 text-sm">
        <label className="block">
          <span className="mb-1 block text-xs text-zinc-500">Tipo</span>
          <select value={tipo} onChange={(e) => setTipo(Number(e.target.value) as FotoTipo)} className="w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 dark:border-zinc-700 dark:bg-zinc-950">
            {([FOTO_TIPO.Antes, FOTO_TIPO.Durante, FOTO_TIPO.Depois] as FotoTipo[]).map((t) => (
              <option key={t} value={t}>{FOTO_TIPO_LABEL[t]}</option>
            ))}
          </select>
        </label>
        <label className="block">
          <span className="mb-1 block text-xs text-zinc-500">Legenda</span>
          <input value={legenda} onChange={(e) => setLegenda(e.target.value)} placeholder="ex: ecrã com fissura no canto" className="w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 dark:border-zinc-700 dark:bg-zinc-950" />
        </label>
        <label className="flex items-center gap-2 text-xs">
          <input type="checkbox" checked={visivel} onChange={(e) => setVisivel(e.target.checked)} />
          Visível no portal cliente público
        </label>
      </div>
    </Modal>
  );
}
