import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarPlus, Trash2 } from 'lucide-react';
import { Button } from '../../components/ui';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import { calendarioApi } from '../../lib/calendario/api';

/**
 * Sprint 619 (Doc 98 F4): ligar o Google Calendar (ou Outlook/iCloud) ao Mender pelo endereço iCal secreto.
 * Só leitura — o Mender não mexe no Google. O endereço fica cifrado e nunca volta a aparecer.
 */
export function CalendariosExternos() {
  const qc = useQueryClient();
  const [aberto, setAberto] = useState(false);
  const [nome, setNome] = useState('Google');
  const [url, setUrl] = useState('');
  const lista = useQuery({ queryKey: ['calendarios-externos'], queryFn: calendarioApi.list });

  const ligar = useMutation({
    mutationFn: () => calendarioApi.ligar(nome.trim(), url.trim()),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['calendarios-externos'] });
      qc.invalidateQueries({ queryKey: ['eventos-externos'] });
      setUrl('');
      setAberto(false);
      toast.success('Calendário ligado', 'Os eventos aparecem na agenda (atualiza de 10 em 10 minutos).');
    },
    onError: (e) => toast.error('Não ficou ligado', apiErrorMessage(e)),
  });
  const remover = useMutation({
    mutationFn: (id: string) => calendarioApi.remover(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['calendarios-externos'] });
      qc.invalidateQueries({ queryKey: ['eventos-externos'] });
    },
    onError: (e) => toast.error('Não foi removido', apiErrorMessage(e)),
  });

  const cals = lista.data ?? [];
  return (
    <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-3 text-sm dark:border-zinc-800 dark:bg-zinc-950/50">
      <div className="flex items-center justify-between gap-2">
        <span className="flex items-center gap-2 font-medium text-zinc-700 dark:text-zinc-200"><CalendarPlus size={14} className="text-violet-600" /> Google Calendar no Mender</span>
        {!aberto && <button type="button" onClick={() => setAberto(true)} className="text-xs font-medium text-brand-700 hover:underline dark:text-brand-300">Ligar</button>}
      </div>
      {cals.length > 0 && (
        <ul className="mt-2 space-y-1">
          {cals.map((c) => (
            <li key={c.id} className="flex items-start gap-2 text-xs">
              <span className="mt-1 h-2 w-2 flex-none rounded-full bg-violet-500" />
              <span className="min-w-0 flex-1">
                <span className="font-medium">{c.nome}</span> <span className="text-zinc-500">· {c.dominio}</span>
                {c.ultimoErro && <span className="block text-rose-600">{c.ultimoErro}</span>}
              </span>
              <button type="button" aria-label={`Remover ${c.nome}`} onClick={() => remover.mutate(c.id)} className="text-zinc-400 hover:text-rose-600"><Trash2 size={12} /></button>
            </li>
          ))}
        </ul>
      )}
      {cals.length === 0 && !aberto && <p className="mt-1 text-xs text-zinc-500">Vê os teus eventos do Google aqui, junto das marcações.</p>}
      {aberto && (
        <form className="mt-2 space-y-2" onSubmit={(e) => { e.preventDefault(); ligar.mutate(); }}>
          <ol className="list-decimal space-y-0.5 pl-4 text-[11px] leading-5 text-zinc-500">
            <li>No computador, abre calendar.google.com → ⚙️ Definições.</li>
            <li>À esquerda, escolhe o teu calendário (o teu nome).</li>
            <li>Em «Integrar calendário», copia o <strong>Endereço secreto em formato iCal</strong>.</li>
            <li>Cola aqui. É privado: fica cifrado e não volta a aparecer.</li>
          </ol>
          <input aria-label="Nome" value={nome} maxLength={60} onChange={(e) => setNome(e.target.value)}
            className="w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-xs dark:border-zinc-700 dark:bg-zinc-900" placeholder="Nome (ex.: Google pessoal)" />
          <input aria-label="Endereço secreto iCal" value={url} onChange={(e) => setUrl(e.target.value)} autoComplete="off"
            className="w-full rounded border border-zinc-300 bg-white px-2 py-1.5 font-mono text-xs dark:border-zinc-700 dark:bg-zinc-900" placeholder="https://calendar.google.com/calendar/ical/…/basic.ics" />
          <div className="flex justify-end gap-2">
            <Button type="button" size="sm" variant="ghost" onClick={() => setAberto(false)}>Cancelar</Button>
            <Button type="submit" size="sm" loading={ligar.isPending} disabled={!url.trim() || !nome.trim()}>Ligar</Button>
          </div>
        </form>
      )}
    </div>
  );
}
