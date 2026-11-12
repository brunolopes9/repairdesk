import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Trash2, X } from 'lucide-react';
import { Button } from '../../components/ui';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import { DIAS, RotinaTipo, TIPO_INFO, TODOS_OS_DIAS, rotinaApi, type GuardarRotinaItem, type RotinaAlimento, type RotinaItem } from '../../lib/rotina/api';

const VAZIO: GuardarRotinaItem = {
  titulo: '', detalhe: null, tipo: RotinaTipo.Trabalho, hora: '09:00', duracaoMin: 60, dias: TODOS_OS_DIAS,
  intervaloDias: null, dataInicio: null, avisosAntes: null, insistir: false, avisarFim: true, ativo: true,
};

const campo = 'mt-1 w-full rounded-lg border border-zinc-300 px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950';

/** Sprint 615: criar/editar um item da rotina (hora, dias, avisos). */
export default function RotinaItemForm({ item, onClose }: { item: RotinaItem | null; onClose: () => void }) {
  const qc = useQueryClient();
  const [f, setF] = useState<GuardarRotinaItem>(() => {
    if (!item) return VAZIO;
    const { id: _id, alimentos: _a, macros: _m, ...resto } = item;
    return resto;
  });
  const [alimentos, setAlimentos] = useState<RotinaAlimento[]>(() => item?.alimentos ?? []);
  const set = <K extends keyof GuardarRotinaItem>(k: K, v: GuardarRotinaItem[K]) => setF((x) => ({ ...x, [k]: v }));
  const repetir = f.intervaloDias ? 'intervalo' : 'semana';

  const guardar = useMutation({
    mutationFn: async () => {
      const r = item ? await rotinaApi.atualizar(item.id, f) : await rotinaApi.criar(f);
      if (f.tipo === RotinaTipo.Refeicao) await rotinaApi.alimentos(r.id, alimentos.filter((a) => a.nome.trim()));
      return r;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rotina'] });
      toast.success(item ? 'Guardado' : 'Criado', 'Os avisos seguem esta configuração a partir de agora.');
      onClose();
    },
    onError: (e) => toast.error('Não ficou guardado', apiErrorMessage(e)),
  });
  const apagar = useMutation({
    mutationFn: () => rotinaApi.apagar(item!.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['rotina'] });
      onClose();
    },
    onError: (e) => toast.error('Não foi apagado', apiErrorMessage(e)),
  });

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 sm:items-center sm:p-4" onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-labelledby="rotina-form-titulo"
        className="max-h-[92vh] w-full overflow-y-auto rounded-t-2xl border border-zinc-200 bg-white p-5 shadow-xl dark:border-zinc-800 dark:bg-zinc-900 sm:max-w-lg sm:rounded-2xl"
        onClick={(e) => e.stopPropagation()}>
        <div className="mb-4 flex items-center justify-between">
          <h2 id="rotina-form-titulo" className="text-lg font-semibold">{item ? 'Editar item' : 'Novo item da rotina'}</h2>
          <Button variant="ghost" size="sm" aria-label="Fechar" onClick={onClose}><X size={16} /></Button>
        </div>

        <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); guardar.mutate(); }}>
          <label className="block text-sm">Título
            <input required maxLength={80} value={f.titulo} onChange={(e) => set('titulo', e.target.value)} className={campo} placeholder="ex.: Almoço" />
          </label>
          <div className="grid grid-cols-2 gap-3">
            <label className="block text-sm">Tipo
              <select value={f.tipo} onChange={(e) => set('tipo', Number(e.target.value) as RotinaTipo)} className={campo}>
                {Object.entries(TIPO_INFO).map(([v, i]) => <option key={v} value={v}>{i.icone} {i.label}</option>)}
              </select>
            </label>
            <label className="block text-sm">Hora
              <input type="time" required value={f.hora} onChange={(e) => set('hora', e.target.value)} className={campo} />
            </label>
          </div>
          <label className="block text-sm">Duração (min) — 0 para um momento (ex.: refeição)
            <input type="number" min={0} max={720} value={f.duracaoMin} onChange={(e) => set('duracaoMin', Number(e.target.value))} className={campo} />
          </label>
          <label className="block text-sm">Detalhe — uma linha por passo
            <textarea rows={3} maxLength={1000} value={f.detalhe ?? ''} onChange={(e) => set('detalhe', e.target.value || null)} className={campo} placeholder={'Arroz\n150–200 g carne ou peixe\nAnanás'} />
          </label>

          {f.tipo === RotinaTipo.Refeicao && (
            <fieldset className="space-y-1 text-sm">
              <legend>Alimentos (valores da quantidade indicada)</legend>
              <div className="grid grid-cols-[1fr_4.5rem_3.5rem_3rem_3rem_3rem_1.5rem] gap-1 text-[11px] text-zinc-500">
                <span>Alimento</span><span>Qtd.</span><span>kcal</span><span>P</span><span>H</span><span>G</span><span />
              </div>
              {alimentos.map((a, i) => (
                <div key={i} className="grid grid-cols-[1fr_4.5rem_3.5rem_3rem_3rem_3rem_1.5rem] gap-1">
                  {(['nome', 'quantidade'] as const).map((k) => (
                    <input key={k} aria-label={k} value={a[k] ?? ''} className="rounded border border-zinc-300 px-1.5 py-1 text-xs dark:border-zinc-700 dark:bg-zinc-950"
                      onChange={(e) => setAlimentos((xs) => xs.map((x, j) => (j === i ? { ...x, [k]: e.target.value } : x)))} />
                  ))}
                  {(['kcal', 'proteinaG', 'hidratosG', 'gorduraG'] as const).map((k) => (
                    <input key={k} aria-label={k} inputMode="decimal" value={a[k]} className="rounded border border-zinc-300 px-1 py-1 text-xs tabular-nums dark:border-zinc-700 dark:bg-zinc-950"
                      onChange={(e) => setAlimentos((xs) => xs.map((x, j) => (j === i ? { ...x, [k]: Number(e.target.value.replace(',', '.')) || 0 } : x)))} />
                  ))}
                  <button type="button" aria-label={`Remover ${a.nome}`} className="text-zinc-400 hover:text-rose-600"
                    onClick={() => setAlimentos((xs) => xs.filter((_, j) => j !== i))}>×</button>
                </div>
              ))}
              <Button type="button" size="sm" variant="ghost"
                onClick={() => setAlimentos((xs) => [...xs, { nome: '', quantidade: '', kcal: 0, proteinaG: 0, hidratosG: 0, gorduraG: 0 }])}>
                + Alimento
              </Button>
            </fieldset>
          )}

          <fieldset className="text-sm">
            <legend>Repete</legend>
            <div className="mt-1 flex gap-2">
              <button type="button" onClick={() => setF((x) => ({ ...x, intervaloDias: null, dias: x.dias || TODOS_OS_DIAS }))}
                className={`rounded-full border px-3 py-1 ${repetir === 'semana' ? 'border-brand-500 bg-brand-50 dark:bg-brand-950/40' : 'border-zinc-300 dark:border-zinc-700'}`}>Dias da semana</button>
              <button type="button" onClick={() => setF((x) => ({ ...x, intervaloDias: x.intervaloDias ?? 14 }))}
                className={`rounded-full border px-3 py-1 ${repetir === 'intervalo' ? 'border-brand-500 bg-brand-50 dark:bg-brand-950/40' : 'border-zinc-300 dark:border-zinc-700'}`}>De N em N dias</button>
            </div>
            {repetir === 'semana' ? (
              <div className="mt-2 flex flex-wrap gap-1">
                {DIAS.map((d) => {
                  const on = (f.dias & d.bit) !== 0;
                  return (
                    <button key={d.bit} type="button" aria-pressed={on} onClick={() => set('dias', f.dias ^ d.bit)}
                      className={`w-11 rounded-lg border py-1 ${on ? 'border-brand-500 bg-brand-500 text-white' : 'border-zinc-300 dark:border-zinc-700'}`}>{d.curto}</button>
                  );
                })}
              </div>
            ) : (
              <div className="mt-2 grid grid-cols-2 gap-3">
                <label className="block">A cada (dias)
                  <input type="number" min={1} max={90} value={f.intervaloDias ?? 14} onChange={(e) => set('intervaloDias', Number(e.target.value))} className={campo} />
                </label>
                <label className="block">A partir de
                  <input type="date" value={f.dataInicio ?? ''} onChange={(e) => set('dataInicio', e.target.value || null)} className={campo} />
                </label>
              </div>
            )}
          </fieldset>

          <fieldset className="space-y-2 text-sm">
            <legend>Avisos no telemóvel</legend>
            <label className="block">Avisar antes (min, separados por vírgula)
              <input value={f.avisosAntes ?? ''} onChange={(e) => set('avisosAntes', e.target.value || null)} className={campo} placeholder="ex.: 15, 5" />
            </label>
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={f.insistir} onChange={(e) => set('insistir', e.target.checked)} />
              Insistir se não marcar como feito (+15 e +30 min)
            </label>
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={f.avisarFim} disabled={f.duracaoMin === 0} onChange={(e) => set('avisarFim', e.target.checked)} />
              Avisar quando o bloco acaba
            </label>
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={f.ativo} onChange={(e) => set('ativo', e.target.checked)} />
              Ativo
            </label>
          </fieldset>

          <div className="flex items-center justify-between pt-2">
            {item ? (
              <Button type="button" variant="danger" size="sm" leftIcon={<Trash2 size={14} />} disabled={apagar.isPending} onClick={() => apagar.mutate()}>Apagar</Button>
            ) : <span />}
            <div className="flex gap-2">
              <Button type="button" variant="ghost" onClick={onClose}>Cancelar</Button>
              <Button type="submit" loading={guardar.isPending}>Guardar</Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
