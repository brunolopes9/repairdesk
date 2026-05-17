import { toast as sonnerToast } from 'sonner';
import { isAxiosError } from 'axios';

/**
 * Helpers de toast padronizados em PT-PT. Usa sonner com richColors.
 * Para evitar duplicação, exporta wrappers simples.
 */
export const toast = {
  success(message: string, description?: string) {
    return sonnerToast.success(message, { description });
  },
  error(message: string, description?: string) {
    return sonnerToast.error(message, { description });
  },
  info(message: string, description?: string) {
    return sonnerToast.info(message, { description });
  },
  warning(message: string, description?: string) {
    return sonnerToast.warning(message, { description });
  },
  /** Extrai mensagem útil de um erro Axios e mostra como toast. */
  fromError(err: unknown, fallback = 'Algo correu mal. Tenta novamente.') {
    let message = fallback;
    if (isAxiosError(err)) {
      const data = err.response?.data as { detail?: string; title?: string } | undefined;
      message = data?.detail ?? data?.title ?? fallback;
    } else if (err instanceof Error) {
      message = err.message;
    }
    return sonnerToast.error(message);
  },
  /** Toast de "a guardar" que actualiza para sucesso/erro automaticamente. */
  promise<T>(
    promise: Promise<T>,
    opts: { loading: string; success: string; error?: string },
  ): Promise<T> {
    return sonnerToast.promise(promise, opts) as unknown as Promise<T>;
  },
};
