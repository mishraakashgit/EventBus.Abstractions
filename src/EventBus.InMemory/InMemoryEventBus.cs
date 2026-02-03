namespace EventBus.InMemory;

using EventBus.Core.Abstractions;
using EventBus.Core.Configuration;
using EventBus.Core.DeadLetter;
using EventBus.Core.Events;
using EventBus.Core.Idempotency;
using EventBus.Core.Retry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Threading.Channels;

/// <summary>
/// In-memory implementation of IEventBus for testing and single-instance scenarios
/// Thread-safe and supports concurrent publish/subscribe operations
/// </summary>
public class InMemoryEventBus : IEventBus, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly EventBusOptions _options;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IDeadLetterQueue? _deadLetterQueue;
    
    // Map of event type to channels (queues)
    private readonly ConcurrentDictionary<Type, object> _channels = new();
    
    // Map of event type to handler types
    private readonly ConcurrentDictionary<Type, List<Type>> _subscriptions = new();
    
    // Background processing tasks
    private readonly List<Task> _processingTasks = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public InMemoryEventBus(
        IServiceProvider serviceProvider,
        ILogger<InMemoryEventBus> logger,
        IOptions<EventBusOptions> options,
        IIdempotencyService idempotencyService,
        IDeadLetterQueue? deadLetterQueue = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _idempotencyService = idempotencyService;
        _deadLetterQueue = deadLetterQueue;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        
        _logger.LogDebug("Publishing event {EventType} with ID {EventId}", 
            eventType.Name, @event.EventId);

        // Get or create channel for this event type
        var channel = GetOrCreateChannel<TEvent>();
        
        try
        {
            await channel.Writer.WriteAsync(@event, cancellationToken);
            
            _logger.LogInformation("Successfully published event {EventType} with ID {EventId}", 
                eventType.Name, @event.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} with ID {EventId}", 
                eventType.Name, @event.EventId);
            throw;
        }
    }

    public async Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default) 
        where TEvent : IEvent
    {
        var eventsList = events.ToList();
        
        _logger.LogDebug("Publishing batch of {Count} events of type {EventType}", 
            eventsList.Count, typeof(TEvent).Name);

        foreach (var @event in eventsList)
        {
            await PublishAsync(@event, cancellationToken);
        }
    }

    public Task SubscribeAsync<TEvent, THandler>()
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventType = typeof(TEvent);
        var handlerType = typeof(THandler);
        
        _logger.LogInformation("Subscribing {HandlerType} to {EventType}", 
            handlerType.Name, eventType.Name);

        // Register the subscription
        _subscriptions.AddOrUpdate(
            eventType,
            _ => new List<Type> { handlerType },
            (_, handlers) =>
            {
                if (!handlers.Contains(handlerType))
                {
                    handlers.Add(handlerType);
                }
                return handlers;
            });

        // Start processing if not already started
        StartProcessing<TEvent>();
        
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync<TEvent, THandler>()
        where TEvent : IEvent
        where THandler : IEventHandler<TEvent>
    {
        var eventType = typeof(TEvent);
        var handlerType = typeof(THandler);
        
        _logger.LogInformation("Unsubscribing {HandlerType} from {EventType}", 
            handlerType.Name, eventType.Name);

        if (_subscriptions.TryGetValue(eventType, out var handlers))
        {
            handlers.Remove(handlerType);
        }
        
        return Task.CompletedTask;
    }

    private Channel<TEvent> GetOrCreateChannel<TEvent>() where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        
        if (_channels.TryGetValue(eventType, out var existingChannel))
        {
            return (Channel<TEvent>)existingChannel;
        }

        var channel = Channel.CreateUnbounded<TEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        _channels.TryAdd(eventType, channel);
        return channel;
    }

    private void StartProcessing<TEvent>() where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        var channel = GetOrCreateChannel<TEvent>();
        
        // Start consumer tasks based on configuration
        for (int i = 0; i < _options.ConcurrentConsumers; i++)
        {
            var task = Task.Run(async () => await ProcessEventsAsync(channel, _cancellationTokenSource.Token));
            _processingTasks.Add(task);
        }
    }

    private async Task ProcessEventsAsync<TEvent>(Channel<TEvent> channel, CancellationToken cancellationToken) 
        where TEvent : IEvent
    {
        await foreach (var @event in channel.Reader.ReadAllAsync(cancellationToken))
        {
            await ProcessEventAsync(@event, attemptNumber: 1, cancellationToken);
        }
    }

    private async Task ProcessEventAsync<TEvent>(TEvent @event, int attemptNumber, CancellationToken cancellationToken) 
        where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        
        // Check idempotency if enabled
        if (_options.EnableIdempotency)
        {
            var canProcess = await _idempotencyService.TryAcquireProcessingLockAsync(
                @event.EventId, 
                _options.IdempotencyTtl, 
                cancellationToken);
            
            if (!canProcess)
            {
                _logger.LogInformation("Event {EventId} already processed, skipping", @event.EventId);
                return;
            }
        }

        // Get handlers for this event type
        if (!_subscriptions.TryGetValue(eventType, out var handlerTypes) || !handlerTypes.Any())
        {
            _logger.LogWarning("No handlers registered for event type {EventType}", eventType.Name);
            return;
        }

        var context = new EventContext
        {
            DeliveryCount = attemptNumber,
            EnqueuedAt = @event.CreatedAt,
            DeliveredAt = DateTimeOffset.UtcNow
        };

        // Process with each handler
        foreach (var handlerType in handlerTypes)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService(handlerType);
                var handleMethod = handlerType.GetMethod(nameof(IEventHandler<TEvent>.HandleAsync));
                
                if (handleMethod != null)
                {
                    var handleTask = (Task?)handleMethod.Invoke(handler, new object[] { @event, context, cancellationToken });
                    if (handleTask != null)
                    {
                        await handleTask;
                    }
                }
                
                _logger.LogInformation("Successfully processed event {EventId} with handler {HandlerType}", 
                    @event.EventId, handlerType.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event {EventId} with handler {HandlerType} (attempt {AttemptNumber})", 
                    @event.EventId, handlerType.Name, attemptNumber);

                // Handle retry logic
                if (RetryDelayCalculator.ShouldRetry(attemptNumber, _options.RetryPolicy))
                {
                    var delay = RetryDelayCalculator.CalculateDelay(attemptNumber, _options.RetryPolicy);
                    
                    _logger.LogInformation("Retrying event {EventId} in {Delay}ms", 
                        @event.EventId, delay.TotalMilliseconds);
                    
                    await Task.Delay(delay, cancellationToken);
                    await ProcessEventAsync(@event, attemptNumber + 1, cancellationToken);
                }
                else
                {
                    _logger.LogError("Max retry attempts exceeded for event {EventId}", @event.EventId);
                    
                    // Move to dead letter queue if enabled
                    if (_options.EnableDeadLetterQueue && _deadLetterQueue != null)
                    {
                        await _deadLetterQueue.AddAsync(new DeadLetterMessage
                        {
                            Event = @event,
                            AttemptCount = attemptNumber,
                            LastException = ex.Message,
                            LastStackTrace = ex.StackTrace,
                            OriginalEnqueueTime = @event.CreatedAt,
                            DeadLetterTime = DateTimeOffset.UtcNow,
                            Reason = DeadLetterReason.MaxRetryAttemptsExceeded
                        }, cancellationToken);
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        Task.WaitAll(_processingTasks.ToArray(), TimeSpan.FromSeconds(10));
        _cancellationTokenSource.Dispose();
    }
}