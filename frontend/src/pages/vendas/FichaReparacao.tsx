import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, ClipboardList, Copy, Eye, EyeOff, FileText, MessageCircle, Save } from 'lucide-react';
import { Button, SectionCard } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { PadraoDesbloqueio } from '../../components/PadraoDesbloqueio';
import { openPdfInNewTab } from '../../lib/downloadPdf';
import { normalizeWaNumber } from '../../lib/phone/formatter';
import { toast } from '../../lib/toast';
import {
  SENHA_TIPO,
  SENHA_TIPO_LABEL,
  fichaApi,
  type ChecklistItem,
  type FichaReparacao as Ficha,
  type ResultadoTeste,
  type SenhaTipo,
} from '../../lib/vendas/ficha';
import { VENDA_ESTADO, type Venda } from '../../lib/vendas/types';

const RESULTADOS: { v: ResultadoTeste; label: string; cls: string }[] = [
  { v: 'ok', label: 'OK', cls: 'bg-emerald-600 text-white' },
  { v: 'falha', label: 'Falha', cls: 'bg-rose-600 text-white' },
  { v: 'nt', label: 'N/T', cls: 'bg-zinc-500 text-white' },
];

/**
 * Sprint 595: a ordem de reparação digital. Preenche-se ao receber o equipamento (IMEI, senha — PIN, letras ou
 * padrão —, se deixa testar, estado, acessórios e a checklist de testes); o cliente assina no balcão ou no
 * telemóvel dele pelo link, e imprime-se em A4 com 2 vias quando for preciso papel.
 */
