import { api } from '../api';

export const DEVICE_CATEGORY = {
  Smartphone: 0,
  Tablet: 1,
  Laptop: 2,
  Desktop: 3,
  Smartwatch: 4,
  Consola: 5,
  Outro: 99,
} as const;
export type DeviceCategory = (typeof DEVICE_CATEGORY)[keyof typeof DEVICE_CATEGORY];

export const DEVICE_CATEGORY_LABEL: Record<DeviceCategory, string> = {
  0: 'Smartphone',
  1: 'Tablet',
  2: 'Computador portátil',
  3: 'Computador desktop',
  4: 'Smartwatch',
  5: 'Consola',
  99: 'Outro',
};

export const RESULTADO = {
  NaoTestado: 0,
  Ok: 1,
  Avaria: 2,
  Marginal: 3,
} as const;
export type Resultado = (typeof RESULTADO)[keyof typeof RESULTADO];

export const RESULTADO_LABEL: Record<Resultado, string> = {
  0: 'N/T',
  1: 'OK',
  2: 'Avaria',
  3: 'Marginal',
};

export interface DiagnosticoTemplate {
  id: string;
  nome: string;
  categoria: DeviceCategory;
  isDefault: boolean;
  activo: boolean;
  items: DiagnosticoTemplateItem[];
}
export interface DiagnosticoTemplateItem {
  id: string;
  label: string;
  descricao: string | null;
  grupo: string | null;
  ordem: number;
  peso: number;
}

export interface DiagnosticoExecucao {
  id: string;
  reparacaoId: string;
  templateId: string | null;
  templateNomeSnapshot: string | null;
  categoria: DeviceCategory;
  completadoEm: string | null;
  notasGerais: string | null;
  score: number | null;
  items: DiagnosticoExecucaoItem[];
}
export interface DiagnosticoExecucaoItem {
  id: string;
  label: string;
  descricao: string | null;
  grupo: string | null;
  ordem: number;
  peso: number;
  resultado: Resultado;
  notas: string | null;
}

export const diagnosticoApi = {
  listTemplates() {
    return api.get<DiagnosticoTemplate[]>('/diagnostico/templates').then((r) => r.data);
  },
  getByReparacao(reparacaoId: string) {
    return api
      .get<DiagnosticoExecucao | null>(`/diagnostico/reparacao/${reparacaoId}`)
      .then((r) => r.data)
      .catch((err) => {
        if (err?.response?.status === 404) return null;
        throw err;
      });
  },
  start(reparacaoId: string, opts: { templateId?: string | null; categoria?: DeviceCategory | null }) {
    return api
      .post<DiagnosticoExecucao>(`/diagnostico/reparacao/${reparacaoId}/start`, {
        templateId: opts.templateId ?? null,
        categoria: opts.categoria ?? null,
      })
      .then((r) => r.data);
  },
  update(
    reparacaoId: string,
    payload: {
      notasGerais: string | null;
      marcarCompletado: boolean;
      items: Array<{ itemId: string; resultado: Resultado; notas: string | null }>;
    },
  ) {
    return api.put<DiagnosticoExecucao>(`/diagnostico/reparacao/${reparacaoId}`, payload).then((r) => r.data);
  },
  remove(reparacaoId: string) {
    return api.delete(`/diagnostico/reparacao/${reparacaoId}`).then(() => undefined);
  },
};
