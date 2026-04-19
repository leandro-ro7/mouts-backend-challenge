namespace Ambev.DeveloperEvaluation.Infrastructure.Messaging;

public class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int LockDurationSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 50;
    public int PollingIntervalSeconds { get; set; } = 5;
    public int RetentionDays { get; set; } = 7;
    public int CleanupIntervalHours { get; set; } = 1;
}
