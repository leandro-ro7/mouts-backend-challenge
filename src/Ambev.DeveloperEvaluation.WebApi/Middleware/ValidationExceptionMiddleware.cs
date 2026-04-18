using Ambev.DeveloperEvaluation.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace Ambev.DeveloperEvaluation.WebApi.Middleware;

public class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ValidationExceptionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await WriteResponse(context, 400, "ValidationError",
                "One or more validation errors occurred.",
                string.Join("; ", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (DomainException ex)
        {
            await WriteResponse(context, 400, "BusinessRuleViolation", ex.Message, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteResponse(context, 401, "Unauthorized", ex.Message, ex.Message);
        }
        catch (NullReferenceException)
        {
            await WriteResponse(context, 401, "Unauthorized", "Authentication required.", "Authentication required.");
        }
        catch (KeyNotFoundException ex)
        {
            await WriteResponse(context, 404, "ResourceNotFound", ex.Message, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await WriteResponse(context, 400, "InvalidOperation", ex.Message, ex.Message);
        }
        catch (Exception ex)
        {
            await WriteResponse(context, 500, "InternalError",
                "An unexpected error occurred.", ex.Message);
        }
    }

    private static Task WriteResponse(HttpContext context, int status, string type, string error, string detail)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = status;

        var body = JsonSerializer.Serialize(
            new { type, error, detail },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return context.Response.WriteAsync(body);
    }
}
