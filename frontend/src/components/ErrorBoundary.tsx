import { Component, type ErrorInfo, type ReactNode } from 'react';
import * as Sentry from '@sentry/react';
import { AlertOctagon, RotateCw, Home } from 'lucide-react';

/**
 * Sprint 253 (Doc 77): apanha render errors em qualquer parte da árvore React.
 *
 * Sem isto, qualquer `throw` durante render — null pointer, type error em props,
 * useQuery select que rebenta — leva a tela completamente branca sem feedback.
 *
 * Estratégia em camadas:
 * - Global (no main.tsx): última barreira; mostra ecrã "Algo correu mal" + recarregar
 * - Por rota (no App.tsx): se Dashboard rebenta, Stock continua a funcionar
 *
 * Reporta a Sentry com componentStack — fica fácil de identificar o ramo que partiu.
 */
interface Props {
  children: ReactNode;
  fallback?: ReactNode;
  /** Identificador (ex: nome da rota) para distinguir boundaries em telemetria. */
  scope?: string;
}

interface State {
  error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Reporta a Sentry com tags + componentStack
    Sentry.withScope((scope) => {
      if (this.props.scope) scope.setTag('errorBoundary', this.props.scope);
      scope.setContext('react', { componentStack: info.componentStack });
      Sentry.captureException(error);
    });
    // Mantém no console em dev para Vite devtools
    if (import.meta.env.DEV) {
      console.error('[ErrorBoundary]', this.props.scope ?? 'global', error, info);
    }
  }

  reset = () => {
    this.setState({ error: null });
  };

  render() {
    if (!this.state.error) return this.props.children;
    if (this.props.fallback) return this.props.fallback;

    return (
      <div className="grid min-h-[50vh] place-items-center px-6">
        <div className="max-w-md text-center">
          <div className="mx-auto grid h-16 w-16 place-items-center rounded-full bg-rose-100 text-rose-600 dark:bg-rose-900/40 dark:text-rose-400">
            <AlertOctagon size={28} strokeWidth={1.75} aria-hidden />
          </div>
          <h1 className="mt-4 text-xl font-semibold tracking-tight">Algo correu mal nesta página.</h1>
          <p className="mt-2 text-sm text-zinc-500">
            {this.props.scope ? `Erro em "${this.props.scope}".` : 'O resto da app continua a funcionar.'}{' '}
            Já avisámos a equipa.
          </p>
          {import.meta.env.DEV && (
            <pre className="mt-3 max-h-40 overflow-auto rounded-lg bg-zinc-100 p-3 text-left text-[11px] text-zinc-700 dark:bg-zinc-900 dark:text-zinc-300">
              {this.state.error.message}
              {'\n'}
              {this.state.error.stack?.split('\n').slice(0, 4).join('\n')}
            </pre>
          )}
          <div className="mt-5 flex justify-center gap-2">
            <button
              type="button"
              onClick={this.reset}
              className="inline-flex h-10 items-center gap-2 rounded-xl bg-brand-600 px-4 text-sm font-medium text-white shadow-sm transition hover:bg-brand-700"
            >
              <RotateCw size={15} strokeWidth={2} /> Tentar de novo
            </button>
            <a
              href="/"
              className="inline-flex h-10 items-center gap-2 rounded-xl border border-zinc-300 bg-white px-4 text-sm font-medium text-zinc-700 transition hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-300"
            >
              <Home size={15} strokeWidth={2} /> Ir para Início
            </a>
          </div>
        </div>
      </div>
    );
  }
}
