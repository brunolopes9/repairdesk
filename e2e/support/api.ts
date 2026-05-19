import { expect, type APIRequestContext, type APIResponse } from '@playwright/test';
import { e2eEnv } from './env';

type Json = Record<string, unknown>;

export interface RepairDeskCliente {
  id: string;
  nome: string;
  telefone: string | null;
}

export interface RepairDeskReparacao {
  id: string;
  numero: number;
  cliente: RepairDeskCliente;
  equipamento: string;
  avaria: string;
  imei: string | null;
  diagnostico: string | null;
  estado: number;
  orcamentoCents: number | null;
  orcamentoAprovado: boolean;
  precoFinalCents: number | null;
  custoPecasCents: number;
  horasGastas: number;
  notas: string | null;
  estadoPagamento: number;
  publicSlug: string | null;
  invoiceExternalId: string | null;
  invoiceNumber: string | null;
}

export interface RepairDeskVenda {
  id: string;
  numero: number;
  cliente: RepairDeskCliente | null;
  status: number;
  paymentMethod: number;
  totalCents: number;
  invoiceExternalId: string | null;
  invoiceNumber: string | null;
}

export interface RepairDeskPart {
  id: string;
  nome: string;
  sku: string | null;
  qtdStock: number;
  custoUnitarioCents: number;
}

export class RepairDeskApi {
  private accessToken: string | null = null;

  constructor(private readonly request: APIRequestContext) {}

  async reset(): Promise<void> {
    const response = await this.request.post(`${e2eEnv.apiURL}/e2e/reset`, {
      headers: e2eEnv.resetKey ? { 'X-E2E-Key': e2eEnv.resetKey } : undefined,
    });
    await this.expectOk(response, 'reset database');
    this.accessToken = null;
  }

  async login(): Promise<void> {
    const response = await this.request.post(`${e2eEnv.apiURL}/auth/login`, {
      data: {
        email: e2eEnv.adminEmail,
        password: e2eEnv.adminPassword,
      },
    });
    const body = await this.expectJson<{ accessToken: string }>(response, 'login');
    this.accessToken = body.accessToken;
  }

  async completeOnboarding(): Promise<void> {
    await this.post('/tenant-settings/me/onboarding/complete', {});
  }

  async configureBilling(): Promise<void> {
    await this.put('/tenant-settings/me/billing', {
      provider: 1,
      apiKey: 'e2e-api-key',
      clientId: null,
      clientSecret: null,
      refreshToken: 'e2e-refresh-token',
      companyId: 1,
      defaultDocumentType: 0,
      defaultSerieId: 1,
      sandboxMode: true,
      defaultProductId: 10,
      defaultTaxId: 23,
      defaultPaymentMethodId: 30,
      defaultMaturityDateId: 40,
      fallbackCustomerId: 50,
      exemptionReason: null,
    });
  }

  createCliente(overrides: Partial<Json> = {}): Promise<RepairDeskCliente> {
    return this.post<RepairDeskCliente>('/clientes', {
      nome: `Cliente E2E ${Date.now()}`,
      telefone: '912345678',
      email: null,
      nif: null,
      notas: null,
      ...overrides,
    });
  }

  createPart(overrides: Partial<Json> = {}): Promise<RepairDeskPart> {
    const stamp = Date.now();
    return this.post<RepairDeskPart>('/parts', {
      sku: `E2E-${stamp}`,
      nome: `Artigo E2E ${stamp}`,
      categoria: 99,
      marca: 'E2E',
      modelo: null,
      priceTableEntryId: null,
      qtdStock: 5,
      qtdMinima: 1,
      custoUnitarioCents: 1299,
      fornecedor: 'E2E',
      localArmazenamento: 'Balcao',
      notas: null,
      ...overrides,
    });
  }

  getPart(id: string): Promise<RepairDeskPart> {
    return this.get<RepairDeskPart>(`/parts/${id}`);
  }

  createReparacao(clienteId: string, overrides: Partial<Json> = {}): Promise<RepairDeskReparacao> {
    return this.post<RepairDeskReparacao>('/reparacoes', {
      clienteId,
      equipamento: `iPhone E2E ${Date.now()}`,
      avaria: 'Ecra partido',
      imei: null,
      orcamentoCents: 8900,
      notas: null,
      estadoInicial: 0,
      equipmentFieldTemplateId: null,
      fields: null,
      ...overrides,
    });
  }

  getRepair(id: string): Promise<{ reparacao: RepairDeskReparacao; timeline: Json[] }> {
    return this.get<{ reparacao: RepairDeskReparacao; timeline: Json[] }>(`/reparacoes/${id}`);
  }

