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
 *
 * Helpers relacionados (importar separadamente):
 *   import { isValidPortugueseNIF, normalizePortugueseNIF } from './pt-nif';
 *   import { verifyWebhookSignature } from './webhook-verify';
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

/** Condição do artigo vendido (snapshot). Sprint 127. */
export type CondicaoArtigoName =
  | 'NaoAplicavel' | 'Novo' | 'OpenBox' | 'Recondicionado' | 'Usado';

/**
 * Sprint 127: cada item da venda com período de garantia individual calculado
 * a partir da `condicao` + defaults do tenant. A loja usa estes para mostrar
 * prazos correctos em `/conta/garantias` por produto.
 */
export interface CheckoutItemSummary {
  descricao: string;
  quantidade: number;
  condicao: CondicaoArtigoName;
  /** Dias de garantia legal para este item (DL 84/2021). */
  garantiaDias: number;
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
  /**
   * Sprint 127: garantia efectiva emitida (em dias) = `Max(items[].garantiaDias)`.
   * `null` apenas se a venda não tem items.
   */
  garantiaDiasEfectivo: number | null;
  /** Sprint 127: items com prazo individual (DL 84/2021 por condição). */
  items: CheckoutItemSummary[];
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
  /** Sprint 133: slug `/g/{slug}` da garantia desta venda. `null` se anulada ou ainda não emitida. */
  garantiaSlug?: string | null;
  /** Sprint 133: garantia activa (não expirada nem anulada). Loja mostra CTA "Ver garantia" só quando true. */
  garantiaActiva?: boolean;
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
  /** Sprint 133: slug `/g/{slug}` da garantia da reparação (60/90d). */
  garantiaSlug?: string | null;
  garantiaActiva?: boolean;
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
  /**
   * Sprint 132: peças com `qtdStock <= qtdMinima` (qtdMinima > 0). Usar em boot fresh ou
   * sanity check — o webhook `parts.stock-baixo` só entrega a transição original.
   */
  lowStockOnly?: boolean;
}

/** Sprint 122: catálogo de produtos vendáveis (telemóveis revendidos). */
export interface ExternalProduct {
  id: string;
  sku: string;
  slug: string;
  brand: string;
  model: string;
  storage: string | null;
  color: string | null;
  /** "Novo" | "GradeA" | "GradeB" | "GradeC" | "OpenBox" | "Premium" */
  grading: string;
  /**
   * Sprint 146: canonical estável para sync com a loja headless.
   * Valores: "Novo" | "A+" | "A" | "B" | "C" | "OpenBox".
   * Use isto em vez de `grading` quando construir filtros/ordenação na loja.
   */
  gradingCanonical: string;
  /**
   * Sprint 146: label PT user-friendly pronto para mostrar.
   * Valores: "Novo" | "Como novo" | "Excelente" | "Bom" | "Aceitável" | "Open Box".
   */
  gradingLabel: string;
  /** "Stock" | "Dropship" */
  supplyType: string;
  priceCents: number;
  stockQuantity: number;
  descriptionMarkdown: string | null;
  attributesJson: string | null;
  seoTitle: string | null;
  seoDescription: string | null;
  supplierName: string | null;
  imageUrls: string[];
  updatedAt: string;
}

export interface ListProductsQuery {
  search?: string;
  brand?: string;
  page?: number;
  pageSize?: number;
  /**
   * Sprint 132: produtos com `stockQuantity <= stockMinima` (stockMinima > 0). Mesma semântica
   * do equivalente em `parts`. Para a loja saber que phones precisa de hide automático após
   * boot fresh ou recovery de outage.
   */
  lowStockOnly?: boolean;
}

// =================================================================
// SUPPLIER INVOICES (Sprint 147+149) — ingest via n8n IMAP
// =================================================================

/**
 * Metadata do email original. Passada do n8n IMAP node ao endpoint ingest
 * para auditoria + correlação. Opcional — ingest funciona sem isto.
 */
