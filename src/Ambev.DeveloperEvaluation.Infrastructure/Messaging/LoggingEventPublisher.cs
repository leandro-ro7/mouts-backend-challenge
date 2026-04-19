using System.Text.Json;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.Infrastructure.Messaging;

/// <summary>
/// Fallback publisher that logs domain events as structured JSON.
/// Swap for RebusEventPublisher (or any broker-backed implementation) in production.
/// </summary>
public class LoggingEventPublisher : IEventPublisher
{
    private readonly ILogger<LoggingEventPublisher> _logger;

    public LoggingEventPublisher(ILogger<LoggingEventPublisher> logger)
        => _logger = logger;

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[DomainEvent] {EventType} at {OccurredAt} | Payload: {Payload}",
            domainEvent.GetType().Name,
            domainEvent.OccurredAt,
            JsonSerializer.Serialize(domainEvent, domainEvent.GetType()));

        return Task.CompletedTask;
    }
}
