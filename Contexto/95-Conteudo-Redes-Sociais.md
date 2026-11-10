# Doc 95 — Módulo «Conteúdo» (redes sociais 24/7 dentro do Mender)

Estado: **proposta** (09/11/2026). Plano de alto nível aprovado pelo Bruno a 08/11: software 24/7 no servidor, dentro do Mender, com aprovação no telemóvel e publicação automática.

## 1. Problema

Hoje a gestão das redes sociais da LopesTech depende de uma sessão do Claude Code no PC do Bruno:

- o calendário, o estado e o registo vivem em ficheiros e na conversa, que pode ser compactada;
- os vídeos dele chegam por cabo;
- a aprovação é feita no chat;
- a publicação é feita a clicar no Business Suite, no TikTok Studio e no Threads, o que é lento e frágil (layouts mudam, popups, conta errada no Chrome).

**Objetivo:** o Mender passa a ser a **fonte única de verdade** do calendário editorial.

- O Bruno envia vídeos e aprova pelo telemóvel.
- O servidor publica sozinho, à hora certa, em todas as redes, 24/7.
- O registo do que saiu fica guardado para sempre.
- A geração de conteúdo vai sendo automatizada por fases.

## 2. Quem usa e como

- **Bruno (Owner)**:
  - envia vídeos brutos do telemóvel;
  - recebe uma notificação push quando há publicações para aprovar;
  - aprova ou rejeita com um toque e motivo opcional;
  - vê o calendário e o histórico.
- **Claude (via ServiceApiKey com scope `conteudo`)**:
  - cria publicações com media e legendas por rede;
  - submete-as para aprovação;
  - lê as rejeições e os vídeos novos.
- **Servidor (BackgroundService)**:
  - publica o que está aprovado quando chega a hora;
  - repete as tentativas com backoff;
  - avisa por push se algo falhar.

## 3. Modelo de dados (tenant-scoped, padrão BaseEntity + ITenantEntity)

| Entidade | Campos principais |
|---|---|
| `ConteudoPublicacao` | Numero (sequencial por tenant, visível), Linha (Reparacoes/Web), Formato (Post, Carrossel, Reel, Story, Texto), Titulo interno, Legenda, AgendadoPara (UTC), Estado, AprovadoPorUserId/AprovadoEm, MotivoRejeicao, AutoAprovada (promo diária), Origem (Claude/Manual/Automatico), ArtigoId?, Notas |
| `ConteudoFicheiro` | PublicacaoId? (null = vídeo bruto na biblioteca), Ordem, Tipo (Imagem/Video), StorageKey (R2), ContentType, Bytes, Largura, Altura, DuracaoSeg, Sha256 (evita duplicados), NomeOriginal, UsadoEm |
| `ConteudoDestino` | PublicacaoId, Rede (Facebook, Instagram, Threads, TikTok), LegendaPropria?, Estado (Pendente, APublicar, Publicado, Falhou, Manual), ExternalId, Permalink, Tentativas, ProximaTentativaEm, UltimoErro, PublicadoEm |
| `RedeSocialLigacao` (Fase B) | Rede, ContaExternaId, NomeConta, TokenCifrado (ISecretProtector), ExpiraEm, Ativa |

**Porquê um estado por destino:** uma publicação pode sair no Facebook e falhar no Instagram. Assim nunca se republica o que já saiu: o destino com `ExternalId` preenchido fica idempotente, e só se repete o que falhou.

## 4. Fluxo de estados (publicação)

```
Rascunho ──submeter──▶ PorAprovar ──aprovar──▶ Aprovada ──(hora)──▶ APublicar ──▶ Publicada
                          │  ▲                     │                    └──▶ ComFalhas (algum destino falhou após 3 tentativas → push)
                     rejeitar│ editar               cancelar
                          ▼  │                     ▼
                       Rejeitada               Cancelada
```

- Editar uma publicação depois de aprovada faz-a voltar a `PorAprovar`, porque o Bruno aprovou outra versão.
- A promo diária com autorização permanente nasce `Aprovada` (`AutoAprovada = true`).
- O worker «reclama» cada publicação com um UPDATE condicional (`WHERE Estado = Aprovada`), para não haver publicação dupla.

