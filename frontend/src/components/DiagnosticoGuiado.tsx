import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import { Stethoscope } from 'lucide-react';
import { SkeletonRow } from './ui';
import {
  DEVICE_CATEGORY,
  DEVICE_CATEGORY_LABEL,
  RESULTADO,
  diagnosticoApi,
  type DeviceCategory,
  type DiagnosticoExecucaoItem,
  type Resultado,
} from '../lib/diagnostico/api';

interface Props {
  reparacaoId: string;
  readOnly?: boolean;
}

export default function DiagnosticoGuiado({ reparacaoId, readOnly = false }: Props) {
  const qc = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const exec = useQuery({
    queryKey: ['diagnostico', reparacaoId],
    queryFn: () => diagnosticoApi.getByReparacao(reparacaoId),
  });

  const templates = useQuery({
    queryKey: ['diagnostico-templates'],
    queryFn: () => diagnosticoApi.listTemplates(),
    enabled: exec.data === null && !readOnly,
    staleTime: 5 * 60_000,
  });

  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null);
  const [selectedCategoria, setSelectedCategoria] = useState<DeviceCategory>(DEVICE_CATEGORY.Smartphone);

  const start = useMutation({
    mutationFn: () =>
      diagnosticoApi.start(reparacaoId, {
        templateId: selectedTemplateId,
        categoria: selectedTemplateId ? null : selectedCategoria,
      }),
    onSuccess: (data) => {
      qc.setQueryData(['diagnostico', reparacaoId], data);
      setError(null);
    },
    onError: (err) => {
      if (isAxiosError(err)) setError(err.response?.data?.detail ?? 'Não foi possível iniciar.');
    },
  });

  if (exec.isLoading) {
    return (
      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <h2 className="flex items-center gap-2 text-sm font-semibold">
          <Stethoscope size={15} strokeWidth={2} className="text-zinc-500" />
          Diagnóstico Guiado
        </h2>
        <div className="mt-3 rounded-lg border border-zinc-200 dark:border-zinc-800">
          <SkeletonRow columns={2} />
        </div>
      </section>
    );
  }

  if (!exec.data) {
    if (readOnly) return null;
    return (
      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <h2 className="flex items-center gap-2 text-sm font-semibold">
          <Stethoscope size={15} strokeWidth={2} className="text-zinc-500" />
          Diagnóstico Guiado
        </h2>
        <p className="mt-2 text-xs text-zinc-500">
          Faz um checklist visual do equipamento antes/depois de reparar. Gera Health Score 0-100 e relatório
          profissional para o cliente.
        </p>
        {error && (
          <div className="mt-2 rounded-md bg-rose-50 px-2 py-1 text-xs text-rose-700 dark:bg-rose-950/30 dark:text-rose-300">
            {error}
          </div>
        )}
        <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
          <label className="text-xs">
            <span className="block text-zinc-500">Template</span>
            <select
              value={selectedTemplateId ?? ''}
              onChange={(e) => setSelectedTemplateId(e.target.value || null)}
              className="mt-1 min-h-11 w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950"
            >
              <option value="">— default por categoria —</option>
              {templates.data?.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nome} {t.isDefault ? ' ★' : ''}
                </option>
              ))}
            </select>
          </label>
          {!selectedTemplateId && (
            <label className="text-xs">
              <span className="block text-zinc-500">Categoria</span>
              <select
                value={selectedCategoria}
                onChange={(e) => setSelectedCategoria(Number(e.target.value) as DeviceCategory)}
                className="mt-1 min-h-11 w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950"
              >
                {Object.entries(DEVICE_CATEGORY).map(([_, v]) => (
                  <option key={v} value={v}>
                    {DEVICE_CATEGORY_LABEL[v as DeviceCategory]}
                  </option>
                ))}
              </select>
            </label>
          )}
        </div>
        <button
          type="button"
          disabled={start.isPending}
          onClick={() => start.mutate()}
          className="mt-3 min-h-11 rounded-md bg-brand-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-60"
        >
          {start.isPending ? 'A iniciar…' : '✚ Iniciar diagnóstico'}
        </button>
      </section>
    );
  }

  return <DiagnosticoActivo execucao={exec.data} reparacaoId={reparacaoId} readOnly={readOnly} />;
}

