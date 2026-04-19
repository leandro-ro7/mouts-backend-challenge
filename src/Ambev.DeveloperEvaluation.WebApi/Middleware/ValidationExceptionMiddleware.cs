using Ambev.DeveloperEvaluation.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Ambev.DeveloperEvaluation.WebApi.Middleware;

public class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationExceptionMiddleware> _logger;

    public ValidationExceptionMiddleware(
        RequestDelegate next,
        ILogger<ValidationExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation failed for {Path}: {Errors}",
                context.Request.Path,
                string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)));

            await WriteResponse(context, 400, "ValidationError",
                "One or more validation errors occurred.",
                string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (DomainException ex)
        {
            _logger.LogWarning("Business rule violation for {Path}: {Message}",
                context.Request.Path, ex.Message);

            await WriteResponse(context, 400, "BusinessRuleViolation", ex.Message, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access for {Path}: {Message}",
                context.Request.Path, ex.Message);

            await WriteResponse(context, 401, "Unauthorized", ex.Message, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Resource not found for {Path}: {Message}",
                context.Request.Path, ex.Message);

            await WriteResponse(context, 404, "ResourceNotFound", ex.Message, ex.Message);
        }
        catch (ConcurrencyException ex)
        {
            _logger.LogWarning("Concurrency conflict for {Path}: {Message}",
                context.Request.Path, ex.Message);

            await WriteResponse(context, 409, "ConcurrencyConflict", ex.Message, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Invalid operation for {Path}: {Message}",
                context.Request.Path, ex.Message);

            await WriteResponse(context, 400, "InvalidOperation", ex.Message, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);

            await WriteResponse(context, 500, "InternalError",
                "An unexpected error occurred.", ex.Message);
        }
    }

    private static Task WriteResponse(HttpContext context, int status, string type, string error, string detail)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = status;

        var body = JsonSerializer.Serialize(
            new { type, error, detail, traceId = context.TraceIdentifier },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return context.Response.WriteAsync(body);
    }
}
