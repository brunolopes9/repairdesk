// Sprint 598: impede o recarregamento automático (versão nova do service worker) enquanto há um
// envio de ficheiros em curso — recarregar a meio perdia a seleção/os uploads no telemóvel.
// O reload fica pendente e acontece quando o último envio terminar.

let bloqueios = 0;
let pendente: (() => void) | null = null;

export function bloquearReload(): () => void {
  bloqueios++;
  let libertado = false;
  return () => {
    if (libertado) return;
    libertado = true;
    bloqueios = Math.max(0, bloqueios - 1);
    if (bloqueios === 0 && pendente) {
      const acao = pendente;
      pendente = null;
      acao();
    }
  };
}

/** Corre já, ou adia se houver envios em curso. */
export function reloadQuandoPossivel(acao: () => void) {
  if (bloqueios === 0) acao();
  else pendente = acao;
}
