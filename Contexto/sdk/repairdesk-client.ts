/**
 * RepairDesk External API Client (TypeScript)
 * =============================================
 *
 * Cliente HTTP zero-deps (fetch nativo) para integração da loja online
 * `shop.lopestech.pt` com o RepairDesk.
 *
 * Uso:
 *   const rd = new RepairDeskClient({
 *     baseUrl: process.env.REPAIRDESK_API_URL!,
 *     apiKey: process.env.REPAIRDESK_API_KEY!,  // server-side only
 *   });
 *
 *   const order = await rd.checkout({ ... });
 *
 * IMPORTANTE: a API key dá acesso TOTAL ao tenant. Nunca exponhas no client-side
 * do Next.js — usa apenas em route handlers / API routes / server actions.
 *
 * Espelha os endpoints `/api/external/*` do RepairDesk backend.
 */

// =================================================================
// TYPES — espelham os DTOs C# do backend
// =================================================================

export type PaymentMethod =
  | 0  // Dinheiro
  | 1  // Multibanco
  | 2  // MBWay
  | 3  // TransferenciaBancaria
  | 4  // Cartao
  | 99; // Outro

export type VendaOrigem =
  | 0  // Balcao
  | 1  // Online
  | 2; // Importacao

export type PartCategoria =
  | 0  // Ecra
  | 1  // Bateria
  | 2  // Conector
  | 3  // Camara
  | 4  // VidroTraseiro
  | 5  // CaboFlex
  | 6  // Tampa
  | 7  // Adesivo
  | 8  // Consumivel
  | 9  // Smartphone
  | 10 // Tablet
  | 11 // Acessorio
  | 99; // Outro

export interface CheckoutCliente {
  nome: string;
  telefone?: string | null;
  email?: string | null;
  /** NIF PT (9 dígitos) — para fatura. Sem NIF, fatura sai a "Consumidor Final". */
  nif?: string | null;
  notas?: string | null;
}

export type CondicaoArtigo =
  | 0  // NaoAplicavel
  | 1  // Novo
  | 2  // OpenBox
  | 3  // Recondicionado
  | 4; // Usado

export interface CheckoutItem {
  /** PartId do RepairDesk para acessórios em stock. Null para descrição livre (Molano dropship). */
  partId?: string | null;
  /** Descrição (obrigatória se partId não fornecido). */
  descricao?: string | null;
  quantidade: number;
  precoUnitarioCents: number;
  descontoCents: number;
  /** Taxa IVA: 0, 6, 13 ou 23 (PT). */
  ivaRate: number;
  /** IMEI obrigatório para Smartphone/Tablet — validação Luhn server-side. */
  imei?: string | null;
  imei2?: string | null;
  /**
   * Sprint 109: garantia upstream (B2B fornecedor → RepairDesk). Snapshot na venda.
   * Quando preenchido, o ReparacaoDetalhe do RepairDesk mostra automaticamente se a
   * cobertura do fornecedor ainda está activa numa reparação futura deste IMEI.
   */
  fornecedorNome?: string | null;
  condicao?: CondicaoArtigo | null;
  /** ISO date (YYYY-MM-DD ou ISO 8601 completo) — até quando o fornecedor cobre. */
  garantiaFornecedorAteAo?: string | null;
}

export interface CheckoutRequest {
  cliente: CheckoutCliente;
  items: CheckoutItem[];
  paymentMethod: PaymentMethod;
  /** Default true — emite fatura via Moloni/InvoiceXpress automaticamente. */
  emitirFatura?: boolean;
  notas?: string | null;
  /** Default Online (1) para integrações external. */
  origem?: VendaOrigem;
}

export interface CheckoutResponse {
  vendaId: string;
  vendaNumero: number;
  clienteId: string;
  /** true se o cliente foi criado nesta chamada (NIF novo). */
  clienteCreated: boolean;
  totalCents: number;
  ivaCents: number;
  faturaNumero: string | null;
  faturaPdfUrl: string | null;
  /** Slug público da garantia: shop.lopestech.pt/garantia/{slug} OU api.repairdesk/g/{slug} */
  garantiaSlug: string | null;
}

