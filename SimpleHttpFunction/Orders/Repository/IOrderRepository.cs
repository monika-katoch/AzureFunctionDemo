using SimpleHttpFunction.Orders.Domain;

namespace SimpleHttpFunction.Orders.Repository;

// =============================================================================
// REPOSITORY ABSTRACTION — where orders are stored.
// =============================================================================
// The function code depends on this INTERFACE, not on any concrete database.
// For this local demo we back it with an in-memory store. In production you'd
// drop in a Cosmos DB / SQL / Table Storage implementation WITHOUT touching the
// functions or service — because state lives outside the (stateless) function.
//
// Q (participant): "Why not just use a static Dictionary in the function?"
// A: Because there is no single function instance. Azure runs many copies; each
//    would have its OWN dictionary, so reads would randomly miss. External,
//    shared storage is the only correct place for state.

public interface IOrderRepository
{
    // Returns the existing order if this idempotency key was already used,
    // otherwise null. This is how we make "create" safe to retry.
    Task<Order?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);

    Task<Order?> GetByIdAsync(string id, CancellationToken ct);

    Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct);

    Task AddAsync(Order order, CancellationToken ct);
}
