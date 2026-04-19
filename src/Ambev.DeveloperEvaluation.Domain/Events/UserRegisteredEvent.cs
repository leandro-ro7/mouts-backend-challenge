using Ambev.DeveloperEvaluation.Domain.Enums;

namespace Ambev.DeveloperEvaluation.Domain.Events;

public record UserRegisteredEvent(
    Guid UserId,
    string Username,
    string Email,
    UserRole Role,
    DateTime CreatedAt) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
}
