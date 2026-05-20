using Microsoft.AspNetCore.Authorization;

namespace RepairDesk.API.Infrastructure;

/// <summary>
/// Requirement de autorização que valida que o caller tem um scope específico OU
/// wildcard ("*"). Para API keys autenticadas via <see cref="ApiKeyAuthHandler"/>.
/// JWT de utilizadores admin passa sempre (são acesso total).
/// </summary>
public sealed class ApiScopeRequirement : IAuthorizationRequirement
{
    public string RequiredScope { get; }
    public ApiScopeRequirement(string scope) => RequiredScope = scope;
}

public sealed class ApiScopeHandler : AuthorizationHandler<ApiScopeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ApiScopeRequirement requirement)
    {
        // JWT (utilizador admin) — passa sempre. Não fazemos scope-check em UI calls.
        if (context.User.HasClaim(c => c.Type == "auth_type" && c.Value != "api_key"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // ApiKey path: wildcard ou scope exacto.
        var scopes = context.User.FindAll("api_scope").Select(c => c.Value).ToHashSet();
        if (scopes.Contains("*") || scopes.Contains(requirement.RequiredScope))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

/// <summary>Conveniência para aplicar em controllers: <c>[ApiScope("read")]</c>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ApiScopeAttribute : AuthorizeAttribute
{
    public ApiScopeAttribute(string scope) : base($"api_scope:{scope}") { }
}
