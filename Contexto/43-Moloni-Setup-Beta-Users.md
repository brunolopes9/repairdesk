# RepairDesk → Moloni — Guia para utilizadores beta

Este é o guia que vais entregar (ou que aparecerá no onboarding) quando uma nova oficina experimentar o RepairDesk e quiser ligar a sua Moloni.

> **Nota técnica:** Sprint 45 (Codex #C14) vai eliminar 5 dos 7 passos abaixo, automatizando-os.
> Enquanto não aterra, este guia ainda é necessário.

---

## Pré-requisitos

1. **Conta Moloni Flex (€10.90/mês) ou superior** — Solo e Base não têm API.
2. **Subutilizador AT criado** no Portal das Finanças com permissão `WSE — Comunicação de documentos`.
3. **Empresa configurada no Moloni** (NIF, morada, CAE, séries comunicadas à AT).

Se ainda não tens a conta Moloni, segue o guia em [`41-Moloni-Setup.md`](41-Moloni-Setup.md).

---

## Setup em 3 secções

### 1. Registo da aplicação no Moloni (uma única vez)

**No painel Moloni:** `Configurações → Developers → Configuração de conta e API`

Ativa a checkbox **"Ativar API"** e preenche:

- **Developer ID** — escolhe um identificador único, ex: `repairdesk-<o-teu-slug>`
- **URI de Resposta (Callback)** — `https://app.repairdesk.pt/api/billing/moloni/oauth/callback`
  (Moloni não aceita `localhost`; este é o URL público do RepairDesk)
- Clica **Atualizar** → vai aparecer a **Chave Secreta (Client Secret)** — copia-a para um sítio seguro

**No RepairDesk:** `Definições → Faturação → Secção 1`

- **Provider** = `Moloni`
- **Modo sandbox** = ❌ desligado (a sandbox é instável; usa produção desde o início)
- **Developer ID** = (cola o que registaste no painel Moloni)
- **Client Secret** = (cola a Chave Secreta)
- Clica **Guardar credenciais**

---

### 2. Ligar conta Moloni (uma única vez)

Clica **Ligar Moloni** → modal abre.

- **Email Moloni** — o email que usas para entrar em `moloni.pt`
- **Password Moloni** — a tua password (não é guardada no RepairDesk — só usada uma vez para obter tokens OAuth2)

> **Segurança:** se preferires, podes criar um **subutilizador Moloni dedicado** ao RepairDesk
> com permissões mínimas (`Configurações → Utilizadores → Novo utilizador`). Recomendado se
> partilhas a oficina com mais pessoas.

Clica **Ligar**. Deve aparecer **"✓ Conta Moloni ligada"**.

A partir daqui, os tokens são renovados automaticamente. Só precisas de re-ligar se ficares
14+ dias sem emitir nenhuma fatura (raríssimo).

---

### 3. Configuração da empresa (5 minutos manuais)

> ⚠️ **Esta secção vai desaparecer no Sprint 45** quando o RepairDesk auto-descobrir tudo.
> Por agora segue os passos.

#### 3.1 Company ID (obrigatório)

No Moloni, abre a tua empresa. Olha à URL do browser:

```
https://www.moloni.pt/companies/12345/...
                                ^^^^^
                                Company ID
```

Cola `12345` no campo **Company ID** do RepairDesk.

> Se só tens 1 empresa no Moloni, o RepairDesk auto-seleccionou-a já quando ligaste.
> Confere o campo — se estiver preenchido, salta este passo.

#### 3.2 Tipo de documento

| Tipo | Quando usar |
|---|---|
| **Fatura simplificada** | B2C (consumidor final), pode ser sem NIF, máx. €1000 |
| **Fatura** | B2B (com NIF empresarial), qualquer valor |

Para uma oficina de reparações, **Fatura simplificada** cobre 90% dos casos.

#### 3.3 Série Moloni

Clica o botão **Sincronizar** ao lado do campo. O RepairDesk pede ao Moloni a lista de
séries que tens comunicadas à AT e mostra um dropdown. Escolhe a tua (geralmente `M`).

Se aparecer **Sem séries disponíveis**:
- Confirma que tens uma série criada no Moloni (`Configurações → Empresa → 5. Séries`)
- Confirma que essa série está **comunicada à AT** (`A. Tributária → Registo de séries → estado activo`)

#### 3.4 Produto/serviço ID

No Moloni: `Tabelas → Artigos → Novo Artigo`

Preenche:
- Nome: **Serviço de reparação**
- Tipo: **Serviço**
- IVA: **23%** (regime normal continente; ajusta se regiões autónomas)
- Unidade: **Unidade**

Guarda. Abre o artigo criado, o ID está na URL:
```
https://www.moloni.pt/products/edit/67890
                                    ^^^^^
                                    Product ID
```

Cola no RepairDesk em **Produto/serviço ID**.

#### 3.5 Tax ID IVA

No Moloni: `Tabelas → Impostos`. Procura o IVA aplicável ao teu regime:

| Regime | Taxa | Código |
|---|---|---|
| Normal continente | 23% | (a comum) |
| Normal Madeira | 22% | |
| Normal Açores | 16% | |
| Isenção art. 53 | 0% | usar motivo isenção M02 |

Edita o imposto. O ID está na URL ou na ficha. Cola em **Tax ID IVA**.

#### 3.6 Método pagamento ID

No Moloni: `Tabelas → Métodos de pagamento`. Escolhe o método **mais usado** na tua oficina:

- **Numerário** — vendas de balcão
- **MB WAY** — pagamento por telemóvel
- **Multibanco** — referência ou contactless
- **Transferência bancária** — B2B

Edita → ID → cola em **Método pagamento ID**.

#### 3.7 Prazo vencimento ID

No Moloni: `Tabelas → Datas de vencimento`. Para reparações (pago à entrega), escolhe
**Pronto pagamento**. Cola o ID.

Se faturas B2B com 30 dias, escolhe **30 dias** em vez.

#### 3.8 Cliente fallback ID

Este é o cliente Moloni usado quando emites uma fatura para alguém que **não te deu NIF**.

No Moloni: `Clientes → Novo cliente`. Preenche:

- **Nome:** Consumidor final
- **NIF:** `999999990` (consumidor anónimo nacional)
- **Morada:** Desconhecido
- **Localidade:** Desconhecido
- **Código Postal:** `0000-000`
- **País:** Portugal

Guarda. Abre o cliente, ID na URL. Cola em **Cliente fallback ID**.

#### 3.9 Motivo isenção (só regime de isenção)

> Se estás em **regime normal IVA**: deixa este campo **VAZIO**. O RepairDesk esconde-o automaticamente.

Se estás em **Isenção Art. 53**, mete `M02`. Confirma com o teu contabilista.

---

## 4. Validar setup

Clica **Testar emissão** no fim da página de Definições. O RepairDesk faz uma chamada
real à Moloni e devolve "Ligação validada" se tudo está OK.

Se aparecer erro, consulta [`42-Moloni-Troubleshooting.md`](42-Moloni-Troubleshooting.md).

---

## 5. Emitir a primeira fatura

1. Vai a **Reparações**
2. Abre uma reparação que esteja marcada como **Paga**
3. No detalhe, vais ver botão **Emitir fatura via Moloni**
4. Clica
5. Confirma o IVA e método de pagamento sugeridos
6. Clica **Emitir**

A fatura é criada no Moloni com:
- Cliente: o cliente da reparação (se tem NIF) ou o Consumidor Final (fallback)
- Linha única: "Serviço de reparação" com o preço final
- IVA: da configuração
- Série: a `M`

A fatura aparece em **eFatura** dentro de 24h se a comunicação AT está configurada (Moloni
faz isto automaticamente).

---

## FAQ rápido

**Q: Posso usar o RepairDesk sem ligar Moloni?**
A: Sim, mas não emite faturas legais. Vais ter de emitir manualmente no painel Moloni e
   colar o número de fatura na reparação.

**Q: O RepairDesk substitui o Moloni?**
A: Não. RepairDesk é o **operacional** (workflow de reparações, fotos, garantias, portal cliente).
   Moloni é o **motor fiscal** (faturação certificada AT). São complementares.

**Q: Quanto custa por mês para usar tudo?**
A: RepairDesk: a definir (beta gratuito). Moloni Flex: €10.90/mês (anual) = ~€130/ano.

**Q: E se o Moloni mudar a API?**
A: O RepairDesk usa a API estável v1 documentada em https://www.moloni.pt/dev/.
   Se mudar (raro), publicamos update do RepairDesk em <24h.

**Q: Como cancelar?**
A: Em `Definições → Faturação → Secção 2`, clicar **Desligar**. Apaga os tokens (revoga
   o acesso). A tua conta Moloni continua intacta com todas as faturas históricas.
