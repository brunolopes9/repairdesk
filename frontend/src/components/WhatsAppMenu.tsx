import { useEffect, useRef, useState } from 'react';
import { ChevronDown, MessageCircle } from 'lucide-react';
import {
  TEMPLATES,
  templatesForState,
  waMeLink,
  type TemplateMeta,
  type WhatsAppVars,
} from '../lib/whatsapp/templates';
import type { RepairStatus } from '../lib/reparacoes/types';

interface Props {
  phone: string;
  vars: WhatsAppVars;
  /** Estado de Reparação para escolher templates contextuais (ignorado se `customList` for passado). */
  estado?: RepairStatus;
  /** Dias desde que entrou no estado actual — usado para escolher template (ex: Lembrete se >7 dias em Pronto). */
  staleDays?: number;
  /** Mostrar todos os templates, não só os contextuais. Default: false. */
  showAll?: boolean;
  /** Lista custom de templates (usado para Trabalhos ou outros fluxos não-Reparação). */
  customList?: TemplateMeta[];
  size?: 'sm' | 'md';
}

/**
 * Dropdown para abrir WhatsApp com mensagem pré-preenchida consoante o estado.
 * O utilizador SEMPRE vê a mensagem no WhatsApp antes de carregar enviar — esta UI só compõe.
 */
export default function WhatsAppMenu({ phone, vars, estado, staleDays, showAll = false, customList, size = 'sm' }: Props) {
  const [open, setOpen] = useState(false);
  const [preview, setPreview] = useState<TemplateMeta | null>(null);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    function onEscape(e: KeyboardEvent) {
      if (e.key === 'Escape') setOpen(false);
    }
    if (open) {
      document.addEventListener('mousedown', onClickOutside);
      document.addEventListener('keydown', onEscape);
    }
    return () => {
      document.removeEventListener('mousedown', onClickOutside);
      document.removeEventListener('keydown', onEscape);
    };
  }, [open]);

  if (!phone) return null;

  const list = customList
    ?? (showAll
      ? Object.values(TEMPLATES)
      : estado != null
        ? templatesForState(estado, { staleDays })
        : Object.values(TEMPLATES));

  function send(template: TemplateMeta) {
    const url = waMeLink(phone, template.build(vars));
    window.open(url, '_blank', 'noopener,noreferrer');
    setOpen(false);
    setPreview(null);
  }

  const buttonCls =
    size === 'sm'
      ? 'h-7 px-2 text-xs'
      : 'h-9 px-3 text-sm';

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        className={`inline-flex items-center gap-1 rounded-lg bg-green-500 font-medium text-white transition hover:bg-green-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-green-300 ${buttonCls}`}
      >
        <MessageCircle size={size === 'sm' ? 12 : 14} strokeWidth={2} />
        <span>WhatsApp</span>
        <ChevronDown size={size === 'sm' ? 12 : 14} strokeWidth={2} className={`transition ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && (
        <div
          role="menu"
          className="absolute right-0 z-30 mt-1 w-72 overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-lg dark:border-zinc-700 dark:bg-zinc-900"
        >
          <div className="border-b border-zinc-100 px-3 py-2 text-[10px] uppercase tracking-wide text-zinc-500 dark:border-zinc-800">
            Notificar via WhatsApp
          </div>
          <ul className="max-h-80 divide-y divide-zinc-100 overflow-y-auto dark:divide-zinc-800">
            {list.map((t) => (
              <li key={t.key}>
                <button
                  type="button"
                  role="menuitem"
                  onClick={() => send(t)}
                  onMouseEnter={() => setPreview(t)}
                  onMouseLeave={() => setPreview(null)}
                  onFocus={() => setPreview(t)}
                  onBlur={() => setPreview(null)}
                  className="block w-full px-3 py-2 text-left text-sm transition hover:bg-zinc-50 focus:bg-zinc-50 focus:outline-none dark:hover:bg-zinc-800 dark:focus:bg-zinc-800"
                >
                  <div className="font-medium text-zinc-900 dark:text-zinc-100">{t.label}</div>
                  <div className="mt-0.5 text-[11px] text-zinc-500">{t.hint}</div>
                </button>
              </li>
            ))}
          </ul>
          {preview && (
            <div className="border-t border-zinc-100 bg-zinc-50 px-3 py-2 text-[11px] leading-relaxed text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-400">
              <div className="mb-1 font-medium text-zinc-700 dark:text-zinc-300">Preview:</div>
              <div className="whitespace-pre-line italic">"{preview.build(vars)}"</div>
            </div>
          )}
          <div className="border-t border-zinc-100 px-3 py-1.5 text-[10px] text-zinc-400 dark:border-zinc-800">
            Vais ver a mensagem no WhatsApp antes de enviar.
          </div>
        </div>
      )}
    </div>
  );
}
