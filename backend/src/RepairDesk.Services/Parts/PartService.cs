using FluentValidation;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Webhooks;

namespace RepairDesk.Services.Parts;

public interface IPartService
{
    Task<PagedResult<PartDto>> SearchAsync(string? query, PartCategoria? categoria, string? marca, bool lowStockOnly, int page, int pageSize, CancellationToken ct = default);
    Task<PartDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PartDto>> LowStockAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> MarcasAsync(CancellationToken ct = default);
    Task<PartDto> CreateAsync(CreatePartRequest req, CancellationToken ct = default);
    Task<PartDto> UpdateAsync(Guid id, UpdatePartRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<PartMovimentoDto> AddMovimentoAsync(Guid partId, CreatePartMovimentoRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<PartMovimentoDto>> MovimentosAsync(Guid? partId, Guid? reparacaoId, CancellationToken ct = default);
    Task<ImportPartsResponse> ImportCsvAsync(string csv, CancellationToken ct = default);
    /// <summary>Sprint 186: previsão reabastecer — Parts onde consumo30d &gt;= stock.</summary>
    Task<IReadOnlyList<ReabastecerSugestao>> ReabastecerSugestoesAsync(int days = 30, CancellationToken ct = default);
}

public class PartService : IPartService
{
    private readonly IPartRepository _repo;
    private readonly IReparacaoRepository _reparacoes;
    private readonly IValidator<CreatePartRequest> _createValidator;
    private readonly IValidator<UpdatePartRequest> _updateValidator;
    private readonly IValidator<CreatePartMovimentoRequest> _movimentoValidator;
    private readonly IWebhookPublisher _webhooks;
    private readonly ITenantContext _tenant;

    public PartService(
        IPartRepository repo,
        IReparacaoRepository reparacoes,
        IValidator<CreatePartRequest> createValidator,
        IValidator<UpdatePartRequest> updateValidator,
        IValidator<CreatePartMovimentoRequest> movimentoValidator,
        IWebhookPublisher webhooks,
        ITenantContext tenant)
    {
        _repo = repo;
        _reparacoes = reparacoes;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _webhooks = webhooks;
        _tenant = tenant;
        _movimentoValidator = movimentoValidator;
    }

    public async Task<PagedResult<PartDto>> SearchAsync(string? query, PartCategoria? categoria, string? marca, bool lowStockOnly, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _repo.SearchAsync(query, categoria, marca, lowStockOnly, page, pageSize, ct);
        return new PagedResult<PartDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<PartDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var part = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Part", id);
        return ToDto(part);
    }

    public async Task<IReadOnlyList<PartDto>> LowStockAsync(CancellationToken ct = default)
        => (await _repo.LowStockAsync(ct)).Select(ToDto).ToList();

    public Task<IReadOnlyList<ReabastecerSugestao>> ReabastecerSugestoesAsync(int days = 30, CancellationToken ct = default)
        => _repo.ReabastecerSugestoesAsync(Math.Clamp(days, 7, 90), ct);

    public Task<IReadOnlyList<string>> MarcasAsync(CancellationToken ct = default)
        => _repo.MarcasAsync(ct);

    public async Task<PartDto> CreateAsync(CreatePartRequest req, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(req, ct);
        var sku = NormalizeSku(req.Sku);
        if (sku is not null && await _repo.SkuExistsAsync(sku, null, ct))
            throw new ConflictException("sku_in_use", "Ja existe uma peça com esse SKU.");

        // Auto-SKU se não fornecido — resolve dor identificada em
        // Contexto/37-Insights-Mercado-Reddit.md (RepairQ tem SKU manual fraco).
        // Formato: {PREFIX}-{NNNN} onde prefix vem da categoria.
        if (sku is null)
        {
            sku = await GenerateNextSkuAsync(req.Categoria, ct);
        }

        var part = new Part
        {
            Sku = sku,
            Nome = req.Nome.Trim(),
            Categoria = req.Categoria,
            Marca = TrimOrNull(req.Marca),
            Modelo = TrimOrNull(req.Modelo),
            PriceTableEntryId = req.PriceTableEntryId,
            QtdStock = req.QtdStock,
            QtdMinima = req.QtdMinima,
            CustoUnitarioCents = req.CustoUnitarioCents,
            Fornecedor = TrimOrNull(req.Fornecedor),
            LocalArmazenamento = TrimOrNull(req.LocalArmazenamento),
            Notas = TrimOrNull(req.Notas),
            Activo = true,
            MostrarLojaOnline = req.MostrarLojaOnline,
        };
        await _repo.AddAsync(part, ct);
        await _repo.SaveAsync(ct);
        // Sprint 125: notifica loja online se este Part vai aparecer no catálogo público.
        if (part.MostrarLojaOnline) await PublishCatalogEventAsync(WebhookEvents.PartsAdicionado, part, ct);
        // Sprint 130: peça nova já abaixo do threshold (ex: importação CSV com stock 0) — alerta.
        if (part.Activo && IsStockBaixo(part.QtdStock, part.QtdMinima))
            await PublishCatalogEventAsync(WebhookEvents.PartsStockBaixo, part, ct);
        return ToDto(part);
    }

    public async Task<PartDto> UpdateAsync(Guid id, UpdatePartRequest req, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(req, ct);
        var part = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Part", id);
        var sku = NormalizeSku(req.Sku);
        if (sku is not null && await _repo.SkuExistsAsync(sku, id, ct))
            throw new ConflictException("sku_in_use", "Ja existe uma peça com esse SKU.");

        var previousMostrar = part.MostrarLojaOnline;
        var previousStockOk = !IsStockBaixo(part.QtdStock, part.QtdMinima);

        part.Sku = sku;
        part.Nome = req.Nome.Trim();
        part.Categoria = req.Categoria;
        part.Marca = TrimOrNull(req.Marca);
        part.Modelo = TrimOrNull(req.Modelo);
        part.PriceTableEntryId = req.PriceTableEntryId;
        part.QtdStock = req.QtdStock;
        part.QtdMinima = req.QtdMinima;
        part.CustoUnitarioCents = req.CustoUnitarioCents;
        part.Fornecedor = TrimOrNull(req.Fornecedor);
        part.LocalArmazenamento = TrimOrNull(req.LocalArmazenamento);
        part.Notas = TrimOrNull(req.Notas);
        part.Activo = req.Activo;
        part.MostrarLojaOnline = req.MostrarLojaOnline;
        await _repo.SaveAsync(ct);

        // Sprint 125: 3 cenários de webhook conforme transição da flag.
        if (!previousMostrar && part.MostrarLojaOnline)
            await PublishCatalogEventAsync(WebhookEvents.PartsAdicionado, part, ct);
        else if (previousMostrar && !part.MostrarLojaOnline)
            await PublishCatalogEventAsync(WebhookEvents.PartsRemovido, part, ct);
        else if (part.MostrarLojaOnline)
            await PublishCatalogEventAsync(WebhookEvents.PartsAtualizado, part, ct);

        // Sprint 130: stock baixo só dispara na transição above→below (evita spam).
        if (part.Activo && previousStockOk && IsStockBaixo(part.QtdStock, part.QtdMinima))
            await PublishCatalogEventAsync(WebhookEvents.PartsStockBaixo, part, ct);

        return ToDto(part);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var part = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Part", id);
        var wasInCatalog = part.MostrarLojaOnline;
        _repo.Remove(part);
        await _repo.SaveAsync(ct);
        if (wasInCatalog) await PublishCatalogEventAsync(WebhookEvents.PartsRemovido, part, ct);
    }

    private async Task PublishCatalogEventAsync(string eventType, Part part, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId) return;
        await _webhooks.PublishAsync(tenantId, eventType, new
        {
            partId = part.Id,
            sku = part.Sku,
            nome = part.Nome,
            categoria = part.Categoria.ToString(),
            marca = part.Marca,
            modelo = part.Modelo,
            qtdStock = part.QtdStock,
            mostrarLojaOnline = part.MostrarLojaOnline,
        }, ct);
    }

    public async Task<PartMovimentoDto> AddMovimentoAsync(Guid partId, CreatePartMovimentoRequest req, CancellationToken ct = default)
    {
        await _movimentoValidator.ValidateAndThrowAsync(req, ct);
        ValidateDeltaSign(req);

        var part = await _repo.FindByIdAsync(partId, ct) ?? throw new NotFoundException("Part", partId);
        if (!part.Activo)
            throw new ConflictException("part_inactive", "Peça inactiva. Reactiva a peça antes de movimentar stock.");

        Reparacao? reparacao = null;
        if (req.ReparacaoId is not null)
        {
            reparacao = await _reparacoes.FindByIdAsync(req.ReparacaoId.Value, ct)
                ?? throw new NotFoundException("Reparacao", req.ReparacaoId.Value);

            // Sprint 198: lock — após reparação Entregue+Pago, não permite mexer em PartMovimentos.
            // Defesa em profundidade: UI já esconde botões (readOnly), backend valida igualmente.
            // Bruno reportou: conseguia eliminar peça de reparação encerrada e a contabilidade
            // ficava errada.
            var isEntregue = reparacao.EntregueEm != null;
            var isPago = reparacao.EstadoPagamento == PaymentStatus.Pago;
            if (isEntregue && isPago)
                throw new ConflictException("reparacao_encerrada",
                    "Esta reparação está entregue e paga. Não é possível alterar peças usadas. " +
                    "Se precisas mesmo, abre Auditoria e regista a correção manualmente.");
        }

        var stockAntes = part.QtdStock;
        var stockDepois = stockAntes + req.Quantidade;
        if (stockDepois < 0)
            throw new ConflictException("stock_negativo", "Stock nao pode ficar negativo.");

        var previousStockOk = !IsStockBaixo(stockAntes, part.QtdMinima);
        part.QtdStock = stockDepois;
        var movimento = new PartMovimento
        {
            PartId = part.Id,
            Quantidade = req.Quantidade,
            StockAntes = stockAntes,
            StockDepois = stockDepois,
            Motivo = req.Motivo,
            ReparacaoId = reparacao?.Id,
            Notas = TrimOrNull(req.Notas),
        };
        _repo.AddMovimento(movimento);
        await _repo.SaveAsync(ct);

        if (reparacao is not null)
        {
            reparacao.CustoPecasCents = await _repo.SumCustoByReparacaoAsync(reparacao.Id, ct);
            await _reparacoes.SaveAsync(ct);
        }

        movimento.Part = part;

        // Sprint 130: alerta stock baixo se este movimento empurrou a peça abaixo do mínimo.
        if (part.Activo && previousStockOk && IsStockBaixo(part.QtdStock, part.QtdMinima))
            await PublishCatalogEventAsync(WebhookEvents.PartsStockBaixo, part, ct);

        return ToMovimentoDto(movimento);
    }

    /// <summary>
    /// Sprint 130: peça está abaixo do mínimo. QtdMinima=0 desliga o alerta (nunca low stock).
    /// </summary>
    private static bool IsStockBaixo(int qtdStock, int qtdMinima)
        => qtdMinima > 0 && qtdStock <= qtdMinima;

    public async Task<IReadOnlyList<PartMovimentoDto>> MovimentosAsync(Guid? partId, Guid? reparacaoId, CancellationToken ct = default)
        => (await _repo.MovimentosAsync(partId, reparacaoId, ct)).Select(ToMovimentoDto).ToList();

    public async Task<ImportPartsResponse> ImportCsvAsync(string csv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new RepairDesk.Core.Exceptions.ValidationException("csv_vazio", "CSV vazio.");

        var rows = CsvParser.Parse(csv);
        if (rows.Count < 2)
            throw new RepairDesk.Core.Exceptions.ValidationException("csv_sem_dados", "CSV precisa de header + pelo menos 1 linha de dados.");

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        int Idx(params string[] names) => header
            .Select((h, i) => new { h, i })
            .FirstOrDefault(x => names.Contains(x.h))?.i ?? -1;

        var iSku = Idx("sku", "referencia", "referência", "ref");
        var iNome = Idx("nome", "peca", "peça", "descricao", "descrição");
        var iCategoria = Idx("categoria", "tipo");
        var iMarca = Idx("marca");
        var iModelo = Idx("modelo");
        var iStock = Idx("stock", "qtd", "quantidade", "qtdstock");
        var iMin = Idx("minimo", "mínimo", "qtdminima", "qtd_minima", "stockminimo");
        var iCusto = Idx("custo", "custounitario", "custo_unitario", "custopeca");
        var iFornecedor = Idx("fornecedor");
        var iLocal = Idx("local", "localarmazenamento", "local_armazenamento", "prateleira");
        var iNotas = Idx("notas", "observacoes", "observações");

        if (iNome < 0)
            throw new RepairDesk.Core.Exceptions.ValidationException("csv_falta_coluna", "Coluna obrigatoria 'nome' nao encontrada. Aceito: sku,nome,categoria,marca,modelo,stock,minimo,custo,fornecedor,local,notas");

        var erros = new List<ImportError>();
        var criadas = new List<PartDto>();
        var ignoradas = 0;

        for (var i = 1; i < rows.Count; i++)
        {
            var linha = i + 1;
            var row = rows[i];
            string? Get(int idx) => idx >= 0 && idx < row.Length ? row[idx].Trim() : null;

            var nome = Get(iNome);
            if (string.IsNullOrWhiteSpace(nome))
            {
                erros.Add(new ImportError(linha, "nome", "Nome em branco.", null));
                continue;
            }

            var sku = NormalizeSku(Get(iSku));
            if (sku is not null && await _repo.SkuExistsAsync(sku, null, ct))
            {
                ignoradas++;
                continue;
            }

            try
            {
                var req = new CreatePartRequest(
                    sku,
                    nome,
                    ParseCategoria(Get(iCategoria)),
                    Get(iMarca),
                    Get(iModelo),
                    null,
                    ParseInt(Get(iStock)) ?? 0,
                    ParseInt(Get(iMin)) ?? 0,
                    ParseEuros(Get(iCusto)) ?? 0,
                    Get(iFornecedor),
                    Get(iLocal),
                    Get(iNotas));
                criadas.Add(await CreateAsync(req, ct));
            }
            catch (FluentValidation.ValidationException vex)
            {
                var first = vex.Errors.FirstOrDefault();
                erros.Add(new ImportError(linha, first?.PropertyName ?? "?", first?.ErrorMessage ?? vex.Message, nome));
            }
            catch (Exception ex)
            {
                erros.Add(new ImportError(linha, "?", ex.Message, nome));
            }
        }

        return new ImportPartsResponse(rows.Count - 1, criadas.Count, ignoradas, erros.Count, criadas, erros);
    }

    private static void ValidateDeltaSign(CreatePartMovimentoRequest req)
    {
        var ok = req.Motivo switch
        {
            PartMovimentoMotivo.Entrada or PartMovimentoMotivo.Devolucao => req.Quantidade > 0,
            PartMovimentoMotivo.Saida or PartMovimentoMotivo.UsoEmReparacao or PartMovimentoMotivo.VendaCliente => req.Quantidade < 0,
            PartMovimentoMotivo.AjusteManual => req.Quantidade != 0,
            _ => false,
        };
        if (!ok)
            throw new RepairDesk.Core.Exceptions.ValidationException(
                "quantidade_sinal_invalido",
                "Entrada/Devolucao usam quantidade positiva; Saida/Uso em reparacao usam quantidade negativa.");
    }

    private static PartCategoria ParseCategoria(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return PartCategoria.Outro;
        var n = raw.Trim().ToLowerInvariant();
        if (int.TryParse(n, out var i) && Enum.IsDefined(typeof(PartCategoria), i))
            return (PartCategoria)i;
        return n switch
        {
            "ecra" or "ecrã" or "display" or "lcd" => PartCategoria.Ecra,
            "bateria" or "battery" => PartCategoria.Bateria,
            "conector" or "carga" or "charging" => PartCategoria.Conector,
            "camara" or "câmara" or "camera" => PartCategoria.Camara,
            "vidro" or "vidro traseiro" or "back glass" => PartCategoria.VidroTraseiro,
            "flex" or "cabo flex" => PartCategoria.CaboFlex,
            "tampa" or "capa traseira" => PartCategoria.Tampa,
            "adesivo" or "cola" => PartCategoria.Adesivo,
            "consumivel" or "consumível" => PartCategoria.Consumivel,
            _ => PartCategoria.Outro,
        };
    }

    private static int? ParseInt(string? raw)
        => int.TryParse(raw, out var value) ? value : null;

    private static int? ParseEuros(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().Replace("€", "").Replace(" ", "").Replace(" ", "");
        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            if (lastComma > lastDot) s = s.Replace(".", "").Replace(",", ".");
            else s = s.Replace(",", "");
        }
        else if (lastComma >= 0) s = s.Replace(",", ".");
        return decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? (int)Math.Round(d * 100)
            : null;
    }

