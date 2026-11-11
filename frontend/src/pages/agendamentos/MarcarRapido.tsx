import { useMemo, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { BellRing, ChevronDown, X } from 'lucide-react';
import { Button } from '../../components/ui';
import { toast } from '../../lib/toast';
import { apiErrorMessage } from '../../lib/errors';
import { appointmentsApi } from '../../lib/appointments/api';

const HORAS = Array.from({ length: 11 }, (_, i) => i + 9); // 09:00–19:00
const DIA_SEMANA = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

function soData(d: Date): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

function proximosDias(): { valor: string; label: string }[] {
  const hoje = new Date();
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(hoje);
    d.setDate(hoje.getDate() + i);
    const label = i === 0 ? 'Hoje' : i === 1 ? 'Amanhã' : `${DIA_SEMANA[d.getDay()]} ${d.getDate()}`;
    return { valor: soData(d), label };
  });
}

const chip = (ativo: boolean) =>
  `min-h-10 rounded-full border px-3 text-sm font-medium transition ${
    ativo
      ? 'border-brand-600 bg-brand-600 text-white'
      : 'border-zinc-200 text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800'
  }`;

/**
 * Sprint 611: marcar um agendamento em segundos — nome, o quê, um toque no dia e outro na hora.
 * Telefone, equipamento e duração ficam em «Mais detalhes». O lembrete chega ao telemóvel 1 h antes.
 */
export function MarcarRapidoModal({
  initialIso,
  onClose,
  onSaved,
}: {
  initialIso?: string | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const dias = useMemo(() => proximosDias(), []);
  const inicio = initialIso ? new Date(initialIso) : null;
  const [nome, setNome] = useState('');
  const [oQue, setOQue] = useState('');
  const [dia, setDia] = useState(inicio ? soData(inicio) : dias[0].valor);
  const [hora, setHora] = useState(
    inicio ? `${String(inicio.getHours()).padStart(2, '0')}:${String(inicio.getMinutes()).padStart(2, '0')}` : '',
  );
  const [detalhes, setDetalhes] = useState(false);
  const [telefone, setTelefone] = useState('');
  const [equipamento, setEquipamento] = useState('');
  const [duracao, setDuracao] = useState(60);

  const outroDia = !dias.some((d) => d.valor === dia);
  const horaChip = HORAS.map((h) => `${String(h).padStart(2, '0')}:00`);
  const quando = dia && hora ? new Date(`${dia}T${hora}`) : null;
  const passado = quando !== null && quando.getTime() < Date.now() - 5 * 60_000;

  const criar = useMutation({
    mutationFn: () => {
      if (nome.trim().length < 2) throw new Error('Escreve o nome do cliente.');
      if (!quando) throw new Error('Escolhe o dia e a hora.');
      return appointmentsApi.create({
        nome: nome.trim(),
        notas: oQue.trim() || null,
        telefone: telefone.trim() || null,
        equipamento: equipamento.trim() || null,
        scheduledAt: quando.toISOString(),
        durationMin: duracao,
      });
    },
    onSuccess: (a) => {
      const d = new Date(a.scheduledAt);
      toast.success(`Marcado: ${a.nome}, ${d.toLocaleDateString('pt-PT', { weekday: 'long', day: 'numeric' })} às ${d.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' })}.`);
      onSaved();
    },
    onError: (e) => toast.error(e instanceof Error && !('isAxiosError' in e) ? e.message : apiErrorMessage(e) || 'Não foi possível marcar.'),
  });

  const input =
    'w-full min-h-11 rounded-lg border border-zinc-200 bg-white px-3 text-base outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 dark:border-zinc-700 dark:bg-zinc-950';

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 sm:items-center sm:p-4" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="marcar-titulo"
        className="max-h-[92vh] w-full overflow-y-auto rounded-t-2xl border border-zinc-200 bg-white p-5 shadow-xl dark:border-zinc-800 dark:bg-zinc-900 sm:max-w-lg sm:rounded-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 id="marcar-titulo" className="text-lg font-semibold">Marcar</h2>
          <button type="button" onClick={onClose} aria-label="Fechar" className="rounded-md p-1 text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800">
            <X size={18} />
          </button>
        </div>

        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); criar.mutate(); }}>
          <input className={input} placeholder="Nome do cliente" value={nome} onChange={(e) => setNome(e.target.value)} autoFocus />
          <input className={input} placeholder="O quê? Ex.: PS5 — limpeza e metal líquido" value={oQue} onChange={(e) => setOQue(e.target.value)} />

          <div>
            <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500">Dia</p>
            <div className="flex flex-wrap gap-2">
              {dias.map((d) => (
                <button key={d.valor} type="button" className={chip(dia === d.valor)} onClick={() => setDia(d.valor)}>
                  {d.label}
                </button>
              ))}
              <label className={`${chip(outroDia)} inline-flex items-center gap-2`}>
                Outro
                <input type="date" value={dia} onChange={(e) => setDia(e.target.value)} className="w-[8.5rem] bg-transparent text-sm outline-none" aria-label="Outro dia" />
              </label>
            </div>
          </div>

          <div>
            <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500">Hora</p>
            <div className="flex flex-wrap gap-2">
              {horaChip.map((h) => (
                <button key={h} type="button" className={chip(hora === h)} onClick={() => setHora(h)}>
                  {h}
                </button>
              ))}
              <label className={`${chip(hora !== '' && !horaChip.includes(hora))} inline-flex items-center gap-2`}>
                Outra
                <input type="time" step={300} value={hora} onChange={(e) => setHora(e.target.value)} className="bg-transparent text-sm outline-none" aria-label="Outra hora" />
              </label>
            </div>
            {passado && <p className="mt-2 text-sm text-amber-700 dark:text-amber-300">Essa hora já passou — confirma o dia.</p>}
          </div>

          <button type="button" onClick={() => setDetalhes((v) => !v)} aria-expanded={detalhes}
            className="inline-flex min-h-9 items-center gap-1 text-sm font-medium text-zinc-600 dark:text-zinc-300">
            Mais detalhes <ChevronDown size={15} className={`transition ${detalhes ? 'rotate-180' : ''}`} />
          </button>
          {detalhes && (
            <div className="space-y-3">
              <input className={input} placeholder="Telefone" inputMode="tel" value={telefone} onChange={(e) => setTelefone(e.target.value)} />
              <input className={input} placeholder="Equipamento (ex.: iPhone 13)" value={equipamento} onChange={(e) => setEquipamento(e.target.value)} />
              <label className="flex items-center gap-3 text-sm text-zinc-600 dark:text-zinc-300">
                Duração
                <select value={duracao} onChange={(e) => setDuracao(Number(e.target.value))} className="min-h-10 rounded-lg border border-zinc-200 bg-white px-2 dark:border-zinc-700 dark:bg-zinc-950">
                  {[15, 30, 45, 60, 90, 120, 180].map((m) => <option key={m} value={m}>{m < 60 ? `${m} min` : `${m / 60} h`.replace('.5', ',5')}</option>)}
                </select>
              </label>
            </div>
          )}

          <p className="flex items-center gap-2 text-xs text-zinc-500">
            <BellRing size={14} /> Recebes um lembrete no telemóvel 1 hora antes.
          </p>

          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" onClick={onClose}>Cancelar</Button>
            <Button type="submit" size="lg" loading={criar.isPending} disabled={!nome.trim() || !quando}>Marcar</Button>
          </div>
        </form>
      </div>
    </div>
  );
}
