using FluentValidation;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.Trabalhos;

public interface ITrabalhoService
{
    Task<PagedResult<TrabalhoDto>> SearchAsync(string? query, TrabalhoStatus? status, JobCategory? categoria, Guid? clienteId, int page, int pageSize, CancellationToken ct = default);
    Task<TrabalhoDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<TrabalhoDto> CreateAsync(CreateTrabalhoRequest req, CancellationToken ct = default);
    Task<TrabalhoDto> UpdateAsync(Guid id, UpdateTrabalhoRequest req, CancellationToken ct = default);
    Task<TrabalhoDto> ReabrirAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class TrabalhoService : ITrabalhoService
{
    private readonly ITrabalhoRepository _repo;
    private readonly IClienteRepository _clientes;
    private readonly IDespesaRepository _despesas;
    private readonly ITenantContext _tenant;
    private readonly IValidator<CreateTrabalhoRequest> _createV;
    private readonly IValidator<UpdateTrabalhoRequest> _updateV;

    public TrabalhoService(
        ITrabalhoRepository repo,
        IClienteRepository clientes,
        IDespesaRepository despesas,
        ITenantContext tenant,
        IValidator<CreateTrabalhoRequest> createV,
        IValidator<UpdateTrabalhoRequest> updateV)
    {
        _repo = repo;
        _clientes = clientes;
        _despesas = despesas;
        _tenant = tenant;
        _createV = createV;
        _updateV = updateV;
    }

    public async Task<PagedResult<TrabalhoDto>> SearchAsync(
        string? query, TrabalhoStatus? status, JobCategory? categoria, Guid? clienteId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _repo.SearchAsync(query, status, categoria, clienteId, page, pageSize, ct);
        var dtos = new List<TrabalhoDto>(items.Count);
        foreach (var t in items)
        {
            var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
            dtos.Add(ToDto(t, custo));
        }
        return new PagedResult<TrabalhoDto>(dtos, page, pageSize, total);
    }

    public async Task<TrabalhoDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Trabalho", id);
        if (t.ClienteId is not null) t.Cliente = await _clientes.FindByIdAsync(t.ClienteId.Value, ct);
        var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
        return ToDto(t, custo);
    }

    public async Task<TrabalhoDto> CreateAsync(CreateTrabalhoRequest req, CancellationToken ct = default)
    {
        await _createV.ValidateAndThrowAsync(req, ct);
        if (!_tenant.HasTenant) throw new ForbiddenException("no_tenant", "Tenant não definido.");

        Cliente? cliente = null;
        if (req.ClienteId is not null)
        {
            cliente = await _clientes.FindByIdAsync(req.ClienteId.Value, ct)
                ?? throw new NotFoundException("Cliente", req.ClienteId.Value);
        }

        var trabalho = new Trabalho
        {
            ClienteId = cliente?.Id,
            Titulo = req.Titulo.Trim(),
            Descricao = req.Descricao?.Trim(),
            Categoria = req.Categoria,
            OrcamentoCents = req.OrcamentoCents,
            // Copia orçamento → preço final (a UI passa a usar só "preço final")
            PrecoFinalCents = req.OrcamentoCents,
            Notas = req.Notas?.Trim(),
            Status = TrabalhoStatus.Orcamento,
        };
        await _repo.CreateWithNextNumeroAsync(trabalho, _tenant.TenantId!.Value, ct);
        trabalho.Cliente = cliente;
        return ToDto(trabalho, 0);
    }

    public async Task<TrabalhoDto> UpdateAsync(Guid id, UpdateTrabalhoRequest req, CancellationToken ct = default)
    {
        await _updateV.ValidateAndThrowAsync(req, ct);
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Trabalho", id);

        Cliente? cliente = null;
        if (req.ClienteId is not null)
        {
            cliente = await _clientes.FindByIdAsync(req.ClienteId.Value, ct)
                ?? throw new NotFoundException("Cliente", req.ClienteId.Value);
        }

        t.ClienteId = cliente?.Id;
        t.Titulo = req.Titulo.Trim();
        t.Descricao = req.Descricao?.Trim();
        t.Categoria = req.Categoria;

        // Quando transita para Concluido, marca data e auto-pago se ainda NaoPago
        if (req.Status == TrabalhoStatus.Concluido && t.Status != TrabalhoStatus.Concluido)
        {
            t.DataConclusao = req.DataConclusao ?? DateTime.UtcNow;
            if (t.EstadoPagamento == PaymentStatus.NaoPago && req.EstadoPagamento == PaymentStatus.NaoPago)
                t.EstadoPagamento = PaymentStatus.Pago;
            else
                t.EstadoPagamento = req.EstadoPagamento;
        }
        else
        {
            t.DataConclusao = req.DataConclusao;
            t.EstadoPagamento = req.EstadoPagamento;
        }

        // Quando começa Em Execução pela primeira vez, marca data início
        if (req.Status == TrabalhoStatus.EmExecucao && t.DataInicio is null)
            t.DataInicio = req.DataInicio ?? DateTime.UtcNow;
        else
            t.DataInicio = req.DataInicio;

        t.Status = req.Status;
        t.OrcamentoCents = req.OrcamentoCents;
        t.PrecoFinalCents = req.PrecoFinalCents;
        t.HorasGastas = req.HorasGastas;
        t.Notas = req.Notas?.Trim();

        await _repo.SaveAsync(ct);
        t.Cliente = cliente ?? (t.ClienteId is not null ? await _clientes.FindByIdAsync(t.ClienteId.Value, ct) : null);
        var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
        return ToDto(t, custo);
    }

    public async Task<TrabalhoDto> ReabrirAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Trabalho", id);

        if (t.Status != TrabalhoStatus.Concluido && t.Status != TrabalhoStatus.Cancelado)
            throw new ConflictException("reabrir_estado_invalido",
                $"Só é possível reabrir trabalhos Concluídos ou Cancelados. Estado actual: {t.Status}.");

        // Volta para EmExecucao + desmarca Pago + limpa DataConclusao
        t.Status = TrabalhoStatus.EmExecucao;
        t.EstadoPagamento = PaymentStatus.NaoPago;
        t.DataConclusao = null;
        await _repo.SaveAsync(ct);
        t.Cliente ??= (t.ClienteId is not null ? await _clientes.FindByIdAsync(t.ClienteId.Value, ct) : null);
        var custo = await _despesas.SumByTrabalhoAsync(t.Id, ct);
        return ToDto(t, custo);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Trabalho", id);
        _repo.Remove(t);
        await _repo.SaveAsync(ct);
    }

    private static TrabalhoDto ToDto(Trabalho t, int custoDespesasCents)
    {
        var cliente = t.Cliente is not null
            ? new ClienteResumo(t.Cliente.Id, t.Cliente.Nome, t.Cliente.Telefone ?? string.Empty)
            : null;
        var receita = t.PrecoFinalCents ?? t.OrcamentoCents ?? 0;
        var lucro = receita - custoDespesasCents;
        return new TrabalhoDto(
            t.Id, t.Numero, cliente,
            t.Titulo, t.Descricao, t.Categoria, t.Status,
            t.CreatedAt, t.DataInicio, t.DataConclusao,
            t.OrcamentoCents, t.PrecoFinalCents, t.HorasGastas,
            t.Notas, t.EstadoPagamento,
            custoDespesasCents, lucro,
            t.InvoiceProvider,
            t.InvoiceExternalId,
            t.InvoicePdfUrl,
            t.InvoiceNumber,
            t.InvoiceEmittedAt);
    }
}
