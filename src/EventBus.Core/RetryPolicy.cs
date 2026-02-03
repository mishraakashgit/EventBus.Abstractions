namespace EventBus.Core.Retry;

/// <summary>
/// Configuration for retry behavior when event processing fails
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Initial delay before first retry
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// Maximum delay between retries (for exponential backoff)
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Backoff multiplier for exponential backoff
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
    
    /// <summary>
    /// Whether to use exponential backoff
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;
    
    /// <summary>
    /// Whether to add jitter to prevent thundering herd
    /// </summary>
    public bool UseJitter { get; set; } = true;
    
    /// <summary>
    /// Action to take when all retries are exhausted
    /// </summary>
    public RetryExhaustedAction ExhaustedAction { get; set; } = RetryExhaustedAction.MoveToDeadLetter;
}

/// <summary>
/// Action to take when retry attempts are exhausted
/// </summary>
public enum RetryExhaustedAction
{
    /// <summary>
    /// Move the message to a dead letter queue for manual inspection
    /// </summary>
    MoveToDeadLetter,
    
    /// <summary>
    /// Discard the message
    /// </summary>
    Discard,
    
    /// <summary>
    /// Re-queue the message with no limit (dangerous!)
    /// </summary>
    RequeueIndefinitely
}

/// <summary>
/// Calculates retry delays based on the configured policy
/// </summary>
public class RetryDelayCalculator
{
    private static readonly Random _random = new();
    
    /// <summary>
    /// Calculates the delay for a given retry attempt
    /// </summary>
    /// <param name="attemptNumber">Current attempt number (1-based)</param>
    /// <param name="policy">Retry policy to use</param>
    /// <returns>Delay duration before next retry</returns>
    public static TimeSpan CalculateDelay(int attemptNumber, RetryPolicy policy)
    {
        if (attemptNumber <= 0)
            return TimeSpan.Zero;
        
        TimeSpan delay;
        
        if (policy.UseExponentialBackoff)
        {
            // Exponential backoff: delay = initial * (multiplier ^ (attempt - 1))
            var exponentialDelay = policy.InitialDelay.TotalMilliseconds * 
                                   Math.Pow(policy.BackoffMultiplier, attemptNumber - 1);
            
            delay = TimeSpan.FromMilliseconds(Math.Min(exponentialDelay, policy.MaxDelay.TotalMilliseconds));
        }
        else
        {
            // Linear backoff
            delay = TimeSpan.FromMilliseconds(policy.InitialDelay.TotalMilliseconds * attemptNumber);
            delay = delay > policy.MaxDelay ? policy.MaxDelay : delay;
        }
        
        // Add jitter to prevent thundering herd
        if (policy.UseJitter)
        {
            var jitterMs = _random.Next(0, (int)(delay.TotalMilliseconds * 0.2)); // +/- 20% jitter
            delay = delay.Add(TimeSpan.FromMilliseconds(jitterMs));
        }
        
        return delay;
    }
    
    /// <summary>
    /// Determines if another retry should be attempted
    /// </summary>
    /// <param name="attemptNumber">Current attempt number</param>
    /// <param name="policy">Retry policy to use</param>
    /// <returns>True if should retry, false otherwise</returns>
    public static bool ShouldRetry(int attemptNumber, RetryPolicy policy)
    {
        return attemptNumber <= policy.MaxRetryAttempts;
    }
}