    private static string? NormalizeSku(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToUpperInvariant();

    private static string? TrimOrNull(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

    /// <summary>
    /// Gera SKU automático no formato {PREFIX}-{NNNN}.
    /// Prefix vem da categoria (ECRA, BAT, CON, etc).
    /// Numero é o próximo livre dentro do tenant — itera até encontrar não-usado.
    /// </summary>
    private async Task<string> GenerateNextSkuAsync(PartCategoria categoria, CancellationToken ct)
    {
        var prefix = SkuPrefixFor(categoria);
        for (var n = 1; n <= 9999; n++)
        {
            var candidate = $"{prefix}-{n:0000}";
            if (!await _repo.SkuExistsAsync(candidate, null, ct))
            {
                return candidate;
            }
        }
        // Edge case: 9999 SKUs já em uso para esta categoria. Cai para timestamp.
        return $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }

    private static string SkuPrefixFor(PartCategoria categoria) => categoria switch
    {
        PartCategoria.Ecra => "ECRA",
        PartCategoria.Bateria => "BAT",
        PartCategoria.Conector => "CON",
        PartCategoria.Camara => "CAM",
        PartCategoria.VidroTraseiro => "VID",
        PartCategoria.CaboFlex => "FLX",
        PartCategoria.Tampa => "TMP",
        PartCategoria.Adesivo => "ADE",
        PartCategoria.Consumivel => "CSM",
        _ => "PCA",
    };

    private static PartDto ToDto(Part p) =>
        new(
            p.Id,
            p.Sku,
            p.Nome,
            p.Categoria,
            p.Marca,
            p.Modelo,
            p.PriceTableEntryId,
            p.QtdStock,
            p.QtdMinima,
            p.CustoUnitarioCents,
            p.QtdStock * p.CustoUnitarioCents,
            p.Fornecedor,
            p.LocalArmazenamento,
            p.Notas,
            p.Activo,
            // Sprint 139: alinhado com Sprint 130 — qtdMinima=0 desliga o alerta.
            // Peças "one-shot" (Bruno encomendou para 1 reparação, não quer alerta recorrente)
            // ficam com 0/0 e NÃO devem aparecer como Stock baixo.
            p.QtdMinima > 0 && p.QtdStock <= p.QtdMinima,
            p.CreatedAt,
            p.UpdatedAt,
            p.MostrarLojaOnline);

    private static PartMovimentoDto ToMovimentoDto(PartMovimento m) =>
        new(
            m.Id,
            m.PartId,
            m.Part?.Nome ?? "(peça)",
            m.Part?.Sku,
            m.Quantidade,
            m.StockAntes,
            m.StockDepois,
            m.Motivo,
            m.ReparacaoId,
            m.Notas,
            m.CreatedAt,
            m.Part?.CustoUnitarioCents ?? 0);
}