export interface OrderStatusResponse {
  vendaId: string;
  vendaNumero: number;
  data: string;
  clienteId: string | null;
  totalCents: number;
  ivaCents: number;
  status: 'Pendente' | 'Paga' | 'Cancelada';
  origem: 'Balcao' | 'Online' | 'Importacao';
  faturaNumero: string | null;
  faturaPdfUrl: string | null;
  faturaEmitidaEm: string | null;
  garantiaSlug: string | null;
  garantiaActiva: boolean;
  garantiaAnulada: boolean;
  canceladaEm: string | null;
}

export interface CancelOrderRequest {
  /** Motivo audit-logged. Opcional. */
  motivo?: string | null;
}

export interface ExternalClienteHistorico {
  clienteId: string;
  nome: string;
  email: string | null;
  telefone: string | null;
  vendas: ExternalVendaResumo[];
  reparacoes: ExternalReparacaoResumo[];
  garantiasActivas: ExternalGarantiaResumo[];
}

export interface ExternalVendaResumo {
  id: string;
  numero: number;
  data: string;
  totalCents: number;
  status: 'Pendente' | 'Paga' | 'Cancelada';
  origem: 'Balcao' | 'Online' | 'Importacao';
  faturaNumero: string | null;
  faturaPdfUrl: string | null;
}

export interface ExternalReparacaoResumo {
  id: string;
  numero: number;
  recebidoEm: string;
  equipamento: string;
  /** RepairStatus enum: 0=Recebido, 1=Diagnostico, 2=AguardaPeca, 3=EmReparacao, 4=Pronto, 5=Entregue, 6=Cancelado */
  estado: number;
  /** Slug do portal público de acompanhamento: /p/{slug}. */
  publicSlug: string | null;
}

export interface ExternalGarantiaResumo {
  slug: string;
  origem: 'Reparacao' | 'Venda';
  dataFim: string;
  diasRestantes: number;
  equipamento: string | null;
}

export interface ExternalHealthResponse {
  status: 'ok';
  /** ISO 8601 UTC do servidor RepairDesk — útil para detectar clock skew >5s. */
  serverTime: string;
  apiVersion: string;
  /** TenantId resolvido pela API key — confirma que a chave aponta para o tenant esperado. */
  tenantId: string | null;
}

export interface ExternalGarantiaDetalhe {
  slug: string;
  origem: 'Reparacao' | 'Venda';
  dataInicio: string;
  dataFim: string;
  diasGarantia: number;
  diasRestantes: number;
  activa: boolean;
  anulada: boolean;
  motivoAnulacao: string | null;
  equipamento: string;
  cobertura: string | null;
  exclusoes: string | null;
  documentoReferencia: string | null;
  numeroFatura: string | null;
}

export interface ExternalPart {
  id: string;
  sku: string | null;
  nome: string;
  categoria: PartCategoria;
  marca: string | null;
  modelo: string | null;
  /** Sprint 121: true se Bruno marcou esta peça para aparecer na loja online. */
  mostrarLojaOnline: boolean;
  qtdStock: number;
  activo: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface ListPartsQuery {
  search?: string;
  categoria?: PartCategoria;
  page?: number;
  pageSize?: number;
  /**
   * Sprint 121: filtra por flag `mostrarLojaOnline` em `Part`. Loja online faz cron sync com
   * `{ lojaOnline: true }` para obter o catálogo vendável. Omitir = devolve todos (default).
   */
  lojaOnline?: boolean;
}

// =================================================================
// ERROR — wrapping de respostas não-2xx
// =================================================================

export class RepairDeskError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly code: string | null = null,
    public readonly body: unknown = null,
  ) {
    super(message);
    this.name = 'RepairDeskError';
  }

  /** Útil para retry logic — 5xx + 408/429 são transientes; 4xx normais não. */
  get isTransient() {
    return this.status >= 500 || this.status === 408 || this.status === 429;
  }
}

// =================================================================
// CLIENT
// =================================================================

export interface RepairDeskClientOptions {
  baseUrl: string;
  apiKey: string;
  /** Timeout em ms (default 30000). */
  timeoutMs?: number;
  /** Número de tentativas em falhas transientes (default 2 = 3 total). */
  maxRetries?: number;
  /** Hook para observabilidade — chamado em cada request. */
  onRequest?: (req: { method: string; url: string }) => void;
}

