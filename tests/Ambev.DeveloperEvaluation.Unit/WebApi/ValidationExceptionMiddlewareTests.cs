using System.Text.Json;
using Ambev.DeveloperEvaluation.Domain.Exceptions;
using Ambev.DeveloperEvaluation.WebApi.Middleware;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.WebApi;

public class ValidationExceptionMiddlewareTests
{
    private static async Task<(int statusCode, JsonElement body)> InvokeAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = _ => throw exception;
        var middleware = new ValidationExceptionMiddleware(next);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var body = JsonSerializer.Deserialize<JsonElement>(json);

        return (context.Response.StatusCode, body);
    }

    [Fact(DisplayName = "ValidationException → 400 with type=ValidationError")]
    public async Task ValidationException_Returns400_WithValidationError()
    {
        var failures = new[] { new ValidationFailure("Field", "Field is required") };
        var ex = new ValidationException(failures);

        var (status, body) = await InvokeAsync(ex);

        status.Should().Be(400);
        body.GetProperty("type").GetString().Should().Be("ValidationError");
        body.GetProperty("detail").GetString().Should().Contain("Field is required");
    }

    [Fact(DisplayName = "DomainException → 400 with type=BusinessRuleViolation")]
    public async Task DomainException_Returns400_WithBusinessRuleViolation()
    {
        var ex = new DomainException("Cannot sell above 20 items.");

        var (status, body) = await InvokeAsync(ex);

        status.Should().Be(400);
        body.GetProperty("type").GetString().Should().Be("BusinessRuleViolation");
        body.GetProperty("error").GetString().Should().Be("Cannot sell above 20 items.");
    }

    [Fact(DisplayName = "KeyNotFoundException → 404 with type=ResourceNotFound")]
    public async Task KeyNotFoundException_Returns404_WithResourceNotFound()
    {
        var ex = new KeyNotFoundException("Sale abc not found.");

        var (status, body) = await InvokeAsync(ex);

        status.Should().Be(404);
        body.GetProperty("type").GetString().Should().Be("ResourceNotFound");
    }

    [Fact(DisplayName = "ConcurrencyException → 409 with type=ConcurrencyConflict")]
    public async Task ConcurrencyException_Returns409_WithConcurrencyConflict()
    {
        var ex = new ConcurrencyException("Sale was modified by another request.");

        var (status, body) = await InvokeAsync(ex);

        status.Should().Be(409);
        body.GetProperty("type").GetString().Should().Be("ConcurrencyConflict");
        body.GetProperty("error").GetString().Should().Contain("modified by another request");
    }

    [Fact(DisplayName = "UnauthorizedAccessException → 401 with type=Unauthorized")]
    public async Task UnauthorizedAccessException_Returns401()
    {
        var ex = new UnauthorizedAccessException("Token expired.");

        var (status, body) = await InvokeAsync(ex);

        status.Should().Be(401);
        body.GetProperty("type").GetString().Should().Be("Unauthorized");
    }

    [Fact(DisplayName = "InvalidOperationException → 400 with type=InvalidOperation")]
    public async Task InvalidOperationException_Returns400_WithInvalidOperation()
    {
        var ex = new InvalidOperationException("Cannot perform this action.");

        var (status, body) = await InvokeAsync(ex);

        status.Should().Be(400);
        body.GetProperty("type").GetString().Should().Be("InvalidOperation");
    }

    [Fact(DisplayName = "Unhandled Exception → 500 with type=InternalError")]
    public async Task UnhandledException_Returns500_WithInternalError()
    {
        var ex = new Exception("Unexpected failure.");

        var (status, body) = await InvokeAsync(ex);

        status.Should().Be(500);
        body.GetProperty("type").GetString().Should().Be("InternalError");
        body.GetProperty("error").GetString().Should().Be("An unexpected error occurred.");
    }

    [Fact(DisplayName = "Next delegate success — response passes through unchanged")]
    public async Task NextSuccess_PassesThrough()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new ValidationExceptionMiddleware(next);
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(200);
    }
}
