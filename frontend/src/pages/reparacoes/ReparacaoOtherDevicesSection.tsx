import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Smartphone, ArrowRight, Shield, Plus } from 'lucide-react';
import { devicesApi } from '../../lib/devices/api';
import { toast } from '../../lib/toast';
import { apiErrorMessage } from '../../lib/errors';

/**
 * Sprint 466: outros Devices do cliente no contexto da reparação atual.
 *
 * Use case: cliente trouxe iPhone para reparar; Bruno vê na lateral que esse
 * cliente também tem iPad e Galaxy Watch registados. Permite oferecer serviços
 * preventivos ou cross-sell quando há contacto presencial.
 *
 * Filtro: exclui o Device que matches o IMEI da reparação (se a reparação tem
 * IMEI conhecido) — evita mostrar o equipamento que está a ser reparado nesta
 * mesma view. Arquivados não aparecem.
 *
 * Não aparece quando não há outros Devices.
 */
export function ReparacaoOtherDevicesSection({
  clienteId,
  reparacaoImei,
  reparacaoEquipamento,
}: {
  clienteId: string;
  reparacaoImei?: string | null;
  /** Sprint 473: usado para sugerir Marca/Modelo ao registar Device a partir desta reparação. */
  reparacaoEquipamento?: string;
}) {
  const qc = useQueryClient();
  const list = useQuery({
    queryKey: ['cliente-devices-na-reparacao', clienteId],
    queryFn: () => devicesApi.listByCliente(clienteId, false),
    staleTime: 60_000,
  });

  const items = list.data ?? [];
  const imeiNorm = reparacaoImei?.replace(/\D/g, '') ?? '';
  const outros = items.filter((d) => !imeiNorm || d.imei !== imeiNorm);

  // Sprint 473: detectar se o IMEI da reparação NÃO tem Device registado.
  // Permite ao staff registar com um click — útil quando cliente trouxe equipamento
  // novo e Bruno quer guardar para futuras reparações ligarem automaticamente.
  const imeiNaoRegistado = imeiNorm.length >= 8 && !items.some((d) => d.imei === imeiNorm);

  const registar = useMutation({
    mutationFn: () => devicesApi.create({
      clienteId,
      tipo: 'Telemóvel',
      // Heuristica simples: primeira palavra de equipamento = marca, resto = modelo.
      // Ex: "iPhone 13 Pro" → marca="iPhone", modelo="13 Pro". Bruno pode editar depois.
      marca: reparacaoEquipamento?.split(' ')[0] || null,
      modelo: reparacaoEquipamento?.split(' ').slice(1).join(' ') || null,
      imei: imeiNorm || null,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cliente-devices-na-reparacao', clienteId] });
      qc.invalidateQueries({ queryKey: ['cliente-devices', clienteId] });
      qc.invalidateQueries({ queryKey: ['device-por-imei', imeiNorm] });
      toast.success('Equipamento registado.', 'Próximas reparações com este IMEI vão ligar automaticamente.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro a registar equipamento.'),
  });

  // Esconde-se quando não há outros Devices E não há sugestão de registo pendente.
  if (list.isLoading) return null;
  if (outros.length === 0 && !imeiNaoRegistado) return null;

  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold">
          <Smartphone size={15} className="text-brand-600" />
          Outros equipamentos do cliente <span className="text-zinc-500">— {outros.length}</span>
        </h2>
        <Link
          to={`/clientes/${clienteId}`}
          className="inline-flex items-center gap-1 text-[11px] text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300"
        >
          ver ficha <ArrowRight size={11} />
        </Link>
      </div>
      <p className="mt-1 text-xs text-zinc-500">
        Equipamentos registados do cliente (asset registry). Podes oferecer manutenção preventiva ou referir cross-sell.
      </p>
      {/* Sprint 473: CTA "Registar este equipamento" quando IMEI da reparação é desconhecido. */}
      {imeiNaoRegistado && (
        <div className="mt-2 flex items-start justify-between gap-2 rounded-lg border border-sky-200 bg-sky-50/70 px-3 py-2 text-xs dark:border-sky-900/40 dark:bg-sky-950/20">
          <span className="text-sky-900 dark:text-sky-200">
            O IMEI <span className="font-mono">{imeiNorm}</span> ainda não está registado como equipamento.
            Registar agora? Próximas reparações com este IMEI vão ligar automaticamente.
          </span>
          <button
            type="button"
            disabled={registar.isPending}
            onClick={() => registar.mutate()}
            className="inline-flex flex-none items-center gap-1 rounded-md bg-sky-600 px-2 py-1 text-[11px] font-medium text-white hover:bg-sky-700 disabled:opacity-60"
          >
            <Plus size={11} /> {registar.isPending ? 'A registar…' : 'Registar'}
          </button>
        </div>
      )}
      <ul className="mt-2 space-y-1.5">
        {outros.slice(0, 6).map((d) => {
          const label = d.apelido || [d.marca, d.modelo].filter(Boolean).join(' ') || d.tipo;
          const garantiaActiva = d.garantiaFabricanteUntil ? new Date(d.garantiaFabricanteUntil) >= new Date() : false;
          return (
            <li key={d.id} className="flex items-center justify-between gap-2 rounded-md border border-zinc-100 px-2.5 py-1.5 text-xs dark:border-zinc-800">
              <div className="min-w-0 flex-1">
                <div className="truncate">
                  <span className="font-medium">{label}</span>
                  {d.cor && <span className="ml-1 text-zinc-500">· {d.cor}</span>}
                </div>
                {d.imei && <div className="font-mono text-[10px] text-zinc-500">IMEI {d.imei}</div>}
              </div>
              {garantiaActiva && (
                <span title={`Garantia fabricante até ${new Date(d.garantiaFabricanteUntil!).toLocaleDateString('pt-PT')}`} className="inline-flex items-center gap-0.5 rounded bg-emerald-100 px-1 py-0.5 text-[9px] text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300">
                  <Shield size={9} /> Em garantia
                </span>
              )}
            </li>
          );
        })}
      </ul>
      {outros.length > 6 && (
        <p className="mt-2 text-[11px] text-zinc-500">+ {outros.length - 6} outros — ver ficha do cliente</p>
      )}
    </section>
  );
}
