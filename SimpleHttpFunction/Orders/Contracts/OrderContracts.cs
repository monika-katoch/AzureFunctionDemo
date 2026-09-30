namespace SimpleHttpFunction.Orders.Contracts;

// =============================================================================
// PUBLIC CONTRACTS (DTOs) — the exact JSON shapes callers send and receive.
// =============================================================================
// These are part of your API's public contract. Keep them stable and explicit.
// Note what's NOT here: no Status, no Id, no CreatedAt on the REQUEST — those are
// decided by the server, never the caller.

// ---- Incoming ----
public sealed record CreateOrderRequest(
    string? CustomerId,
    List<CreateOrderLine>? Lines,
    string? Currency);

public sealed record CreateOrderLine(
    string? Sku,
    int Quantity,
    decimal UnitPrice);

// ---- Outgoing ----
public sealed record OrderResponse(
    string Id,
    string CustomerId,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<OrderLineResponse> Lines);

public sealed record OrderLineResponse(string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);
