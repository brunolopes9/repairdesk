import type { ReactNode } from 'react';

interface PopoverProps {
  open: boolean;
  content: ReactNode;
  children: ReactNode;
  className?: string;
}

export function Popover({ open, content, children, className = '' }: PopoverProps) {
  return (
    <div className={`relative ${className}`}>
      {children}
      {open && (
        <div
          role="tooltip"
          className="pointer-events-none absolute left-0 right-0 top-full z-10 mt-2 rounded-lg border border-brand-200 bg-white p-3 text-sm shadow-lg dark:border-brand-900 dark:bg-zinc-900 sm:left-1/2 sm:right-auto sm:w-72 sm:-translate-x-1/2"
        >
          <div className="absolute -top-1.5 left-6 h-3 w-3 rotate-45 border-l border-t border-brand-200 bg-white dark:border-brand-900 dark:bg-zinc-900 sm:left-1/2 sm:-translate-x-1/2" />
          {content}
        </div>
      )}
    </div>
  );
}
