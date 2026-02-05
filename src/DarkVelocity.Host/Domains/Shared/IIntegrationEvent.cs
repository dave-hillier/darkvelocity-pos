namespace DarkVelocity.Host.Events;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
}

[GenerateSerializer]
public abstract record IntegrationEvent : IIntegrationEvent
{
    [Id(0)] public Guid EventId { get; init; } = Guid.NewGuid();
    [Id(1)] public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public abstract string EventType { get; }
}
