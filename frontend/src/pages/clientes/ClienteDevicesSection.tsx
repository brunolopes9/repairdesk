import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Smartphone, Plus, Edit2, Archive, ArchiveRestore, Trash2, Shield, ShieldOff } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button } from '../../components/ui/Button';
import { toast } from '../../lib/toast';
import { apiErrorMessage } from '../../lib/errors';
import { devicesApi, type Device, type CreateDeviceForm, type UpdateDeviceForm } from '../../lib/devices/api';

/**
 * Sprint 462 (Doc 90 Tier 2 #6 — UI do asset registry): gestão de equipamentos
 * persistentes do cliente na ficha. Coexiste com "Equipamentos" derived (que continua
 * a mostrar telemóveis vistos em reparações/vendas), mas esta secção dá ao staff um
 * registo deliberado: "este iPhone é do João, comprou em 2023, fabricante até 2025."
 *
 * Pattern espelhado de ReparacaoComunicacoesSection (S452): query staleTime 30s,
 * mutate invalida, modal inline para criar/editar.
 */
export function ClienteDevicesSection({ clienteId }: { clienteId: string }) {
  const qc = useQueryClient();
  const [showArchived, setShowArchived] = useState(false);
  const [editing, setEditing] = useState<Device | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<Device | null>(null);

  const list = useQuery({
    queryKey: ['cliente-devices', clienteId, showArchived],
    queryFn: () => devicesApi.listByCliente(clienteId, showArchived),
    staleTime: 30_000,
  });

  const remove = useMutation({
    mutationFn: (id: string) => devicesApi.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cliente-devices', clienteId] });
      setConfirmDelete(null);
      toast.success('Equipamento apagado.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao apagar.'),
  });

  const items = list.data ?? [];
  const ativos = items.filter((d) => !d.arquivado);
  const arquivados = items.filter((d) => d.arquivado);

  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="flex items-center gap-2 text-sm font-semibold">
            <Smartphone size={16} strokeWidth={2} className="text-brand-600" />
            Equipamentos registados <span className="text-zinc-500">— {ativos.length}{arquivados.length > 0 ? ` + ${arquivados.length} arquivados` : ''}</span>
          </h2>
          <p className="mt-1 text-xs text-zinc-500">
            Equipamentos persistentes do cliente. Permite guardar Apelido, IMEI, garantia do fabricante e histórico — independente de reparações.
          </p>
        </div>
        <div className="mt-2 flex items-center gap-2 sm:mt-0">
          {arquivados.length > 0 && (
            <label className="flex items-center gap-1.5 text-[11px] text-zinc-500">
              <input
                type="checkbox"
                checked={showArchived}
                onChange={(e) => setShowArchived(e.target.checked)}
              />
              Ver arquivados
            </label>
          )}
          <Button size="sm" leftIcon={<Plus size={14} />} onClick={() => setCreateOpen(true)}>
            Adicionar
          </Button>
        </div>
      </div>

      {items.length === 0 && !list.isLoading && (
        <div className="mt-3 rounded-lg border border-dashed border-zinc-200 px-3 py-6 text-center text-xs text-zinc-500 dark:border-zinc-800">
          Sem equipamentos registados. Adiciona o iPhone/portátil/etc do cliente para guardares IMEI, garantia do fabricante e histórico em um só sítio.
        </div>
      )}

      {items.length > 0 && (
        <div className="mt-3 grid grid-cols-1 gap-3 xl:grid-cols-2">
          {items.map((d) => (
            <DeviceCard
              key={d.id}
              device={d}
              onEdit={() => setEditing(d)}
              onDelete={() => setConfirmDelete(d)}
            />
          ))}
        </div>
      )}

      <DeviceFormModal
        open={createOpen}
        clienteId={clienteId}
        device={null}
        onClose={() => setCreateOpen(false)}
        onSaved={() => {
          qc.invalidateQueries({ queryKey: ['cliente-devices', clienteId] });
          setCreateOpen(false);
        }}
      />
      <DeviceFormModal
        open={editing !== null}
        clienteId={clienteId}
        device={editing}
        onClose={() => setEditing(null)}
        onSaved={() => {
          qc.invalidateQueries({ queryKey: ['cliente-devices', clienteId] });
          setEditing(null);
        }}
      />

      <Modal
        open={confirmDelete !== null}
        title="Apagar equipamento"
        onClose={() => setConfirmDelete(null)}
        footer={<>
          <button type="button" onClick={() => setConfirmDelete(null)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button
            type="button"
            disabled={remove.isPending}
            onClick={() => confirmDelete && remove.mutate(confirmDelete.id)}
            className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
          >
            {remove.isPending ? 'A apagar…' : 'Apagar definitivo'}
          </button>
        </>}
      >
        {confirmDelete && (
          <div className="space-y-2 text-sm">
            <p>Apagar definitivamente <strong>{deviceLabel(confirmDelete)}</strong>?</p>
            <p className="text-xs text-amber-700 dark:text-amber-300">
              Para preservar histórico, considera <strong>arquivar</strong> em vez de apagar — editar e marcar como arquivado.
            </p>
          </div>
        )}
      </Modal>
    </section>
  );
}

