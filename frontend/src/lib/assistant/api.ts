import { api } from '../api';

export interface AssistantMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface AssistantAnswer {
  answer: string;
}

/**
 * Sprint 369: assistente interno read-only. Envia a pergunta + histórico recente; o backend
 * usa Claude com tools de leitura (stock/reparações/vendas) scoped ao tenant.
 */
export const assistantApi = {
  ask(question: string, history: AssistantMessage[]) {
    return api
      .post<AssistantAnswer>('/assistant/ask', { question, history })
      .then((r) => r.data);
  },
};
