# 79 — Auditoria DRY Frontend

**Data:** 2026-05-24
**Pedido Bruno:** identificar duplicação de código, sugerir refactor para componentes reutilizáveis / hooks / utils.

**Escopo:** frontend (26.5k LOC TS/TSX em 36+ ficheiros).

---

## Helpers já existentes (verificar antes de duplicar)

| Helper | Localização | Uso |
|---|---|---|
| `formatCents()` | `lib/money.ts` | 187× em 31 ficheiros — bem usado |
| `apiErrorMessage()` | `lib/errors.ts` (Sprint 253) | 0× hoje — toast genérico via MutationCache |
| `isAxiosError()` | axios | usado em try/catch directamente |
| `<SkeletonCard/>`, `<SkeletonRow/>`, `<SkeletonTable/>` | `components/ui/Skeleton.tsx` | usado mas há skeleton inline em vários sítios |
| `<PageHeader/>`, `<EmptyState/>`, `<Button/>` | `components/ui/` | usados |
| `<Modal/>` | `components/Modal.tsx` | usado mas state local em cada página |

---

## Duplicações identificadas

### 🔴 P1 (alto valor + fácil)

#### 1. Formatação de datas — 22 ocorrências em 11 ficheiros
```tsx
new Date(repair.createdAt).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short' })
new Date(audit.timestamp).toLocaleString('pt-PT', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
```
Variações: short, long, with time, without. Padrões pt-PT espalhados.

**Refactor:** `lib/dates.ts` com `formatDate(d, variant?)` + `formatDateTime(d)` + `formatRelative(d)`.

#### 2. `window.confirm()` para destrutivas — 8 ocorrências em 8 ficheiros
```tsx
if (confirm('Apagar este registo? Não pode ser revertido.')) {
  remove.mutate(id);
}
```
UX nativo do browser: feio em mobile, não temalizável, bloqueia event loop.

**Refactor:** `<ConfirmDialog/>` + `useConfirm()` hook que devolve `Promise<boolean>`.

#### 3. State boolean de modal — 44 ocorrências em 8 ficheiros
```tsx
const [open, setOpen] = useState(false);
// ... <Modal open={open} onClose={() => setOpen(false)}> ...
```

**Refactor:** `useDisclosure()` hook devolve `{ open, onOpen, onClose, onToggle }` (pattern Chakra UI).

### 🟡 P2 (médio valor)

#### 4. Paginated query — 8 ficheiros
Padrão repetido:
```tsx
const [page, setPage] = useState(1);
const [pageSize] = useState(20);
const query = useQuery({ queryKey: ['x', page, pageSize], queryFn: () => api.search({ page, pageSize }) });
```
Includes paginação UI + `keepPreviousData` (alguns sítios) + reset de page quando filters mudam.

**Refactor:** `usePaginatedQuery<T>({ key, fetcher, initialPageSize? })` retorna `{ data, page, setPage, totalPages }`.

#### 5. Search debounced — 12 ocorrências em 11 ficheiros
```tsx
const [q, setQ] = useState('');
const [debouncedQ, setDebouncedQ] = useState('');
useEffect(() => {
  const t = setTimeout(() => setDebouncedQ(q), 300);
  return () => clearTimeout(t);
}, [q]);
```

**Refactor:** `useDebouncedValue(value, delay)` hook genérico.

#### 6. Mutation + invalidateQueries + toast.success — ~50 ocorrências
```tsx
const create = useMutation({
  mutationFn: api.create,
  onSuccess: () => {
    qc.invalidateQueries({ queryKey: ['x'] });
    toast.success('Criado.');
    setOpen(false);
  },
});
```

**Refactor:** `useCreateMutation({ resource, invalidateKeys, onSuccess? })` HOF que injecta toast + invalidação automaticamente. **Risco:** abstrai demais — pode ser dificultar custom flows. **Decisão:** manter como pattern documentado, não como HOF.

### 🟢 P3 (baixo valor / estilo)

#### 7. Formulários com label + input + error inline
~30 ocorrências de:
```tsx
<label className="text-xs">
  <span className="block text-zinc-500">Email</span>
  <input ... className="mt-1 min-h-11 w-full rounded-md border ..." />
  {error && <div className="mt-1 text-xs text-rose-600">{error}</div>}
</label>
```

**Decisão:** **não refactor** agora. Tailwind classes inline são explícitas. Abstrair em `<FormField/>` é tentador mas perde flexibilidade (autocomplete diferente, prefix/suffix, etc.). Pattern já é consistente, basta documentar.

#### 8. `console.error` para fallback de UI
Algumas componentes ainda têm `console.error('falha em X', err)` sem reportar a Sentry. ~11 ocorrências.

**Refactor:** substituir por `Sentry.captureException(err, { tags: { feature: 'X' } })`. Já fiz para `FotosReparacao` (Sprint 253). Os restantes 8 ficaram como `console.error` na auditoria Doc 77 porque eram legítimos (debugging).

---

## Sprint 254 — Implementação P1

Vou agora:
1. Criar `lib/dates.ts` com 3 helpers
2. Criar `hooks/useDisclosure.ts`
3. Criar `components/ConfirmDialog.tsx` + `hooks/useConfirm.ts`
4. **Não fazer refactor mass** — adicionar e deixar como deps disponíveis. Pages migram organicamente quando alguém tocar nelas.

P2/P3 fica documentado como sprint futura — não vale o churn massivo agora.

[[reference-docker-setup]]
