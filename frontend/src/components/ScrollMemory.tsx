import { useEffect, useLayoutEffect } from 'react';
import { useLocation, useNavigationType } from 'react-router-dom';

const chave = (k: string) => `rd.scroll.${k}`;

/**
 * Sprint 592: ao voltar atrás (botão "Voltar" ou seta do navegador) a página abre no mesmo sítio onde se estava —
 * ex.: a 2.ª página das compras, lá em baixo. Guarda o scroll de cada entrada do histórico (sessionStorage) e
 * repõe-no quando se volta a ela; ao abrir uma página nova começa no topo. Como as listas carregam dados, tenta
 * repor durante ~2 s até a página ter altura suficiente.
 */
export function ScrollMemory() {
  const location = useLocation();
  const tipo = useNavigationType();

  useEffect(() => {
    if ('scrollRestoration' in window.history) window.history.scrollRestoration = 'manual';
  }, []);

  // Guarda a posição desta entrada do histórico enquanto se faz scroll.
  useEffect(() => {
    let pendente = 0;
    const guardar = () => {
      if (pendente) return;
      pendente = window.setTimeout(() => {
        pendente = 0;
        try { sessionStorage.setItem(chave(location.key), String(Math.round(window.scrollY))); } catch { /* sem storage */ }
      }, 150);
    };
    window.addEventListener('scroll', guardar, { passive: true });
    return () => { window.removeEventListener('scroll', guardar); window.clearTimeout(pendente); };
  }, [location.key]);

  useLayoutEffect(() => {
    // Filtros que mudam o URL com "replace" (useFiltrosUrl) não mexem no scroll.
    if (tipo === 'REPLACE') return;
    if (tipo !== 'POP') { window.scrollTo(0, 0); return; }
    let y = 0;
    try { y = Number(sessionStorage.getItem(chave(location.key))) || 0; } catch { /* sem storage */ }
    if (y <= 0) { window.scrollTo(0, 0); return; }
    const fim = performance.now() + 2000;
    let frame = 0;
    const tentar = () => {
      const max = document.documentElement.scrollHeight - window.innerHeight;
      window.scrollTo(0, Math.min(y, Math.max(0, max)));
      if (max < y && performance.now() < fim) frame = requestAnimationFrame(tentar);
    };
    tentar();
    // Se a pessoa começar a fazer scroll, deixa de insistir.
    const parar = () => cancelAnimationFrame(frame);
    window.addEventListener('wheel', parar, { once: true, passive: true });
    window.addEventListener('touchstart', parar, { once: true, passive: true });
    return () => { cancelAnimationFrame(frame); window.removeEventListener('wheel', parar); window.removeEventListener('touchstart', parar); };
  }, [location.key, tipo]);

  return null;
}
