namespace Ambev.DeveloperEvaluation.Domain.Events;

public record SaleModifiedEvent(
    Guid SaleId,
    string SaleNumber,
    SaleSnapshot Previous,
    SaleSnapshot Current) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public int Version { get; } = 1;
}
