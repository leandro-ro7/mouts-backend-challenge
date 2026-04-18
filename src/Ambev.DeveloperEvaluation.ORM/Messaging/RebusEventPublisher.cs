using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Services;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Ambev.DeveloperEvaluation.ORM.Messaging;

public class RebusEventPublisher : IEventPublisher
{
    private readonly IBus _bus;
    private readonly ILogger<RebusEventPublisher> _logger;

    public RebusEventPublisher(IBus bus, ILogger<RebusEventPublisher> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        await _bus.Publish(domainEvent);
        _logger.LogInformation(
            "Domain event published: {EventType} at {OccurredAt}",
            domainEvent.GetType().Name,
            domainEvent.OccurredAt);
    }
}