## 5. Publicação (Fase B)

- **Facebook Page**:
  - fotos: `/{page}/photos`; carrossel: post com várias fotos;
  - reels: `/{page}/video_reels`;
  - stories: `/{page}/photo_stories` e `/{page}/video_stories`.
- **Instagram (conta profissional)**:
  - criar o container (`image_url`/`video_url`, `media_type` REELS/STORIES/CAROUSEL);
  - esperar `status_code = FINISHED`;
  - depois `media_publish`;
  - limite: 50 publicações por 24 h.
- **Threads**: `/{user}/threads` com container, publicado cerca de 30 s depois; limite de 250 por 24 h.
- **TikTok** (Content Posting API):
  - a publicação direta exige auditoria da app;
  - até lá: upload como rascunho para a caixa do TikTok do Bruno, ou estado `Manual`, em que eu publico pelo Studio.
- **URLs de media**: o Meta precisa de ir buscar o ficheiro, por isso há um endpoint público assinado (HMAC, expira em 24 h): `/api/public/conteudo/media/{id}?exp=…&sig=…`.
- **Ligação das contas**:
  - um botão «Ligar Facebook/Instagram/Threads» em Definições faz o OAuth;
  - o Bruno faz login ele próprio e os tokens ficam cifrados;
  - nunca passam pelo chat.
- **Falhas**:
  - 3 tentativas com backoff (2, 10 e 30 min);
  - depois o destino fica em `Falhou`, com push «Falhou no Instagram: <erro>».

## 6. Upload de vídeos do telemóvel

- Os vídeos dele têm 5 a 20 min (centenas de MB a GB).
- **Não passam pela API**: o browser pede um **URL pré-assinado do R2** e faz PUT direto para o R2 (até 5 GB), com barra de progresso.
- Depois confirma o upload na API.
- Isto evita os limites de tamanho de pedidos do servidor e a carga na API.
- Requer CORS no bucket R2 para `app.lopestech.pt`.

## 7. Geração (Fase C)

- **Job diário com a Claude API**:
  - plano do dia com o histórico, para não repetir temas;
  - textos dos Threads;
  - legendas;
  - promo do dia a partir do stock do Mender: artigo com stock > 0, sem a flag «não promover», não promovido nos últimos N dias.
- **Worker de render** (contentor com Chromium headless + ffmpeg): os templates HTML que já uso (stories, carrosséis, promo) passam a correr no servidor.
- Os reels com edição dos vídeos dele continuam feitos por mim (Claude Code), até o processo estar maduro.

## 8. Métricas (Fase D)

- Insights por publicação: alcance, gostos, guardados e visualizações dos reels.
- Servem para aprender as melhores horas e formatos e alimentar o plano diário.

## 9. Segurança e permissões

- Só **Owner** aprova, liga contas e vê tokens. Os tokens nunca são devolvidos ao frontend.
- Aprovações e publicações vão para a auditoria (`AuditEntry`).
- Os URLs públicos de media são assinados, expiram e não listam nada.
- ServiceApiKey com o novo scope `conteudo`: não dá acesso a clientes nem a faturas.

## 10. Fases e entregas

| Fase | Entrega | Depende do Bruno? |
|---|---|---|
| **A** | Entidades e migração, API `/api/conteudo`, página **Conteúdo** (calendário semanal, «Por aprovar» com pré-visualização de imagens/vídeo e botões Aprovar/Rejeitar grandes para o telemóvel, «Enviar vídeos», histórico), push «N publicações para aprovar», scope `conteudo` | Não |
| **B** | Publicação automática Meta (FB+IG) e Threads, OAuth em Definições, retries e alertas | Sim: criar a app no developers.facebook.com (10 min, eu guio) |
| **B2** | TikTok (rascunho na caixa, depois publicação direta) | Sim: app TikTok e auditoria |
| **C** | Plano diário e textos com a Claude API, promo diária automática, render no servidor | Não |
| **D** | Métricas e otimização de horários | Não |

## 11. Riscos

