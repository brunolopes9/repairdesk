using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Billing;

namespace RepairDesk.API.Infrastructure;

public class ProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ProblemDetailsExceptionMiddleware(RequestDelegate next, ILogger<ProblemDetailsExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (DomainException ex)
        {
            var status = ex switch
            {
                NotFoundException => HttpStatusCode.NotFound,
                ConflictException => HttpStatusCode.Conflict,
                RepairDesk.Core.Exceptions.ValidationException => HttpStatusCode.UnprocessableEntity,
                BillingProviderException => HttpStatusCode.UnprocessableEntity,
                ForbiddenException => HttpStatusCode.Forbidden,
                _ => HttpStatusCode.BadRequest
            };
            await WriteProblem(ctx, (int)status, ex.Code, ex.Message, ex);
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => string.IsNullOrEmpty(g.Key) ? "_" : ToCamelCase(g.Key),
                    g => g.Select(e => e.ErrorMessage).ToArray());
            await WriteProblem(ctx, 422, "validation_error", "Dados inválidos.", null, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Path}", ctx.Request.Path);
            await WriteProblem(ctx, 500, "internal_error", "Erro interno. Tenta novamente.", ex);
        }
    }

    private async Task WriteProblem(HttpContext ctx, int status, string code, string detail, Exception? ex = null, IDictionary<string, string[]>? errors = null)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
            // Sprint 226: URN canónico sem depender de DNS — após rebrand para Mender.
            Type = $"urn:mender:errors:{code}",
            Instance = ctx.Request.Path
        };
        if (errors is not null) problem.Extensions["errors"] = errors;
        if (ex is not null && (_env.IsDevelopment() || _env.IsEnvironment("Testing")))
        {
            problem.Extensions["exception"] = ex.GetType().FullName;
            problem.Extensions["exceptionMessage"] = ex.Message;
            problem.Extensions["stackTrace"] = ex.StackTrace;
            if (ex.InnerException is not null)
            {
                problem.Extensions["innerException"] = ex.InnerException.GetType().FullName;
                problem.Extensions["innerMessage"] = ex.InnerException.Message;
            }
        }
        await ctx.Response.WriteAsJsonAsync(problem);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0])) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
