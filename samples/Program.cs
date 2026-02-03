using EventBus.Core.Abstractions;
using EventBus.Core.Configuration;
using EventBus.Core.Events;
using EventBus.Core.Retry;
using EventBus.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Create host with event bus configured
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configure event bus with production-grade settings
        services.AddInMemoryEventBus(options =>
        {
            options.EnableIdempotency = true;
            options.IdempotencyTtl = TimeSpan.FromHours(1);
            options.ConcurrentConsumers = 3; // Process 3 messages concurrently
            
            options.RetryPolicy = new RetryPolicy
            {
                MaxRetryAttempts = 3,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                UseExponentialBackoff = true,
                UseJitter = true
            };
        });
        
        // Register event handlers
        services.AddScoped<IEventHandler<OrderCreatedEvent>, OrderCreatedHandler>();
        services.AddScoped<IEventHandler<OrderCreatedEvent>, EmailNotificationHandler>();
        services.AddScoped<IEventHandler<OrderShippedEvent>, OrderShippedHandler>();
        services.AddScoped<IEventHandler<PaymentProcessedEvent>, PaymentProcessedHandler>();
    })
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddConsole();
    })
    .Build();

// Get event bus instance
var eventBus = host.Services.GetRequiredService<IEventBus>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("🚀 Event Bus Demo Starting...\n");

// Subscribe to events
await eventBus.SubscribeAsync<OrderCreatedEvent, OrderCreatedHandler>();
await eventBus.SubscribeAsync<OrderCreatedEvent, EmailNotificationHandler>();
await eventBus.SubscribeAsync<OrderShippedEvent, OrderShippedHandler>();
await eventBus.SubscribeAsync<PaymentProcessedEvent, PaymentProcessedHandler>();

logger.LogInformation("✅ Subscriptions registered\n");

// Scenario 1: Single order flow
logger.LogInformation("📦 Scenario 1: Processing a single order...");
var order = new OrderCreatedEvent
{
    OrderId = "ORD-12345",
    CustomerId = "CUST-789",
    TotalAmount = 299.99m,
    Items = new List<string> { "Laptop", "Mouse", "Keyboard" }
};

await eventBus.PublishAsync(order);
await Task.Delay(500); // Give time for processing
logger.LogInformation("");

// Scenario 2: Batch orders (high throughput)
logger.LogInformation("🚄 Scenario 2: Processing batch of 100 orders...");
var batchOrders = Enumerable.Range(1, 100)
    .Select(i => new OrderCreatedEvent
    {
        OrderId = $"ORD-BATCH-{i:D5}",
        CustomerId = $"CUST-{i % 20}",
        TotalAmount = i * 10.5m,
        Items = new List<string> { $"Product-{i}" }
    });

await eventBus.PublishBatchAsync(batchOrders);
await Task.Delay(2000); // Give time for processing
logger.LogInformation("");

// Scenario 3: Idempotency test (duplicate prevention)
logger.LogInformation("🔒 Scenario 3: Testing idempotency (duplicate prevention)...");
var duplicateOrder = new OrderCreatedEvent
{
    EventId = "DUPLICATE-TEST-123", // Same event ID
    OrderId = "ORD-DUPLICATE",
    CustomerId = "CUST-999",
    TotalAmount = 199.99m,
    Items = new List<string> { "Widget" }
};

logger.LogInformation("Publishing same event 3 times with same EventId...");
await eventBus.PublishAsync(duplicateOrder);
await eventBus.PublishAsync(duplicateOrder);
await eventBus.PublishAsync(duplicateOrder);
await Task.Delay(500);
logger.LogInformation("✅ Should only process once due to idempotency\n");

// Scenario 4: Multi-event correlation (order lifecycle)
logger.LogInformation("🔗 Scenario 4: Correlated events (order lifecycle)...");
var correlationId = Guid.NewGuid().ToString();

var newOrder = new OrderCreatedEvent
{
    OrderId = "ORD-CORR-456",
    CustomerId = "CUST-PREMIUM-1",
    TotalAmount = 1299.99m,
    Items = new List<string> { "MacBook Pro", "AirPods" },
    CorrelationId = correlationId
};

var payment = new PaymentProcessedEvent
{
    OrderId = "ORD-CORR-456",
    PaymentId = "PAY-789",
    Amount = 1299.99m,
    CorrelationId = correlationId
};

var shipment = new OrderShippedEvent
{
    OrderId = "ORD-CORR-456",
    TrackingNumber = "TRACK-XYZ-789",
    EstimatedDelivery = DateTime.UtcNow.AddDays(3),
    CorrelationId = correlationId
};

