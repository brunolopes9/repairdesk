import { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Sparkles, Upload, FileText, Check, AlertTriangle } from 'lucide-react';
import Modal from './Modal';
import { Button } from './ui';
import { toast } from '../lib/toast';
import { productsApi, type CsvColumnMapping } from '../lib/products/api';
import { fornecedoresApi } from '../lib/fornecedores/api';

/**
 * Sprint 203: modal universal de import CSV. Funciona com qualquer fornecedor:
 * 1. Bruno escolhe fornecedor + faz upload CSV
 * 2. Frontend chama detectCsvColumns → Claude sugere mapping (~0.05¢)
 * 3. Bruno vê preview + pode editar manualmente
 * 4. Confirma → import + guarda mapping no Fornecedor (próximo upload = direct)
 */
export default function UniversalCsvImportModal({ open, onClose, onImported }: { open: boolean; onClose: () => void; onImported: () => void }) {
  const [fornecedorId, setFornecedorId] = useState<string>('');
  const [csv, setCsv] = useState<string>('');
  const [filename, setFilename] = useState<string>('');
  const [mapping, setMapping] = useState<CsvColumnMapping | null>(null);
  const [header, setHeader] = useState<string[]>([]);
  const [confidence, setConfidence] = useState<string>('');
  const [notes, setNotes] = useState<string>('');
  const [step, setStep] = useState<'upload' | 'review' | 'done'>('upload');

  const fornecedores = useQuery({
    queryKey: ['fornecedores-active'],
    queryFn: () => fornecedoresApi.list(false),
    enabled: open,
  });

  useEffect(() => {
    if (!open) {
      setCsv(''); setFilename(''); setMapping(null); setHeader([]); setStep('upload');
      setConfidence(''); setNotes('');
    }
  }, [open]);

  const detect = useMutation({
    // Sprint 209: passa fornecedorId → se Fornecedor tem mapping cached, backend devolve
    // direct sem chamar Claude (poupa ~0.05¢ + 2-3s).
    mutationFn: () => productsApi.detectCsvColumns(csv, fornecedorId || undefined),
    onSuccess: (r) => {
      setHeader(r.header);
      if (r.detected && r.mapping) {
        const { confidence: c, notes: n, ...m } = r.mapping;
        setMapping(m);
        setConfidence(c);
        setNotes(n);
        // Sprint 209: source='cache' = mapping já guardado neste fornecedor — toast informativo.
        if (r.source === 'cache') {
          toast.success('Mapping recuperado do fornecedor', 'Skip Claude — revê e importa.');
        }
      } else {
        // Sem Claude — mapping vazio, Bruno preenche tudo manual
        setMapping({ sku: null, brand: null, model: null, product: null, storage: null,
                     color: null, grading: null, price: null, stock: null, cost: null, images: null });
        setConfidence('manual');
        setNotes(r.reason ?? 'Preenche o mapping manualmente.');
      }
      setStep('review');
    },
    onError: (e) => toast.fromError(e, 'Falhou detectar colunas'),
  });

  const importCsv = useMutation({
    mutationFn: () => productsApi.importCsvWithMapping(fornecedorId, csv, mapping!, true),
    onSuccess: (r) => {
      toast.success(`Import OK: ${r.created} criados, ${r.updated} actualizados`,
        r.errors?.length ? `${r.errors.length} erros — vê detalhe.` : 'Mapping guardado para próximos uploads.');
      onImported();
      onClose();
    },
    onError: (e) => toast.fromError(e, 'Falhou import CSV'),
  });

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setFilename(file.name);
    const reader = new FileReader();
    reader.onload = () => setCsv(typeof reader.result === 'string' ? reader.result : '');
    reader.readAsText(file);
  }

  const canProceed = !!fornecedorId && !!csv && csv.length > 50;
  const canImport = mapping && mapping.sku && mapping.price && (mapping.product || (mapping.brand && mapping.model));

  return (
    <Modal open={open} onClose={onClose} title="Importar CSV (universal)">
      <div className="space-y-4">
        {step === 'upload' && (
          <>
            <p className="text-xs text-zinc-600 dark:text-zinc-400">
              Faz upload de qualquer CSV de fornecedor. Claude analisa o cabeçalho e sugere o mapping
              automaticamente. Confirmas 1× e nos próximos uploads é instantâneo.
            </p>
            <div>
              <label className="mb-1 block text-xs font-medium">Fornecedor</label>
              <select value={fornecedorId} onChange={(e) => setFornecedorId(e.target.value)}
                className="w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-900">
                <option value="">— escolhe —</option>
                {fornecedores.data?.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
              </select>
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium">Ficheiro CSV</label>
              <label className="flex cursor-pointer items-center justify-center gap-2 rounded-md border-2 border-dashed border-brand-300 bg-brand-50/30 p-4 text-sm text-brand-700 hover:bg-brand-50/60 dark:border-brand-800 dark:bg-brand-950/20 dark:text-brand-300">
                <Upload size={16} />
                <input type="file" accept=".csv,text/csv" className="sr-only" onChange={handleFileChange} />
                {filename || 'Clica para escolher CSV…'}
              </label>
              {csv && <p className="mt-1 text-[10px] text-zinc-500">{csv.length.toLocaleString()} chars · {csv.split('\n').length} linhas</p>}
            </div>
          </>
        )}

        {step === 'review' && mapping && (
          <>
            <div className="rounded-md border border-zinc-200 bg-zinc-50 p-3 text-xs dark:border-zinc-700 dark:bg-zinc-900">
              <div className="flex items-center gap-2 font-medium">
                {confidence === 'high' && <><Check size={14} className="text-green-600" /> Detecção alta confiança</>}
                {confidence === 'medium' && <><Sparkles size={14} className="text-amber-600" /> Detecção média (revê)</>}
                {(confidence === 'low' || confidence === 'manual') && <><AlertTriangle size={14} className="text-rose-600" /> Revisão necessária</>}
              </div>
              {notes && <p className="mt-1 text-zinc-600 dark:text-zinc-400">{notes}</p>}
            </div>

            <p className="text-[11px] text-zinc-500">
              <strong>Header detectado:</strong> {header.join(' · ')}
            </p>

            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              {([['sku', 'SKU *'], ['price', 'Preço *'],
                 ['brand', 'Marca'], ['model', 'Modelo'],
                 ['product', 'Produto (combinado)'], ['storage', 'Storage'],
                 ['color', 'Cor'], ['grading', 'Grade'],
                 ['stock', 'Stock'], ['cost', 'Custo'],
                 ['images', 'Imagens']] as const).map(([key, label]) => (
                <div key={key}>
                  <label className="block text-[11px] font-medium text-zinc-600 dark:text-zinc-400">{label}</label>
                  <select
                    value={mapping[key] ?? ''}
                    onChange={(e) => setMapping({ ...mapping, [key]: e.target.value || null })}
                    className="mt-0.5 w-full rounded border border-zinc-300 bg-white px-2 py-1 text-xs dark:border-zinc-700 dark:bg-zinc-900"
                  >
                    <option value="">— sem mapping —</option>
                    {header.map((h) => <option key={h} value={h}>{h}</option>)}
                  </select>
                </div>
              ))}
            </div>

            {!canImport && (
              <p className="text-[11px] text-rose-600">
                Mapping mínimo: SKU + Preço + (Marca + Modelo) OU Produto combinado.
              </p>
            )}
          </>
        )}

        <div className="flex justify-end gap-2 border-t border-zinc-100 pt-3 dark:border-zinc-800">
          <Button type="button" variant="ghost" onClick={onClose}>Cancelar</Button>
          {step === 'upload' && (
            <Button type="button" disabled={!canProceed} loading={detect.isPending}
              onClick={() => detect.mutate()} leftIcon={<Sparkles size={14} />}>
              {detect.isPending ? 'Claude a analisar…' : 'Analisar com IA (~0.05¢)'}
            </Button>
          )}
          {step === 'review' && (
            <Button type="button" disabled={!canImport} loading={importCsv.isPending}
              onClick={() => importCsv.mutate()} leftIcon={<FileText size={14} />}>
              {importCsv.isPending ? 'A importar…' : 'Importar + Guardar mapping'}
            </Button>
          )}
        </div>
      </div>
    </Modal>
  );
}
