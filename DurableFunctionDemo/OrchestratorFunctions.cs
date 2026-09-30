using DurableFunctionDemo.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace DurableFunctionDemo;

// =============================================================================
// ORCHESTRATOR FUNCTION — the workflow "conductor".
// =============================================================================
// The orchestrator does NO real work itself. It decides WHICH activities run,
// in what order, and how their results combine. Durable persists its progress
// after every step, so if the process crashes mid-workflow it RESUMES exactly
// where it left off — that durability is the whole point.
//
// !!! THE GOLDEN RULE: ORCHESTRATOR CODE MUST BE DETERMINISTIC !!!
//   Durable runs this method many times ("replay"), rebuilding state from the
//   saved history. So it must return the same decisions every replay. That means
//   inside an orchestrator you must NOT:
//     - use DateTime.Now / Guid.NewGuid() / random  -> use context.CurrentUtcDateTime
//       and context.NewGuid() instead
//     - do I/O, call APIs, read config, or await non-durable tasks
//     - rely on static state
//   All of that belongs in ACTIVITIES. The orchestrator only calls them.

public sealed class OrchestratorFunctions
{
    [Function(nameof(FetchDashboardOrchestrator))]
    public async Task<CustomerDashboard> FetchDashboardOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // Replay-safe logger: suppresses duplicate log lines emitted during replay.
        var log = context.CreateReplaySafeLogger(nameof(FetchDashboardOrchestrator));

        var userId = context.GetInput<string>() ?? "unknown";
        log.LogInformation("Orchestration started for {UserId}", userId);

        // ---------------------------------------------------------------------
        // ROBUST RETRY POLICY (applied to the flaky Orders activity).
        // ---------------------------------------------------------------------
        // Exponential backoff with a cap — the standard, safe shape:
        //   attempt 1 fails -> wait 2s
        //   attempt 2 fails -> wait 4s   (2 * backoffCoefficient)
        //   attempt 3 fails -> wait 8s   ... capped at maxRetryInterval
        //   up to maxNumberOfAttempts total, and give up after retryTimeout.
        // Backoff prevents hammering a struggling dependency; the cap keeps waits
        // sane; the attempt/timeout limits guarantee the workflow can't retry
        // forever. Durable performs the waits as DURABLE TIMERS — it isn't
        // blocking a thread; the orchestration is unloaded and rescheduled.
        var retryPolicy = new RetryPolicy(
            maxNumberOfAttempts: 4,
            firstRetryInterval: TimeSpan.FromSeconds(2),
            backoffCoefficient: 2.0,
            maxRetryInterval: TimeSpan.FromSeconds(30),
            retryTimeout: TimeSpan.FromMinutes(2));

        var retryOptions = TaskOptions.FromRetryPolicy(retryPolicy);

        // ---------------------------------------------------------------------
        // FAN-OUT: start all three fetches WITHOUT awaiting yet. Because we don't
        // await here, Durable dispatches them to run in PARALLEL on the activity
        // workers. (Awaiting each in turn would run them sequentially instead.)
        // ---------------------------------------------------------------------
        Task<ProfileData> profileTask =
            context.CallActivityAsync<ProfileData>(nameof(ActivityFunctions.GetProfileAsync), userId);

        Task<OrdersData> ordersTask =
            context.CallActivityAsync<OrdersData>(nameof(ActivityFunctions.GetOrdersAsync), userId, retryOptions);

        Task<RecommendationsData> recsTask =
            context.CallActivityAsync<RecommendationsData>(nameof(ActivityFunctions.GetRecommendationsAsync), userId);

        // ---------------------------------------------------------------------
        // FAN-IN: wait for ALL of them, then combine. Total wall-clock time is
        // roughly the SLOWEST activity, not the sum — that's the parallelism win.
        // If any task ultimately fails (after its retries), Task.WhenAll throws
        // and the orchestration is marked Failed.
        // ---------------------------------------------------------------------
        await Task.WhenAll(profileTask, ordersTask, recsTask);

        var dashboard = new CustomerDashboard(
            UserId: userId,
            Profile: profileTask.Result,
            Orders: ordersTask.Result,
            Recommendations: recsTask.Result,
            // Deterministic timestamp — safe inside an orchestrator.
            AssembledAtUtc: new DateTimeOffset(context.CurrentUtcDateTime, TimeSpan.Zero));

        log.LogInformation("Orchestration completed for {UserId}", userId);
        return dashboard;
    }
}
