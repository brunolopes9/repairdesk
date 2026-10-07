import { expect, type APIRequestContext, type APIResponse } from '@playwright/test';
import { e2eEnv } from './env';

type Json = Record<string, unknown>;

export interface RepairDeskCliente {
  id: string;
  nome: string;
  telefone: string | null;
}

/** Doc 94: reparação, serviço ou produto — tudo é uma Venda. */
export interface RepairDeskVenda {
  id: string;
  numero: number;
  tipo: number;
  estado: number;
  cliente: RepairDeskCliente | null;
  equipamento: string | null;
  totalCents: number;
  invoiceNumber: string | null;
  publicSlug: string | null;
  garantiaSlug: string | null;
}

export interface RepairDeskLote {
  linhaId: string;
  descricao: string;
  quantidadeEmStock: number;
}

export const VENDA_TIPO = { Produto: 0, Reparacao: 1, Servico: 2 } as const;
export const VENDA_ESTADO = { Orcamento: 0, AEsperaPeca: 1, Pronta: 2, Entregue: 3, Cancelada: 4, EmCurso: 5 } as const;

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
        login: e2eEnv.adminEmail,
        password: e2eEnv.adminPassword,
      },
    });
    const body = await this.expectJson<{ accessToken: string }>(response, 'login');
    this.accessToken = body.accessToken;
  }

  async completeOnboarding(): Promise<void> {
    await this.post('/tenant-settings/me/onboarding/complete', {});
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

  /** Cria um fornecedor nacional e uma compra com um lote de stock; devolve o id do lote. */
  async createLote(descricao: string, quantidade: number, precoUnitarioPago: number): Promise<string> {
    const fornecedor = await this.post<{ id: string }>('/fornecedores', {
      name: `Fornecedor E2E ${Date.now()}`,
      active: true,
      regimeIva: 0,
      pais: 'PT',
    });
    const compra = await this.post<{ linhas: { id: string }[] }>('/compras', {
      fornecedorId: fornecedor.id,
      data: new Date().toISOString().slice(0, 10),
      numeroFatura: `FT E2E ${Date.now()}`,
      numerosEncomenda: null,
      metodoPagamento: null,
      portesPagos: 0,
      portesIva: null,
      totalDocumento: null,
      notas: null,
      linhas: [{ id: null, descricao, quantidade, precoUnitarioPago, taxaIvaCompra: null, lucroUnitario: null, localizacao: null }],
    });
    return compra.linhas[0].id;
  }

  async stockDoLote(loteId: string): Promise<number> {
    const inventario = await this.get<RepairDeskLote[]>('/compras/inventario');
    return inventario.find((l) => l.linhaId === loteId)?.quantidadeEmStock ?? 0;
  }

  createVenda(payload: Json): Promise<RepairDeskVenda> {
    return this.post<RepairDeskVenda>('/vendas', payload);
  }

  /** Reparação em Orçamento com uma linha de mão de obra. */
  createReparacao(clienteId: string, equipamento: string, problema: string, valorCents: number): Promise<RepairDeskVenda> {
    return this.createVenda({
      tipo: VENDA_TIPO.Reparacao,
      clienteId,
      equipamento,
      problema,
      notas: null,
      linhas: [{ id: null, compraLinhaId: null, descricao: 'Mão de obra', quantidade: 1, precoUnitarioCents: valorCents }],
      estado: VENDA_ESTADO.Orcamento,
    });
  }

  mudarEstado(id: string, estado: number, paymentMethod: number | null = null): Promise<RepairDeskVenda> {
    return this.post<RepairDeskVenda>(`/vendas/${id}/estado`, { estado, paymentMethod });
  }

  getVenda(id: string): Promise<RepairDeskVenda> {
    return this.get<RepairDeskVenda>(`/vendas/${id}`);
  }

  async uploadRepairPhoto(vendaId: string, tipo: 0 | 1 | 2, legenda: string): Promise<Json> {
    const png = Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=',
      'base64',
    );
    const response = await this.request.post(`${e2eEnv.apiURL}/vendas/${vendaId}/fotos`, {
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
