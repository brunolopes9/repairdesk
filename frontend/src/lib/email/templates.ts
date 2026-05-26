/**
 * Sprint 348 (Doc 83 Pillar 3): templates de email análogos aos de WhatsApp,
 * mas com tom mais formal (mais espaço, header/saudação/assinatura).
 *
 * O envio é 1-click via mailto: — o utilizador vê e edita no cliente de mail
 * antes de enviar (Outlook/Gmail/etc), igual ao fluxo WhatsApp.
 */

import type { RepairStatus } from '../reparacoes/types';

export interface EmailVars {
  cliente_nome: string;
  equipamento: string;
  loja_nome?: string;
  horario_loja?: string;
  numero_reparacao?: number | string;
  valor?: string;
  link_aprovacao?: string;
  data_pronto?: string;
}

export type EmailTemplateKey =
  | 'Recebido'
  | 'Diagnostico'
  | 'Orcamento'
  | 'AguardaPeca'
  | 'EmReparacao'
  | 'Pronto'
  | 'LembreteLevantamento'
  | 'Cancelado';

export interface EmailTemplateMeta {
  key: EmailTemplateKey;
  label: string;
  hint: string;
  build: (v: EmailVars) => { subject: string; body: string };
}

function loja(v: EmailVars) { return v.loja_nome ?? 'a loja'; }
function ref(v: EmailVars) { return v.numero_reparacao != null ? ` #${v.numero_reparacao}` : ''; }
function assinatura(v: EmailVars) {
  return `\n\nCumprimentos,\n${v.loja_nome ?? 'Equipa de reparação'}`;
}

export const EMAIL_TEMPLATES: Record<EmailTemplateKey, EmailTemplateMeta> = {
  Recebido: {
    key: 'Recebido',
    label: 'Confirmar recepção',
    hint: 'Confirma entrada do equipamento.',
    build: (v) => ({
      subject: `Reparação${ref(v)} — Recepção confirmada (${v.equipamento})`,
      body:
        `Olá ${v.cliente_nome},\n\n` +
        `Confirmamos a entrada do seu ${v.equipamento} em ${loja(v)}. ` +
        `Vamos registar tudo e iniciar a análise nos próximos dias.\n\n` +
        `Avisamos por aqui assim que tivermos novidades.` +
        assinatura(v),
    }),
  },
  Diagnostico: {
    key: 'Diagnostico',
    label: 'Em diagnóstico',
    hint: 'Cliente quer saber em que ponto está.',
    build: (v) => ({
      subject: `Reparação${ref(v)} — Em diagnóstico`,
      body:
        `Olá ${v.cliente_nome},\n\n` +
        `O seu ${v.equipamento} está actualmente em diagnóstico. Estamos a testar com cuidado ` +
        `para perceber a origem do problema e voltamos a contactar assim que tivermos conclusão e orçamento.` +
        assinatura(v),
    }),
  },
  Orcamento: {
    key: 'Orcamento',
    label: 'Enviar orçamento',
    hint: 'Tem orçamento aprovável.',
    build: (v) => ({
      subject: `Reparação${ref(v)} — Orçamento para o seu ${v.equipamento}`,
      body:
        `Olá ${v.cliente_nome},\n\n` +
        `Já temos o orçamento da reparação do seu ${v.equipamento}: ${v.valor ?? '[valor]'}.\n\n` +
        (v.link_aprovacao ? `Pode aprovar online: ${v.link_aprovacao}\n\n` : '') +
        `Caso prefira, responda a este email com "Aprovo" e avançamos.` +
        assinatura(v),
    }),
  },
  AguardaPeca: {
    key: 'AguardaPeca',
    label: 'Aguarda peça',
    hint: 'Aguardamos chegada da peça encomendada.',
    build: (v) => ({
      subject: `Reparação${ref(v)} — A aguardar peça`,
      body:
        `Olá ${v.cliente_nome},\n\n` +
        `A reparação do seu ${v.equipamento} está actualmente à espera da chegada da peça encomendada. ` +
        `Assim que chegar avançamos com a reparação e voltamos a contactar.` +
        assinatura(v),
    }),
  },
  EmReparacao: {
    key: 'EmReparacao',
    label: 'Em reparação',
    hint: 'Já estamos a trabalhar nele.',
    build: (v) => ({
      subject: `Reparação${ref(v)} — Em curso`,
      body:
        `Olá ${v.cliente_nome},\n\n` +
        `Começámos a reparação do seu ${v.equipamento}. ` +
        `Avisamos assim que estiver pronto para levantar.` +
        assinatura(v),
    }),
  },
  Pronto: {
    key: 'Pronto',
    label: 'Pronto para levantar',
    hint: 'Cliente pode passar a levantar.',
    build: (v) => ({
      subject: `Reparação${ref(v)} — Pronto para levantar`,
      body:
        `Olá ${v.cliente_nome},\n\n` +
        `O seu ${v.equipamento} já está pronto para levantamento em ${loja(v)}.` +
        (v.horario_loja ? `\n\nHorário: ${v.horario_loja}` : '') +
        assinatura(v),
    }),
  },
  LembreteLevantamento: {
    key: 'LembreteLevantamento',
    label: 'Lembrete de levantamento',
    hint: 'Pronto há > 7 dias.',
    build: (v) => ({
      subject: `Reparação${ref(v)} — Lembrete: pronto para levantar`,
      body:
        `Olá ${v.cliente_nome},\n\n` +
        `Lembramos que o seu ${v.equipamento} continua pronto e guardado em ${loja(v)}` +
        (v.data_pronto ? ` desde ${v.data_pronto}` : '') + `.\n\n` +
        `Quando puder, passe para levantar ou responda a este email para combinarmos outro momento.` +
        assinatura(v),
    }),
  },
  Cancelado: {
    key: 'Cancelado',
    label: 'Confirmar cancelamento',
    hint: 'Reparação cancelada — combinar levantamento.',
    build: (v) => ({
      subject: `Reparação${ref(v)} — Cancelamento confirmado`,
      body:
        `Olá ${v.cliente_nome},\n\n` +
        `Confirmamos o cancelamento da reparação do seu ${v.equipamento}. ` +
        `Quando lhe der jeito, podemos combinar o levantamento.` +
        assinatura(v),
    }),
  },
};

export function emailTemplatesForState(estado: RepairStatus, opts: { staleDays?: number } = {}): EmailTemplateMeta[] {
  const t = EMAIL_TEMPLATES;
  const isStale = (opts.staleDays ?? 0) >= 7;
  switch (estado) {
    case 0: return [t.Recebido, t.Diagnostico, t.Orcamento];
    case 1: return [t.Diagnostico, t.Orcamento];
    case 2: return [t.AguardaPeca];
    case 3: return [t.EmReparacao];
    case 4: return isStale ? [t.LembreteLevantamento, t.Pronto] : [t.Pronto, t.LembreteLevantamento];
    case 6: return [t.Cancelado];
    case 7: return [t.Orcamento];
    default: return Object.values(t);
  }
}

/** Compõe URL mailto: com subject + body URL-encoded. */
export function mailtoLink(email: string, subject: string, body: string): string {
  return `mailto:${encodeURIComponent(email)}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
}