export class RepairDeskClient {
  private readonly baseUrl: string;
  private readonly apiKey: string;
  private readonly timeoutMs: number;
  private readonly maxRetries: number;
  private readonly onRequest?: RepairDeskClientOptions['onRequest'];

  constructor(opts: RepairDeskClientOptions) {
    this.baseUrl = opts.baseUrl.replace(/\/+$/, '');
    this.apiKey = opts.apiKey;
    this.timeoutMs = opts.timeoutMs ?? 30_000;
    this.maxRetries = opts.maxRetries ?? 2;
    this.onRequest = opts.onRequest;
  }

  /**
   * Health check rápido — confirma que a API key é válida e devolve a hora do servidor.
   * Use no startup da loja online ou periodicamente para detectar:
   *  - API key revogada (401)
   *  - Clock skew (compara serverTime com Date.now())
   *  - Tenant errado (tenantId não bate com o esperado)
   */
  health(): Promise<ExternalHealthResponse> {
    return this.request('GET', '/api/external/health');
  }

  /** Fecha venda atómica (cliente lookup-or-create + venda + fatura + garantia). */
  checkout(req: CheckoutRequest): Promise<CheckoutResponse> {
    return this.request('POST', '/api/external/checkout', req);
  }

  /** Consulta estado da venda (incluindo garantia activa/anulada). */
  getOrder(vendaId: string): Promise<OrderStatusResponse> {
    return this.request('GET', `/api/external/orders/${vendaId}`);
  }

  /**
   * Cancela venda (devolução DL 24/2014 ou troca). Cascateia:
   * anula fatura Moloni/InvoiceXpress + repõe stock + anula garantia.
   * Idempotente — pode chamar 2x sem efeitos secundários.
   */
  cancelOrder(vendaId: string, motivo?: string): Promise<OrderStatusResponse> {
    return this.request('POST', `/api/external/orders/${vendaId}/cancel`,
      { motivo: motivo ?? null } satisfies CancelOrderRequest);
  }

  /** Lista catálogo de Parts ativas (acessórios para cross-sell). */
  listParts(query: ListPartsQuery = {}): Promise<PagedResult<ExternalPart>> {
    const params = new URLSearchParams();
    if (query.search) params.set('search', query.search);
    if (query.categoria !== undefined) params.set('categoria', String(query.categoria));
    if (query.lojaOnline !== undefined) params.set('lojaOnline', String(query.lojaOnline));
    if (query.page) params.set('page', String(query.page));
    if (query.pageSize) params.set('pageSize', String(query.pageSize));
    const qs = params.toString();
    return this.request('GET', `/api/external/parts${qs ? `?${qs}` : ''}`);
  }

  /**
   * Histórico agregado do cliente por NIF: vendas + reparações + garantias activas.
   * Para a página "Os meus pedidos" — não precisa de manter BD local de orders.
   * Devolve null se o NIF não corresponde a cliente do tenant (404 → null).
   */
  async getHistoricoByNif(nif: string): Promise<ExternalClienteHistorico | null> {
    try {
      return await this.request<ExternalClienteHistorico>(
        'GET',
        `/api/external/clientes/${encodeURIComponent(nif)}/historico`);
    } catch (e) {
      if (e instanceof RepairDeskError && e.status === 404) return null;
      throw e;
    }
  }

  /**
   * Detalhe de uma garantia por slug — útil para a loja online mostrar info no painel
   * cliente sem mascaramento. Devolve null se o slug não pertence ao tenant.
   */
  async getGarantia(slug: string): Promise<ExternalGarantiaDetalhe | null> {
    try {
      return await this.request<ExternalGarantiaDetalhe>(
        'GET',
        `/api/external/garantias/${encodeURIComponent(slug)}`);
    } catch (e) {
      if (e instanceof RepairDeskError && e.status === 404) return null;
      throw e;
    }
  }

  // ---------------- internals ----------------

