using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace RepairDesk.Tests.Security;

public class RolesMatrixDocTests
{
    [Fact]
    public void RolesMatrixDoc_SnapshotMatchesControllerAttributes()
    {
        var rows = BuildRows();
        var snapshot = string.Join('\n', rows);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant()[..16];
        var marker = $"<!-- roles-matrix-snapshot:{hash} -->";
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "Contexto",
            "71-Roles-Matrix.md"));

        File.Exists(path).Should().BeTrue($"expected roles matrix doc at {path}");
        var doc = File.ReadAllText(path);

        doc.Should().Contain(marker, $"roles matrix changed. Expected marker: {marker}\n\n{snapshot}");
    }

    private static IReadOnlyList<string> BuildRows()
    {
        var controllerTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && t is { IsAbstract: false })
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToArray();

        var rows = new List<string>();
        foreach (var controller in controllerTypes)
        {
            var classRoute = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? "api/[controller]";
            var classAuthorize = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();
            var classAllowAnonymous = controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

            var methods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(method => new
                {
                    Method = method,
                    Http = method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).ToArray()
                })
                .Where(x => x.Http.Length > 0)
                .OrderBy(x => x.Method.Name, StringComparer.Ordinal);

            foreach (var method in methods)
            {
                var methodAuthorize = method.Method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();
                var allowAnonymous = classAllowAnonymous || method.Method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();
                var access = ResolveAccess(allowAnonymous, methodAuthorize.Length > 0 ? methodAuthorize : classAuthorize);

                foreach (var http in method.Http)
                {
                    var verbs = string.Join(",", http.HttpMethods.OrderBy(x => x, StringComparer.Ordinal));
                    var route = CombineRoute(classRoute, http.Template, controller.Name);
                    rows.Add($"{controller.Name}|{method.Method.Name}|{verbs}|/{route}|{access}");
                }
            }
        }

        return rows;
    }

    private static string ResolveAccess(bool allowAnonymous, IReadOnlyCollection<AuthorizeAttribute> authorize)
    {
        if (allowAnonymous || authorize.Count == 0)
            return "Anonymous";

        var parts = authorize.Select(attr =>
        {
            var scopes = new List<string>();
            if (!string.IsNullOrWhiteSpace(attr.Roles)) scopes.Add($"Roles={attr.Roles}");
            if (!string.IsNullOrWhiteSpace(attr.Policy)) scopes.Add($"Policy={attr.Policy}");
            if (!string.IsNullOrWhiteSpace(attr.AuthenticationSchemes)) scopes.Add($"Schemes={attr.AuthenticationSchemes}");
            return scopes.Count == 0 ? "Authenticated" : string.Join(";", scopes);
        });

        return string.Join(" + ", parts.OrderBy(x => x, StringComparer.Ordinal));
    }

    private static string CombineRoute(string classRoute, string? methodRoute, string controllerName)
    {
        var controllerToken = controllerName.EndsWith("Controller", StringComparison.Ordinal)
            ? controllerName[..^"Controller".Length]
            : controllerName;
        var route = classRoute.Replace("[controller]", controllerToken, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(methodRoute))
            route = methodRoute.StartsWith("/", StringComparison.Ordinal) ? methodRoute.TrimStart('/') : $"{route.TrimEnd('/')}/{methodRoute.TrimStart('/')}";

        return route.Replace("//", "/", StringComparison.Ordinal);
    }
}