function DeviceCard({ device, onEdit, onDelete }: { device: Device; onEdit: () => void; onDelete: () => void }) {
  const garantiaActiva = device.garantiaFabricanteUntil
    ? new Date(device.garantiaFabricanteUntil) >= new Date()
    : false;

  return (
    <div className={`rounded-lg border p-3 text-sm ${device.arquivado ? 'border-zinc-200 bg-zinc-50 opacity-70 dark:border-zinc-800 dark:bg-zinc-950/40' : 'border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900'}`}>
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-1.5">
            <span className="font-medium">{deviceLabel(device)}</span>
            {device.arquivado && <span className="rounded bg-zinc-200 px-1.5 py-0.5 text-[10px] text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">Arquivado</span>}
          </div>
          {device.apelido && <div className="text-[11px] text-zinc-500">"{device.apelido}"</div>}
        </div>
        <div className="flex flex-none items-center gap-1">
          <button type="button" onClick={onEdit} className="text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200" title="Editar">
            <Edit2 size={13} />
          </button>
          <button type="button" onClick={onDelete} className="text-zinc-400 hover:text-rose-600 dark:hover:text-rose-400" title="Apagar">
            <Trash2 size={13} />
          </button>
        </div>
      </div>

      <dl className="mt-2 grid grid-cols-2 gap-x-3 gap-y-1 text-[11px]">
        {device.imei && (
          <>
            <dt className="text-zinc-500">IMEI</dt>
            <dd className="font-mono">{device.imei}</dd>
          </>
        )}
        {device.serial && (
          <>
            <dt className="text-zinc-500">Serial</dt>
            <dd className="font-mono">{device.serial}</dd>
          </>
        )}
        {device.cor && (
          <>
            <dt className="text-zinc-500">Cor</dt>
            <dd>{device.cor}</dd>
          </>
        )}
        {device.dataAquisicao && (
          <>
            <dt className="text-zinc-500">Adquirido</dt>
            <dd>{new Date(device.dataAquisicao).toLocaleDateString('pt-PT')}</dd>
          </>
        )}
        {device.garantiaFabricanteUntil && (
          <>
            <dt className="text-zinc-500">Garantia fabricante</dt>
            <dd className={garantiaActiva ? 'text-emerald-700 dark:text-emerald-300' : 'text-zinc-500'}>
              {garantiaActiva ? <Shield size={10} className="mr-0.5 inline" /> : <ShieldOff size={10} className="mr-0.5 inline" />}
              até {new Date(device.garantiaFabricanteUntil).toLocaleDateString('pt-PT')}
            </dd>
          </>
        )}
      </dl>
      {device.notas && (
        <p className="mt-2 whitespace-pre-wrap rounded bg-zinc-50 px-2 py-1.5 text-[11px] text-zinc-600 dark:bg-zinc-950 dark:text-zinc-400">
          {device.notas}
        </p>
      )}
    </div>
  );
}

function deviceLabel(d: Device): string {
  const parts = [d.tipo];
  if (d.marca) parts.push(d.marca);
  if (d.modelo) parts.push(d.modelo);
  return parts.join(' ');
}

