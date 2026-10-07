// Templates WhatsApp por estado da reparação (Venda do tipo Reparação — Doc 94 Fase 4c).
// Conteúdo derivado de Contexto/11-WhatsApp-Templates.md.
// Modo "padrão" PT-PT (tratamento por tu) — variantes informal/profissional adiados.

import { VENDA_ESTADO, type VendaEstado } from '../vendas/types';
import type { TenantPreferencesRoot } from '../tenantPreferences/types';

export interface WhatsAppVars {
  cliente_nome: string;
  equipamento: string;
  loja_nome?: string;
  horario_loja?: string;
  numero_reparacao?: number | string;
  valor?: string;
  link_aprovacao?: string;
  link_review_google?: string;
  peca_nome?: string;
  prazo_estimado?: string;
  data_pronto?: string;
}

/** Chaves iguais aos estados da Venda no backend (TenantPreferencesDefaults). */
export type TemplateKey =
  | 'Orcamento'
  | 'AEsperaPeca'
  | 'EmCurso'
  | 'Pronta'
  | 'Entregue'
  | 'Cancelada'
  | 'LembreteLevantamento'
  | 'PedidoReview'
  | 'PrazoDerrapou';

export interface TemplateMeta {
  key: TemplateKey;
  label: string;
  /** Curta descrição do contexto adequado. */
  hint: string;
  /** Constrói a mensagem com vars substituídas. */
  build: (v: WhatsAppVars) => string;
}

type TemplateOptions = {
  staleDays?: number;
  staleThreshold?: number;
  preferences?: TenantPreferencesRoot | null;
};

function prazoFallback(v: WhatsAppVars): string {
  return v.prazo_estimado ?? 'os próximos dias';
}

function pecaFallback(v: WhatsAppVars): string {
  return v.peca_nome ?? 'a peça encomendada';
}

export const TEMPLATES: Record<TemplateKey, TemplateMeta> = {


  Orcamento: {
    key: 'Orcamento',
    label: 'Enviar orçamento',
    hint: 'Tem orçamento aprovável.',
    build: (v) => {
      const valor = v.valor ?? '[valor]';
      const link = v.link_aprovacao ? ` ou usa ${v.link_aprovacao} para avançarmos` : '';
      return `Olá ${v.cliente_nome}, já temos o orçamento para o teu ${v.equipamento}: ${valor}. Se estiver tudo bem para ti, responde a esta mensagem com "Aprovo"${link}.`;
    },
  },

  AEsperaPeca: {
    key: 'AEsperaPeca',
    label: 'Aguarda peça',
    hint: 'Encomendámos a peça, aguarda chegada.',
    build: (v) =>
      `Olá ${v.cliente_nome}, a reparação do teu ${v.equipamento} está a aguardar a chegada de ${pecaFallback(v)}. A previsão atual é ${prazoFallback(v)}; avisamos-te assim que chegar.`,
  },

  EmCurso: {
    key: 'EmCurso',
    label: 'Em reparação',
    hint: 'Estamos a trabalhar nele agora.',
    build: (v) =>
      `Olá ${v.cliente_nome}, começámos a reparação do teu ${v.equipamento}. Se tudo correr dentro do previsto, voltamos a falar contigo até ${prazoFallback(v)}.`,
  },

  Pronta: {
    key: 'Pronta',
    label: 'Pronto para levantar',
    hint: 'Cliente pode passar a levantar.',
    build: (v) => {
      const horario = v.horario_loja ? ` Podes passar quando der jeito dentro do nosso horário: ${v.horario_loja}.` : ' Podes passar quando der jeito.';
      return `Olá ${v.cliente_nome}, o teu ${v.equipamento} já está pronto para levantamento na ${v.loja_nome ?? 'loja'}.${horario}`;
    },
  },

  Entregue: {
    key: 'Entregue',
    label: 'Agradecer entrega',
    hint: 'Cliente acabou de levantar — agradecer.',
    build: (v) =>
      `Olá ${v.cliente_nome}, obrigado por teres confiado em nós para tratar do teu ${v.equipamento}. Se notares alguma coisa estranha nos próximos dias, responde por aqui.`,
  },

  Cancelada: {
    key: 'Cancelada',
    label: 'Confirmar cancelamento',
    hint: 'Reparação cancelada, combinar levantamento do equipamento.',
    build: (v) =>
      `Olá ${v.cliente_nome}, confirmamos o cancelamento da reparação do teu ${v.equipamento}. Quando quiseres, podes combinar connosco o levantamento ou os próximos passos.`,
  },

  LembreteLevantamento: {
    key: 'LembreteLevantamento',
    label: 'Lembrete de levantamento',
    hint: 'Pronto há > 7 dias e ainda não levantou.',
    build: (v) => {
      const desde = v.data_pronto ? ` desde ${v.data_pronto}` : '';
      const horario = v.horario_loja ? ` dentro do horário ${v.horario_loja}` : '';
      return `Olá ${v.cliente_nome}, o teu ${v.equipamento} está pronto para levantamento${desde} e continua guardado na ${v.loja_nome ?? 'loja'}. Quando puderes, passa${horario} ou diz-nos se precisas de combinar outro momento.`;
    },
  },

  PedidoReview: {
    key: 'PedidoReview',
    label: 'Pedir avaliação Google',
    hint: 'Entregue há 5+ dias, sem reclamação. Requer opt-in.',
    build: (v) => {
      const link = v.link_review_google ? ` ${v.link_review_google}` : '';
      return `Olá ${v.cliente_nome}, passaram alguns dias desde que levantaste o ${v.equipamento}. Se ficou tudo bem, ajudava-nos muito deixares uma avaliação no Google:${link}. Obrigado pela confiança.`;
    },
  },

  PrazoDerrapou: {
    key: 'PrazoDerrapou',
    label: 'Avisar atraso',
    hint: 'Prazo previsto vai derrapar.',
    build: (v) =>
      `Olá ${v.cliente_nome}, a reparação do teu ${v.equipamento} vai demorar mais do que o previsto. Preferimos avisar-te já: a nova previsão é ${prazoFallback(v)}, e se mudar voltamos a contactar.`,
  },
};