export interface SupplierInvoiceEmailMeta {
  /** RFC 5322 Message-ID — único globalmente, util para dedupe ao nível n8n. */
  messageId?: string | null;
  subject?: string | null;
  from?: string | null;
  receivedAt?: string | null;
}

/** Status do registo após ingest. */
export type SupplierInvoiceImportStatusValue = 'Pending' | 'Approved' | 'Rejected' | 'Failed';

export interface SupplierInvoiceIngestResult {
  importId: string;
  /** 0=Pending, 1=Approved, 2=Rejected, 3=Failed — numeric enum do backend. */
  status: number;
  fornecedorNameRaw: string | null;
  fornecedorId: string | null;
  totalCents: number | null;
  documentNumber: string | null;
  pdfRelativePath: string;
  /** true quando o SHA256 do PDF já existia → seguro fazer retry idempotente. */
  wasDuplicate: boolean;
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
    if (query.lowStockOnly) params.set('lowStockOnly', 'true');
    if (query.page) params.set('page', String(query.page));
    if (query.pageSize) params.set('pageSize', String(query.pageSize));
    const qs = params.toString();
    return this.request('GET', `/api/external/parts${qs ? `?${qs}` : ''}`);
  }

  /**
   * Sprint 122: lista catálogo de Products (telemóveis revendidos). Filtra automaticamente
   * por Active + MostrarLojaOnline no backend — não precisas de passar nada.
   */
  listProducts(query: ListProductsQuery = {}): Promise<PagedResult<ExternalProduct>> {
    const params = new URLSearchParams();
    if (query.search) params.set('search', query.search);
    if (query.brand) params.set('brand', query.brand);
    if (query.lowStockOnly) params.set('lowStockOnly', 'true');
    if (query.page) params.set('page', String(query.page));
    if (query.pageSize) params.set('pageSize', String(query.pageSize));
    const qs = params.toString();
    return this.request('GET', `/api/external/products${qs ? `?${qs}` : ''}`);
  }

  /** Detalhe de um Product por slug. Devolve null em 404. */
  async getProduct(slug: string): Promise<ExternalProduct | null> {
    try {
      return await this.request<ExternalProduct>('GET', `/api/external/products/${encodeURIComponent(slug)}`);
    } catch (e) {
      if (e instanceof RepairDeskError && e.status === 404) return null;
      throw e;
    }
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

  /**
   * Sprint 149: submete um PDF de fatura de fornecedor (recebido via IMAP/email).
   * Tipicamente chamado por workflow n8n após `IMAP Trigger` apanhar email novo.
   *
   * Requer API key com scope `ingest`.
   *
   * @param pdfBytes  Bytes do PDF (vai ser encoded base64 antes de enviar).
   * @param emailMeta Metadata do email original — usado para auditoria + dedupe por messageId.
   *
   * Resultado:
   * - `wasDuplicate: true` quando o SHA256 do PDF já existe → seguro re-enviar idempotente.
   * - `status: "Pending"` quando o parser confirmou fornecedor + total → Bruno revê e aprova.
   * - `status: "Failed"` quando o parser não conseguiu extrair dados → ainda assim o PDF é
   *   guardado, mas Bruno terá de meter valores manuais antes de aprovar.
   */
  async ingestSupplierInvoice(
    pdfBytes: Uint8Array | ArrayBuffer,
    emailMeta?: SupplierInvoiceEmailMeta,
  ): Promise<SupplierInvoiceIngestResult> {
    const bytes = pdfBytes instanceof Uint8Array ? pdfBytes : new Uint8Array(pdfBytes);
    let binary = '';
    const chunk = 8192;
    for (let i = 0; i < bytes.byteLength; i += chunk) {
      binary += String.fromCharCode(...bytes.subarray(i, Math.min(i + chunk, bytes.byteLength)));
    }
    const pdfBase64 = btoa(binary);
    return this.request<SupplierInvoiceIngestResult>('POST', '/api/external/supplier-invoices/ingest', {
      pdfBase64,
      emailMeta: emailMeta ?? null,
    });
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