function DeviceFormModal({
  open,
  clienteId,
  device,
  onClose,
  onSaved,
}: {
  open: boolean;
  clienteId: string;
  device: Device | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const isEdit = device !== null;
  const [form, setForm] = useState<CreateDeviceForm | UpdateDeviceForm>(() =>
    device
      ? {
          tipo: device.tipo,
          marca: device.marca,
          modelo: device.modelo,
          apelido: device.apelido,
          imei: device.imei,
          serial: device.serial,
          cor: device.cor,
          dataAquisicao: device.dataAquisicao,
          garantiaFabricanteUntil: device.garantiaFabricanteUntil,
          notas: device.notas,
          arquivado: device.arquivado,
        }
      : {
          clienteId,
          tipo: 'Telemóvel',
          marca: null,
          modelo: null,
          apelido: null,
          imei: null,
          serial: null,
          cor: null,
          dataAquisicao: null,
          garantiaFabricanteUntil: null,
          notas: null,
        },
  );

  const save = useMutation({
    mutationFn: () => {
      if (isEdit && device) {
        return devicesApi.update(device.id, form as UpdateDeviceForm);
      }
      return devicesApi.create({ ...(form as CreateDeviceForm), clienteId });
    },
    onSuccess: () => {
      toast.success(isEdit ? 'Equipamento atualizado.' : 'Equipamento adicionado.');
      onSaved();
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro a guardar.'),
  });

  const input = 'w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950';

  return (
    <Modal
      open={open}
      title={isEdit ? 'Editar equipamento' : 'Adicionar equipamento'}
      onClose={onClose}
      footer={<>
        <button type="button" onClick={onClose} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
        <Button onClick={() => save.mutate()} loading={save.isPending} disabled={!form.tipo || form.tipo.trim().length < 2}>
          {isEdit ? 'Guardar' : 'Adicionar'}
        </Button>
      </>}
    >
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label="Tipo *">
          <select
            value={form.tipo}
            onChange={(e) => setForm({ ...form, tipo: e.target.value })}
            className={input}
          >
            <option>Telemóvel</option>
            <option>Tablet</option>
            <option>Portátil</option>
            <option>Smartwatch</option>
            <option>Consola</option>
            <option>Outro</option>
          </select>
        </Field>
        <Field label="Apelido (opcional)">
          <input
            value={form.apelido ?? ''}
            onChange={(e) => setForm({ ...form, apelido: e.target.value || null })}
            placeholder={'iPhone do João'}
            className={input}
          />
        </Field>
        <Field label="Marca">
          <input value={form.marca ?? ''} onChange={(e) => setForm({ ...form, marca: e.target.value || null })} placeholder="Apple, Samsung…" className={input} />
        </Field>
        <Field label="Modelo">
          <input value={form.modelo ?? ''} onChange={(e) => setForm({ ...form, modelo: e.target.value || null })} placeholder="iPhone 13, Galaxy S22…" className={input} />
        </Field>
        <Field label="IMEI">
          <input value={form.imei ?? ''} onChange={(e) => setForm({ ...form, imei: e.target.value || null })} placeholder="só dígitos" className={input + ' font-mono'} />
        </Field>
        <Field label="Serial">
          <input value={form.serial ?? ''} onChange={(e) => setForm({ ...form, serial: e.target.value || null })} className={input + ' font-mono'} />
        </Field>
        <Field label="Cor">
          <input value={form.cor ?? ''} onChange={(e) => setForm({ ...form, cor: e.target.value || null })} className={input} />
        </Field>
        <Field label="Data de aquisição">
          <input type="date" value={form.dataAquisicao ?? ''} onChange={(e) => setForm({ ...form, dataAquisicao: e.target.value || null })} className={input} />
        </Field>
        <Field label="Garantia fabricante até">
          <input type="date" value={form.garantiaFabricanteUntil ?? ''} onChange={(e) => setForm({ ...form, garantiaFabricanteUntil: e.target.value || null })} className={input} />
        </Field>
        <Field label="Notas internas" className="sm:col-span-2">
          <textarea
            rows={2}
            value={form.notas ?? ''}
            onChange={(e) => setForm({ ...form, notas: e.target.value || null })}
            className={input}
          />
        </Field>
        {isEdit && (
          <Field label="" className="sm:col-span-2">
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={(form as UpdateDeviceForm).arquivado}
                onChange={(e) => setForm({ ...form, arquivado: e.target.checked } as UpdateDeviceForm)}
              />
              <span className="inline-flex items-center gap-1">
                {(form as UpdateDeviceForm).arquivado ? <Archive size={13} /> : <ArchiveRestore size={13} />}
                Equipamento arquivado (cliente já não tem)
              </span>
            </label>
          </Field>
        )}
      </div>
    </Modal>
  );
}

function Field({ label, children, className }: { label: string; children: React.ReactNode; className?: string }) {
  return (
    <label className={`flex flex-col gap-1 text-xs ${className ?? ''}`}>
      {label && <span className="text-zinc-600 dark:text-zinc-400">{label}</span>}
      {children}
    </label>
  );
}
