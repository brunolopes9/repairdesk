import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from './Button';

/**
 * Sprint 592: "Anterior / Seguinte" de uma lista paginada. Usa-se em cima e em baixo da tabela — não é preciso
 * descer até ao fim para mudar de página. Não aparece com uma só página.
 */
export function Paginacao({ page, pages, total, label, onPage, posicao = 'baixo' }: {
  page: number;
  pages: number;
  total: number;
  /** "vendas", "documentos"… */
  label: string;
  onPage: (p: number) => void;
  posicao?: 'cima' | 'baixo';
}) {
  if (pages <= 1) return null;
  const borda = posicao === 'cima' ? 'border-b' : 'border-t';
  return (
    <div className={`flex items-center justify-between gap-2 ${borda} border-zinc-100 px-4 py-2 text-sm dark:border-zinc-800`}>
      <span className="text-zinc-500">{total} {label} · página {page} de {pages}</span>
      <div className="flex gap-2">
        <Button size="sm" variant="secondary" leftIcon={<ChevronLeft size={14} />} disabled={page <= 1}
          onClick={() => { onPage(page - 1); if (posicao === 'baixo') window.scrollTo({ top: 0 }); }}>Anterior</Button>
        <Button size="sm" variant="secondary" disabled={page >= pages}
          onClick={() => { onPage(page + 1); if (posicao === 'baixo') window.scrollTo({ top: 0 }); }}>
          Seguinte <ChevronRight size={14} className="ml-1 inline" />
        </Button>
      </div>
    </div>
  );
}
