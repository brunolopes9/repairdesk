import { api } from '../api';
import type { SignatureDto, SignatureType } from './types';

export const signaturesApi = {
  list(reparacaoId: string) {
    return api.get<SignatureDto[]>(`/reparacoes/${reparacaoId}/signatures`).then((r) => r.data);
  },
  capture(reparacaoId: string, payload: {
    tipo: SignatureType;
    imagemDataUrl: string;
    assinanteNome: string;
    assinanteContacto?: string;
  }) {
    return api.post<SignatureDto>(`/reparacoes/${reparacaoId}/signatures`, payload).then((r) => r.data);
  },
  delete(reparacaoId: string, signatureId: string) {
    return api.delete<void>(`/reparacoes/${reparacaoId}/signatures/${signatureId}`).then((r) => r.data);
  },
};
