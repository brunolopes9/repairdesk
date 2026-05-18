// Templates WhatsApp por estado de reparação.
// Conteúdo derivado de Contexto/11-WhatsApp-Templates.md.
// Modo "padrão" PT-PT (tratamento por tu) — variantes informal/profissional adiados.

import type { RepairStatus } from '../reparacoes/types';

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

export type TemplateKey =
  | 'Recebido'
  | 'Diagnostico'
  | 'Orcamento'
  | 'AguardaPeca'
  | 'EmReparacao'
  | 'Pronto'
  | 'Entregue'
  | 'Cancelado'
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

function prazoFallback(v: WhatsAppVars): string {
  return v.prazo_estimado ?? 'os próximos dias';
}

function pecaFallback(v: WhatsAppVars): string {
  return v.peca_nome ?? 'a peça encomendada';
}

export const TEMPLATES: Record<TemplateKey, TemplateMeta> = {
  Recebido: {
    key: 'Recebido',
    label: 'Confirmar recepção',
    hint: 'Acabámos de receber o equipamento na loja.',
    build: (v) =>
      `Olá ${v.cliente_nome}, confirmamos a entrada do teu ${v.equipamento} na ${v.loja_nome ?? 'loja'}. Vamos registar tudo e começar a análise; assim que houver novidades falamos contigo por aqui.`,
  },

  Diagnostico: {
    key: 'Diagnostico',
    label: 'Em diagnóstico',
    hint: 'Estamos a analisar o equipamento.',
    build: (v) =>
      `Olá ${v.cliente_nome}, o teu ${v.equipamento} está em diagnóstico. Estamos a testar com cuidado para perceber a origem do problema e voltamos a contactar assim que tivermos uma conclusão.`,
  },

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

  AguardaPeca: {
    key: 'AguardaPeca',
    label: 'Aguarda peça',
    hint: 'Encomendámos a peça, aguarda chegada.',
    build: (v) =>
      `Olá ${v.cliente_nome}, a reparação do teu ${v.equipamento} está a aguardar a chegada de ${pecaFallback(v)}. A previsão atual é ${prazoFallback(v)}; avisamos-te assim que chegar.`,
  },

  EmReparacao: {
    key: 'EmReparacao',
    label: 'Em reparação',
    hint: 'Estamos a trabalhar nele agora.',
    build: (v) =>
      `Olá ${v.cliente_nome}, começámos a reparação do teu ${v.equipamento}. Se tudo correr dentro do previsto, voltamos a falar contigo até ${prazoFallback(v)}.`,
  },

  Pronto: {
    key: 'Pronto',
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

  Cancelado: {
    key: 'Cancelado',
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

/**
 * Lista de templates relevantes para o estado actual da reparação.
 * Ordem: o mais provável primeiro.
 */
export function templatesForState(estado: RepairStatus, opts: { staleDays?: number } = {}): TemplateMeta[] {
  const t = TEMPLATES;
  const stale7 = (opts.staleDays ?? 0) >= 7;

  switch (estado) {
    case 0: // Recebido
      return [t.Recebido, t.Diagnostico, t.Orcamento, t.PrazoDerrapou];
    case 1: // Diagnostico
      return [t.Diagnostico, t.Orcamento, t.PrazoDerrapou];
    case 2: // AguardaPeca
      return [t.AguardaPeca, t.PrazoDerrapou];
    case 3: // EmReparacao
      return [t.EmReparacao, t.PrazoDerrapou];
    case 4: // Pronto
      return stale7 ? [t.LembreteLevantamento, t.Pronto] : [t.Pronto, t.LembreteLevantamento];
    case 5: // Entregue
      return [t.Entregue, t.PedidoReview];
    case 6: // Cancelado
      return [t.Cancelado];
    case 7: // Orcamento
      return [t.Orcamento, t.PrazoDerrapou];
    default:
      return Object.values(t);
  }
}

/**
 * Templates relevantes para o estado de um Trabalho (não-reparação).
 * TrabalhoStatus: 0=Orcamento, 1=Aceite, 2=EmExecucao, 3=Concluido, 4=Cancelado.
 */
export function templatesForTrabalhoStatus(status: number): TemplateMeta[] {
  const t = TEMPLATES;
  switch (status) {
    case 0: // Orcamento
      return [t.Orcamento, t.PrazoDerrapou];
    case 1: // Aceite
      return [t.EmReparacao, t.PrazoDerrapou];
    case 2: // EmExecucao
      return [t.EmReparacao, t.PrazoDerrapou, t.Pronto];
    case 3: // Concluido
      return [t.Pronto, t.Entregue, t.LembreteLevantamento, t.PedidoReview];
    case 4: // Cancelado
      return [t.Cancelado];
    default:
      return Object.values(t);
  }
}

/** Compõe URL wa.me com mensagem URL-encoded. */
export function waMeLink(phoneE164: string, message: string): string {
  const phone = phoneE164.replace(/[^\d+]/g, '').replace(/^\+/, '');
  return `https://wa.me/${phone}?text=${encodeURIComponent(message)}`;
}
