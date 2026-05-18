import { api } from '../api';
import type {
  EquipmentFieldTemplate,
  EquipmentFieldValue,
  SetEquipmentFieldValue,
  UpsertEquipmentFieldDefinition,
  UpsertEquipmentFieldTemplate,
} from './types';

function normalizeTemplate(t: UpsertEquipmentFieldTemplate): UpsertEquipmentFieldTemplate {
  return {
    ...t,
    nome: t.nome.trim(),
    fields: t.fields.map((f, index) => ({
      ...f,
      label: f.label.trim(),
      ordem: index,
      options: f.options.map((o) => o.trim()).filter(Boolean),
    })),
  };
}

export const equipmentFieldTemplatesApi = {
  list(includeInactive = true) {
    return api
      .get<EquipmentFieldTemplate[]>('/equipment-field-templates', { params: { includeInactive } })
      .then((r) => r.data);
  },
  active() {
    return api.get<EquipmentFieldTemplate[]>('/equipment-field-templates/active').then((r) => r.data);
  },
  create(payload: UpsertEquipmentFieldTemplate) {
    return api.post<EquipmentFieldTemplate>('/equipment-field-templates', normalizeTemplate(payload)).then((r) => r.data);
  },
  update(id: string, payload: UpsertEquipmentFieldTemplate) {
    return api.put<EquipmentFieldTemplate>(`/equipment-field-templates/${id}`, normalizeTemplate(payload)).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/equipment-field-templates/${id}`).then(() => undefined);
  },
  reorder(ids: string[]) {
    return api.patch('/equipment-field-templates/order', { ids }).then(() => undefined);
  },
};

export const equipmentFieldValuesApi = {
  set(reparacaoId: string, templateId: string | null, values: SetEquipmentFieldValue[]) {
    return api
      .post<EquipmentFieldValue[]>(`/reparacoes/${reparacaoId}/fields`, { templateId, values })
      .then((r) => r.data);
  },
};

export function toUpsert(template: EquipmentFieldTemplate): UpsertEquipmentFieldTemplate {
  return {
    nome: template.nome,
    categoria: template.categoria,
    isActive: template.isActive,
    fields: template.fields.map((f): UpsertEquipmentFieldDefinition => ({
      id: f.id,
      label: f.label,
      type: f.type,
      options: f.options,
      required: f.required,
      ordem: f.ordem,
      visibleInPortal: f.visibleInPortal,
    })),
  };
}
