using DurableFunctionDemo.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DurableFunctionDemo;

// =============================================================================
// ACTIVITY FUNCTIONS — the units of ACTUAL work.
// =============================================================================
// An activity is where side effects live: calling an API, querying a DB, doing
// I/O. Unlike the orchestrator, activities MAY be non-deterministic (use clocks,
// randomness, network) — the orchestrator only ever *calls* them and records the
// result. Each activity runs to completion on some worker; if it throws, Durable
// can retry it per the policy defined in the orchestrator.
//
// We simulate three independent data sources. GetOrdersAsync is deliberately
// FLAKY so you can watch the retry policy kick in.

public sealed class ActivityFunctions
{
    // A process-wide counter used ONLY to make the demo's flaky activity fail the
    // first couple of times and then succeed. (In real code the transient error
    // would come from the network, not a counter.)
    private static int _ordersAttempts;

    [Function(nameof(GetProfileAsync))]
    public async Task<ProfileData> GetProfileAsync(
        [ActivityTrigger] string userId, FunctionContext ctx)
    {
        var log = ctx.GetLogger(nameof(GetProfileAsync));
        log.LogInformation("Fetching PROFILE for {UserId}", userId);
        await Task.Delay(300); // pretend this is a network call
        return new ProfileData(userId, "Ada Lovelace", "ada@example.com");
    }

    // FLAKY on purpose: throws the first 2 attempts, succeeds on the 3rd.
    [Function(nameof(GetOrdersAsync))]
    public async Task<OrdersData> GetOrdersAsync(
        [ActivityTrigger] string userId, FunctionContext ctx)
    {
        var log = ctx.GetLogger(nameof(GetOrdersAsync));
        var attempt = Interlocked.Increment(ref _ordersAttempts);
        log.LogInformation("Fetching ORDERS for {UserId} (attempt {Attempt})", userId, attempt);

        await Task.Delay(300);

        if (attempt < 3)
        {
            // A TRANSIENT failure — the kind retries are meant to absorb (a brief
            // timeout, a 503, a throttling response). Throwing here signals the
            // orchestrator's retry policy to wait and try again.
            log.LogWarning("ORDERS source transiently failed on attempt {Attempt}", attempt);
            throw new TimeoutException($"Orders service unavailable (attempt {attempt}).");
        }

        log.LogInformation("ORDERS source recovered on attempt {Attempt}", attempt);
        return new OrdersData(OrderCount: 42, LifetimeValue: 1234.56m);
    }

    [Function(nameof(GetRecommendationsAsync))]
    public async Task<RecommendationsData> GetRecommendationsAsync(
        [ActivityTrigger] string userId, FunctionContext ctx)
    {
        var log = ctx.GetLogger(nameof(GetRecommendationsAsync));
        log.LogInformation("Fetching RECOMMENDATIONS for {UserId}", userId);
        await Task.Delay(300);
        return new RecommendationsData(new[] { "SKU-1", "SKU-2", "SKU-3" });
    }
}