logger.LogInformation($"Publishing correlated events with ID: {correlationId}");
await eventBus.PublishAsync(newOrder);
await Task.Delay(200);
await eventBus.PublishAsync(payment);
await Task.Delay(200);
await eventBus.PublishAsync(shipment);
await Task.Delay(500);
logger.LogInformation("");

// Scenario 5: Retry behavior
logger.LogInformation("🔄 Scenario 5: Testing retry behavior...");
var failingOrder = new OrderCreatedEvent
{
    OrderId = "ORD-FAIL-999",
    CustomerId = "CUST-TEST-RETRY",
    TotalAmount = -1, // This will cause the handler to fail
    Items = new List<string> { "Test Item" }
};

await eventBus.PublishAsync(failingOrder);
await Task.Delay(10000); // Wait for retries to complete
logger.LogInformation("");

logger.LogInformation("🎉 Demo completed! Press any key to exit...");
Console.ReadKey();

// ============================================
// Event Definitions
// ============================================

public record OrderCreatedEvent : EventBase
{
    public string OrderId { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public List<string> Items { get; init; } = new();
}

public record OrderShippedEvent : EventBase
{
    public string OrderId { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public DateTime EstimatedDelivery { get; init; }
}

public record PaymentProcessedEvent : EventBase
{
    public string OrderId { get; init; } = string.Empty;
    public string PaymentId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

// ============================================
// Event Handlers
// ============================================

public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedHandler> _logger;
    
    public OrderCreatedHandler(ILogger<OrderCreatedHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task HandleAsync(OrderCreatedEvent @event, EventContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "📝 [OrderCreatedHandler] Processing order {OrderId} for customer {CustomerId} | Items: {ItemCount} | Total: ${Total:F2} | Attempt: {Attempt}",
            @event.OrderId, @event.CustomerId, @event.Items.Count, @event.TotalAmount, context.DeliveryCount);
        
        // Simulate processing failure for negative amounts (retry test)
        if (@event.TotalAmount < 0)
        {
            _logger.LogError("❌ [OrderCreatedHandler] Invalid order amount: ${Amount}", @event.TotalAmount);
            throw new InvalidOperationException("Order amount cannot be negative");
        }
        
        // Simulate processing time
        await Task.Delay(50, cancellationToken);
        
        _logger.LogInformation("✅ [OrderCreatedHandler] Order {OrderId} processed successfully", @event.OrderId);
    }
}

public class EmailNotificationHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<EmailNotificationHandler> _logger;
    
    public EmailNotificationHandler(ILogger<EmailNotificationHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task HandleAsync(OrderCreatedEvent @event, EventContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "📧 [EmailNotificationHandler] Sending confirmation email for order {OrderId} to customer {CustomerId}",
            @event.OrderId, @event.CustomerId);
        
        // Simulate sending email
        await Task.Delay(100, cancellationToken);
        
        _logger.LogInformation("✅ [EmailNotificationHandler] Email sent for order {OrderId}", @event.OrderId);
    }
}

public class OrderShippedHandler : IEventHandler<OrderShippedEvent>
{
    private readonly ILogger<OrderShippedHandler> _logger;
    
    public OrderShippedHandler(ILogger<OrderShippedHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task HandleAsync(OrderShippedEvent @event, EventContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "🚚 [OrderShippedHandler] Order {OrderId} shipped | Tracking: {TrackingNumber} | ETA: {ETA:yyyy-MM-dd}",
            @event.OrderId, @event.TrackingNumber, @event.EstimatedDelivery);
        
        await Task.Delay(30, cancellationToken);
        
        _logger.LogInformation("✅ [OrderShippedHandler] Shipping notification processed for order {OrderId}", @event.OrderId);
    }
}

public class PaymentProcessedHandler : IEventHandler<PaymentProcessedEvent>
{
    private readonly ILogger<PaymentProcessedHandler> _logger;
    
    public PaymentProcessedHandler(ILogger<PaymentProcessedHandler> logger)
    {
        _logger = logger;
    }
    
    public async Task HandleAsync(PaymentProcessedEvent @event, EventContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "💳 [PaymentProcessedHandler] Payment {PaymentId} processed for order {OrderId} | Amount: ${Amount:F2}",
            @event.PaymentId, @event.OrderId, @event.Amount);
        
        await Task.Delay(75, cancellationToken);
        
        _logger.LogInformation("✅ [PaymentProcessedHandler] Payment confirmation sent for order {OrderId}", @event.OrderId);
    }
}