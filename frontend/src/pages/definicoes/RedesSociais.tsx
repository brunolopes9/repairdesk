import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Copy, Link2, Share2, Unlink } from 'lucide-react';
import { useConfirm } from '../../components/ConfirmDialog';
import { BackButton, Button } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { RedeSocial, REDE_LABEL } from '../../lib/conteudo/types';
import { apiErrorMessage } from '../../lib/errors';
import { redesSociaisApi, type RedeLigada, type RedesSociaisEstado } from '../../lib/redesSociais/api';
import { toast } from '../../lib/toast';

/**
 * Sprint 599 (Doc 95 Fase B): ligar Facebook, Instagram e Threads para o Mender publicar sozinho.
 * Três passos à vista: 1) dados da app Meta/Threads, 2) ligar as contas (login feito pelo dono na
 * própria Meta), 3) interruptor da publicação automática. Os secrets nunca voltam ao browser.
 */
export default function RedesSociais() {
  const qc = useQueryClient();
  const [params, setParams] = useSearchParams();
  const estado = useQuery({ queryKey: ['redes-sociais'], queryFn: redesSociaisApi.estado });

  // Regresso do OAuth: ?ligado=… ou ?erro=…
  useEffect(() => {
    const ligado = params.get('ligado');
    const erro = params.get('erro');
    if (!ligado && !erro) return;
    if (ligado) toast.success(`Ligado: ${ligado}`);
    if (erro) toast.error(erro);
    setParams({}, { replace: true });
    qc.invalidateQueries({ queryKey: ['redes-sociais'] });
  }, [params, setParams, qc]);

  const atualizar = (e: RedesSociaisEstado) => qc.setQueryData(['redes-sociais'], e);

  return (
    <div className="mx-auto max-w-3xl space-y-5 px-4 py-6">
      <BackButton to="/definicoes" label="Voltar a Definições" />
      <header className="space-y-2">
        <h1 className="flex items-center gap-2 text-2xl font-semibold">
          <Share2 size={24} strokeWidth={2} />
          Redes sociais
        </h1>
        <p className="text-sm text-zinc-500">
          Liga as contas da loja para o Mender publicar sozinho, à hora marcada, o que aprovas em Conteúdo.
        </p>
      </header>

      {estado.isLoading || !estado.data ? null : (
        <>
          <PassoApps estado={estado.data} onGuardado={atualizar} />
          <PassoContas estado={estado.data} onMudou={atualizar} />
          <PassoAuto estado={estado.data} onMudou={atualizar} />
        </>
      )}
    </div>
  );
}

function Passo({ n, titulo, children }: { n: number; titulo: string; children: React.ReactNode }) {
  return (
    <section className="rounded-lg border border-zinc-200 bg-white p-4 shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
      <h2 className="flex items-center gap-2 text-base font-semibold">
        <span className="grid h-6 w-6 place-items-center rounded-full bg-brand-600 text-xs font-bold text-white">{n}</span>
        {titulo}
      </h2>
      <div className="mt-3 space-y-3">{children}</div>
    </section>
  );
}

function Copiar({ valor }: { valor: string }) {
  return (
    <div className="flex items-center gap-2">
      <code className="min-w-0 flex-1 truncate rounded bg-zinc-50 px-2 py-1.5 text-xs dark:bg-zinc-950">{valor}</code>
      <button
        type="button"
        onClick={() => navigator.clipboard?.writeText(valor).then(() => toast.success('Copiado.'))}
        className="inline-flex min-h-9 items-center gap-1 rounded-md border border-zinc-300 px-2 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800"
      >
        <Copy size={12} /> Copiar
      </button>
    </div>
  );
}

