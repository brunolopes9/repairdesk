using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Reparacoes;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 354 (Doc 83 Pillar 9): gestão interna dos pedidos de reparação que
/// chegaram pelo widget público. Staff converte em Reparacao ou rejeita.
/// </summary>
[ApiController]
[Route("api/repair-requests")]
[Authorize]
public sealed class RepairRequestsController : ControllerBase
{
    private readonly IRepairRequestRepository _repo;
    private readonly IClienteService _clientes;
    private readonly IReparacaoService _reparacoes;
    private readonly IAuditLogger _audit;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;

    public RepairRequestsController(
        IRepairRequestRepository repo,
        IClienteService clientes,
        IReparacaoService reparacoes,
        IAuditLogger audit,
        ITenantContext tenant,
        ICurrentUser user)
    {
        _repo = repo;
        _clientes = clientes;
        _reparacoes = reparacoes;
        _audit = audit;
        _tenant = tenant;
        _user = user;
    }

    public sealed record RequestDto(
        Guid Id, string Nome, string? Email, string? Telefone, string Equipamento,
        string Descricao, RepairRequestEstado Estado, Guid? ReparacaoId,
        string? MotivoRejeicao, DateTime CreatedAt);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RequestDto>>> List([FromQuery] RepairRequestEstado? estado, CancellationToken ct)
    {
        var rows = await _repo.ListAsync(estado, ct);
        return Ok(rows.Select(MapDto).ToList());
    }

    [HttpGet("count-pendentes")]
    public async Task<ActionResult<int>> CountPendentes(CancellationToken ct) =>
        Ok(await _repo.CountPendentesAsync(ct));

    /// <summary>Converte o pedido numa reparação real (lookup-or-create cliente).</summary>
    [HttpPost("{id:guid}/converter")]
    public async Task<ActionResult<RequestDto>> Converter(Guid id, CancellationToken ct)
    {
        var req = await _repo.FindByIdAsync(id, ct);
        if (req is null) return NotFound();
        if (req.Estado != RepairRequestEstado.Pendente)
            return Conflict(new { code = "not_pendente", message = "Pedido já foi tratado." });

        // Lookup-or-create cliente por telefone/email.
        var lookup = await _clientes.LookupOrCreateAsync(
            new CreateClienteRequest(req.Nome, req.Telefone, req.Email, null, "Criado via widget de pedido online."), ct);

        var rep = await _reparacoes.CreateAsync(new CreateReparacaoRequest(
            ClienteId: lookup.Cliente.Id,
            Equipamento: req.Equipamento,
            Avaria: req.Descricao,
            Imei: null,
            OrcamentoCents: null,
            Notas: "Pedido submetido online pelo cliente.",
            EstadoInicial: RepairStatus.Recebido), ct);

        req.Estado = RepairRequestEstado.Convertido;
        req.ReparacaoId = rep.Id;
        await _repo.SaveAsync(ct);

        if (_tenant.TenantId is { } tid)
            await _audit.LogAsync(AuditAction.Create, "RepairRequest", req.Id, new { ConvertedTo = rep.Id }, tid, _user.UserId, ct);

        return Ok(MapDto(req));
    }

    public sealed record RejeitarRequest(string? Motivo);

    [HttpPost("{id:guid}/rejeitar")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RequestDto>> Rejeitar(Guid id, [FromBody] RejeitarRequest? body, CancellationToken ct)
    {
        var req = await _repo.FindByIdAsync(id, ct);
        if (req is null) return NotFound();
        if (req.Estado != RepairRequestEstado.Pendente)
            return Conflict(new { code = "not_pendente" });

        req.Estado = RepairRequestEstado.Rejeitado;
        req.MotivoRejeicao = string.IsNullOrWhiteSpace(body?.Motivo) ? null : body!.Motivo.Trim();
        await _repo.SaveAsync(ct);
        return Ok(MapDto(req));
    }

    private static RequestDto MapDto(Core.Entities.RepairRequest r) =>
        new(r.Id, r.Nome, r.Email, r.Telefone, r.Equipamento, r.Descricao,
            r.Estado, r.ReparacaoId, r.MotivoRejeicao, r.CreatedAt);
}
