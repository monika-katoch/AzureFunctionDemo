using System.Collections.Concurrent;
using SimpleHttpFunction.Orders.Domain;

namespace SimpleHttpFunction.Orders.Repository;

// =============================================================================
// IN-MEMORY REPOSITORY — a demo-only stand-in for a real database.
// =============================================================================
// Registered as a SINGLETON so data survives across requests *within one running
// host process* — good enough to demo on your laptop.
//
// !!! Honest caveat to state out loud in the workshop !!!
//   This deliberately CHEATS on statelessness so the demo is self-contained.
//   In Azure, where multiple instances run, this dictionary would NOT be shared
//   between them and data would appear to come and go. That is precisely WHY
//   real deployments use an external store. The interface (IOrderRepository) is
//   the seam where you'd swap in Cosmos DB / SQL with zero changes elsewhere.

public sealed class InMemoryOrderRepository : IOrderRepository
{
    // ConcurrentDictionary because a stateless function is highly concurrent:
    // many requests can hit this singleton at the same time.
    private readonly ConcurrentDictionary<string, Order> _byId = new();
    private readonly ConcurrentDictionary<string, string> _idByIdempotencyKey = new();

    public Task<Order?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        if (_idByIdempotencyKey.TryGetValue(idempotencyKey, out var id)
            && _byId.TryGetValue(id, out var order))
        {
            return Task.FromResult<Order?>(order);
        }
        return Task.FromResult<Order?>(null);
    }

    public Task<Order?> GetByIdAsync(string id, CancellationToken ct)
        => Task.FromResult(_byId.TryGetValue(id, out var order) ? order : null);

    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Order>>(_byId.Values.ToList());

    public Task AddAsync(Order order, CancellationToken ct)
    {
        _byId[order.Id] = order;
        _idByIdempotencyKey[order.IdempotencyKey] = order.Id;
        return Task.CompletedTask;
    }
}