function PassoApps({ estado, onGuardado }: { estado: RedesSociaisEstado; onGuardado: (e: RedesSociaisEstado) => void }) {
  const [metaId, setMetaId] = useState(estado.metaAppId ?? '');
  const [metaSecret, setMetaSecret] = useState('');
  const [metaConfig, setMetaConfig] = useState(estado.metaLoginConfigId ?? '');
  const [thId, setThId] = useState(estado.threadsAppId ?? '');
  const [thSecret, setThSecret] = useState('');

  const guardar = useMutation({
    mutationFn: () =>
      redesSociaisApi.guardarApps({
        metaAppId: metaId.trim() || null,
        metaAppSecret: metaSecret.trim() || null,
        threadsAppId: thId.trim() || null,
        threadsAppSecret: thSecret.trim() || null,
        metaLoginConfigId: metaConfig.trim() || null,
      }),
    onSuccess: (e) => {
      onGuardado(e);
      setMetaSecret('');
      setThSecret('');
      toast.success('Dados das apps guardados.');
    },
    onError: (err) => toast.error(apiErrorMessage(err)),
  });

  return (
    <Passo n={1} titulo="App da Meta (feito uma vez)">
      <p className="text-sm text-zinc-600 dark:text-zinc-400">
        Em developers.facebook.com cria uma app (tipo Empresa) com os casos de uso de Facebook/Instagram e de
        Threads. Copia para aqui o App ID e o App Secret de cada um, e regista estes endereços de redirecionamento:
      </p>
      <div className="space-y-1.5">
        <p className={labelCls}>Redirecionamento Facebook/Instagram</p>
        <Copiar valor={estado.redirectMeta} />
        <p className={labelCls}>Redirecionamento Threads</p>
        <Copiar valor={estado.redirectThreads} />
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <label>
          <span className={labelCls}>Meta — App ID</span>
          <input className={inputCls} inputMode="numeric" value={metaId} onChange={(e) => setMetaId(e.target.value)} />
        </label>
        <label>
          <span className={labelCls}>Meta — App Secret {estado.metaAppSecretDefinido && '(guardado)'}</span>
          <input className={inputCls} type="password" autoComplete="off" placeholder={estado.metaAppSecretDefinido ? '••••••••  (deixa vazio para manter)' : ''}
            value={metaSecret} onChange={(e) => setMetaSecret(e.target.value)} />
        </label>
        <label className="sm:col-span-2">
          <span className={labelCls}>Meta — ID da configuração do login (Facebook Login for Business → Configurations)</span>
          <input className={inputCls} inputMode="numeric" value={metaConfig} onChange={(e) => setMetaConfig(e.target.value)} />
        </label>
        <label>
          <span className={labelCls}>Threads — App ID</span>
          <input className={inputCls} inputMode="numeric" value={thId} onChange={(e) => setThId(e.target.value)} />
        </label>
        <label>
          <span className={labelCls}>Threads — App Secret {estado.threadsAppSecretDefinido && '(guardado)'}</span>
          <input className={inputCls} type="password" autoComplete="off" placeholder={estado.threadsAppSecretDefinido ? '••••••••  (deixa vazio para manter)' : ''}
            value={thSecret} onChange={(e) => setThSecret(e.target.value)} />
        </label>
      </div>
      <div className="flex justify-end">
        <Button loading={guardar.isPending} onClick={() => guardar.mutate()}>Guardar</Button>
      </div>
    </Passo>
  );
}

function PassoContas({ estado, onMudou }: { estado: RedesSociaisEstado; onMudou: (e: RedesSociaisEstado) => void }) {
  const confirm = useConfirm();
  const porRede = (r: RedeSocial) => estado.ligacoes.find((l) => l.rede === r);

  const ligar = useMutation({
    mutationFn: (p: 'meta' | 'threads') => redesSociaisApi.urlLigar(p),
    onSuccess: (url) => window.location.assign(url),
    onError: (err) => toast.error(apiErrorMessage(err)),
  });
  const desligar = useMutation({
    mutationFn: (r: RedeSocial) => redesSociaisApi.desligar(r),
    onSuccess: (e) => {
      onMudou(e);
      toast.success('Conta desligada.');
    },
    onError: (err) => toast.error(apiErrorMessage(err)),
  });

  const pedirDesligar = async (r: RedeSocial) => {
    const ok = await confirm({
      title: `Desligar ${REDE_LABEL[r]}?`,
      description: 'O Mender deixa de publicar nesta rede até ligares outra vez.',
      confirmLabel: 'Desligar',
      destructive: true,
    });
    if (ok) desligar.mutate(r);
  };

  return (
    <Passo n={2} titulo="Ligar as contas">
      <div className="grid gap-3 sm:grid-cols-2">
        <CartaoRede
          titulo="Facebook e Instagram"
          contas={[porRede(RedeSocial.Facebook), porRede(RedeSocial.Instagram)]}
          podeLigar={Boolean(estado.metaAppId && estado.metaAppSecretDefinido)}
          aLigar={ligar.isPending && ligar.variables === 'meta'}
          dica="No ecrã da Meta escolhe só a Página da loja (e a conta de Instagram ligada a ela)."
          onLigar={() => ligar.mutate('meta')}
          onDesligar={pedirDesligar}
        />
        <CartaoRede
          titulo="Threads"
          contas={[porRede(RedeSocial.Threads)]}
          podeLigar={Boolean(estado.threadsAppId && estado.threadsAppSecretDefinido)}
          aLigar={ligar.isPending && ligar.variables === 'threads'}
          dica="Entra com a conta @lopestech.pt."
          onLigar={() => ligar.mutate('threads')}
          onDesligar={pedirDesligar}
        />
      </div>
      <TokenThreads onLigado={onMudou} />
      <p className="text-xs text-zinc-500">TikTok: até o TikTok aprovar a app, continua a ser publicado à mão.</p>
    </Passo>
  );
}

/**
 * Alternativa ao OAuth do Threads: o dono gera um token no painel da Meta (casos de uso → Threads →
 * Settings → «User Token Generator») e cola-o aqui. Não precisa do App Secret.
 */
