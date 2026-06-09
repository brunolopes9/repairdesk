using FluentValidation;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Despesas;

public interface IDespesaService
{
    Task<PagedResult<DespesaDto>> SearchAsync(string? query, DespesaCategoria? categoria, IReadOnlyCollection<DespesaCategoria>? categoriaIn, bool includeSupplierInvoiceImports, bool excludeSupplierInvoiceImports, DateTime? from, DateTime? to, Guid? trabalhoId, Guid? reparacaoId, bool? isRecorrente, int page, int pageSize, CancellationToken ct = default);
    Task<DespesaDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<DespesaDto> CreateAsync(CreateDespesaRequest req, CancellationToken ct = default);
    Task<DespesaDto> UpdateAsync(Guid id, UpdateDespesaRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ConvertDespesaToStockResult> ConvertToStockAsync(Guid id, ConvertDespesaToStockRequest req, CancellationToken ct = default);
}

public class DespesaService : IDespesaService
{
    private readonly IDespesaRepository _repo;
    private readonly IPartRepository _parts;
    private readonly IValidator<CreateDespesaRequest> _createV;
    private readonly IValidator<UpdateDespesaRequest> _updateV;

    public DespesaService(
        IDespesaRepository repo,
        IPartRepository parts,
        IValidator<CreateDespesaRequest> createV,
        IValidator<UpdateDespesaRequest> updateV)
    {
        _repo = repo;
        _parts = parts;
        _createV = createV;
        _updateV = updateV;
    }

    public async Task<PagedResult<DespesaDto>> SearchAsync(
        string? query, DespesaCategoria? categoria, IReadOnlyCollection<DespesaCategoria>? categoriaIn,
        bool includeSupplierInvoiceImports, bool excludeSupplierInvoiceImports, DateTime? from, DateTime? to,
        Guid? trabalhoId, Guid? reparacaoId, bool? isRecorrente, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _repo.SearchAsync(query, categoria, categoriaIn, includeSupplierInvoiceImports, excludeSupplierInvoiceImports, from, to, trabalhoId, reparacaoId, isRecorrente, page, pageSize, ct);
        return new PagedResult<DespesaDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<DespesaDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var d = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Despesa", id);
        return ToDto(d);
    }

    public async Task<DespesaDto> CreateAsync(CreateDespesaRequest req, CancellationToken ct = default)
    {
        await _createV.ValidateAndThrowAsync(req, ct);

        var d = new Despesa
        {
            Descricao = req.Descricao.Trim(),
            Categoria = req.Categoria,
            ValorCents = req.ValorCents,
            Data = req.Data ?? DateTime.UtcNow,
            Fornecedor = req.Fornecedor?.Trim(),
            NumeroEncomenda = req.NumeroEncomenda?.Trim(),
            Notas = req.Notas?.Trim(),
            TrabalhoId = req.TrabalhoId,
            ReparacaoId = req.ReparacaoId,
            IsCogs = req.IsCogs,
            IsRecorrente = req.IsRecorrente,
            PeriodicidadeMeses = req.IsRecorrente ? req.PeriodicidadeMeses : null,
            ReverseCharge = req.ReverseCharge,
        };
        await _repo.AddAsync(d, ct);
        await _repo.SaveAsync(ct);
        return ToDto(d);
    }

    public async Task<DespesaDto> UpdateAsync(Guid id, UpdateDespesaRequest req, CancellationToken ct = default)
    {
        await _updateV.ValidateAndThrowAsync(req, ct);
        var d = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Despesa", id);

        d.Descricao = req.Descricao.Trim();
        d.Categoria = req.Categoria;
        d.ValorCents = req.ValorCents;
        d.Data = req.Data;
        d.Fornecedor = req.Fornecedor?.Trim();
        d.NumeroEncomenda = req.NumeroEncomenda?.Trim();
        d.Notas = req.Notas?.Trim();
        d.TrabalhoId = req.TrabalhoId;
        d.ReparacaoId = req.ReparacaoId;
        d.IsCogs = req.IsCogs;
        d.IsRecorrente = req.IsRecorrente;
        d.PeriodicidadeMeses = req.IsRecorrente ? req.PeriodicidadeMeses : null;

        await _repo.SaveAsync(ct);
        return ToDto(d);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var d = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Despesa", id);
        _repo.Remove(d);
        await _repo.SaveAsync(ct);
    }

