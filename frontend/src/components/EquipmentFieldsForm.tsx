import { EQUIPMENT_FIELD_TYPE, type EquipmentFieldTemplate, type SetEquipmentFieldValue } from '../lib/equipmentFields/types';

export type EquipmentFieldValuesMap = Record<string, string>;

interface EquipmentFieldsFormProps {
  template: EquipmentFieldTemplate | null;
  values: EquipmentFieldValuesMap;
  onChange: (fieldDefinitionId: string, value: string) => void;
  disabled?: boolean;
}

const inputCls =
  'w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 disabled:bg-zinc-100 disabled:text-zinc-500 dark:border-zinc-700 dark:bg-zinc-950 dark:disabled:bg-zinc-900';

export function initEquipmentFieldValues(
  template: EquipmentFieldTemplate,
  current: EquipmentFieldValuesMap = {},
): EquipmentFieldValuesMap {
  return Object.fromEntries(template.fields.map((field) => [field.id, current[field.id] ?? '']));
}

export function buildEquipmentFieldValues(
  template: EquipmentFieldTemplate,
  values: EquipmentFieldValuesMap,
): SetEquipmentFieldValue[] {
  return template.fields.map((field) => ({
    fieldDefinitionId: field.id,
    value: values[field.id]?.trim() || null,
  }));
}

export function missingRequiredEquipmentFields(
  template: EquipmentFieldTemplate | null,
  values: EquipmentFieldValuesMap,
): boolean {
  return template?.fields.some((field) => field.required && !values[field.id]?.trim()) ?? false;
}

export default function EquipmentFieldsForm({ template, values, onChange, disabled = false }: EquipmentFieldsFormProps) {
  if (!template) return null;

  if (template.fields.length === 0) {
    return (
      <p className="rounded-lg border border-dashed border-zinc-300 px-3 py-2 text-xs text-zinc-500 dark:border-zinc-700">
        Este template ainda nao tem campos configurados.
      </p>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
      {template.fields.map((field) => {
        const value = values[field.id] ?? '';
        return (
          <div key={field.id} className="space-y-1">
            <label className="text-xs font-medium uppercase tracking-wide text-zinc-500">
              {field.label} {field.required && <span className="text-red-500">*</span>}
            </label>

            {field.type === EQUIPMENT_FIELD_TYPE.Select ? (
              <select
                disabled={disabled}
                value={value}
                onChange={(e) => onChange(field.id, e.target.value)}
                className={inputCls}
              >
                <option value="">Selecionar...</option>
                {field.options.map((option) => (
                  <option key={option} value={option}>{option}</option>
                ))}
              </select>
            ) : field.type === EQUIPMENT_FIELD_TYPE.Boolean ? (
              <label className={`flex items-center gap-2 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950 ${disabled ? 'opacity-60' : 'cursor-pointer'}`}>
                <input
                  disabled={disabled}
                  type="checkbox"
                  checked={value === 'true'}
                  onChange={(e) => onChange(field.id, e.target.checked ? 'true' : 'false')}
                  className="h-4 w-4 rounded border-zinc-300 text-brand-600 focus:ring-brand-500"
                />
                <span>{value === 'true' ? 'Sim' : 'Nao'}</span>
              </label>
            ) : (
              <input
                disabled={disabled}
                type={field.type === EQUIPMENT_FIELD_TYPE.Number ? 'number' : 'text'}
                value={value}
                onChange={(e) => onChange(field.id, e.target.value)}
                className={inputCls}
              />
            )}
          </div>
        );
      })}
    </div>
  );
}
