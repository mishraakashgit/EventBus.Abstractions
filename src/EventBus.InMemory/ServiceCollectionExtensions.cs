namespace EventBus.InMemory;

using EventBus.Core.Abstractions;
using EventBus.Core.Configuration;
using EventBus.Core.DeadLetter;
using EventBus.Core.Idempotency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Extension methods for registering InMemory event bus
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the InMemory event bus to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddInMemoryEventBus(
        this IServiceCollection services,
        Action<EventBusOptions>? configure = null)
    {
        // Configure options
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<EventBusOptions>(options => { });
        }

        // Register core services
        services.TryAddSingleton<IIdempotencyService, InMemoryIdempotencyService>();
        services.TryAddSingleton<IDeadLetterQueue, InMemoryDeadLetterQueue>();
        
        // Register event bus as singleton
        services.AddSingleton<IEventBus, InMemoryEventBus>();
        
        return services;
    }
}

/// <summary>
/// In-memory implementation of dead letter queue
/// </summary>
public class InMemoryDeadLetterQueue : IDeadLetterQueue
{
    private readonly List<DeadLetterMessage> _messages = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task AddAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _messages.Add(message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _messages.Take(count).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RequeueAsync(string eventId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var message = _messages.FirstOrDefault(m => m.Event.EventId == eventId);
            if (message != null)
            {
                _messages.Remove(message);
                // In a real implementation, this would republish to the main queue
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(string eventId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var message = _messages.FirstOrDefault(m => m.Event.EventId == eventId);
            if (message != null)
            {
                _messages.Remove(message);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _messages.Count;
        }
        finally
        {
            _lock.Release();
        }
    }
}