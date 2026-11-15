import { useCallback, useEffect, useRef, useState } from 'react';
import { isAxiosError } from 'axios';

export type EstadoAutosave = 'idle' | 'pendente' | 'a-guardar' | 'guardado' | 'erro';

interface Opcoes {
  /** Espera depois da última alteração antes de gravar (ms). */
  atraso?: number;
  /** false = não grava (ex.: valor inválido ou ainda a carregar). */
  ativo?: boolean;
}

/**
 * Sprint 671: grava sozinho — sem botão «Guardar». Cada alteração espera `atraso` ms de pausa e grava; as gravações
 * nunca correm em paralelo (a seguinte espera pela anterior e leva sempre o valor mais recente). Compara por JSON,
 * por isso voltar ao valor já gravado não grava outra vez.
 *
 * `reiniciar(v)` marca `v` como o que está gravado (ao abrir outro registo). `gravarJa()` grava o pendente já
 * (ao fechar a janela) e devolve a promessa.
 */
export function useAutosave<T>(valor: T, guardar: (v: T) => Promise<unknown>, { atraso = 700, ativo = true }: Opcoes = {}) {
  const [estado, setEstado] = useState<EstadoAutosave>('idle');
  const [erro, setErro] = useState<string | null>(null);
  const gravado = useRef<string | null>(null);
  const pendente = useRef<T | null>(null);
  const emCurso = useRef<Promise<void> | null>(null);
  const timer = useRef<number | null>(null);
  const guardarRef = useRef(guardar);
  useEffect(() => { guardarRef.current = guardar; }, [guardar]);

  const correr = useCallback(async (): Promise<void> => {
    if (timer.current) { window.clearTimeout(timer.current); timer.current = null; }
    if (emCurso.current) await emCurso.current;
    const v = pendente.current;
    if (v === null) return;
    const chave = JSON.stringify(v);
    pendente.current = null;
    if (chave === gravado.current) { setEstado('guardado'); return; }
    setEstado('a-guardar');
    const p = (async () => {
      try {
        await guardarRef.current(v);
        gravado.current = chave;
        setErro(null);
        setEstado(pendente.current === null ? 'guardado' : 'pendente');
      } catch (e) {
        pendente.current ??= v; // fica por gravar — «Tentar de novo» volta a enviar
        setErro(mensagem(e));
        setEstado('erro');
      }
    })();
    emCurso.current = p;
    await p;
    emCurso.current = null;
  }, []);

  useEffect(() => {
    if (!ativo || gravado.current === null) return;
    const chave = JSON.stringify(valor);
    if (chave === gravado.current && pendente.current === null) return;
    pendente.current = valor;
    setEstado('pendente');
    if (timer.current) window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => { void correr(); }, atraso);
  }, [valor, ativo, atraso, correr]);

  useEffect(() => () => { if (timer.current) window.clearTimeout(timer.current); }, []);

  const reiniciar = useCallback((v: T) => {
    if (timer.current) { window.clearTimeout(timer.current); timer.current = null; }
    gravado.current = JSON.stringify(v);
    pendente.current = null;
    setErro(null);
    setEstado('idle');
  }, []);

  const gravarJa = useCallback(() => correr(), [correr]);

  return { estado, erro, reiniciar, gravarJa, tentarDeNovo: gravarJa };
}

function mensagem(e: unknown): string {
  if (isAxiosError(e)) {
    const d = e.response?.data as { detail?: string; title?: string } | undefined;
    return d?.detail ?? d?.title ?? 'Sem ligação ao servidor.';
  }
  return e instanceof Error ? e.message : 'Não foi possível guardar.';
}
