import { useCallback, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

/**
 * Sprint 592: filtros, pesquisa, ordenação e página de uma lista guardados no URL (?q=…&page=2…). Ao abrir um
 * registo e carregar em "Voltar" (ou na seta do navegador) a lista volta exatamente como estava — a mesma página e
 * os mesmos filtros — e o link pode ser partilhado/guardado. Mudar qualquer filtro volta à página 1.
 *
 * Os valores por defeito não aparecem no URL. Atualiza com `replace` (não enche o histórico a cada tecla).
 */
export function useFiltrosUrl<T extends Record<string, string>>(defaults: T) {
  const [params, setParams] = useSearchParams();
  const [base] = useState(defaults);

  const valores = useMemo(() => {
    const o = { ...base };
    for (const k of Object.keys(o) as (keyof T)[]) {
      const v = params.get(k as string);
      if (v != null) o[k] = v as T[keyof T];
    }
    return o;
  }, [params, base]);

  const set = useCallback((patch: Partial<T>) => {
    setParams((prev) => {
      const next = new URLSearchParams(prev);
      for (const [k, v] of Object.entries(patch)) {
        if (v == null || v === base[k]) next.delete(k);
        else next.set(k, v);
      }
      if (!('page' in patch)) next.delete('page');
      return next;
    }, { replace: true });
  }, [setParams, base]);

  const page = Math.max(1, Number(valores.page) || 1);
  const setPage = useCallback((p: number) => set({ page: String(p) } as unknown as Partial<T>), [set]);

  return { f: valores, set, page, setPage };
}
