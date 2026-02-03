namespace EventBus.Core.Abstractions;

using EventBus.Core.Events;

/// <summary>
/// Defines the contract for handling events from the event bus
/// </summary>
/// <typeparam name="TEvent">Type of event this handler processes</typeparam>
public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    /// <summary>
    /// Handles the event asynchronously
    /// </summary>
    /// <param name="event">The event to handle</param>
    /// <param name="context">Context information about the event</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task HandleAsync(TEvent @event, EventContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Context information about an event being processed
/// </summary>
public class EventContext
{
    /// <summary>
    /// Number of times this event has been attempted
    /// </summary>
    public int DeliveryCount { get; init; }
    
    /// <summary>
    /// Timestamp when the event was first enqueued
    /// </summary>
    public DateTimeOffset EnqueuedAt { get; init; }
    
    /// <summary>
    /// Timestamp when this delivery attempt started
    /// </summary>
    public DateTimeOffset DeliveredAt { get; init; }
    
    /// <summary>
    /// Additional metadata about the message
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
    
    /// <summary>
    /// Indicates if this is a redelivery after a previous failure
    /// </summary>
    public bool IsRedelivery => DeliveryCount > 1;
}