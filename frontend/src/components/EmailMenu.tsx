import { useEffect, useRef, useState } from 'react';
import { ChevronDown, Mail } from 'lucide-react';
import {
  EMAIL_TEMPLATES,
  emailTemplatesForState,
  mailtoLink,
  type EmailTemplateMeta,
  type EmailVars,
} from '../lib/email/templates';
import type { RepairStatus } from '../lib/reparacoes/types';
import { toast } from '../lib/toast';

interface Props {
  email: string;
  vars: EmailVars;
  estado?: RepairStatus;
  staleDays?: number;
  showAll?: boolean;
  size?: 'sm' | 'md';
}

/**
 * Sprint 348 (Doc 83 Pillar 3): dropdown para abrir mailto: com template
 * pré-preenchido consoante o estado da reparação. O utilizador sempre vê o
 * email no cliente de mail antes de carregar enviar — esta UI só compõe.
 */
export default function EmailMenu({ email, vars, estado, staleDays, showAll = false, size = 'sm' }: Props) {
  const [open, setOpen] = useState(false);
  const [preview, setPreview] = useState<EmailTemplateMeta | null>(null);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    function onEscape(e: KeyboardEvent) { if (e.key === 'Escape') setOpen(false); }
    if (open) {
      document.addEventListener('mousedown', onClickOutside);
      document.addEventListener('keydown', onEscape);
    }
    return () => {
      document.removeEventListener('mousedown', onClickOutside);
      document.removeEventListener('keydown', onEscape);
    };
  }, [open]);

  if (!email) return null;

  const list: EmailTemplateMeta[] = showAll || estado == null
    ? Object.values(EMAIL_TEMPLATES)
    : emailTemplatesForState(estado, { staleDays });

  function send(template: EmailTemplateMeta) {
    try {
      const { subject, body } = template.build(vars);
      const url = mailtoLink(email, subject, body);
      window.location.href = url;
      setOpen(false);
      setPreview(null);
    } catch (err) {
      toast.fromError(err, 'Não foi possível preparar o email.');
    }
  }

  const buttonCls = size === 'sm' ? 'h-7 px-2 text-xs' : 'h-9 px-3 text-sm';

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        className={`inline-flex items-center gap-1 rounded-lg bg-sky-600 font-medium text-white transition hover:bg-sky-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-300 ${buttonCls}`}
      >
        <Mail size={size === 'sm' ? 12 : 14} strokeWidth={2} />
        <span>Email</span>
        <ChevronDown size={size === 'sm' ? 12 : 14} strokeWidth={2} className={`transition ${open ? 'rotate-180' : ''}`} />
      </button>

      {open && (
        <div role="menu" className="absolute right-0 z-30 mt-1 w-72 overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-lg dark:border-zinc-700 dark:bg-zinc-900">
          <div className="border-b border-zinc-100 px-3 py-2 text-[10px] uppercase tracking-wide text-zinc-500 dark:border-zinc-800">
            Enviar email pré-preenchido
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
              <div className="font-medium">{preview.build(vars).subject}</div>
              <div className="mt-1 whitespace-pre-line italic">{preview.build(vars).body.slice(0, 220)}{preview.build(vars).body.length > 220 ? '…' : ''}</div>
            </div>
          )}
          <div className="border-t border-zinc-100 px-3 py-1.5 text-[10px] text-zinc-400 dark:border-zinc-800">
            Vais ver o email no teu cliente antes de enviar.
          </div>
        </div>
      )}
    </div>
  );
}
