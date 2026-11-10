/**
 * Sprint 607: empresa que ESTE separador está a mostrar.
 *
 * A sessão (cookie de refresh) é partilhada por todos os separadores, mas cada separador tem o seu
 * access token e os dados em cache da empresa que carregou. Se a empresa da sessão mudar noutro
 * separador, este separador tem de recarregar — nunca pode continuar a mostrar a LopesTech enquanto
 * os pedidos já vão para a Cortex (ou vice-versa).
 */
let tenantDoSeparador: string | null = null;

const CANAL = 'mender-sessao';

interface MensagemEmpresa {
  tipo: 'empresa';
  tenantId: string;
}

export function definirEmpresaDoSeparador(tenantId: string | null): void {
  tenantDoSeparador = tenantId;
}

export function empresaDoSeparador(): string | null {
  return tenantDoSeparador;
}

/** Recarrega o separador na empresa que a sessão tem agora (dados em cache são descartados). */
export function recarregarNaEmpresaAtual(): void {
  window.location.assign('/');
}

/** Avisa os outros separadores de que a sessão passou para outra empresa. */
export function avisarTrocaDeEmpresa(tenantId: string): void {
  if (typeof BroadcastChannel === 'undefined') return;
  const canal = new BroadcastChannel(CANAL);
  canal.postMessage({ tipo: 'empresa', tenantId } satisfies MensagemEmpresa);
  canal.close();
}

/** Ouve trocas de empresa feitas noutros separadores. Devolve a função para deixar de ouvir. */
export function ouvirTrocasDeEmpresa(): () => void {
  if (typeof BroadcastChannel === 'undefined') return () => {};
  const canal = new BroadcastChannel(CANAL);
  canal.onmessage = (e: MessageEvent<MensagemEmpresa>) => {
    if (e.data?.tipo === 'empresa' && tenantDoSeparador && e.data.tenantId !== tenantDoSeparador) {
      recarregarNaEmpresaAtual();
    }
  };
  return () => canal.close();
}
