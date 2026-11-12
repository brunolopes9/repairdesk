import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Copy, KeyRound } from 'lucide-react';
import { Button } from '../../components/ui';
import { api } from '../../lib/api';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';

interface SegredoGerado {
  endereco: string;
  segredo: string;
  urlMender: string;
}

/**
 * Sprint 632 (Doc 98 F5): faturas@… na Cloudflare. Um Email Worker envia cada email para o Mender (que tira os
 * PDFs); o que não tem PDF (ex.: código de confirmação do Gmail) volta para o email do dono. O segredo aparece
 * uma única vez — é para colar na Cloudflare (Worker → Settings → Variables → MENDER_SECRET, tipo Secret).
 */
export function FaturasCloudflare({ endereco, temSegredo, ultimoEmailEm }: { endereco: string | null; temSegredo: boolean; ultimoEmailEm: string | null }) {
  const qc = useQueryClient();
  const [novo, setNovo] = useState(endereco ?? 'faturas@lopestech.pt');
  const [gerado, setGerado] = useState<SegredoGerado | null>(null);

  const gerar = useMutation({
    mutationFn: () => api.post<SegredoGerado>('/faturas-email/cloudflare', { endereco: novo }).then((r) => r.data),
    onSuccess: (d) => {
      setGerado(d);
      qc.invalidateQueries({ queryKey: ['faturas-email'] });
    },
    onError: (e) => toast.error('Não ficou ligado', apiErrorMessage(e)),
  });

  async function copiar(texto: string) {
    try {
      await navigator.clipboard.writeText(texto);
      toast.success('Copiado');
    } catch {
      toast.error('Não consegui copiar — seleciona e copia à mão.');
    }
  }

  return (
    <div className="rounded-xl border border-zinc-200 p-4 text-sm dark:border-zinc-800">
      <h3 className="font-semibold">Endereço na Cloudflare (recomendado)</h3>
      <p className="mt-1 text-zinc-500">
        Sem caixa de correio: a Cloudflare recebe em <strong>{endereco ?? 'faturas@lopestech.pt'}</strong> e entrega ao Mender. Os emails sem PDF voltam para o teu Gmail.
      </p>
      {temSegredo && !gerado && (
        <p className="mt-2 text-xs text-zinc-500">
          Ligado a <strong>{endereco}</strong>. {ultimoEmailEm ? `Último email: ${new Date(ultimoEmailEm).toLocaleString('pt-PT')}.` : 'Ainda não chegou nenhum email.'}
        </p>
      )}
      {!gerado ? (
        <div className="mt-3 flex flex-wrap items-end gap-2">
          <label className="block min-w-[16rem] flex-1">
            <span className="text-xs text-zinc-500">Endereço</span>
            <input value={novo} onChange={(e) => setNovo(e.target.value)} className="mt-1 w-full rounded-lg border border-zinc-300 px-3 py-2 dark:border-zinc-700 dark:bg-zinc-950" />
          </label>
          <Button leftIcon={<KeyRound size={15} />} loading={gerar.isPending} onClick={() => gerar.mutate()}>
            {temSegredo ? 'Gerar segredo novo' : 'Gerar segredo'}
          </Button>
        </div>
      ) : (
        <div className="mt-3 space-y-2 rounded-lg bg-amber-50 p-3 dark:bg-amber-950/30">
          <p className="font-medium text-amber-900 dark:text-amber-200">Copia agora — não volta a aparecer.</p>
          <div className="flex gap-2">
            <input readOnly value={gerado.segredo} onFocus={(e) => e.target.select()} aria-label="Segredo"
              className="min-w-0 flex-1 rounded border border-zinc-300 bg-white px-2 py-1.5 font-mono text-xs dark:border-zinc-700 dark:bg-zinc-900" />
            <Button size="sm" variant="secondary" leftIcon={<Copy size={13} />} onClick={() => copiar(gerado.segredo)}>Copiar</Button>
          </div>
          <ol className="list-decimal space-y-0.5 pl-5 text-xs text-zinc-700 dark:text-zinc-300">
            <li>Cloudflare → Workers &amp; Pages → <strong>mender-faturas</strong> → Settings → Variables and Secrets.</li>
            <li>Em <code>MENDER_SECRET</code> (tipo <em>Secret</em>) cola este valor e carrega em Deploy.</li>
            <li>Pronto: o que chegar a {gerado.endereco} entra no Mender «por aprovar».</li>
          </ol>
        </div>
      )}
    </div>
  );
}
