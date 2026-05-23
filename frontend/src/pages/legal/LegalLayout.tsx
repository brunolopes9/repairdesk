import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { Wrench } from 'lucide-react';

interface Props {
  title: string;
  lastUpdated: string;
  children: ReactNode;
}

export default function LegalLayout({ title, lastUpdated, children }: Props) {
  return (
    <div className="min-h-screen bg-zinc-50 text-zinc-900 dark:bg-zinc-950 dark:text-zinc-100">
      <header className="border-b border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mx-auto flex max-w-3xl items-center gap-3 px-4 py-4">
          <Link to="/" className="flex items-center gap-2 font-semibold">
            <span className="grid h-8 w-8 place-items-center rounded-lg bg-brand-600 text-white">
              <Wrench size={16} strokeWidth={2} />
            </span>
            Reparo
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-4 py-10">
        <h1 className="text-3xl font-semibold tracking-tight">{title}</h1>
        <p className="mt-1 text-sm text-zinc-500">Última actualização: {lastUpdated}</p>
        <div className="prose-legal mt-8 space-y-6 text-sm leading-relaxed text-zinc-700 dark:text-zinc-300">
          {children}
        </div>
      </main>

      <footer className="mt-10 border-t border-zinc-200 bg-white py-6 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="mx-auto flex max-w-3xl flex-wrap items-center justify-between gap-3 px-4 text-xs text-zinc-500">
          <div>© {new Date().getFullYear()} LopesTech · Bruno Lopes</div>
          <nav className="flex flex-wrap gap-x-4 gap-y-1">
            <Link to="/privacidade" className="hover:text-zinc-700 dark:hover:text-zinc-300">Privacidade</Link>
            <Link to="/termos" className="hover:text-zinc-700 dark:hover:text-zinc-300">Termos</Link>
            <Link to="/cookies" className="hover:text-zinc-700 dark:hover:text-zinc-300">Cookies</Link>
            <Link to="/dpa" className="hover:text-zinc-700 dark:hover:text-zinc-300">DPA</Link>
            <Link to="/sub-processors" className="hover:text-zinc-700 dark:hover:text-zinc-300">Sub-processadores</Link>
            <a href="mailto:privacidade@lopestech.pt" className="hover:text-zinc-700 dark:hover:text-zinc-300">privacidade@lopestech.pt</a>
          </nav>
        </div>
      </footer>
    </div>
  );
}

interface SectionProps {
  title: string;
  children: ReactNode;
}

export function Section({ title, children }: SectionProps) {
  return (
    <section className="space-y-3">
      <h2 className="text-lg font-semibold text-zinc-900 dark:text-zinc-100">{title}</h2>
      {children}
    </section>
  );
}
