namespace SimpleHttpFunction.Orders.Domain;

// =============================================================================
// DOMAIN MODEL — the internal, authoritative shape of an Order.
// =============================================================================
// This is deliberately SEPARATE from the request/response DTOs (see Contracts/).
// Why? So the public API contract can evolve independently of internal storage,
// and so callers can never set fields they shouldn't (e.g. Status, CreatedAtUtc).
// This separation is a core enterprise practice.

public enum OrderStatus
{
    Received,   // accepted and persisted, not yet processed
    Processing,
    Completed,
    Rejected
}

public sealed class Order
{
    public required string Id { get; init; }              // our server-generated id
    public required string CustomerId { get; init; }
    public required IReadOnlyList<OrderLine> Lines { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public OrderStatus Status { get; set; } = OrderStatus.Received;
    public required DateTimeOffset CreatedAtUtc { get; init; }

    // The idempotency key the caller supplied. Storing it lets us detect and
    // safely ignore duplicate submissions (see OrderService). Critical because a
    // stateless function may receive the SAME request twice (client retries,
    // network hiccups, at-least-once delivery from a queue).
    public required string IdempotencyKey { get; init; }
}

public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);
