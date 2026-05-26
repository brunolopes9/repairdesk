import { Bell, BellOff, Loader2 } from 'lucide-react';
import { Button } from './ui/Button';
import { useStaffPush } from '../lib/push/useStaffPush';

/**
 * Sprint 366: cartão para ligar/desligar notificações push NESTE dispositivo (telemóvel
 * ou desktop). Avisa de pedidos online novos mesmo com a app fechada/em segundo plano.
 * Para o telemóvel: instala primeiro a app (Adicionar ao ecrã principal) e ativa aqui.
 */
export function StaffPushToggle() {
  const { supported, status, error, subscribe, unsubscribe } = useStaffPush();

  if (!supported) {
    return (
      <div className="rounded-xl border border-zinc-200 bg-white p-4 text-sm text-zinc-500 dark:border-zinc-800 dark:bg-zinc-900">
        Este dispositivo/navegador não suporta notificações push.
      </div>
    );
  }

  const subscribed = status === 'subscribed';
  const busy = status === 'busy' || status === 'checking';

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <div className={`mt-0.5 rounded-lg p-2 ${subscribed ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300' : 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800'}`}>
            {subscribed ? <Bell size={18} /> : <BellOff size={18} />}
          </div>
          <div>
            <p className="text-sm font-medium">Notificações neste dispositivo</p>
            <p className="text-xs text-zinc-500">
              {subscribed
                ? 'Ativas. Recebes aviso de pedidos online novos mesmo com a app fechada.'
                : 'Ativa para seres avisado de pedidos online novos — ideal no telemóvel (instala a app primeiro).'}
            </p>
            {status === 'denied' && (
              <p className="mt-1 text-xs text-amber-700 dark:text-amber-400">
                Bloqueaste as notificações para este site. Tens de as reativar nas definições do navegador.
              </p>
            )}
            {status === 'error' && error && (
              <p className="mt-1 text-xs text-red-600 dark:text-red-400">{error}</p>
            )}
          </div>
        </div>
        {busy ? (
          <Loader2 className="mt-1 animate-spin text-zinc-400" size={18} />
        ) : subscribed ? (
          <Button type="button" variant="secondary" onClick={unsubscribe}>Desligar</Button>
        ) : (
          <Button type="button" onClick={subscribe} disabled={status === 'denied'}>Ativar</Button>
        )}
      </div>
    </div>
  );
}
