export const EQUIPMENT_FIELD_TYPE = {
  Text: 0,
  Number: 1,
  Select: 2,
  Boolean: 3,
} as const;

export type EquipmentFieldType = (typeof EQUIPMENT_FIELD_TYPE)[keyof typeof EQUIPMENT_FIELD_TYPE];

export const EQUIPMENT_FIELD_TYPE_LABEL: Record<EquipmentFieldType, string> = {
  0: 'Texto',
  1: 'Número',
  2: 'Select',
  3: 'Sim/Não',
};

export const DEVICE_CATEGORY_LABEL: Record<number, string> = {
  0: 'Telemóvel',
  1: 'Tablet',
  2: 'Laptop',
  3: 'Desktop',
  4: 'Smartwatch',
  5: 'Consola',
  99: 'Outro',
};

export interface EquipmentFieldDefinition {
  id: string;
  label: string;
  type: EquipmentFieldType;
  options: string[];
  required: boolean;
  ordem: number;
  visibleInPortal: boolean;
}

export interface EquipmentFieldTemplate {
  id: string;
  nome: string;
  categoria: number;
  isActive: boolean;
  ordem: number;
  fields: EquipmentFieldDefinition[];
}

export interface UpsertEquipmentFieldDefinition {
  id?: string | null;
  label: string;
  type: EquipmentFieldType;
  options: string[];
  required: boolean;
  ordem: number;
  visibleInPortal: boolean;
}

export interface UpsertEquipmentFieldTemplate {
  nome: string;
  categoria: number;
  isActive: boolean;
  fields: UpsertEquipmentFieldDefinition[];
}

export interface SetEquipmentFieldValue {
  fieldDefinitionId: string;
  value: string | null;
}

export interface EquipmentFieldValue {
  fieldDefinitionId: string;
  label: string;
  type: EquipmentFieldType;
  value: string | null;
  required: boolean;
  visibleInPortal: boolean;
  ordem: number;
}