  private async request<T>(
    method: 'GET' | 'POST',
    path: string,
    body?: unknown,
  ): Promise<T> {
    const url = `${this.baseUrl}${path}`;
    this.onRequest?.({ method, url });

    let lastErr: unknown = null;
    for (let attempt = 0; attempt <= this.maxRetries; attempt++) {
      try {
        return await this.requestOnce<T>(method, url, body);
      } catch (e) {
        lastErr = e;
        if (e instanceof RepairDeskError && !e.isTransient) throw e;
        if (attempt === this.maxRetries) throw e;
        // backoff exponencial: 200ms, 800ms, 3200ms (com jitter pequeno)
        const baseMs = 200 * Math.pow(4, attempt);
        await sleep(baseMs + Math.random() * 200);
      }
    }
    throw lastErr;
  }

  private async requestOnce<T>(
    method: 'GET' | 'POST',
    url: string,
    body?: unknown,
  ): Promise<T> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.timeoutMs);

    let resp: Response;
    try {
      resp = await fetch(url, {
        method,
        headers: {
          'X-Api-Key': this.apiKey,
          'Content-Type': 'application/json',
          'Accept': 'application/json',
        },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: controller.signal,
      });
    } catch (e) {
      if ((e as Error).name === 'AbortError') {
        throw new RepairDeskError(`Timeout após ${this.timeoutMs}ms: ${method} ${url}`, 408);
      }
      throw new RepairDeskError(`Erro de rede: ${(e as Error).message}`, 0, null, e);
    } finally {
      clearTimeout(timer);
    }

    const contentType = resp.headers.get('content-type') ?? '';
    const isJson = contentType.includes('application/json');
    const payload = isJson ? await resp.json().catch(() => null) : await resp.text().catch(() => null);

    if (!resp.ok) {
      const code = isJson && payload && typeof payload === 'object'
        ? (payload as Record<string, unknown>).code as string | null ?? null
        : null;
      const message = isJson && payload && typeof payload === 'object'
        ? (payload as Record<string, unknown>).title as string | undefined
          ?? (payload as Record<string, unknown>).detail as string | undefined
          ?? `${resp.status} ${resp.statusText}`
        : `${resp.status} ${resp.statusText}`;
      throw new RepairDeskError(message, resp.status, code, payload);
    }

    return (payload as T);
  }
}

function sleep(ms: number) {
  return new Promise<void>(r => setTimeout(r, ms));
}

// =================================================================
// EXEMPLO DE USO (delete antes de copiar para produção)
// =================================================================

/*
const rd = new RepairDeskClient({
  baseUrl: 'https://api.lopestech.pt',
  apiKey: process.env.REPAIRDESK_API_KEY!,
  onRequest: ({ method, url }) => console.log(`[rd] ${method} ${url}`),
});

// 1. Após checkout Stripe bem-sucedido (em /api/checkout-success.ts ou webhook handler):
const order = await rd.checkout({
  cliente: {
    nome: 'Maria Silva',
    email: 'maria@example.com',
    telefone: '+351912000000',
    nif: '504000004',
  },
  items: [
    {
      descricao: 'iPhone 13 Grade A 256GB Blue',
      quantidade: 1,
      precoUnitarioCents: 49900,
      descontoCents: 0,
      ivaRate: 23,
      imei: '490154203237518',
    },
  ],
  paymentMethod: 4,  // Cartao
  emitirFatura: true,
});

// 2. Email ao cliente:
//    - Recibo PDF: ${rd.baseUrl}/api/vendas/${order.vendaId}/recibo.pdf (precisa de JWT/admin)
//    - Fatura PDF: order.faturaPdfUrl (link direto Moloni)
//    - Garantia:   https://lopestech.pt/garantia/${order.garantiaSlug}

// 3. Cliente quer devolver dentro de 14d:
const cancelled = await rd.cancelOrder(order.vendaId, 'Cliente desistiu (14d direito livre)');

// 4. Listar capas para cross-sell:
const capas = await rd.listParts({ categoria: 11 });  // Acessorio

// 5. Página "Os meus pedidos" (server-side, após login do cliente):
const historico = await rd.getHistoricoByNif(session.user.nif);
if (historico) {
  console.log(`${historico.nome} tem ${historico.vendas.length} compras`);
  historico.garantiasActivas.forEach(g => {
    console.log(`Garantia ${g.equipamento}: ${g.diasRestantes} dias`);
    // link: https://app.lopestech.pt/g/${g.slug}
  });
}
*/
