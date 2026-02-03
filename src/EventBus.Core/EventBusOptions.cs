namespace EventBus.Core.Configuration;

using EventBus.Core.Retry;

/// <summary>
/// Configuration options for the event bus
/// </summary>
public class EventBusOptions
{
    /// <summary>
    /// Prefix for all queue/exchange names (useful for multi-tenant scenarios)
    /// </summary>
    public string? Prefix { get; set; }
    
    /// <summary>
    /// Whether to enable idempotency checking
    /// </summary>
    public bool EnableIdempotency { get; set; } = true;
    
    /// <summary>
    /// How long to keep idempotency records (to prevent memory leaks)
    /// </summary>
    public TimeSpan IdempotencyTtl { get; set; } = TimeSpan.FromHours(24);
    
    /// <summary>
    /// Retry policy for failed message processing
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = new();
    
    /// <summary>
    /// Whether to enable dead letter queue
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; } = true;
    
    /// <summary>
    /// Maximum message size in bytes (0 = no limit)
    /// </summary>
    public int MaxMessageSizeBytes { get; set; } = 0;
    
    /// <summary>
    /// Message time-to-live (how long messages remain in queue before expiring)
    /// </summary>
    public TimeSpan? MessageTtl { get; set; }
    
    /// <summary>
    /// Number of concurrent consumers per subscription
    /// </summary>
    public int ConcurrentConsumers { get; set; } = 1;
    
    /// <summary>
    /// Prefetch count for message batching (broker-specific)
    /// </summary>
    public ushort PrefetchCount { get; set; } = 10;
    
    /// <summary>
    /// Whether to auto-delete queues when no consumers are connected
    /// </summary>
    public bool AutoDeleteQueues { get; set; } = false;
    
    /// <summary>
    /// Whether queues should be durable (survive broker restarts)
    /// </summary>
    public bool DurableQueues { get; set; } = true;
    
    /// <summary>
    /// Timeout for publish operations
    /// </summary>
    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Whether to use publisher confirms (RabbitMQ-specific, ensures message delivery)
    /// </summary>
    public bool UsePublisherConfirms { get; set; } = true;
}

/// <summary>
/// Telemetry and metrics options
/// </summary>
public class TelemetryOptions
{
    /// <summary>
    /// Whether to enable distributed tracing
    /// </summary>
    public bool EnableTracing { get; set; } = true;
    
    /// <summary>
    /// Whether to enable metrics collection
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
    
    /// <summary>
    /// Whether to log message payloads (disable for sensitive data)
    /// </summary>
    public bool LogMessagePayloads { get; set; } = false;
    
    /// <summary>
    /// Service name for distributed tracing
    /// </summary>
    public string ServiceName { get; set; } = "EventBus";
}