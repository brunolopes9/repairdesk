export const SIGNATURE_TYPE = {
  EntradaAutorizacao: 0,
  EntregaLevantamento: 1,
} as const;

export type SignatureType = (typeof SIGNATURE_TYPE)[keyof typeof SIGNATURE_TYPE];

export const SIGNATURE_TYPE_LABEL: Record<SignatureType, string> = {
  0: 'Autorização de reparação (entrada)',
  1: 'Recibo de entrega (levantamento)',
};

export interface SignatureDto {
  id: string;
  reparacaoId: string;
  tipo: SignatureType;
  imagemDataUrl: string;
  assinanteNome: string;
  assinanteContacto: string | null;
  signedAt: string;
}
