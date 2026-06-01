using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Payments;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 303: gestão de pagamentos. Fase A — apenas MockPaymentProvider para dev
/// (auto-approve). Fase B adiciona IFTHENPAY real + webhook callback.
/// </summary>
[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _service;
    private readonly ITenantContext _tenant;

    public PaymentsController(IPaymentService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public sealed record InitiateRequest(
        Guid VendaId,
        PaymentMethod Method,
        PaymentProvider Provider,
        int AmountCents,
        string? CustomerPhone,
        string? CustomerEmail,
        string? Description);

    public sealed record PaymentDto(
        Guid Id,
        Guid? VendaId,
        Guid? ReparacaoId,
        PaymentMethod Method,
        PaymentProvider Provider,
        int AmountCents,
        PaymentStatus Status,
        string? ProviderRef,
        string? ExternalId,
        DateTime CreatedAt,
        DateTime? ConfirmedAt,
        DateTime? ExpiresAt,
        string? FailureReason);

    [HttpPost]
    public async Task<ActionResult<PaymentDto>> Initiate([FromBody] InitiateRequest req, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            return Unauthorized();

        var initiation = new PaymentInitiationRequest(
            TenantId: tenantId,
            VendaId: req.VendaId,
            Method: req.Method,
            AmountCents: req.AmountCents,
            CustomerPhone: req.CustomerPhone,
            CustomerEmail: req.CustomerEmail,
            Description: req.Description);

        var payment = await _service.InitiateAsync(initiation, req.Provider, ct);

        return Ok(new PaymentDto(
            payment.Id, payment.VendaId, payment.ReparacaoId, payment.Method, payment.Provider,
            payment.AmountCents, payment.Status, payment.ProviderRef, payment.ExternalId,
            payment.CreatedAt, payment.ConfirmedAt, payment.ExpiresAt, payment.FailureReason));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentDto>> Get(Guid id, CancellationToken ct)
    {
        var p = await _service.GetAsync(id, ct);
        if (p is null) return NotFound();
        return Ok(ToDto(p));
    }

    [HttpGet("by-venda/{vendaId:guid}")]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListByVenda(Guid vendaId, CancellationToken ct)
    {
        var list = await _service.GetByVendaAsync(vendaId, ct);
        return Ok(list.Select(ToDto).ToList());
    }

    /// <summary>Sprint 496: pagamentos de uma reparação (proveniência MBWay no admin).</summary>
    [HttpGet("by-reparacao/{reparacaoId:guid}")]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListByReparacao(Guid reparacaoId, CancellationToken ct)
    {
        var list = await _service.GetByReparacaoAsync(reparacaoId, ct);
        return Ok(list.Select(ToDto).ToList());
    }

    private static PaymentDto ToDto(RepairDesk.Core.Entities.Payment p) => new(
        p.Id, p.VendaId, p.ReparacaoId, p.Method, p.Provider, p.AmountCents, p.Status,
        p.ProviderRef, p.ExternalId, p.CreatedAt, p.ConfirmedAt, p.ExpiresAt, p.FailureReason);
}