export function renderPreferenceTemplate(texto: string, vars: WhatsAppVars): string {
  const values: Record<string, string | number | undefined> = {
    ...vars,
    cliente: vars.cliente_nome,
    cliente_nome: vars.cliente_nome,
    equipamento: vars.equipamento,
    loja: vars.loja_nome ?? 'loja',
    loja_nome: vars.loja_nome ?? 'loja',
    horario_loja: vars.horario_loja ?? '',
    valor: vars.valor ?? '[valor]',
    link_aprovacao: vars.link_aprovacao ?? '',
    link_review_google: vars.link_review_google ?? '',
    peca_nome: vars.peca_nome ?? 'a peça encomendada',
    prazo_estimado: vars.prazo_estimado ?? 'os próximos dias',
    data_pronto: vars.data_pronto ?? '',
  };

  return texto.replace(/{{\s*([\w_]+)\s*}}/g, (_, key: string) => {
    const value = values[key];
    return value == null || value === '' ? '' : String(value);
  });
}

export function templatesFromPreferences(preferences?: TenantPreferencesRoot | null): Record<TemplateKey, TemplateMeta> {
  const configured = preferences?.communication.templatesByState;
  if (!configured) return TEMPLATES;

  const merged = { ...TEMPLATES };
  for (const key of Object.keys(TEMPLATES) as TemplateKey[]) {
    const custom = configured[key];
    if (!custom) continue;
    const fallback = TEMPLATES[key];
    merged[key] = {
      ...fallback,
      build: (v) => renderPreferenceTemplate(custom.texto || fallback.build(v), v),
    };
  }
  return merged;
}

/**
 * Lista de templates relevantes para o estado atual da reparação.
 * Ordem: o mais provável primeiro.
 */
export function templatesForState(estado: VendaEstado, opts: TemplateOptions = {}): TemplateMeta[] {
  const t = templatesFromPreferences(opts.preferences);
  const staleThreshold = opts.staleThreshold ?? opts.preferences?.communication.staleDaysThreshold ?? 7;
  const isStale = (opts.staleDays ?? 0) >= staleThreshold;

  switch (estado) {
    case VENDA_ESTADO.Orcamento:
      return [t.Orcamento, t.PrazoDerrapou];
    case VENDA_ESTADO.EmCurso:
      return [t.EmCurso, t.PrazoDerrapou];
    case VENDA_ESTADO.AEsperaPeca:
      return [t.AEsperaPeca, t.PrazoDerrapou];
    case VENDA_ESTADO.Pronta:
      return isStale ? [t.LembreteLevantamento, t.Pronta] : [t.Pronta, t.LembreteLevantamento];
    case VENDA_ESTADO.Entregue:
      return [t.Entregue, t.PedidoReview];
    case VENDA_ESTADO.Cancelada:
      return [t.Cancelada];
    default:
      return Object.values(t);
  }
}

/** Compõe URL wa.me com mensagem URL-encoded. */
export function waMeLink(phoneE164: string, message: string): string {
  const phone = phoneE164.replace(/[^\d+]/g, '').replace(/^\+/, '');
  return `https://wa.me/${phone}?text=${encodeURIComponent(message)}`;
}
