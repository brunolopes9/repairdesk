import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useMutation } from '@tanstack/react-query';
import { CheckCircle2, Wrench } from 'lucide-react';
import { repairRequestPublicApi, type SubmitPayload } from '../lib/repairRequests/publicApi';

/**
 * Sprint 354 (Doc 83 Pillar 9): página pública do widget de pedido de reparação.
 * Acessível em /pedido/:slug sem login. O cliente preenche, submete e fica um
 * lead Pendente no backoffice da loja.
 */
export default function PedidoReparacao() {
  const { slug } = useParams<{ slug: string }>();
  const [form, setForm] = useState<SubmitPayload>({ nome: '', email: '', telefone: '', equipamento: '', descricao: '', website: '' });
  const [done, setDone] = useState(false);

  const info = useQuery({
    queryKey: ['intake-widget-info', slug],
    queryFn: () => repairRequestPublicApi.info(slug!),
    enabled: !!slug,
    retry: false,
  });

  const submitMut = useMutation({
    mutationFn: () => repairRequestPublicApi.submit(slug!, form),
    onSuccess: () => setDone(true),
  });

  const brand = info.data?.primaryColor ?? '#0EA5E9';

  if (info.isError) {
    return (
      <div className="mx-auto mt-20 max-w-md px-4 text-center">
        <p className="text-zinc-600">Este formulário não está disponível.</p>
      </div>
    );
  }

  if (done) {
    return (
      <div className="mx-auto mt-20 max-w-md px-4 text-center">
        <CheckCircle2 size={48} className="mx-auto mb-3" style={{ color: brand }} />
        <h1 className="text-xl font-semibold">Pedido recebido!</h1>
        <p className="mt-2 text-sm text-zinc-600">
          Obrigado. A {info.data?.lojaNome ?? 'loja'} vai analisar o teu pedido e entrar em contacto em breve.
        </p>
      </div>
    );
  }

  const canSubmit = form.nome.trim().length >= 2 && form.equipamento.trim().length >= 2 && form.descricao.trim().length >= 5;

  return (
    <div className="mx-auto mt-10 mb-16 max-w-lg px-4">
      <header className="mb-5 text-center">
        <div className="mx-auto mb-2 flex h-12 w-12 items-center justify-center rounded-full" style={{ background: `${brand}20` }}>
          <Wrench size={22} style={{ color: brand }} />
        </div>
        <h1 className="text-xl font-semibold">{info.data?.lojaNome ?? 'Pedir reparação'}</h1>
        <p className="mt-1 text-sm text-zinc-500">Descreve o problema e entramos em contacto.</p>
      </header>

      <form
        onSubmit={(e) => { e.preventDefault(); if (canSubmit) submitMut.mutate(); }}
        className="space-y-3"
      >
        {/* Honeypot — escondido. Bots preenchem, humanos não vêem. */}
        <input
          type="text" tabIndex={-1} autoComplete="off"
          value={form.website}
          onChange={(e) => setForm({ ...form, website: e.target.value })}
          className="hidden" aria-hidden="true"
        />

        <Field label="Nome *">
          <input type="text" required value={form.nome} onChange={(e) => setForm({ ...form, nome: e.target.value })} className={inputCls} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Telefone">
            <input type="tel" value={form.telefone ?? ''} onChange={(e) => setForm({ ...form, telefone: e.target.value })} className={inputCls} />
          </Field>
          <Field label="Email">
            <input type="email" value={form.email ?? ''} onChange={(e) => setForm({ ...form, email: e.target.value })} className={inputCls} />
          </Field>
        </div>
        <Field label="Equipamento *">
          <input type="text" required placeholder="ex: iPhone 13, Samsung A52…" value={form.equipamento} onChange={(e) => setForm({ ...form, equipamento: e.target.value })} className={inputCls} />
        </Field>
        <Field label="Qual é o problema? *">
          <textarea required rows={4} value={form.descricao} onChange={(e) => setForm({ ...form, descricao: e.target.value })} className={inputCls} />
        </Field>

        {submitMut.isError && (
          <p className="text-sm text-rose-600">Não foi possível enviar. Verifica os campos e tenta de novo.</p>
        )}

        <button
          type="submit" disabled={!canSubmit || submitMut.isPending}
          className="w-full rounded-lg py-2.5 text-sm font-medium text-white disabled:opacity-50"
          style={{ background: brand }}
        >
          {submitMut.isPending ? 'A enviar…' : 'Enviar pedido'}
        </button>
        <p className="text-center text-[11px] text-zinc-400">
          Ao enviar concordas que a loja te contacte sobre este pedido.
        </p>
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
