namespace EventBus.Core.DeadLetter;

using EventBus.Core.Events;

/// <summary>
/// Represents a message that failed processing and was moved to dead letter queue
/// </summary>
public class DeadLetterMessage
{
    /// <summary>
    /// The original event that failed
    /// </summary>
    public required IEvent Event { get; init; }
    
    /// <summary>
    /// Number of times processing was attempted
    /// </summary>
    public int AttemptCount { get; init; }
    
    /// <summary>
    /// Exception details from the last failed attempt
    /// </summary>
    public string? LastException { get; init; }
    
    /// <summary>
    /// Stack trace from the last failed attempt
    /// </summary>
    public string? LastStackTrace { get; init; }
    
    /// <summary>
    /// When the message was first enqueued
    /// </summary>
    public DateTimeOffset OriginalEnqueueTime { get; init; }
    
    /// <summary>
    /// When the message was moved to dead letter queue
    /// </summary>
    public DateTimeOffset DeadLetterTime { get; init; }
    
    /// <summary>
    /// Reason why the message was dead lettered
    /// </summary>
    public DeadLetterReason Reason { get; init; }
    
    /// <summary>
    /// Additional context about the failure
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Reason why a message was moved to dead letter queue
/// </summary>
public enum DeadLetterReason
{
    /// <summary>
    /// Maximum retry attempts exceeded
    /// </summary>
    MaxRetryAttemptsExceeded,
    
    /// <summary>
    /// Message expired before it could be processed
    /// </summary>
    MessageExpired,
    
    /// <summary>
    /// Malformed message that cannot be deserialized
    /// </summary>
    DeserializationFailed,
    
    /// <summary>
    /// Handler threw a non-retryable exception
    /// </summary>
    NonRetryableException,
    
    /// <summary>
    /// Manually moved to dead letter by operator
    /// </summary>
    Manual
}

/// <summary>
/// Service for managing dead letter messages
/// </summary>
public interface IDeadLetterQueue
{
    /// <summary>
    /// Adds a message to the dead letter queue
    /// </summary>
    /// <param name="message">The dead letter message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddAsync(DeadLetterMessage message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves messages from the dead letter queue
    /// </summary>
    /// <param name="count">Maximum number of messages to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of dead letter messages</returns>
    Task<IEnumerable<DeadLetterMessage>> GetMessagesAsync(int count = 100, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Requeues a dead letter message for reprocessing
    /// </summary>
    /// <param name="eventId">ID of the event to requeue</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RequeueAsync(string eventId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Permanently removes a message from the dead letter queue
    /// </summary>
    /// <param name="eventId">ID of the event to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(string eventId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the count of messages in the dead letter queue
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of messages in dead letter queue</returns>
    Task<long> GetCountAsync(CancellationToken cancellationToken = default);
}