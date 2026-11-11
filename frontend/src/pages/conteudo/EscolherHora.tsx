import { useEffect, useState } from 'react';
import Modal from '../../components/Modal';
import { Button } from '../../components/ui';

/** `datetime-local` usa a hora local sem fuso: converte nos dois sentidos. */
function paraInput(d: Date): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

function atalhos(): { label: string; data: Date }[] {
  const agora = new Date();
  const em1h = new Date(agora.getTime() + 60 * 60 * 1000);
  em1h.setMinutes(Math.ceil(em1h.getMinutes() / 5) * 5, 0, 0);
  const hoje = (h: number, m = 0) => {
    const d = new Date(agora);
    d.setHours(h, m, 0, 0);
    return d;
  };
  const amanha = (h: number, m = 0) => {
    const d = hoje(h, m);
    d.setDate(d.getDate() + 1);
    return d;
  };
  return [
    { label: 'Daqui a 1 hora', data: em1h },
    { label: 'Hoje 13:00', data: hoje(13) },
    { label: 'Hoje 18:30', data: hoje(18, 30) },
    { label: 'Hoje 21:00', data: hoje(21) },
    { label: 'Amanhã 09:30', data: amanha(9, 30) },
  ].filter((a) => a.data.getTime() > agora.getTime());
}

/**
 * Sprint 610: escolher a hora de uma publicação — atalhos de um toque (as horas que mais
 * resultam) ou data e hora à escolha. Devolve a hora em ISO (UTC).
 */
export function EscolherHora({
  open,
  titulo,
  inicial,
  confirmar,
  loading,
  onClose,
  onConfirm,
}: {
  open: boolean;
  titulo: string;
  inicial: string | null;
  confirmar: string;
  loading?: boolean;
  onClose: () => void;
  onConfirm: (isoUtc: string) => void;
}) {
  const [valor, setValor] = useState('');
  useEffect(() => {
    if (!open) return;
    const base = inicial ? new Date(inicial) : new Date(Date.now() + 60 * 60 * 1000);
    setValor(paraInput(base.getTime() > Date.now() ? base : new Date(Date.now() + 10 * 60 * 1000)));
  }, [open, inicial]);

  const data = valor ? new Date(valor) : null;
  const passada = data !== null && data.getTime() < Date.now() - 60_000;

  return (
    <Modal
      open={open}
      title={titulo}
      onClose={onClose}
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>
            Voltar
          </Button>
          <Button loading={loading} disabled={!data || passada} onClick={() => data && onConfirm(data.toISOString())}>
            {confirmar}
          </Button>
        </div>
      }
    >
      <div className="flex flex-wrap gap-2">
        {atalhos().map((a) => (
          <button
            key={a.label}
            type="button"
            onClick={() => setValor(paraInput(a.data))}
            className="min-h-9 rounded-full border border-zinc-200 px-3 text-sm text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800"
          >
            {a.label}
          </button>
        ))}
      </div>
      <label className="mt-4 block text-sm font-medium text-zinc-700 dark:text-zinc-300">
        Data e hora
        <input
          type="datetime-local"
          value={valor}
          onChange={(e) => setValor(e.target.value)}
          className="mt-1 block min-h-11 w-full rounded-md border border-zinc-200 bg-white px-3 text-base outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 dark:border-zinc-700 dark:bg-zinc-950"
        />
      </label>
      {passada && <p className="mt-2 text-sm text-rose-600">Essa hora já passou — escolhe outra ou usa «Publicar já».</p>}
    </Modal>
  );
}
