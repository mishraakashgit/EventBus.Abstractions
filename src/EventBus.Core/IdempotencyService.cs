namespace EventBus.Core.Idempotency;

/// <summary>
/// Service for ensuring idempotent message processing
/// Prevents duplicate processing of the same event
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Checks if an event has already been processed
    /// </summary>
    /// <param name="eventId">Unique identifier of the event</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if already processed, false otherwise</returns>
    Task<bool> IsProcessedAsync(string eventId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks an event as processed
    /// </summary>
    /// <param name="eventId">Unique identifier of the event</param>
    /// <param name="ttl">Time-to-live for the record (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsProcessedAsync(string eventId, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Attempts to acquire a lock for processing an event
    /// This is an atomic operation that both checks and marks in one step
    /// </summary>
    /// <param name="eventId">Unique identifier of the event</param>
    /// <param name="ttl">Time-to-live for the record</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if lock acquired (event not processed), false if already processed</returns>
    Task<bool> TryAcquireProcessingLockAsync(string eventId, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory implementation of idempotency service
/// Suitable for single-instance scenarios or testing
/// For distributed systems, use Redis or similar
/// </summary>
public class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly Dictionary<string, DateTimeOffset> _processedEvents = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(24);

    public async Task<bool> IsProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            CleanupExpiredEntries();
            return _processedEvents.ContainsKey(eventId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task MarkAsProcessedAsync(string eventId, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            CleanupExpiredEntries();
            var expiresAt = DateTimeOffset.UtcNow.Add(ttl ?? _defaultTtl);
            _processedEvents[eventId] = expiresAt;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> TryAcquireProcessingLockAsync(string eventId, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            CleanupExpiredEntries();
            
            if (_processedEvents.ContainsKey(eventId))
            {
                return false; // Already processed
            }
            
            var expiresAt = DateTimeOffset.UtcNow.Add(ttl ?? _defaultTtl);
            _processedEvents[eventId] = expiresAt;
            return true; // Lock acquired
        }
        finally
        {
            _lock.Release();
        }
    }

    private void CleanupExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _processedEvents
            .Where(kvp => kvp.Value < now)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in expiredKeys)
        {
            _processedEvents.Remove(key);
        }
    }
}