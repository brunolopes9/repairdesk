import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Link2, Save, Trash2 } from 'lucide-react';
import { BackButton, Button, PageHeader, SectionCard } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { useConfirm } from '../../components/ConfirmDialog';
import { moloniApi, type MoloniSerie } from '../../lib/moloni/api';
import { toast } from '../../lib/toast';

function SerieSelect({ series, label, value, onChange }: { series: MoloniSerie[]; label: string; value: number | ''; onChange: (v: number | '') => void }) {
  return (
    <label>
      <span className={labelCls}>{label}</span>
      <select value={value} onChange={(ev) => onChange(ev.target.value ? Number(ev.target.value) : '')} className={inputCls}>
        {series.map((s) => <option key={s.id} value={s.id}>{s.nome}{s.predefinida ? ' (predefinida)' : ''}</option>)}
      </select>
    </label>
  );
}

/**
 * Sprint 567: ligação ao Moloni ON. A chave fica encriptada no servidor (nunca volta ao browser).
 * As séries dizem em que série sai cada tipo de venda (na venda ainda se pode escolher outra, ex.: TRADING).
 */
export default function MoloniOn() {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const estado = useQuery({ queryKey: ['moloni', 'estado'], queryFn: moloniApi.estado });
  const [chave, setChave] = useState('');
  const [servico, setServico] = useState<number | ''>('');
  const [reparacao, setReparacao] = useState<number | ''>('');
  const [produto, setProduto] = useState<number | ''>('');

  useEffect(() => {
    const e = estado.data;
    if (!e) return;
    setServico(e.serieServico ?? '');
    setReparacao(e.serieReparacao ?? '');
    setProduto(e.serieProduto ?? '');
  }, [estado.data]);

  const guardar = useMutation({
    mutationFn: () => moloniApi.configurar({
      apiKey: chave.trim() || null,
      serieServico: servico === '' ? null : servico,
      serieReparacao: reparacao === '' ? null : reparacao,
      serieProduto: produto === '' ? null : produto,
    }),
    onSuccess: (e) => {
      qc.setQueryData(['moloni', 'estado'], e);
      setChave('');
      toast.success('Moloni ON ligado', `Empresa ${e.companyId} · ${e.series.length} séries`);
    },
    onError: (err) => toast.fromError(err, 'Não foi possível ligar ao Moloni ON.'),
  });

  const remover = useMutation({
    mutationFn: () => moloniApi.remover(),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['moloni'] });
      toast.success('Ligação ao Moloni ON removida');
    },
  });

  const e = estado.data;
  const series = e?.series ?? [];

  return (
    <div className="space-y-4">
      <BackButton to="/definicoes" label="Definições" />
      <PageHeader title="Moloni ON" description="Faturas, faturas-recibo, recibos, orçamentos e anulações emitidos a partir do Mender." />

      <SectionCard title={
        <span className="flex items-center gap-2">
          {e?.configurado ? <CheckCircle2 size={16} className="text-emerald-600" /> : <Link2 size={16} />}
          {e?.configurado ? `Ligado · empresa ${e.companyId}` : 'Por ligar'}
        </span>
      }>
        <div className="space-y-3">
          <label className="block">
            <span className={labelCls}>Chave da API (Moloni ON → Conta → API → Chaves API)</span>
            <input type="password" value={chave} onChange={(ev) => setChave(ev.target.value)} className={inputCls} autoComplete="off"
              placeholder={e?.configurado ? 'Guardada (encriptada). Cola uma nova só para a substituir.' : 'apik:…'} />
          </label>
          <p className="text-xs text-zinc-500">
            Precisa do módulo <strong>Cliente API</strong> ativo na subscrição do Moloni ON. A chave fica encriptada no servidor e nunca é mostrada.
          </p>

          {e?.configurado && (
            <div className="grid gap-3 sm:grid-cols-3">
              <SerieSelect series={series} label="Série — serviços / software" value={servico} onChange={setServico} />
              <SerieSelect series={series} label="Série — reparações" value={reparacao} onChange={setReparacao} />
              <SerieSelect series={series} label="Série — balcão / produtos" value={produto} onChange={setProduto} />
            </div>
          )}

          <div className="flex flex-wrap gap-2">
            <Button type="button" leftIcon={<Save size={15} />} loading={guardar.isPending} disabled={!e?.configurado && !chave.trim()} onClick={() => guardar.mutate()}>
              {e?.configurado ? 'Guardar' : 'Ligar ao Moloni ON'}
            </Button>
            {e?.configurado && (
              <Button type="button" variant="ghost" leftIcon={<Trash2 size={15} />} loading={remover.isPending}
                onClick={async () => { if (await confirm({ title: 'Desligar o Moloni ON?', description: 'Os documentos já emitidos ficam no Moloni. Deixas de poder emitir pelo Mender até voltares a ligar.', confirmLabel: 'Desligar', destructive: true })) remover.mutate(); }}>
                Desligar
              </Button>
            )}
          </div>
        </div>
      </SectionCard>
    </div>
  );
}
