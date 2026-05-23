import { useEffect, useState } from 'react';
import { WifiOff, Download } from 'lucide-react';

/**
 * Sprint 194: indicador online/offline + botão instalar PWA. Discreto na bottom-right.
 * Doc 24 Fase 1 — só visual. Sem outbox/sync (Fases 2-4).
 *
 * `beforeinstallprompt` é Chromium-only. Em Safari/iOS o utilizador tem de fazer
 * "Partilhar → Adicionar ao ecrã principal" manualmente. Mostramos hint contextual.
 */
interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

export default function PwaStatus() {
  const [online, setOnline] = useState(navigator.onLine);
  const [installPrompt, setInstallPrompt] = useState<BeforeInstallPromptEvent | null>(null);
  const [showIosHint, setShowIosHint] = useState(false);
  const [dismissedInstall, setDismissedInstall] = useState(
    () => localStorage.getItem('pwa-install-dismissed') === '1',
  );

  useEffect(() => {
    const onOnline = () => setOnline(true);
    const onOffline = () => setOnline(false);
    const onPrompt = (e: Event) => {
      e.preventDefault();
      setInstallPrompt(e as BeforeInstallPromptEvent);
    };
    window.addEventListener('online', onOnline);
    window.addEventListener('offline', onOffline);
    window.addEventListener('beforeinstallprompt', onPrompt);
    return () => {
      window.removeEventListener('online', onOnline);
      window.removeEventListener('offline', onOffline);
      window.removeEventListener('beforeinstallprompt', onPrompt);
    };
  }, []);

  const isIos = /iPad|iPhone|iPod/.test(navigator.userAgent) && !('MSStream' in window);
  const isStandalone = window.matchMedia('(display-mode: standalone)').matches
    || ('standalone' in navigator && (navigator as { standalone?: boolean }).standalone === true);

  function handleInstall() {
    if (installPrompt) {
      installPrompt.prompt();
      installPrompt.userChoice.then(() => setInstallPrompt(null));
    } else if (isIos) {
      setShowIosHint(true);
    }
  }

  function dismissInstall() {
    setDismissedInstall(true);
    localStorage.setItem('pwa-install-dismissed', '1');
  }

  const canInstall = !isStandalone && !dismissedInstall && (installPrompt !== null || isIos);

  return (
    <>
      {/* Indicador offline (só aparece quando offline) */}
      {!online && (
        <div className="fixed bottom-4 left-1/2 z-50 -translate-x-1/2 transform rounded-full bg-amber-500 px-3 py-1.5 text-xs font-medium text-white shadow-lg flex items-center gap-1.5">
          <WifiOff size={14} /> Sem internet — dados podem estar desatualizados
        </div>
      )}
      {/* Placeholder Wifi para futuro indicador "a sincronizar" — não renderizado ainda. */}

      {/* Botão instalar PWA (Chromium) ou hint iOS */}
      {canInstall && (
        <div className="fixed bottom-4 right-4 z-40 max-w-xs rounded-lg border border-brand-200 bg-white p-3 shadow-lg dark:border-brand-800/40 dark:bg-zinc-900">
          <div className="flex items-start gap-2">
            <Download size={16} className="mt-0.5 flex-none text-brand-600 dark:text-brand-400" />
            <div className="flex-1 text-xs">
              <div className="font-medium text-zinc-900 dark:text-zinc-100">Instala como app</div>
              <div className="mt-0.5 text-zinc-600 dark:text-zinc-400">
                {isIos
                  ? 'Acesso rápido + funciona em modo standalone.'
                  : 'Carrega mais rápido + ícone no telemóvel.'}
              </div>
              {showIosHint && (
                <div className="mt-2 rounded bg-zinc-50 px-2 py-1.5 text-[11px] text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">
                  Toca em <strong>Partilhar</strong> ↗ no Safari, depois <strong>Adicionar ao ecrã principal</strong>.
                </div>
              )}
              <div className="mt-2 flex gap-2">
                <button
                  type="button"
                  onClick={handleInstall}
                  className="rounded bg-brand-600 px-2 py-1 text-[11px] font-medium text-white hover:bg-brand-700"
                >
                  {isIos ? 'Como instalar?' : 'Instalar'}
                </button>
                <button
                  type="button"
                  onClick={dismissInstall}
                  className="rounded border border-zinc-200 px-2 py-1 text-[11px] text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
                >
                  Agora não
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
