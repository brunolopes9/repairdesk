import { useRef } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ImagePlus, Trash2, Store } from 'lucide-react';
import { toast } from '../../lib/toast';
import {
  shopConditionImagesApi,
  SHOP_CONDITION_GRADES,
  type ShopConditionImage,
} from '../../lib/shopConditionImages/api';

/**
 * Sprint 531: gestão das 4 imagens ilustrativas por estado de condição (A+/A/B+/B) que a loja
 * online mostra no seletor visual estilo Swappie. Mender = single source of truth.
 */
export function ShopConditionImagesSection() {
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: ['shop-condition-images'],
    queryFn: shopConditionImagesApi.list,
  });
  const byGrade = new Map((data ?? []).map((i) => [i.grade, i]));

  const upload = useMutation({
    mutationFn: ({ grade, file }: { grade: string; file: File }) => shopConditionImagesApi.upload(grade, file),
    onSuccess: () => {
      toast.success('Imagem de estado actualizada.');
      qc.invalidateQueries({ queryKey: ['shop-condition-images'] });
    },
    onError: (e) => toast.fromError(e, 'Não foi possível carregar a imagem.'),
  });
  const remove = useMutation({
    mutationFn: (grade: string) => shopConditionImagesApi.remove(grade),
    onSuccess: () => {
      toast.success('Imagem removida.');
      qc.invalidateQueries({ queryKey: ['shop-condition-images'] });
    },
    onError: (e) => toast.fromError(e, 'Não foi possível remover a imagem.'),
  });

  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="mb-1 flex items-center gap-2 text-sm font-semibold">
        <Store size={17} strokeWidth={2} />
        Imagens de estado (loja online)
      </div>
      <p className="mb-4 text-xs text-zinc-500">
        Imagem ilustrativa por estado de condição que a loja mostra no seletor (estilo Swappie).
        Geridas aqui no Mender — a loja consome-as. Cada estado tem 1 imagem; os que ficarem vazios
        usam a imagem por defeito da loja.
      </p>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {SHOP_CONDITION_GRADES.map((g) => (
          <GradeSlot
            key={g.slug}
            label={g.label}
            hint={g.hint}
            img={byGrade.get(g.slug)}
            loading={isLoading}
            busy={upload.isPending || remove.isPending}
            onUpload={(file) => upload.mutate({ grade: g.slug, file })}
            onRemove={() => remove.mutate(g.slug)}
          />
        ))}
      </div>
    </section>
  );
}

function GradeSlot({
  label,
  hint,
  img,
  loading,
  busy,
  onUpload,
  onRemove,
}: {
  label: string;
  hint: string;
  img: ShopConditionImage | undefined;
  loading: boolean;
  busy: boolean;
  onUpload: (file: File) => void;
  onRemove: () => void;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  return (
    <div className="flex flex-col rounded-lg border border-zinc-200 p-2 dark:border-zinc-800">
      <div className="mb-1 text-xs font-semibold">{label}</div>
      <div className="mb-2 text-[10px] leading-tight text-zinc-400">{hint}</div>
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        disabled={busy}
        className="relative flex aspect-square items-center justify-center overflow-hidden rounded-md border border-dashed border-zinc-300 bg-zinc-50 text-zinc-400 transition hover:border-brand-400 hover:text-brand-500 disabled:opacity-60 dark:border-zinc-700 dark:bg-zinc-950/40"
        title={img ? 'Substituir imagem' : 'Carregar imagem'}
      >
        {img ? (
          <img src={img.url} alt={img.alt ?? label} className="h-full w-full object-cover" />
        ) : (
          <span className="flex flex-col items-center gap-1 text-[11px]">
            <ImagePlus size={20} />
            {loading ? 'A carregar…' : 'Carregar'}
          </span>
        )}
      </button>
      <input
        ref={inputRef}
        type="file"
        accept="image/*"
        className="hidden"
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) onUpload(file);
          e.target.value = '';
        }}
      />
      {img && (
        <button
          type="button"
          onClick={onRemove}
          disabled={busy}
          className="mt-1.5 inline-flex items-center justify-center gap-1 rounded-md border border-zinc-200 px-2 py-1 text-[11px] text-zinc-500 hover:bg-zinc-50 disabled:opacity-60 dark:border-zinc-700 dark:hover:bg-zinc-800"
        >
          <Trash2 size={12} /> Remover
        </button>
      )}
    </div>
  );
}
