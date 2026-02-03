namespace EventBus.Core.Abstractions;

using EventBus.Core.Events;

/// <summary>
/// Defines the contract for publishing events to the event bus
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to the event bus asynchronously
    /// </summary>
    /// <typeparam name="TEvent">Type of event to publish</typeparam>
    /// <param name="event">The event instance to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : IEvent;
    
    /// <summary>
    /// Publishes multiple events as a batch operation
    /// </summary>
    /// <typeparam name="TEvent">Type of events to publish</typeparam>
    /// <param name="events">Collection of events to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default) 
        where TEvent : IEvent;
    
    /// <summary>
    /// Subscribes to events of a specific type
    /// </summary>
    /// <typeparam name="TEvent">Type of event to subscribe to</typeparam>
    /// <typeparam name="THandler">Type of handler that will process the event</typeparam>
    /// <returns>Task representing the asynchronous operation</returns>
    Task SubscribeAsync<TEvent, THandler>()
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>;
    
    /// <summary>
    /// Unsubscribes from events of a specific type
    /// </summary>
    /// <typeparam name="TEvent">Type of event to unsubscribe from</typeparam>
    /// <typeparam name="THandler">Type of handler to remove</typeparam>
    /// <returns>Task representing the asynchronous operation</returns>
    Task UnsubscribeAsync<TEvent, THandler>()
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>;
}