- **Alterações das APIs Meta e revisão de apps**: para contas próprias (o Bruno é admin da app), o modo de desenvolvimento chega e não é preciso App Review.
- **Tokens expiram** (Threads: 60 dias): refresh automático e push 7 dias antes.
- **Vídeos que o Instagram rejeita** (codec, proporção): validar com ffprobe no upload (H.264/AAC, 9:16, ≤ 90 s para reels) e mostrar o aviso antes de aprovar.
- **Disco**: os vídeos brutos vão para o R2, não para o disco do VPS (61 GB livres, partilhado com outros projetos).

## 12. Fase B — decisões de implementação (09/11/2026)

- **Credenciais da app Meta/Threads por tenant, cifradas** (`Tenant.MetaAppId` + `MetaAppSecretCipherText`, idem Threads), escritas pelo dono em Definições → Redes sociais — mesmo padrão do BYOK Anthropic (S172). Nunca passam pelo chat nem pelo git.
- **OAuth**: o frontend pede o URL de ligação (JWT) e navega; o callback `/api/redes/{meta|threads}/callback` é anónimo e valida um `state` assinado (HMAC: tenant + utilizador + rede + expiração 15 min). Redirect URIs a registar na app: `https://app.lopestech.pt/api/redes/meta/callback` e `.../threads/callback`.
- **Tokens** em `RedeSocialLigacao` (cifrados): Facebook = token da Página (não expira, obtido de user token de longa duração); Instagram = mesma Página + id da conta IG profissional; Threads = token de 60 dias, renovado automaticamente quando faltam < 15 dias (push se falhar).
- **Interruptor «Publicação automática»** (`Tenant.ConteudoAutoPublicar`, desligado por defeito): enquanto eu ainda agendo à mão no Business Suite, o servidor não publica — evita duplicados. Só destinos `Pendente` são publicados; `AgendadoNaRede` nunca.
- **Publicador** (BackgroundService, 1/min): publicações `Aprovada` com hora ≤ agora → reserva (`APublicar`, UPDATE condicional) → publica cada destino suportado (FB, IG, Threads; TikTok fica manual) → grava `ExternalId` logo após cada sucesso (idempotência) → falhas com backoff 2/10/30 min, à 3.ª `Falhou` + push. Token inválido (erro 190) marca a ligação como inválida e avisa.
- **Media**: URLs pré-assinados do R2 com 24 h — o Meta e o Threads vão buscar os ficheiros diretamente.

## 13. Sprint 603 — TikTok segundo as regras de publicação direta (10/11/2026)

O TikTok só aprova apps de publicação direta se o utilizador escolher tudo no momento
(https://developers.tiktok.com/doc/content-sharing-guidelines). Por isso:

- **Ecrã:** o cartão de aprovação tem o painel «Publicar no TikTok» (`TikTokPainel`):
  - conta lida na hora (`GET /api/redes/tiktok/criador` → creator_info);
  - texto editável (até 2200 caracteres);
  - privacidade escolhida numa lista sem valor por omissão, só com as opções que a conta permite;
  - comentários, dueto e stitch desligados até os ligar, e bloqueados se a conta os desligou;
  - divulgação comercial (a tua marca / conteúdo de marca, este último não pode ser «Só eu»);
  - consentimento com os links Music Usage Confirmation / Branded Content Policy;
  - aviso de que o processamento demora.
- **Gravação:** `PUT /api/conteudo/{id}/tiktok` (só o dono) grava em `ConteudoDestino.OpcoesJson` + `LegendaPropria`.
- **Aprovação:** um reel com TikTok pendente e conta ligada só se aprova com a privacidade escolhida.
- **Publicador:**
  - sem opções não pega no destino TikTok (também filtrado na query);
  - antes de publicar relê o creator_info: se a privacidade já não é permitida ou o vídeo excede `max_video_post_duration_sec`, falha com mensagem clara;
  - envia `disable_*` e `brand_*_toggle` conforme as escolhas.
- **Definições:** a privacidade deixou de ser escolhida em Definições (o campo `Tenant.TikTokPrivacidade` fica sem uso).
- **Enquanto a app não está auditada:** só SELF_ONLY, e a conta TikTok tem de estar privada no momento da publicação.
