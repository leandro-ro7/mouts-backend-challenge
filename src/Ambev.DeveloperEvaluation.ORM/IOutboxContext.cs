using Ambev.DeveloperEvaluation.Domain.Common;
using Ambev.DeveloperEvaluation.ORM.Outbox;

namespace Ambev.DeveloperEvaluation.ORM;

public interface IOutboxContext
{
    IEnumerable<AggregateRoot> GetTrackedAggregates();
    void AddOutboxMessage(OutboxMessage message);
}
