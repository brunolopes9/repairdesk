using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.TenantPreferences;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/whatsapp-notifications")]
[Authorize]
public class WhatsAppNotificationsController : ControllerBase
{
    private readonly IWhatsAppNotificationLogRepository _repo;

    public WhatsAppNotificationsController(IWhatsAppNotificationLogRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("sent")]
    public async Task<WhatsAppNotificationStatusDto> Sent(
        [FromQuery] Guid entityId,
        [FromQuery] string templateKey,
        [FromQuery] string entityType = "Reparacao",
        CancellationToken ct = default)
    {
        Validate(entityId, templateKey, entityType);
        var exists = await _repo.ExistsAsync(entityId, templateKey.Trim(), entityType.Trim(), ct);
        return new WhatsAppNotificationStatusDto(exists);
    }

    [HttpPost]
    public async Task<WhatsAppNotificationStatusDto> Create(
        [FromBody] CreateWhatsAppNotificationLogRequest request,
        CancellationToken ct)
    {
        Validate(request.EntityId, request.TemplateKey, request.EntityType);

        var entityType = request.EntityType.Trim();
        var templateKey = request.TemplateKey.Trim();
        if (await _repo.ExistsAsync(request.EntityId, templateKey, entityType, ct))
            return new WhatsAppNotificationStatusDto(true);

        await _repo.AddAsync(new WhatsAppNotificationLog
        {
            EntityType = entityType,
            EntityId = request.EntityId,
            TemplateKey = templateKey,
            Estado = request.Estado is { } estado && Enum.IsDefined(typeof(RepairStatus), estado)
                ? (RepairStatus)estado
                : null,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            SentAtUtc = DateTime.UtcNow,
        }, ct);
        await _repo.SaveAsync(ct);

        return new WhatsAppNotificationStatusDto(true);
    }

    private static void Validate(Guid entityId, string? templateKey, string? entityType)
    {
        if (entityId == Guid.Empty)
            throw new ValidationException("whatsapp_entity_required", "Entidade obrigatoria.");
        if (string.IsNullOrWhiteSpace(templateKey) || templateKey.Length > 80)
            throw new ValidationException("whatsapp_template_invalid", "Template WhatsApp invalido.");
        if (string.IsNullOrWhiteSpace(entityType) || entityType.Length > 80)
            throw new ValidationException("whatsapp_entity_type_invalid", "Tipo de entidade invalido.");
    }
}
