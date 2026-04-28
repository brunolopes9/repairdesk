using System.Security.Claims;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.API.Infrastructure;

public class HttpTenantContext : ITenantContext
{
    public const string TenantIdClaim = "tenant_id";

    private readonly IHttpContextAccessor _accessor;

    public HttpTenantContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? TenantId
    {
        get
        {
            var raw = _accessor.HttpContext?.User?.FindFirstValue(TenantIdClaim);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public bool HasTenant => TenantId is not null;
}

public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? UserId
    {
        get
        {
            var raw = _accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => _accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => _accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
