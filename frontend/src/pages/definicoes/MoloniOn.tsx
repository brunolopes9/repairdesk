import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Link2, Receipt, Save, Trash2, Users } from 'lucide-react';
import { Link } from 'react-router-dom';
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
      {e?.configurado && <ClientesMoloni isAdmin />}
    </div>
  );
}

function ClientesMoloni({ isAdmin }: { isAdmin: boolean }) {
  const qc = useQueryClient();
  const importar = useMutation({
    mutationFn: moloniApi.importarClientes,
    onSuccess: (r) => {
      toast.success('Clientes do Moloni ON importados', `${r.ligados} ligados pelo NIF · ${r.criados} criados · ${r.jaLigados} já estavam ligados`);
      qc.invalidateQueries({ queryKey: ['clientes'] });
    },
    onError: (err) => toast.fromError(err, 'Não foi possível importar.'),
  });
  const ligar = useMutation({
    mutationFn: moloniApi.ligarVendas,
    onSuccess: (r) => {
      toast.success('Vendas ligadas ao Moloni ON', `${r.ligadas} ligadas (${r.comRecibo} com recibo)${r.semCorrespondencia ? ` · ${r.semCorrespondencia} sem fatura correspondente` : ''}`);
      qc.invalidateQueries({ queryKey: ['vendas'] });
      qc.invalidateQueries({ queryKey: ['moloni', 'documentos'] });
    },
    onError: (err) => toast.fromError(err, 'Não foi possível ligar as vendas.'),
  });
  return (
    <SectionCard title="Sincronizar com o Moloni ON">
      <div className="space-y-3 text-sm text-zinc-600 dark:text-zinc-300">
        <p>Os clientes geres em <Link to="/clientes" className="text-brand-600 underline">Clientes</Link>: ao criar ou alterar um cliente no Mender, ele é criado/atualizado no Moloni ON (pelo NIF). O consumidor final é sempre o cliente predefinido do Moloni.</p>
        <p>Para trazer para o Mender os clientes que já tinhas no Moloni (os que ainda não estão cá são criados; os que já estão ficam ligados pelo NIF):</p>
        {isAdmin && <Button type="button" leftIcon={<Users size={15} />} loading={importar.isPending} onClick={() => importar.mutate()}>Importar clientes do Moloni ON</Button>}
        <p>Vendas com nº de fatura que ainda não estão ligadas ao Moloni (ex.: importadas dos PDFs): liga cada uma à sua fatura e ao recibo, pelo nº — assim pagar ou anular aqui atualiza a venda.</p>
        {isAdmin && <Button type="button" variant="secondary" leftIcon={<Receipt size={15} />} loading={ligar.isPending} onClick={() => ligar.mutate()}>Ligar vendas às faturas do Moloni</Button>}
      </div>
    </SectionCard>
  );
}
