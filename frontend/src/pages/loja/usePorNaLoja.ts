import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { apiErrorMessage } from '../../lib/errors';
import { lojaApi } from '../../lib/loja/api';
import { toast } from '../../lib/toast';

/**
 * 23/11: «Pôr na loja» num clique — o servidor cria o produto a partir do artigo (título, marca, preço),
 * vai buscar as fotos oficiais ao fornecedor e a IA escreve descrição e especificações. Fica em rascunho:
 * abre o produto para o dono rever e carregar em Publicar.
 */
export function usePorNaLoja() {
  const navigate = useNavigate();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (artigoId: string) => lojaApi.doArtigo(artigoId),
    onSuccess: (r) => {
      void qc.invalidateQueries({ queryKey: ['loja', 'produtos'] });
      qc.setQueryData(['loja', 'produto', r.produto.id], r.produto);
      const feito = [r.fotosImportadas ? `${r.fotosImportadas} foto(s)` : null, r.textoGerado ? 'descrição e especificações' : null]
        .filter(Boolean).join(' + ');
      toast.success(`Produto #${r.produto.numero} criado${feito ? ` com ${feito}` : ''}`, r.aviso ?? 'Revê o preço e carrega em Publicar.');
      navigate(`/loja/${r.produto.id}`);
    },
    onError: (e) => toast.error(apiErrorMessage(e)),
  });
}
