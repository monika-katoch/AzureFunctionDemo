namespace DurableFunctionDemo.Models;

// =============================================================================
// The data each activity fetches, and the combined result the orchestrator
// assembles. Everything passed to/from orchestrations and activities must be
// JSON-serializable — Durable persists it to storage between steps.
// =============================================================================

// One data source's payload (e.g. from a microservice or third-party API).
public sealed record ProfileData(string UserId, string DisplayName, string Email);
public sealed record OrdersData(int OrderCount, decimal LifetimeValue);
public sealed record RecommendationsData(IReadOnlyList<string> Skus);

// The fan-in result: three independent fetches merged into one response.
public sealed record CustomerDashboard(
    string UserId,
    ProfileData Profile,
    OrdersData Orders,
    RecommendationsData Recommendations,
    DateTimeOffset AssembledAtUtc);
