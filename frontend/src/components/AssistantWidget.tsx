import { useRef, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Bot, Send, Sparkles, X } from 'lucide-react';
import { assistantApi, type AssistantMessage } from '../lib/assistant/api';

/**
 * Sprint 369: assistente interno (read-only) flutuante. Pergunta em linguagem natural sobre
 * stock/reparações/vendas. Botão canto inferior direito → painel de chat. Histórico em estado
 * local (não persiste — sessão de conversa leve).
 */
const SUGESTOES = [
  'Quanto stock há de ecrãs Samsung?',
  'Quantas reparações este mês?',
  'Que peças estão em stock baixo?',
  'Total de vendas dos últimos 7 dias',
];

export function AssistantWidget() {
  const [open, setOpen] = useState(false);
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<AssistantMessage[]>([]);
  const scrollRef = useRef<HTMLDivElement>(null);

  const ask = useMutation({
    mutationFn: (question: string) => assistantApi.ask(question, messages.slice(-10)),
    onSuccess: (data) => {
      setMessages((m) => [...m, { role: 'assistant', content: data.answer }]);
      queueScroll();
    },
    onError: () => {
      setMessages((m) => [...m, { role: 'assistant', content: 'Não consegui responder agora. Tenta outra vez.' }]);
      queueScroll();
    },
  });

  function queueScroll() {
    requestAnimationFrame(() => scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' }));
  }

  function send(question: string) {
    const q = question.trim();
    if (!q || ask.isPending) return;
    setMessages((m) => [...m, { role: 'user', content: q }]);
    setInput('');
    queueScroll();
    ask.mutate(q);
  }

  return (
    <>
      {!open && (
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="fixed bottom-20 right-4 z-40 flex h-12 w-12 items-center justify-center rounded-full bg-brand-600 text-white shadow-lg shadow-brand-600/30 transition hover:bg-brand-700 sm:bottom-6"
          aria-label="Abrir assistente"
        >
          <Sparkles size={22} />
        </button>
      )}

      {open && (
        <div className="fixed bottom-4 right-4 z-40 flex h-[32rem] max-h-[80vh] w-[22rem] max-w-[calc(100vw-2rem)] flex-col overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-2xl dark:border-zinc-800 dark:bg-zinc-900">
          <div className="flex items-center gap-2 border-b border-zinc-200 px-4 py-3 dark:border-zinc-800">
            <div className="grid h-8 w-8 place-items-center rounded-lg bg-brand-50 text-brand-600 dark:bg-zinc-800">
              <Bot size={18} />
            </div>
            <div className="flex-1">
              <p className="text-sm font-semibold leading-tight">Assistente</p>
              <p className="text-[11px] text-zinc-500">Pergunta sobre stock, reparações, vendas</p>
            </div>
            <button type="button" onClick={() => setOpen(false)} className="rounded-md p-1 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700 dark:hover:bg-zinc-800" aria-label="Fechar">
              <X size={18} />
            </button>
          </div>

          <div ref={scrollRef} className="flex-1 space-y-3 overflow-y-auto p-4">
            {messages.length === 0 && (
              <div className="space-y-3">
                <p className="text-sm text-zinc-500">Olá! Pergunta-me em português, por exemplo:</p>
                <div className="flex flex-col gap-2">
                  {SUGESTOES.map((s) => (
                    <button
                      key={s}
                      type="button"
                      onClick={() => send(s)}
                      className="rounded-lg border border-zinc-200 px-3 py-2 text-left text-xs text-zinc-600 transition hover:border-brand-300 hover:bg-brand-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
                    >
                      {s}
                    </button>
                  ))}
                </div>
                <p className="text-[11px] text-zinc-400">Só consulta informação — não altera nada.</p>
              </div>
            )}
            {messages.map((m, i) => (
              <div key={i} className={m.role === 'user' ? 'flex justify-end' : 'flex justify-start'}>
                <div
                  className={`max-w-[85%] whitespace-pre-wrap rounded-2xl px-3 py-2 text-sm ${
                    m.role === 'user'
                      ? 'bg-brand-600 text-white'
                      : 'bg-zinc-100 text-zinc-800 dark:bg-zinc-800 dark:text-zinc-100'
                  }`}
                >
                  {m.content}
                </div>
              </div>
            ))}
            {ask.isPending && (
              <div className="flex justify-start">
                <div className="rounded-2xl bg-zinc-100 px-3 py-2 text-sm text-zinc-500 dark:bg-zinc-800">a pensar…</div>
              </div>
            )}
          </div>

          <form
            onSubmit={(e) => { e.preventDefault(); send(input); }}
            className="flex items-center gap-2 border-t border-zinc-200 p-3 dark:border-zinc-800"
          >
            <input
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder="Escreve a tua pergunta…"
              className="flex-1 rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
            />
            <button
              type="submit"
              disabled={ask.isPending || !input.trim()}
              className="grid h-9 w-9 flex-none place-items-center rounded-lg bg-brand-600 text-white transition hover:bg-brand-700 disabled:opacity-40"
              aria-label="Enviar"
            >
              <Send size={16} />
            </button>
          </form>
        </div>
      )}
    </>
  );
}