function TokenThreads({ onLigado }: { onLigado: (e: RedesSociaisEstado) => void }) {
  const [token, setToken] = useState('');
  const ligar = useMutation({
    mutationFn: () => redesSociaisApi.ligarThreadsComToken(token.trim()),
    onSuccess: (e) => {
      onLigado(e);
      setToken('');
      toast.success('Threads ligado.');
    },
    onError: (err) => toast.error(apiErrorMessage(err)),
  });
  return (
    <div className="rounded-md border border-dashed border-zinc-300 p-3 dark:border-zinc-700">
      <p className="text-sm font-medium">Threads com token (sem App Secret)</p>
      <p className="mt-1 text-xs text-zinc-500">
        Na app da Meta: Casos de utilização → Access the Threads API → Settings → «User Token Generator» → gerar
        o token da conta @lopestech.pt e colá-lo aqui.
      </p>
      <div className="mt-2 flex gap-2">
        <input className={inputCls} type="password" autoComplete="off" placeholder="Cola aqui o token"
          value={token} onChange={(e) => setToken(e.target.value)} />
        <Button loading={ligar.isPending} disabled={token.trim().length < 20} onClick={() => ligar.mutate()}>Ligar</Button>
      </div>
    </div>
  );
}

function CartaoRede({
  titulo, contas, podeLigar, aLigar, dica, onLigar, onDesligar,
}: {
  titulo: string;
  contas: (RedeLigada | undefined)[];
  podeLigar: boolean;
  aLigar: boolean;
  dica: string;
  onLigar: () => void;
  onDesligar: (r: RedeSocial) => void;
}) {
  const ligadas = contas.filter((c): c is RedeLigada => Boolean(c));
  return (
    <div className="rounded-md border border-zinc-200 p-3 dark:border-zinc-800">
      <p className="font-medium">{titulo}</p>
      {ligadas.length === 0 ? (
        <p className="mt-1 text-sm text-zinc-500">Por ligar.</p>
      ) : (
        <ul className="mt-2 space-y-1.5">
          {ligadas.map((l) => (
            <li key={l.rede} className="flex items-start justify-between gap-2 text-sm">
              <span className="min-w-0">
                <span className={`inline-flex items-center gap-1 ${l.valida ? 'text-emerald-700 dark:text-emerald-300' : 'text-rose-700 dark:text-rose-300'}`}>
                  {l.valida ? <CheckCircle2 size={14} /> : <AlertTriangle size={14} />}
                  {REDE_LABEL[l.rede]}: {l.contaNome}
                </span>
                {l.tokenExpiraEm && (
                  <span className="block text-xs text-zinc-500">Acesso renovado sozinho · válido até {new Date(l.tokenExpiraEm).toLocaleDateString('pt-PT')}</span>
                )}
                {l.ultimoErro && <span className="block text-xs text-rose-700 dark:text-rose-300">{l.ultimoErro}</span>}
              </span>
              <button type="button" onClick={() => onDesligar(l.rede)} className="shrink-0 text-zinc-400 hover:text-rose-600" title="Desligar">
                <Unlink size={14} />
              </button>
            </li>
          ))}
        </ul>
      )}
      <p className="mt-2 text-xs text-zinc-500">{dica}</p>
      <Button className="mt-3 w-full" variant={ligadas.length ? 'secondary' : 'primary'} leftIcon={<Link2 size={16} />}
        disabled={!podeLigar} loading={aLigar} onClick={onLigar}>
        {ligadas.length ? 'Ligar outra vez' : 'Ligar'}
      </Button>
      {!podeLigar && <p className="mt-1 text-xs text-amber-700 dark:text-amber-300">Primeiro preenche o passo 1.</p>}
    </div>
  );
}

function PassoAuto({ estado, onMudou }: { estado: RedesSociaisEstado; onMudou: (e: RedesSociaisEstado) => void }) {
  const confirm = useConfirm();
  const mudar = useMutation({
    mutationFn: (ligado: boolean) => redesSociaisApi.autoPublicar(ligado),
    onSuccess: (e) => {
      onMudou(e);
      toast.success(e.autoPublicar ? 'Publicação automática ligada.' : 'Publicação automática desligada.');
    },
    onError: (err) => toast.error(apiErrorMessage(err)),
  });

  return (
    <Passo n={3} titulo="Publicação automática">
      <div className="flex items-start justify-between gap-4">
        <p className="text-sm text-zinc-600 dark:text-zinc-400">
          Ligado: tudo o que aprovares sai sozinho à hora marcada nas redes ligadas. Se alguma rede falhar,
          tento mais 3 vezes e recebes uma notificação.
        </p>
        <Button
          variant={estado.autoPublicar ? 'secondary' : 'primary'}
          loading={mudar.isPending}
          onClick={async () => {
            if (!estado.autoPublicar) {
              const ok = await confirm({
                title: 'Ligar a publicação automática?',
                description: 'Confirma que o que já está agendado à mão no Business Suite foi marcado como «agendado» — senão sai duas vezes.',
                confirmLabel: 'Ligar',
              });
              if (!ok) return;
            }
            mudar.mutate(!estado.autoPublicar);
          }}
        >
          {estado.autoPublicar ? 'Desligar' : 'Ligar'}
        </Button>
      </div>
      <p className={`text-sm font-medium ${estado.autoPublicar ? 'text-emerald-700 dark:text-emerald-300' : 'text-zinc-500'}`}>
        {estado.autoPublicar ? '● Ligada' : '○ Desligada'}
      </p>
    </Passo>
  );
}
