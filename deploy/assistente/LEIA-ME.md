# Assistente do Mender (LopesTech) — Telegram + voz

Fala com o Mender por Telegram (texto ou nota de voz). O Claude corre pela subscrição Max (`claude -p`, sem créditos de API).

- Pasta: `~/assistente` no Hetzner, contentor `mender-assistente`.
- Ferramentas: `mender_mcp.py` (criar tarefa, rotina, agenda, peças por encomendar, finanças do mês, desfazer).
- Fila de 1, teto de 60 pedidos/dia (`teto_dia`), limite partilhado `limite-<DATA>` com o estúdio e a rotina da LopesTech.
- Só responde ao chat do Bruno (`dono_chat_id`). `/nova` recomeça a conversa.

## Ligar (Bruno)
1. Telegram → @BotFather → /newbot → copia o token → no servidor: `bash ~/assistente/token_telegram.sh`
2. No Mender: Definições → Chaves de API → nova chave só com o scope «assistente» → no servidor: `bash ~/assistente/chave_mender.sh`
3. Abre o bot novo e escreve /start.
