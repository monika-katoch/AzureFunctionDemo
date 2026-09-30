using Microsoft.Extensions.Logging;
using SimpleHttpFunction.Orders.Contracts;
using SimpleHttpFunction.Orders.Domain;
using SimpleHttpFunction.Orders.Repository;

namespace SimpleHttpFunction.Orders.Services;

// =============================================================================
// SERVICE LAYER — the business logic. Functions stay thin; this holds the rules.
// =============================================================================
// Responsibilities here:
//   - Enforce idempotency (never create the same order twice).
//   - Compute server-owned fields (id, totals, timestamps, status).
//   - Map the public request DTO -> internal domain model.
// It knows nothing about HTTP. That makes it unit-testable and reusable (e.g.
// the same logic could later be triggered by a queue instead of HTTP).

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IOrderRepository repository, ILogger<OrderService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CreateOrderResult> CreateAsync(
        CreateOrderRequest request,
        string idempotencyKey,
        CancellationToken ct)
    {
        // IDEMPOTENCY: if we've seen this key, return what we already stored.
        // This is what makes the endpoint safe to retry — the heart of reliable
        // stateless processing.
        var existing = await _repository.FindByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Duplicate order submission ignored for idempotency key {Key}; returning existing order {OrderId}",
                idempotencyKey, existing.Id);
            return new CreateOrderResult(existing, WasDuplicate: true);
        }

        // Map DTO -> domain, computing the fields the SERVER owns.
        var lines = request.Lines!
            .Select(l => new OrderLine(l.Sku!, l.Quantity, l.UnitPrice))
            .ToList();

        var order = new Order
        {
            Id = Guid.NewGuid().ToString("N"),          // server-generated, never from caller
            CustomerId = request.CustomerId!,
            Lines = lines,
            TotalAmount = lines.Sum(l => l.Quantity * l.UnitPrice),
            Currency = request.Currency!.ToUpperInvariant(),
            Status = OrderStatus.Received,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = idempotencyKey
        };

        await _repository.AddAsync(order, ct);

        _logger.LogInformation(
            "Created order {OrderId} for customer {CustomerId}, total {Total} {Currency}",
            order.Id, order.CustomerId, order.TotalAmount, order.Currency);

        // In a real system you'd now publish an event / enqueue a message here so
        // downstream processing happens asynchronously (Service Bus, Storage Queue).
        return new CreateOrderResult(order, WasDuplicate: false);
    }

    public Task<Order?> GetAsync(string id, CancellationToken ct) => _repository.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct) => _repository.ListAsync(ct);
}
