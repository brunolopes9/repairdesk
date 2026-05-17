using FluentValidation;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Despesas;

public interface IDespesaService
{
    Task<PagedResult<DespesaDto>> SearchAsync(string? query, DespesaCategoria? categoria, DateTime? from, DateTime? to, Guid? trabalhoId, Guid? reparacaoId, int page, int pageSize, CancellationToken ct = default);
    Task<DespesaDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<DespesaDto> CreateAsync(CreateDespesaRequest req, CancellationToken ct = default);
    Task<DespesaDto> UpdateAsync(Guid id, UpdateDespesaRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class DespesaService : IDespesaService
{
    private readonly IDespesaRepository _repo;
    private readonly IValidator<CreateDespesaRequest> _createV;
    private readonly IValidator<UpdateDespesaRequest> _updateV;

    public DespesaService(
        IDespesaRepository repo,
        IValidator<CreateDespesaRequest> createV,
        IValidator<UpdateDespesaRequest> updateV)
    {
        _repo = repo;
        _createV = createV;
        _updateV = updateV;
    }

    public async Task<PagedResult<DespesaDto>> SearchAsync(
        string? query, DespesaCategoria? categoria, DateTime? from, DateTime? to,
        Guid? trabalhoId, Guid? reparacaoId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _repo.SearchAsync(query, categoria, from, to, trabalhoId, reparacaoId, page, pageSize, ct);
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

        await _repo.SaveAsync(ct);
        return ToDto(d);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var d = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Despesa", id);
        _repo.Remove(d);
        await _repo.SaveAsync(ct);
    }

    private static DespesaDto ToDto(Despesa d) =>
        new(d.Id, d.Descricao, d.Categoria, d.ValorCents, d.Data, d.Fornecedor, d.NumeroEncomenda, d.Notas,
            d.TrabalhoId, d.ReparacaoId, d.CreatedAt);
}
