using SimpleHttpFunction.Orders.Contracts;
using SimpleHttpFunction.Orders.Domain;

namespace SimpleHttpFunction.Orders.Services;

// Result of a create attempt. WasDuplicate = true means the idempotency key had
// already been used and we returned the ORIGINAL order instead of creating a new
// one — so the caller can respond 200 (already existed) vs 201 (created).
public readonly record struct CreateOrderResult(Order Order, bool WasDuplicate);

public interface IOrderService
{
    Task<CreateOrderResult> CreateAsync(
        CreateOrderRequest request,
        string idempotencyKey,
        CancellationToken ct);

    Task<Order?> GetAsync(string id, CancellationToken ct);

    Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct);
}
