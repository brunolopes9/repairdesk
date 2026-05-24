import { useCallback, useState } from 'react';

/**
 * Sprint 254 (Doc 79): hook para gerir o estado open/closed de modais e drawers.
 * Substitui o padrão repetido `const [open, setOpen] = useState(false)` que aparece
 * 44× em 8 ficheiros.
 *
 * @example
 *   const editModal = useDisclosure();
 *   <Button onClick={editModal.onOpen}>Editar</Button>
 *   <Modal open={editModal.open} onClose={editModal.onClose}>...</Modal>
 */
export interface DisclosureState {
  open: boolean;
  onOpen: () => void;
  onClose: () => void;
  onToggle: () => void;
  setOpen: (open: boolean) => void;
}

export function useDisclosure(initial = false): DisclosureState {
  const [open, setOpen] = useState(initial);
  const onOpen = useCallback(() => setOpen(true), []);
  const onClose = useCallback(() => setOpen(false), []);
  const onToggle = useCallback(() => setOpen((p) => !p), []);
  return { open, onOpen, onClose, onToggle, setOpen };
}
