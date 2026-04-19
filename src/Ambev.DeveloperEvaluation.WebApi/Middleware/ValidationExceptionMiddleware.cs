using Ambev.DeveloperEvaluation.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Ambev.DeveloperEvaluation.WebApi.Middleware;

public class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationExceptionMiddleware> _logger;

    // OCP: adding a new exception type is a single dictionary entry — no method body change.
    private static readonly Dictionary<Type, (int Status, string Code, LogLevel Level, Func<Exception, string> GetMessage)> ExceptionHandlers =
        new()
        {
            [typeof(ValidationException)] = (
                400, "ValidationError", LogLevel.Warning,
                ex => string.Join("; ", ((ValidationException)ex).Errors.Select(e => e.ErrorMessage))),

            [typeof(DomainException)] = (
                400, "BusinessRuleViolation", LogLevel.Warning,
                ex => ex.Message),

            [typeof(UnauthorizedAccessException)] = (
                401, "Unauthorized", LogLevel.Warning,
                ex => ex.Message),

            [typeof(KeyNotFoundException)] = (
                404, "ResourceNotFound", LogLevel.Warning,
                ex => ex.Message),

            [typeof(ConcurrencyException)] = (
                409, "ConcurrencyConflict", LogLevel.Warning,
                ex => ex.Message),

            [typeof(InvalidOperationException)] = (
                400, "InvalidOperation", LogLevel.Warning,
                ex => ex.Message),
        };

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
        catch (Exception ex)
        {
            if (ExceptionHandlers.TryGetValue(ex.GetType(), out var handler))
            {
                var message = handler.GetMessage(ex);
                _logger.Log(handler.Level, "Exception {Code} for {Path}: {Message}",
                    handler.Code, context.Request.Path, message);
                await WriteResponse(context, handler.Status, handler.Code, message, message);
            }
            else
            {
                _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
                await WriteResponse(context, 500, "InternalError",
                    "An unexpected error occurred.", "An unexpected error occurred.");
            }
        }
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static Task WriteResponse(HttpContext context, int status, string type, string error, string detail)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = status;

        var body = JsonSerializer.Serialize(
            new { type, error, detail, traceId = context.TraceIdentifier },
            JsonOptions);

        return context.Response.WriteAsync(body);
    }
}
