using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.InternalTasks;

public sealed record InternalTaskDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime? DueAt,
    InternalTaskStatus Status,
    DateTime? CompletedAt,
    Guid? AssignedToUserId,
    string? AssignedToDisplayName,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    Guid? VendaId,
    int? VendaNumero,
    TarefaFoco Foco = TarefaFoco.Nenhum);

public sealed record CreateInternalTaskRequest(
    string Title,
    string? Description,
    DateTime? DueAt,
    Guid? AssignedToUserId,
    Guid? VendaId,
    TarefaFoco Foco = TarefaFoco.Nenhum);

public sealed record UpdateInternalTaskRequest(
    string Title,
    string? Description,
    DateTime? DueAt,
    Guid? AssignedToUserId,
    Guid? VendaId);

public sealed record ChangeInternalTaskStatusRequest(InternalTaskStatus Status);

/// <summary>Sprint 617: mudar o foco (Hoje / Semana / Ideia / nenhum).</summary>
public sealed record MudarFocoRequest(TarefaFoco Foco);

public interface IInternalTaskService
{
    Task<IReadOnlyList<InternalTaskDto>> ListAsync(
        InternalTaskStatus? status,
        Guid? assignedToUserId,
        Guid? vendaId,
        CancellationToken ct = default);
    Task<InternalTaskDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<InternalTaskDto> CreateAsync(CreateInternalTaskRequest req, CancellationToken ct = default);
    Task<InternalTaskDto> UpdateAsync(Guid id, UpdateInternalTaskRequest req, CancellationToken ct = default);
    Task<InternalTaskDto> ChangeStatusAsync(Guid id, InternalTaskStatus status, CancellationToken ct = default);
    Task<InternalTaskDto> MudarFocoAsync(Guid id, TarefaFoco foco, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class InternalTaskService : IInternalTaskService
{
    private readonly IInternalTaskRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly IAuditLogger _audit;

    public InternalTaskService(
        IInternalTaskRepository repo,
        ITenantContext tenant,
        ICurrentUser user,
        IAuditLogger audit)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    public async Task<IReadOnlyList<InternalTaskDto>> ListAsync(
        InternalTaskStatus? status,
        Guid? assignedToUserId,
        Guid? vendaId,
        CancellationToken ct = default)
    {
        var list = await _repo.ListAsync(status, assignedToUserId, vendaId, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<InternalTaskDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("InternalTask", id);
        return ToDto(t);
    }

    public async Task<InternalTaskDto> CreateAsync(CreateInternalTaskRequest req, CancellationToken ct = default)
    {
        var title = (req.Title ?? "").Trim();
        if (title.Length is < 2 or > 200)
            throw new ValidationException("title_invalido", "Título obrigatório (2 a 200 caracteres).");
        if (_user.UserId is not { } uid)
            throw new ValidationException("user_required", "Sessão sem utilizador associado.");

        var task = new InternalTask
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId ?? Guid.Empty,
            Title = title,
            Description = TrimOrNull(req.Description),
            DueAt = req.DueAt is null ? null : DateTime.SpecifyKind(req.DueAt.Value, DateTimeKind.Utc),
            AssignedToUserId = req.AssignedToUserId,
            VendaId = req.VendaId,
            CreatedByUserId = uid,
            Status = InternalTaskStatus.Pendente,
            Foco = req.Foco,
        };
        await ValidarFocoAsync(task, req.Foco, ct);
        await _repo.AddAsync(task, ct);
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Create, "InternalTask", task.Id,
            new { title, task.AssignedToUserId, task.VendaId }, task.TenantId, uid, ct);

        return (await GetByIdAsync(task.Id, ct))!;
    }

    public async Task<InternalTaskDto> UpdateAsync(Guid id, UpdateInternalTaskRequest req, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("InternalTask", id);
        var title = (req.Title ?? "").Trim();
        if (title.Length is < 2 or > 200)
            throw new ValidationException("title_invalido", "Título obrigatório (2 a 200 caracteres).");

        t.Title = title;
        t.Description = TrimOrNull(req.Description);
        t.DueAt = req.DueAt is null ? null : DateTime.SpecifyKind(req.DueAt.Value, DateTimeKind.Utc);
        t.AssignedToUserId = req.AssignedToUserId;
        t.VendaId = req.VendaId;
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Update, "InternalTask", t.Id,
            new { t.Title, t.AssignedToUserId, t.VendaId }, t.TenantId, _user.UserId, ct);

        return ToDto((await _repo.FindByIdAsync(id, ct))!);
    }

    public async Task<InternalTaskDto> ChangeStatusAsync(Guid id, InternalTaskStatus status, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("InternalTask", id);
        if (t.Status == status) return ToDto(t);

        t.Status = status;
        t.CompletedAt = status == InternalTaskStatus.Concluida ? DateTime.UtcNow : null;
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Update, "InternalTask", t.Id,
            new { status = status.ToString() }, t.TenantId, _user.UserId, ct);

        return ToDto(t);
    }

    public const int MaxHoje = 3;

    public async Task<InternalTaskDto> MudarFocoAsync(Guid id, TarefaFoco foco, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("InternalTask", id);
        if (t.Foco == foco) return ToDto(t);
        await ValidarFocoAsync(t, foco, ct);
        t.Foco = foco;
        await _repo.SaveAsync(ct);
        return ToDto(t);
    }

    /// <summary>
    /// «Hoje» tem no máximo 3 tarefas pendentes por pessoa — o objetivo é escolher, não acumular.
    /// Conta para quem a tarefa está atribuída (ou para quem a criou, se não estiver atribuída).
    /// </summary>
    private async Task ValidarFocoAsync(InternalTask t, TarefaFoco foco, CancellationToken ct)
    {
        if (!Enum.IsDefined(foco)) throw new ValidationException("foco_invalido", "Foco inválido.");
        if (foco != TarefaFoco.Hoje || t.Status != InternalTaskStatus.Pendente) return;
        var dono = t.AssignedToUserId ?? t.CreatedByUserId;
        if (await _repo.ContarHojePendentesAsync(dono, t.Id, ct) >= MaxHoje)
            throw new ValidationException("max_hoje", $"«Hoje» já tem {MaxHoje} prioridades. Conclui uma ou passa-a para «Semana» antes de pôr outra.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("InternalTask", id);
        _repo.Remove(t);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, "InternalTask", id,
            new { t.Title }, t.TenantId, _user.UserId, ct);
    }

    private static string? TrimOrNull(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static InternalTaskDto ToDto(InternalTask t) => new(
        t.Id,
        t.Title,
        t.Description,
        t.DueAt,
        t.Status,
        t.CompletedAt,
        t.AssignedToUserId,
        t.AssignedToUser?.DisplayName,
        t.CreatedByUserId,
        t.CreatedAt,
        t.VendaId,
        t.Venda?.Numero,
        t.Foco);
}
