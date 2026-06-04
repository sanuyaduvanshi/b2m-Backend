using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Pettle.Application.Common.Errors;

namespace Pettle.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ValidationException ex)
        {
            var fields = ex.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "_" : ToCamel(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            await Write(ctx, StatusCodes.Status400BadRequest, "validation_error", "Validation failed", fields);
        }
        catch (AppException ex)
        {
            var status = ex.Code switch
            {
                "not_found" => StatusCodes.Status404NotFound,
                "forbidden" => StatusCodes.Status403Forbidden,
                "conflict" => StatusCodes.Status409Conflict,
                "validation_error" => StatusCodes.Status400BadRequest,
                "business_rule" => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status400BadRequest
            };
            await Write(ctx, status, ex.Code, ex.Message, ex.FieldErrors);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception");
            await Write(ctx, StatusCodes.Status500InternalServerError, "internal_error", "Something went wrong.", null);
        }
    }

    private static string ToCamel(string s)
    {
        // FluentValidation gives "Property.Subprop[0].Field" — keep dotted path, lower-case first segment chars
        if (string.IsNullOrEmpty(s) || char.IsLower(s[0])) return s;
        return char.ToLowerInvariant(s[0]) + s[1..];
    }

    private static async Task Write(HttpContext ctx, int status, string code, string message, IReadOnlyDictionary<string, string[]>? fields)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = code,
            message,
            fields
        });
    }
}
