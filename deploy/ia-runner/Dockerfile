# Sprint 667: IA do Mender pela subscrição Claude (Max) — `claude -p` com CLAUDE_CODE_OAUTH_TOKEN, sem créditos da API.
FROM node:22-slim
RUN npm install -g @anthropic-ai/claude-code && claude --version
RUN useradd -m ia
USER ia
WORKDIR /home/ia
COPY --chown=ia server.mjs .
EXPOSE 8090
CMD ["node", "server.mjs"]
