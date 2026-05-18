using System.Security.Claims;
using Serilog.Core;
using Serilog.Events;

namespace RepairDesk.API.Infrastructure;

public sealed class TenantIdLogEventEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor? _accessor;

    public TenantIdLogEventEnricher(IHttpContextAccessor? accessor)
    {
        _accessor = accessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var tenantId = _accessor?.HttpContext?.User?.FindFirstValue(HttpTenantContext.TenantIdClaim);
        if (string.IsNullOrWhiteSpace(tenantId))
            return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TenantId", tenantId));
    }
}