function DiagnosticoActivo({
  execucao,
  reparacaoId,
  readOnly,
}: {
  execucao: NonNullable<Awaited<ReturnType<typeof diagnosticoApi.getByReparacao>>>;
  reparacaoId: string;
  readOnly: boolean;
}) {
  const qc = useQueryClient();
  const [items, setItems] = useState<DiagnosticoExecucaoItem[]>(execucao.items);
  const [notasGerais, setNotasGerais] = useState(execucao.notasGerais ?? '');
  const [error, setError] = useState<string | null>(null);
  const [savedAt, setSavedAt] = useState<Date | null>(null);
  const debounceRef = useRef<number | null>(null);

  // Reset quando o servidor devolve novo execução. Depende só de id/completadoEm
  // de propósito — incluir items/notasGerais nas deps causaria reset a cada
  // edição local antes de gravar (apagaria o trabalho do utilizador).
  useEffect(() => {
    setItems(execucao.items);
    setNotasGerais(execucao.notasGerais ?? '');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [execucao.id, execucao.completadoEm]);

  const update = useMutation({
    mutationFn: (markComplete: boolean) =>
      diagnosticoApi.update(reparacaoId, {
        notasGerais: notasGerais.trim() || null,
        marcarCompletado: markComplete,
        items: items.map((i) => ({ itemId: i.id, resultado: i.resultado, notas: i.notas })),
      }),
    onSuccess: (data) => {
      qc.setQueryData(['diagnostico', reparacaoId], data);
      setSavedAt(new Date());
      setError(null);
    },
    onError: (err) => {
      if (isAxiosError(err)) setError(err.response?.data?.detail ?? 'Erro ao guardar.');
    },
  });

  const remove = useMutation({
    mutationFn: () => diagnosticoApi.remove(reparacaoId),
    onSuccess: () => {
      qc.setQueryData(['diagnostico', reparacaoId], null);
    },
  });

  function setResult(id: string, resultado: Resultado) {
    setItems((prev) => prev.map((i) => (i.id === id ? { ...i, resultado } : i)));
    triggerAutoSave();
  }

  function setItemNotas(id: string, notas: string) {
    setItems((prev) => prev.map((i) => (i.id === id ? { ...i, notas } : i)));
    triggerAutoSave();
  }

  function triggerAutoSave() {
    if (readOnly) return;
    if (debounceRef.current) window.clearTimeout(debounceRef.current);
    debounceRef.current = window.setTimeout(() => update.mutate(false), 800);
  }

  // Cálculo local do score (mesma lógica do backend, para feedback imediato)
  const score = useMemo(() => calcularScore(items), [items]);
  const tested = items.filter((i) => i.resultado !== RESULTADO.NaoTestado).length;
  const avarias = items.filter((i) => i.resultado === RESULTADO.Avaria).length;

  // Agrupar por grupo
  const groups = useMemo(() => {
    const map = new Map<string, DiagnosticoExecucaoItem[]>();
    for (const item of items) {
      const key = item.grupo ?? 'Geral';
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(item);
    }
    return Array.from(map.entries());
  }, [items]);

  const scoreToneCls =
    score == null
      ? 'text-zinc-500'
      : score >= 80
        ? 'text-emerald-600 dark:text-emerald-400'
        : score >= 50
          ? 'text-amber-600 dark:text-amber-400'
          : 'text-rose-600 dark:text-rose-400';

  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="flex items-center gap-2 text-sm font-semibold">
          <Stethoscope size={15} strokeWidth={2} className="text-zinc-500" />
          Diagnóstico Guiado
        </h2>
          <p className="text-[11px] text-zinc-500">
            {execucao.templateNomeSnapshot ?? 'Template removido'} · {tested}/{items.length} testados · {avarias}{' '}
            {avarias === 1 ? 'avaria' : 'avarias'}
            {execucao.completadoEm && ' · ✓ completado'}
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="text-right">
            <div className={`text-3xl font-semibold tabular-nums ${scoreToneCls}`}>
              {score == null ? '—' : `${score}`}
              <span className="text-base font-normal text-zinc-500">/100</span>
            </div>
            <div className="text-[10px] uppercase text-zinc-500">Health Score</div>
          </div>
        </div>
      </div>

      {error && (
        <div className="mt-3 rounded-md bg-rose-50 px-2 py-1 text-xs text-rose-700 dark:bg-rose-950/30 dark:text-rose-300">
          {error}
        </div>
      )}

      <div className="mt-4 space-y-4">
        {groups.map(([grupo, itemsGrupo]) => (
          <div key={grupo}>
            <h3 className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-zinc-500">{grupo}</h3>
            <ul className="space-y-1">
              {itemsGrupo.map((item) => (
                <li
                  key={item.id}
                  className="rounded-lg border border-zinc-200 bg-zinc-50/50 p-2 dark:border-zinc-800 dark:bg-zinc-950/50"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="min-w-0 flex-1">
                      <div className="text-sm font-medium">{item.label}</div>
                      {item.descricao && <div className="text-[11px] text-zinc-500">{item.descricao}</div>}
                    </div>
                    {readOnly ? (
                      <ResultBadge resultado={item.resultado} />
                    ) : (
                      <ResultButtons
                        value={item.resultado}
                        onChange={(r) => setResult(item.id, r)}
                      />
                    )}
                  </div>
                  {(item.resultado === RESULTADO.Avaria || item.resultado === RESULTADO.Marginal || item.notas) && !readOnly && (
                    <input
                      type="text"
                      placeholder="Nota / detalhe…"
                      value={item.notas ?? ''}
                      onChange={(e) => setItemNotas(item.id, e.target.value)}
                      className="mt-2 min-h-11 w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-xs dark:border-zinc-700 dark:bg-zinc-950"
                    />
                  )}
                  {readOnly && item.notas && (
                    <div className="mt-1 text-[11px] text-zinc-500">↳ {item.notas}</div>
                  )}
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

      {!readOnly && (
        <div className="mt-4 space-y-2">
          <label className="block">
            <span className="text-[11px] uppercase text-zinc-500">Notas gerais</span>
            <textarea
              rows={2}
              value={notasGerais}
              onChange={(e) => {
                setNotasGerais(e.target.value);
                triggerAutoSave();
              }}
              placeholder="Observações para o relatório do cliente…"
              className="mt-1 min-h-20 w-full resize-none rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950"
            />
          </label>
          <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-zinc-500">
            <span>
              {update.isPending ? 'A guardar…' : savedAt ? `Guardado às ${savedAt.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}` : ''}
            </span>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => {
                  if (confirm('Apagar diagnóstico? Vais perder o checklist actual.')) remove.mutate();
                }}
                className="min-h-10 rounded-md px-3 py-2 text-rose-600 hover:bg-rose-50 dark:text-rose-400 dark:hover:bg-rose-950/30"
              >
                Apagar
              </button>
              {!execucao.completadoEm && (
                <button
                  type="button"
                  onClick={() => update.mutate(true)}
                  disabled={update.isPending}
                  className="min-h-10 rounded-md bg-emerald-600 px-3 py-2 text-xs font-medium text-white disabled:opacity-60"
                >
                  ✓ Marcar concluído
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

function ResultButtons({ value, onChange }: { value: Resultado; onChange: (r: Resultado) => void }) {
  const opts: Array<{ value: Resultado; label: string; cls: string }> = [
    { value: RESULTADO.Ok, label: '✓ OK', cls: 'bg-emerald-100 text-emerald-800 ring-emerald-300 dark:bg-emerald-900/40 dark:text-emerald-200' },
    { value: RESULTADO.Marginal, label: '◐ Mar.', cls: 'bg-amber-100 text-amber-800 ring-amber-300 dark:bg-amber-900/40 dark:text-amber-200' },
    { value: RESULTADO.Avaria, label: '✕ Av.', cls: 'bg-rose-100 text-rose-800 ring-rose-300 dark:bg-rose-900/40 dark:text-rose-200' },
    { value: RESULTADO.NaoTestado, label: 'N/T', cls: 'bg-zinc-100 text-zinc-700 ring-zinc-300 dark:bg-zinc-800 dark:text-zinc-300' },
  ];
  return (
    <div className="flex flex-wrap gap-1">
      {opts.map((o) => (
        <button
          key={o.value}
          type="button"
          onClick={() => onChange(o.value)}
          className={`min-h-10 rounded-md px-3 py-1 text-[11px] font-medium ring-1 transition ${
            value === o.value ? o.cls + ' ring-2 shadow-sm' : 'bg-white text-zinc-500 ring-zinc-200 hover:bg-zinc-50 dark:bg-zinc-900 dark:ring-zinc-700'
          }`}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}

function ResultBadge({ resultado }: { resultado: Resultado }) {
  const map: Record<Resultado, { label: string; cls: string }> = {
    0: { label: 'N/T', cls: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300' },
    1: { label: '✓ OK', cls: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200' },
    2: { label: '✕ Avaria', cls: 'bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-200' },
    3: { label: '◐ Marginal', cls: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200' },
  };
  const r = map[resultado];
  return <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${r.cls}`}>{r.label}</span>;
}

function calcularScore(items: DiagnosticoExecucaoItem[]): number | null {
  const testados = items.filter((i) => i.resultado !== RESULTADO.NaoTestado);
  if (testados.length === 0) return null;
  const pesoTotal = testados.reduce((s, i) => s + i.peso, 0);
  if (pesoTotal === 0) return null;
  const pontos = testados.reduce((s, i) => {
    if (i.resultado === RESULTADO.Ok) return s + i.peso * 1.0;
    if (i.resultado === RESULTADO.Marginal) return s + i.peso * 0.5;
    return s;
  }, 0);
  return Math.round((pontos / pesoTotal) * 100);
}
