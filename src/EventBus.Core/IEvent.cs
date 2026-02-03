namespace EventBus.Core.Events;

/// <summary>
/// Base interface for all events that can be published through the event bus
/// </summary>
public interface IEvent
{
    /// <summary>
    /// Unique identifier for this event instance
    /// Used for idempotency checking and correlation
    /// </summary>
    string EventId { get; }
    
    /// <summary>
    /// Timestamp when the event was created
    /// </summary>
    DateTimeOffset CreatedAt { get; }
    
    /// <summary>
    /// Optional correlation ID for tracing related events across services
    /// </summary>
    string? CorrelationId { get; }
    
    /// <summary>
    /// Type name of the event, used for routing and serialization
    /// </summary>
    string EventType { get; }
}

/// <summary>
/// Base implementation of IEvent with common properties
/// </summary>
public abstract record EventBase : IEvent
{
    /// <inheritdoc />
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    
    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    
    /// <inheritdoc />
    public string? CorrelationId { get; init; }
    
    /// <inheritdoc />
    public string EventType => GetType().Name;
}