export default function FichaReparacao({ venda }: { venda: Venda }) {
  const qc = useQueryClient();
  const ficha = useQuery({ queryKey: ['venda-ficha', venda.id], queryFn: () => fichaApi.get(venda.id) });
  const [form, setForm] = useState<Ficha | null>(null);
  const [verSenha, setVerSenha] = useState(false);
  useEffect(() => { if (ficha.data) setForm(ficha.data); }, [ficha.data]);

  const guardar = useMutation({
    mutationFn: (f: Ficha) => fichaApi.guardar(venda.id, {
      imei: f.imei, senhaTipo: f.senhaTipo, senha: f.senha, permiteTestes: f.permiteTestes,
      estadoFisico: f.estadoFisico, acessorios: f.acessorios, checklist: f.checklist,
    }),
    onSuccess: (f) => { qc.setQueryData(['venda-ficha', venda.id], f); toast.success('Ordem de reparação guardada'); },
    onError: (err) => toast.fromError(err),
  });

  if (!form) return <SectionCard title="Ordem de reparação"><div className="h-24 animate-pulse rounded-lg bg-zinc-100 dark:bg-zinc-800" /></SectionCard>;

  const fechada = venda.estado === VENDA_ESTADO.Entregue || venda.estado === VENDA_ESTADO.Cancelada;
  const set = (patch: Partial<Ficha>) => setForm((f) => (f ? { ...f, ...patch } : f));
  const setTeste = (chave: string, campo: 'entrada' | 'saida', v: ResultadoTeste | null) =>
    set({ checklist: form.checklist.map((c) => (c.chave === chave ? { ...c, [campo]: c[campo] === v ? null : v } : c)) });
  const tudo = (campo: 'entrada' | 'saida') => set({ checklist: form.checklist.map((c) => ({ ...c, [campo]: 'ok' as const })) });

  const link = form.linkAssinatura;
  const tel = venda.cliente?.telefone ? normalizeWaNumber(venda.cliente.telefone) : null;
  const msg = link ? `Olá ${venda.cliente?.nome.split(' ')[0] ?? ''}! Aqui está a ordem de reparação do seu ${venda.equipamento ?? 'equipamento'}. Pode ver e assinar aqui: ${link}` : '';

  return (
    <SectionCard
      title={<span className="flex items-center gap-2"><ClipboardList size={16} /> Ordem de reparação</span>}
      action={
        <div className="flex flex-wrap gap-2">
          <Button type="button" size="sm" variant="secondary" leftIcon={<FileText size={14} />}
            title="A4 para cortar ao meio: via da loja (com a senha) e via do cliente, mais a checklist em 2 vias"
            onClick={() => openPdfInNewTab(fichaApi.ordemPdfPath(venda.id)).catch((e) => toast.fromError(e))}>
            Imprimir (2 vias)
          </Button>
          {!fechada && (
            <Button type="button" size="sm" leftIcon={<Save size={14} />} loading={guardar.isPending} onClick={() => guardar.mutate(form)}>Guardar</Button>
          )}
        </div>
      }
    >
      <div className="space-y-4">
        {/* Assinatura do cliente: no balcão (em baixo, "Assinaturas") ou no telemóvel dele pelo link. */}
        <div className={`flex flex-col gap-2 rounded-lg border p-3 text-sm sm:flex-row sm:items-center ${form.clienteAssinouEm ? 'border-emerald-200 bg-emerald-50 dark:border-emerald-900/50 dark:bg-emerald-950/20' : 'border-amber-200 bg-amber-50 dark:border-amber-900/50 dark:bg-amber-950/20'}`}>
          <span className="flex-1">
            {form.clienteAssinouEm
              ? <><CheckCircle2 size={14} className="mr-1 inline text-emerald-600" />Cliente assinou {form.clienteAssinouPeloLink ? 'pelo link' : 'no balcão'} em {new Date(form.clienteAssinouEm).toLocaleString('pt-PT')}.</>
              : 'O cliente ainda não assinou. Envia-lhe o link (vê a ordem e assina no telemóvel) ou recolhe a assinatura no balcão, em baixo.'}
            {!form.tecnicoAssinouEm && <span className="block text-xs text-zinc-500">Falta a assinatura do técnico (em baixo, «Assinaturas»).</span>}
          </span>
          {link && !form.clienteAssinouEm && !fechada && (
            <div className="flex gap-2">
              <Button type="button" size="sm" variant="secondary" leftIcon={<Copy size={14} />}
                onClick={() => navigator.clipboard.writeText(link).then(() => toast.success('Link copiado'), () => toast.error('Não consegui copiar.'))}>
                Copiar link
              </Button>
              {tel && (
                <a href={`https://wa.me/${tel}?text=${encodeURIComponent(msg)}`} target="_blank" rel="noreferrer"
                  className="inline-flex min-h-9 items-center gap-1.5 rounded-lg bg-emerald-600 px-3 text-sm font-medium text-white hover:bg-emerald-700">
                  <MessageCircle size={14} /> WhatsApp
                </a>
              )}
            </div>
          )}
        </div>

        <div className="grid gap-4 lg:grid-cols-2">
          <div className="space-y-3">
            <label className="block">
              <span className={labelCls}>IMEI / nº de série</span>
              <input value={form.imei ?? ''} onChange={(e) => set({ imei: e.target.value })} className={inputCls} maxLength={20} disabled={fechada} placeholder="*#06# no telemóvel" />
            </label>
            <div>
              <span className={labelCls}>Deixa testar o equipamento?</span>
              <div className="flex gap-2">
                {[{ v: true, l: 'Sim' }, { v: false, l: 'Não' }].map((o) => (
                  <button key={o.l} type="button" disabled={fechada} onClick={() => set({ permiteTestes: form.permiteTestes === o.v ? null : o.v })}
                    className={`min-h-9 flex-1 rounded-lg border px-3 text-sm font-medium ${form.permiteTestes === o.v ? 'border-brand-600 bg-brand-600 text-white' : 'border-zinc-300 dark:border-zinc-700'}`}>
                    {o.l}
                  </button>
                ))}
              </div>
            </div>
            <label className="block">
              <span className={labelCls}>Estado físico à entrada</span>
              <textarea rows={2} value={form.estadoFisico ?? ''} onChange={(e) => set({ estadoFisico: e.target.value })} className={`${inputCls} resize-none`}
                maxLength={1000} disabled={fechada} placeholder="Riscos, amolgadelas, vidro traseiro partido…" />
            </label>
            <label className="block">
              <span className={labelCls}>Acessórios entregues</span>
              <input value={form.acessorios ?? ''} onChange={(e) => set({ acessorios: e.target.value })} className={inputCls} maxLength={500} disabled={fechada} placeholder="Capa, carregador, cartão SIM…" />
            </label>
          </div>

          <div>
            <span className={labelCls}>Senha de desbloqueio</span>
            <div className="mb-2 flex flex-wrap gap-1">
              {(Object.keys(SENHA_TIPO_LABEL).map(Number) as SenhaTipo[]).map((t) => (
                <button key={t} type="button" disabled={fechada} onClick={() => set({ senhaTipo: t, senha: t === form.senhaTipo ? form.senha : '' })}
                  className={`min-h-9 rounded-lg border px-3 text-xs font-medium ${form.senhaTipo === t ? 'border-brand-600 bg-brand-600 text-white' : 'border-zinc-300 dark:border-zinc-700'}`}>
                  {SENHA_TIPO_LABEL[t]}
                </button>
              ))}
            </div>
            {(form.senhaTipo === SENHA_TIPO.Pin || form.senhaTipo === SENHA_TIPO.Alfanumerica) && (
              <div className="flex gap-2">
                <input type={verSenha ? 'text' : 'password'} autoComplete="off" value={form.senha ?? ''} disabled={fechada}
                  inputMode={form.senhaTipo === SENHA_TIPO.Pin ? 'numeric' : 'text'} maxLength={form.senhaTipo === SENHA_TIPO.Pin ? 8 : 64}
                  onChange={(e) => set({ senha: form.senhaTipo === SENHA_TIPO.Pin ? e.target.value.replace(/\D/g, '') : e.target.value })}
                  className={inputCls} placeholder={form.senhaTipo === SENHA_TIPO.Pin ? '4 a 8 dígitos' : 'Letras e números'} aria-label="Senha" />
                <Button type="button" variant="icon" aria-label={verSenha ? 'Esconder' : 'Mostrar'} onClick={() => setVerSenha((v) => !v)}>
                  {verSenha ? <EyeOff size={15} /> : <Eye size={15} />}
                </Button>
              </div>
            )}
            {form.senhaTipo === SENHA_TIPO.Padrao && (
              <PadraoDesbloqueio value={form.senha ?? ''} onChange={fechada ? undefined : (v) => set({ senha: v })} />
            )}
            <p className="mt-2 text-[11px] text-zinc-500">
              {fechada && form.senhaTipo !== SENHA_TIPO.Nenhuma && !form.senha
                ? 'A senha foi apagada ao fechar a reparação.'
                : 'Guardada cifrada, só aparece na via da loja (nunca no link nem na via do cliente) e apaga-se ao entregar.'}
            </p>
          </div>
        </div>

        <Checklist itens={form.checklist} fechada={fechada} onTeste={setTeste} onTudo={tudo} />
      </div>
    </SectionCard>
  );
}

