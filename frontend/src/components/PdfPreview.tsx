import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '../lib/api';

/**
 * Sprint 592: mostra um PDF (ou imagem) autenticado dentro da página — ex.: a fatura de uma despesa ao lado dos
 * dados. O ficheiro vem pela API (com o token) e é mostrado num iframe a partir de um blob local.
 */
export function PdfPreview({ path, title, className = 'h-[75vh]' }: { path: string; title: string; className?: string }) {
  const ficheiro = useQuery({
    queryKey: ['pdf-preview', path],
    queryFn: () => api.get<Blob>(path, { responseType: 'blob' }).then((r) => r.data),
    staleTime: 5 * 60_000,
  });
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!ficheiro.data) return;
    const u = URL.createObjectURL(ficheiro.data);
    setUrl(u);
    return () => URL.revokeObjectURL(u);
  }, [ficheiro.data]);

  if (ficheiro.isError) return <p className="p-4 text-sm text-rose-600">Não foi possível carregar a fatura.</p>;
  if (!url) return <div className={`${className} w-full animate-pulse rounded-lg bg-zinc-100 dark:bg-zinc-800`} />;
  return <iframe src={url} title={title} className={`${className} w-full rounded-lg border border-zinc-200 bg-white dark:border-zinc-800`} />;
}
