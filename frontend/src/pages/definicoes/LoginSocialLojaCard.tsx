import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, UserRound } from 'lucide-react';
import { Button, SectionCard } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { api } from '../../lib/api';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';

interface LoginSocialConfig { googleClientId: string | null; facebookPronto: boolean; facebookAppId: string | null }

const loginSocialApi = {
  get: () => api.get<LoginSocialConfig>('/loja/produtos/login-social').then((r) => r.data),
  guardar: (googleClientId: string | null) => api.put<LoginSocialConfig>('/loja/produtos/login-social', { googleClientId }).then((r) => r.data),
};

/**
 * 23/11: contas de cliente na loja — «Continuar com Google» (Client ID da app OAuth do Google) e «Continuar com
 * Facebook» (usa a app Meta de Definições → Redes sociais). Email e palavra-passe funcionam sempre.
 */
export function LoginSocialLojaCard() {
  const qc = useQueryClient();
  const cfg = useQuery({ queryKey: ['loja', 'login-social'], queryFn: loginSocialApi.get });
  const [google, setGoogle] = useState('');
  useEffect(() => { if (cfg.data) setGoogle(cfg.data.googleClientId ?? ''); }, [cfg.data]);
  const guardar = useMutation({
    mutationFn: () => loginSocialApi.guardar(google.trim() || null),
    onSuccess: (d) => { qc.setQueryData(['loja', 'login-social'], d); toast.success('Guardado — o botão aparece na loja dentro de 1 a 2 minutos.'); },
    onError: (e) => toast.error(apiErrorMessage(e)),
  });

  return (
    <SectionCard title={<span className="flex items-center gap-2"><UserRound size={16} /> Contas de cliente (iniciar sessão na loja)</span>}>
      <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-300">Email e palavra-passe funcionam sempre. Aqui ligas os botões «Continuar com Google» e «Continuar com Facebook».</p>
      <label className={labelCls}>
        Google — Client ID
        <input className={inputCls} value={google} onChange={(e) => setGoogle(e.target.value)} placeholder="000000000000-xxxx.apps.googleusercontent.com" spellCheck={false} />
      </label>
      <p className="mt-1 text-xs text-zinc-500">Google Cloud → APIs e serviços → Credenciais → ID de cliente OAuth (aplicação Web), com a origem https://lopestech.pt.</p>
      <p className="mt-3 flex items-center gap-2 text-sm">
        {cfg.data?.facebookPronto
          ? <><CheckCircle2 size={16} className="text-emerald-600" /> Facebook: usa a app Meta {cfg.data.facebookAppId} (Definições → Redes sociais).</>
          : <>Facebook: falta a app Meta em Definições → Redes sociais.</>}
      </p>
      <div className="mt-3"><Button onClick={() => guardar.mutate()} loading={guardar.isPending}>Guardar</Button></div>
    </SectionCard>
  );
}
