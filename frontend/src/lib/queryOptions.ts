/**
 * Sprint 363: opções para listas operacionais que devem parecer "ao vivo".
 *
 * Refrescam quando o utilizador volta à janela (mudou de tab, ou esteve no
 * detalhe e voltou à lista) e fazem poll de 30s enquanto a tab está activa —
 * o React Query NÃO faz poll em background por defeito (refetchIntervalInBackground
 * é false), por isso não há pedidos desperdiçados com a tab escondida.
 *
 * Aplicar SÓ a queries de listas de leitura. NUNCA a queries que alimentam
 * formulários: um refetch ao ganhar foco pode resetar o que o utilizador está
 * a escrever. É por isso que o default global em main.tsx mantém
 * refetchOnWindowFocus: false — este opt-in é cirúrgico e seguro.
 */
export const liveListOptions = {
  refetchOnWindowFocus: true,
  refetchInterval: 30_000,
} as const;