    /// <summary>
    /// Sprint 540: tira uma compra de inventário do limbo "Despesa-Peças" e cria uma Part real
    /// com movimento de entrada (fica visível no Stock e consumível em reparações). A despesa é
    /// removida (não duplicada) para a compra não contar duas vezes no Relatório IVA — o
    /// SumPecasCustoComIvaAsync soma PartMovimento Entrada + Despesas Peças/Material.
    /// É um MOVE com efeito fiscal NULO: mesmo valor, mesmo período.
    /// </summary>
    public async Task<ConvertDespesaToStockResult> ConvertToStockAsync(Guid id, ConvertDespesaToStockRequest req, CancellationToken ct = default)
    {
        var d = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Despesa", id);

        // Só compras de inventário fazem sentido como stock. Peças/Material já contam como
        // "compras de stock" dedutíveis (S178b) → mover para Entrada mantém esse efeito.
        // PecasUsadas é regime da margem (IVA não dedutível na compra) e OpEx não é stock.
        if (d.Categoria is not (DespesaCategoria.Pecas or DespesaCategoria.Material))
            throw new RepairDesk.Core.Exceptions.ValidationException("despesa_nao_convertivel",
                "Só despesas de categoria Peças ou Material podem ser convertidas em stock.");

        var quantidade = Math.Max(1, req.Quantidade);
        // net-zero: quantidade × custoUnitário reconstrói o ValorCents original (exato para qtd=1).
        var custoUnit = d.ValorCents / quantidade;

        var sku = string.IsNullOrWhiteSpace(req.Sku) ? null : req.Sku.Trim().ToUpperInvariant();
        if (sku != null && await _parts.SkuExistsAsync(sku, null, ct))
            throw new ConflictException("part_sku_exists", $"Já existe uma peça com SKU '{sku}'.");

        var part = new Part
        {
            TenantId = d.TenantId,
            Sku = sku,
            Nome = string.IsNullOrWhiteSpace(req.Nome) ? d.Descricao.Trim() : req.Nome.Trim(),
            Categoria = req.Categoria,
            Marca = req.Marca?.Trim(),
            Modelo = req.Modelo?.Trim(),
            CustoUnitarioCents = custoUnit,
            Fornecedor = d.Fornecedor,
            LocalArmazenamento = req.LocalArmazenamento?.Trim(),
            Notas = $"Convertida da despesa \"{d.Descricao}\"" + (string.IsNullOrWhiteSpace(d.NumeroEncomenda) ? "" : $" (Enc. {d.NumeroEncomenda})"),
            QtdStock = 0,
        };
        await _parts.AddAsync(part, ct);

        var movimento = new PartMovimento
        {
            TenantId = d.TenantId,
            PartId = part.Id,
            Quantidade = quantidade,
            StockAntes = 0,
            StockDepois = quantidade,
            Motivo = PartMovimentoMotivo.Entrada,
            ReverseCharge = d.ReverseCharge, // mantém o tratamento IVA da compra original (intra-UE)
            Notas = $"Entrada por conversão da despesa (compra a {d.Fornecedor ?? "fornecedor"}).",
        };
        _parts.AddMovimento(movimento);
        part.QtdStock += quantidade;

        var dataOriginal = d.Data;
        _repo.Remove(d); // soft-delete (interceptor) → reversível
        await _parts.SaveAsync(ct); // 1ª gravação — o interceptor carimba CreatedAt = agora

        // Preserva o PERÍODO de IVA: o relatório conta o PartMovimento por CreatedAt, mas a despesa
        // contava por Data. Sem isto, a dedução saltava do período original para hoje. O
        // StampAuditFields só reescreve CreatedAt em entidades Added; numa 2ª gravação (Modified) fica.
        if (movimento.CreatedAt != dataOriginal)
        {
            movimento.CreatedAt = dataOriginal;
            await _parts.SaveAsync(ct);
        }

        return new ConvertDespesaToStockResult(part.Id, part.Nome, quantidade, custoUnit);
    }

    private static DespesaDto ToDto(Despesa d) =>
        new(d.Id, d.Descricao, d.Categoria, d.ValorCents, d.Data, d.Fornecedor, d.NumeroEncomenda, d.Notas,
            d.TrabalhoId, d.ReparacaoId, d.CreatedAt, d.IsCogs, d.IsRecorrente, d.PeriodicidadeMeses);
}
