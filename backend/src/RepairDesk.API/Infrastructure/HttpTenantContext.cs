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
    public string? IpAddress
    {
        get
        {
            var ctx = _accessor.HttpContext;
            var forwarded = ctx?.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(forwarded)
                ? ctx?.Connection.RemoteIpAddress?.ToString()
                : forwarded.Split(',')[0].Trim();
        }
    }

    public string? UserAgent => _accessor.HttpContext?.Request.Headers.UserAgent.ToString();
    public bool IsInRole(string role) => _accessor.HttpContext?.User?.IsInRole(role) ?? false;
}
