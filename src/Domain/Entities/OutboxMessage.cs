namespace Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string? DeduplicationKey { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public Guid? ProcessingLeaseId { get; private set; }
    public DateTime? LeaseExpiresOnUtc { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(string type, string payload, DateTime occurredOnUtc, string? deduplicationKey = null)
    {
        Type = type;
        Payload = payload;
        OccurredOnUtc = occurredOnUtc;
        DeduplicationKey = deduplicationKey;
    }

    public void MarkProcessed(DateTime processedOnUtc)
    {
        ProcessedOnUtc = processedOnUtc;
        ProcessingLeaseId = null;
        LeaseExpiresOnUtc = null;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Error = error;
    }

    public void Claim(Guid leaseId, DateTime leaseExpiresOnUtc)
    {
        ProcessingLeaseId = leaseId;
        LeaseExpiresOnUtc = leaseExpiresOnUtc;
    }
}
