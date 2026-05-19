using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Services.Webhooks;

namespace RepairDesk.API.Controllers;

[ApiController]
[Route("api/webhooks")]
[Authorize(Roles = "Admin")]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookSubscriptionService _service;

    public WebhooksController(IWebhookSubscriptionService service) => _service = service;

    [HttpGet]
    public Task<IReadOnlyList<WebhookSubscriptionDto>> List(CancellationToken ct)
        => _service.ListAsync(ct);

    [HttpGet("events")]
    public IReadOnlyList<string> Events() => _service.ListEventTypes();

    [HttpPost]
    public Task<CreateWebhookSubscriptionResponse> Create([FromBody] CreateWebhookSubscriptionRequest req, CancellationToken ct)
        => _service.CreateAsync(req, ct);

    [HttpPut("{id:guid}")]
    public Task<WebhookSubscriptionDto> Update(Guid id, [FromBody] UpdateWebhookSubscriptionRequest req, CancellationToken ct)
        => _service.UpdateAsync(id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/deliveries")]
    public Task<IReadOnlyList<WebhookDeliveryDto>> Deliveries(Guid id, [FromQuery] int take = 50, CancellationToken ct = default)
        => _service.ListDeliveriesAsync(id, take, ct);

    [HttpPost("deliveries/{deliveryId:guid}/retry")]
    public Task<WebhookDeliveryDto> RetryDelivery(Guid deliveryId, CancellationToken ct)
        => _service.RetryDeliveryAsync(deliveryId, ct);
}