  changeEstado(id: string, estado: number, notas: string | null = null): Promise<RepairDeskReparacao> {
    return this.post<RepairDeskReparacao>(`/reparacoes/${id}/estado`, { estado, notas });
  }

  async setRepairPayment(id: string, estadoPagamento: number): Promise<RepairDeskReparacao> {
    const detail = await this.getRepair(id);
    const r = detail.reparacao;
    return this.put<RepairDeskReparacao>(`/reparacoes/${id}`, {
      clienteId: r.cliente.id,
      equipamento: r.equipamento,
      avaria: r.avaria,
      imei: r.imei,
      diagnostico: r.diagnostico,
      orcamentoCents: r.orcamentoCents,
      orcamentoAprovado: r.orcamentoAprovado,
      precoFinalCents: r.precoFinalCents ?? r.orcamentoCents,
      custoPecasCents: r.custoPecasCents,
      horasGastas: r.horasGastas,
      notas: r.notas,
      estadoPagamento,
      equipmentFieldTemplateId: null,
      fields: null,
    });
  }

  emitRepairInvoice(id: string): Promise<{ number: string; pdfUrl: string | null; emittedAt: string }> {
    return this.post(`/reparacoes/${id}/emitir-fatura`, {
      vatPercent: 23,
      paymentMethod: 'MBWay',
    });
  }

  listReparacoesPagasSemFatura(): Promise<RepairDeskReparacao[]> {
    return this.get('/reparacoes/pagas-sem-fatura?limit=100');
  }

  bulkEmitReparacoes(ids: string[]): Promise<Array<{ id: string; success: boolean; invoiceNumber: string | null; errorMessage: string | null }>> {
    return this.post('/reparacoes/bulk-emit-faturas', { ids });
  }

  createVenda(payload: Json): Promise<RepairDeskVenda> {
    return this.post<RepairDeskVenda>('/vendas', payload);
  }

  payVenda(id: string, paymentMethod = 2, emitirFatura = false): Promise<{ venda: RepairDeskVenda; invoice: Json | null }> {
    return this.post(`/vendas/${id}/marcar-paga`, { paymentMethod, emitirFatura });
  }

  getVenda(id: string): Promise<RepairDeskVenda> {
    return this.get<RepairDeskVenda>(`/vendas/${id}`);
  }

  cancelVenda(id: string): Promise<RepairDeskVenda> {
    return this.post<RepairDeskVenda>(`/vendas/${id}/cancelar`, {});
  }

  async uploadRepairPhoto(reparacaoId: string, tipo: 0 | 1 | 2, legenda: string): Promise<Json> {
    const png = Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=',
      'base64',
    );
    const response = await this.request.post(`${e2eEnv.apiURL}/reparacoes/${reparacaoId}/fotos`, {
      headers: this.authHeaders(),
      multipart: {
        tipo: String(tipo),
        legenda,
        file: {
          name: `foto-${tipo}.png`,
          mimeType: 'image/png',
          buffer: png,
        },
      },
    });
    return this.expectJson<Json>(response, 'upload repair photo');
  }

  private get<T>(path: string): Promise<T> {
    return this.expectJson(this.request.get(`${e2eEnv.apiURL}${path}`, { headers: this.authHeaders() }), `GET ${path}`);
  }

  private post<T = Json>(path: string, data: Json): Promise<T> {
    return this.expectJson(this.request.post(`${e2eEnv.apiURL}${path}`, { headers: this.authHeaders(), data }), `POST ${path}`);
  }

  private put<T = Json>(path: string, data: Json): Promise<T> {
    return this.expectJson(this.request.put(`${e2eEnv.apiURL}${path}`, { headers: this.authHeaders(), data }), `PUT ${path}`);
  }

  private authHeaders(): Record<string, string> {
    if (!this.accessToken) throw new Error('RepairDeskApi.login() must run before authenticated calls.');
    return { Authorization: `Bearer ${this.accessToken}` };
  }

  private async expectOk(responseOrPromise: APIResponse | Promise<APIResponse>, label: string): Promise<APIResponse> {
    const response = await responseOrPromise;
    if (!response.ok()) {
      throw new Error(`${label} failed: HTTP ${response.status()} ${await response.text()}`);
    }
    return response;
  }

  private async expectJson<T>(responseOrPromise: APIResponse | Promise<APIResponse>, label: string): Promise<T> {
    const response = await this.expectOk(responseOrPromise, label);
    const body = (await response.json()) as T;
    expect(body).toBeTruthy();
    return body;
  }
}
