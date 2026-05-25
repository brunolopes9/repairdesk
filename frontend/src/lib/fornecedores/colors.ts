export function fornecedorChipClass(code?: string | null) {
  switch (code?.trim().toLowerCase()) {
    case 'molano':
      return 'bg-blue-100 text-blue-700 ring-blue-200 dark:bg-blue-950/50 dark:text-blue-300 dark:ring-blue-800';
    case 'tudo4mobile':
      return 'bg-emerald-100 text-emerald-700 ring-emerald-200 dark:bg-emerald-950/50 dark:text-emerald-300 dark:ring-emerald-800';
    default:
      return 'bg-zinc-100 text-zinc-600 ring-zinc-200 dark:bg-zinc-800 dark:text-zinc-300 dark:ring-zinc-700';
  }
}