function Checklist({ itens, fechada, onTeste, onTudo }: {
  itens: ChecklistItem[];
  fechada: boolean;
  onTeste: (chave: string, campo: 'entrada' | 'saida', v: ResultadoTeste | null) => void;
  onTudo: (campo: 'entrada' | 'saida') => void;
}) {
  return (
    <div>
      <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
        <span className={labelCls}>Checklist de testes</span>
        {!fechada && (
          <div className="flex gap-2 text-xs">
            <button type="button" className="underline" onClick={() => onTudo('entrada')}>tudo OK à entrada</button>
            <button type="button" className="underline" onClick={() => onTudo('saida')}>tudo OK à saída</button>
          </div>
        )}
      </div>
      <div className="grid gap-x-6 sm:grid-cols-2">
        {itens.map((c) => (
          <div key={c.chave} className="flex items-center gap-2 border-b border-zinc-100 py-1.5 text-sm dark:border-zinc-800">
            <span className="min-w-0 flex-1 truncate">{c.label}</span>
            {(['entrada', 'saida'] as const).map((campo) => (
              <div key={campo} className="flex items-center gap-0.5" role="radiogroup" aria-label={`${c.label} — ${campo === 'entrada' ? 'entrada' : 'saída'}`}>
                <span className="w-9 text-right text-[10px] text-zinc-400">{campo === 'entrada' ? 'ent.' : 'saída'}</span>
                {RESULTADOS.map((r) => (
                  <button key={r.v} type="button" role="radio" aria-checked={c[campo] === r.v} disabled={fechada}
                    onClick={() => onTeste(c.chave, campo, r.v)}
                    className={`h-7 min-w-9 rounded px-1 text-[11px] font-semibold ${c[campo] === r.v ? r.cls : 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800'}`}>
                    {r.label}
                  </button>
                ))}
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}
