# Push Notifications PWA - Portal Cliente

Ultima atualizacao: 2026-05-19  
Status: implementado na Sprint 46

## Objetivo

Permitir que o cliente final subscreva notificacoes Web Push no portal publico da reparacao. Quando a loja muda o estado da reparacao, o cliente recebe uma notificacao no browser/mobile e o clique abre de volta `/portal/{slug}`.

## Decisao tecnica

- Standard Web Push W3C, sem Firebase Cloud Messaging.
- Biblioteca backend: `WebPush` para VAPID + envio.
- Subscricoes guardadas por reparacao em `PushSubscriptions`.
- VAPID em config quando existir; se nao existir, o primeiro pedido gera chaves e guarda em `SystemSettings`.
- Envio assíncrono: `ReparacaoService.ChangeEstadoAsync` enfileira job; `PushNotificationWorker` consome e envia.
- Limpeza RGPD: `PushSubscriptionCleanupWorker` remove subscricoes de reparacoes entregues ha mais de 30 dias.

## Configuracao

Em producao, definir via environment variables, nunca em git:

```env
Push__Enabled=true
Push__VapidPublicKey=
Push__VapidPrivateKey=
Push__Subject=mailto:suporte@repairdesk.pt
Push__TtlSeconds=86400
Push__DeliveredRetentionDays=30
```

Se `VapidPublicKey` e `VapidPrivateKey` ficarem vazios, o backend gera um par no primeiro acesso a:

```http
GET /api/public/portal/push/vapid-public-key
```

Isto e pratico para beta, mas em producao e melhor gerar uma vez, guardar em secret manager/env vars e manter estavel.

## Endpoints

```http
GET /api/public/portal/push/vapid-public-key
POST /api/public/portal/{slug}/push/subscribe
DELETE /api/public/portal/{slug}/push/unsubscribe
```

Body de subscribe:

```json
{
  "endpoint": "https://updates.push.services.mozilla.com/wpush/v2/...",
  "expirationTime": null,
  "keys": {
    "p256dh": "...",
    "auth": "..."
  }
}
```

So sao guardados `endpoint`, `p256dh`, `auth`, `ReparacaoId` e `TenantId`. Nao sao guardados nome, telefone, email, NIF ou IMEI.

## Fluxo

1. Cliente abre `/portal/{slug}`.
2. Se o browser suportar Push API, aparece o card "Notificacoes".
3. Cliente clica "Receber notificacoes".
4. Frontend pede permissao ao browser, cria `PushSubscription` no service worker e envia para o backend.
5. Operador muda estado da reparacao.
6. Backend enfileira job e envia notificacao para todas as subscricoes dessa reparacao.
7. Service worker mostra a notificacao.
8. Click na notificacao abre `/portal/{slug}`.

## Limites conhecidos

- Safari iOS exige que o site esteja instalado como PWA para push funcionar.
- Se o cliente bloquear notificacoes no browser, o RepairDesk nao pode reverter isso; tem de ser nas definicoes do browser.
- Em localhost, o browser permite service worker/push por ser origem segura especial. Em producao, precisa HTTPS.
- Subscricoes expiradas ou removidas pelo browser sao apagadas quando o push provider responde 404/410.

## Verificacao manual

1. Abrir o portal publico de uma reparacao num browser com suporte push.
2. Clicar "Receber notificacoes" e aceitar permissao.
3. Confirmar no backend que existe linha em `PushSubscriptions`.
4. No backoffice, mudar estado da reparacao para `Pronto`.
5. Confirmar que aparece notificacao.
6. Clicar na notificacao e validar que abre `/portal/{slug}`.
7. Clicar "Deixar de receber" e confirmar que a linha e removida.

## Testes automatizados

Coberto em `PushNotificationsApiTests`:

- VAPID public key gera/persiste chaves.
- Subscribe guarda endpoint e chaves.
- Unsubscribe remove a subscricao.
- Envio usa as subscricoes da reparacao via sender falso.
