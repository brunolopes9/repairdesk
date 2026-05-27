import { useEffect, useMemo, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useMutation } from '@tanstack/react-query';
import { CalendarCheck, CheckCircle2 } from 'lucide-react';
import { bookingPublicApi, type SubmitBookingPayload } from '../lib/booking/publicApi';

/**
 * Sprint 389 (Doc 84): página pública de marcação online. Acessível em /agendar/:slug sem login.
 * O cliente escolhe data + hora e deixa contacto; fica um Appointment Online/Agendado que o staff
 * vê em /agendamentos (e é avisado por push).
 */
export default function Agendar() {
  const { slug } = useParams<{ slug: string }>();
  const [form, setForm] = useState({ nome: '', telefone: '', email: '', equipamento: '', notas: '', website: '' });
  const [data, setData] = useState('');
  const [hora, setHora] = useState('');
  const [done, setDone] = useState(false);

  const info = useQuery({
    queryKey: ['booking-info', slug],
    queryFn: () => bookingPublicApi.info(slug!),
    enabled: !!slug,
    retry: false,
  });

  const submitMut = useMutation({
    mutationFn: () => {
      const scheduledAt = new Date(`${data}T${hora}:00`).toISOString();
      const payload: SubmitBookingPayload = {
        nome: form.nome.trim(),
        telefone: form.telefone.trim() || null,
        email: form.email.trim() || null,
        equipamento: form.equipamento.trim() || null,
        notas: form.notas.trim() || null,
        scheduledAt,
        durationMin: 30,
        website: form.website,
      };
      return bookingPublicApi.submit(slug!, payload);
    },
    onSuccess: () => setDone(true),
    onError: () => {
      // Refresca disponibilidade (a hora pode ter sido ocupada entretanto).
      if (slug && data) bookingPublicApi.availability(slug, data).then((t) => setTaken(new Set(t))).catch(() => {});
    },
  });

  const slotOcupado = (submitMut.error as { response?: { status?: number } } | undefined)?.response?.status === 409;

  const brand = info.data?.primaryColor ?? '#0EA5E9';

  // Intervalo de datas: hoje → +90 dias. Slots 09:00–18:30 de 30 em 30 min.
  const { minDate, maxDate } = useMemo(() => {
    const today = new Date();
    const max = new Date();
    max.setDate(max.getDate() + 90);
    return { minDate: today.toISOString().slice(0, 10), maxDate: max.toISOString().slice(0, 10) };
  }, []);
  // Sprint 396: slots gerados a partir do horário configurável da loja (info), de slotMinutes em slotMinutes.
  const slots = useMemo(() => {
    const open = info.data?.openHour ?? 9;
    const close = info.data?.closeHour ?? 19;
    const step = info.data?.slotMinutes ?? 30;
    const out: string[] = [];
    for (let m = open * 60; m + step <= close * 60; m += step) {
      out.push(`${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`);
    }
    return out;
  }, [info.data?.openHour, info.data?.closeHour, info.data?.slotMinutes]);

  // Slots já ocupados no dia escolhido — desativados no dropdown.
  const [taken, setTaken] = useState<Set<string>>(new Set());
  useEffect(() => {
    if (!slug || !data) { setTaken(new Set()); return; }
    let cancel = false;
    bookingPublicApi.availability(slug, data)
      .then((t) => { if (!cancel) setTaken(new Set(t)); })
      .catch(() => { if (!cancel) setTaken(new Set()); });
    return () => { cancel = true; };
  }, [slug, data]);

  // Se a hora escolhida ficou ocupada (ou fora dos slots), limpa-a.
  useEffect(() => {
    if (hora && (taken.has(hora) || !slots.includes(hora))) setHora('');
  }, [taken, slots, hora]);

  if (info.isError) {
    return <div className="mx-auto mt-20 max-w-md px-4 text-center"><p className="text-zinc-600">Esta página de marcação não está disponível.</p></div>;
  }

  if (done) {
    const quando = data && hora ? new Date(`${data}T${hora}:00`) : null;
    return (
      <div className="mx-auto mt-20 max-w-md px-4 text-center">
        <CheckCircle2 size={48} className="mx-auto mb-3" style={{ color: brand }} />
        <h1 className="text-xl font-semibold">Marcação enviada!</h1>
        <p className="mt-2 text-sm text-zinc-600">
          {quando ? <>Pedido para <strong>{quando.toLocaleString('pt-PT', { dateStyle: 'long', timeStyle: 'short' })}</strong>. </> : null}
          A {info.data?.lojaNome ?? 'loja'} confirma contigo em breve.
        </p>
      </div>
    );
  }

  const canSubmit =
    form.nome.trim().length >= 2 &&
    (form.telefone.trim().length > 0 || form.email.trim().length > 0) &&
    !!data && !!hora;

  return (
    <div className="mx-auto mt-10 mb-16 max-w-lg px-4">
      <header className="mb-5 text-center">
        <div className="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full" style={{ background: `${brand}20` }}>
          <CalendarCheck size={22} style={{ color: brand }} />
        </div>
        <h1 className="text-xl font-semibold">{info.data?.lojaNome ?? 'Marcar hora'}</h1>
        <p className="mt-1 text-sm text-zinc-500">Escolhe o dia e a hora que te dá jeito.</p>
      </header>

      <form onSubmit={(e) => { e.preventDefault(); if (canSubmit) submitMut.mutate(); }} className="space-y-3">
        {/* Honeypot */}
        <input type="text" tabIndex={-1} autoComplete="off" value={form.website} onChange={(e) => setForm({ ...form, website: e.target.value })} className="hidden" aria-hidden="true" />

        <div className="grid grid-cols-2 gap-3">
          <Field label="Data *">
            <input type="date" required min={minDate} max={maxDate} value={data} onChange={(e) => setData(e.target.value)} className={inputCls} />
          </Field>
          <Field label="Hora *">
            <select required value={hora} onChange={(e) => setHora(e.target.value)} className={inputCls}>
              <option value="">—</option>
              {slots.map((s) => <option key={s} value={s} disabled={taken.has(s)}>{s}{taken.has(s) ? ' (ocupado)' : ''}</option>)}
            </select>
          </Field>
        </div>

        <Field label="Nome *">
          <input type="text" required value={form.nome} onChange={(e) => setForm({ ...form, nome: e.target.value })} className={inputCls} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Telefone">
            <input type="tel" value={form.telefone} onChange={(e) => setForm({ ...form, telefone: e.target.value })} className={inputCls} />
          </Field>
          <Field label="Email">
            <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} className={inputCls} />
          </Field>
        </div>
        <p className="-mt-1 text-[11px] text-zinc-400">Indica pelo menos um contacto (telefone ou email).</p>
        <Field label="Equipamento / motivo">
          <input type="text" placeholder="ex: iPhone 13 sem som, entregar Samsung…" value={form.equipamento} onChange={(e) => setForm({ ...form, equipamento: e.target.value })} className={inputCls} />
        </Field>
        <Field label="Notas">
          <textarea rows={3} value={form.notas} onChange={(e) => setForm({ ...form, notas: e.target.value })} className={inputCls} />
        </Field>

        {submitMut.isError && (
          <p className="text-sm text-rose-600">
            {slotOcupado ? 'Essa hora foi ocupada entretanto — escolhe outra (as ocupadas estão assinaladas).' : 'Não foi possível marcar. Verifica os campos e tenta de novo.'}
          </p>
        )}

        <button type="submit" disabled={!canSubmit || submitMut.isPending} className="w-full rounded-lg py-2.5 text-sm font-medium text-white disabled:opacity-50" style={{ background: brand }}>
          {submitMut.isPending ? 'A enviar…' : 'Marcar hora'}
        </button>
        <p className="text-center text-[11px] text-zinc-400">A loja confirma a disponibilidade e entra em contacto.</p>
      </form>
    </div>
  );
}

const inputCls = 'w-full rounded-lg border border-zinc-300 px-3 py-2 text-sm focus:border-zinc-400 focus:outline-none dark:border-zinc-700 dark:bg-zinc-800';

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium text-zinc-600 dark:text-zinc-400">{label}</span>
      {children}
    </label>
  );
}
