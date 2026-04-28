using System.Net;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.API.Infrastructure;

public class ProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;

    public ProblemDetailsExceptionMiddleware(RequestDelegate next, ILogger<ProblemDetailsExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
                ValidationException => HttpStatusCode.UnprocessableEntity,
                ForbiddenException => HttpStatusCode.Forbidden,
                _ => HttpStatusCode.BadRequest
            };
            await WriteProblem(ctx, (int)status, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Path}", ctx.Request.Path);
            await WriteProblem(ctx, 500, "internal_error", "Erro interno. Tenta novamente.");
        }
    }

    private static async Task WriteProblem(HttpContext ctx, int status, string code, string detail)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = status,
            Title = code,
            Detail = detail,
            Type = $"https://repairdesk.app/errors/{code}",
            Instance = ctx.Request.Path
        };
        await ctx.Response.WriteAsJsonAsync(problem);
    }
}
