namespace Ambev.DeveloperEvaluation.ORM.Outbox;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    // LockedUntil: expiry-based lock prevents two processor instances from dispatching
    // the same message. A message whose LockedUntil is in the past is available for retry.
    public DateTime? LockedUntil { get; set; }

    // ClaimId: unique per-invocation token written atomically with LockedUntil via
    // "UPDATE ... WHERE Id = ANY(SELECT ... FOR UPDATE SKIP LOCKED)". After the UPDATE,
    // the processor fetches exactly its claimed rows using this token.
    // Cleared on success (ProcessedAt set) or failure (LockedUntil released).
    public Guid? ClaimId { get; set; }
}
