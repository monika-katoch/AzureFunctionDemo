using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace DurableFunctionDemo;

// =============================================================================
// CLIENT / STARTER FUNCTIONS — the ordinary HTTP entry points.
// =============================================================================
// Orchestrators aren't called directly over HTTP. A normal function receives the
// request and uses the injected [DurableClient] to START an orchestration and to
// QUERY its status. Starting is ASYNCHRONOUS: we immediately return 202 Accepted
// with an id the caller polls — because the workflow may run for seconds, minutes,
// or days.

public sealed class HttpStartFunctions
{
    // -------------------------------------------------------------------------
    // START: POST /api/dashboard/{userId}
    // -------------------------------------------------------------------------
    [Function(nameof(StartDashboard))]
    public async Task<IActionResult> StartDashboard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "dashboard/{userId}")] HttpRequest req,
        // The Durable extension injects this client for us.
        [DurableClient] DurableTaskClient client,
        string userId,
        FunctionContext ctx)
    {
        var log = ctx.GetLogger(nameof(StartDashboard));

        // Schedule the orchestrator. This returns instantly with an instance id;
        // the actual work runs in the background, survives restarts, and is
        // tracked in storage.
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(OrchestratorFunctions.FetchDashboardOrchestrator),
            input: userId);

        log.LogInformation("Started orchestration {InstanceId} for {UserId}", instanceId, userId);

        // 202 Accepted + where to poll for the result. This is the standard
        // async-workflow HTTP pattern.
        return new AcceptedResult(
            location: $"/api/dashboard/status/{instanceId}",
            value: new
            {
                instanceId,
                statusQueryGetUri = $"/api/dashboard/status/{instanceId}"
            });
    }

    // -------------------------------------------------------------------------
    // STATUS: GET /api/dashboard/status/{instanceId}
    // -------------------------------------------------------------------------
    // Poll this until runtimeStatus is Completed (or Failed). When completed, the
    // assembled dashboard is included as the output.
    [Function(nameof(GetDashboardStatus))]
    public async Task<IActionResult> GetDashboardStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/status/{instanceId}")] HttpRequest req,
        [DurableClient] DurableTaskClient client,
        string instanceId)
    {
        // getInputsAndOutputs: true so we can read the final result when done.
        OrchestrationMetadata? metadata = await client.GetInstanceAsync(instanceId, getInputsAndOutputs: true);

        if (metadata is null)
        {
            return new NotFoundObjectResult(new { error = $"No orchestration with id '{instanceId}'." });
        }

        // Only Completed instances have a meaningful output to deserialize.
        object? output = metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed
            ? metadata.ReadOutputAs<object>()
            : null;

        return new OkObjectResult(new
        {
            instanceId = metadata.InstanceId,
            runtimeStatus = metadata.RuntimeStatus.ToString(),
            createdAt = metadata.CreatedAt,
            lastUpdatedAt = metadata.LastUpdatedAt,
            output
        });
    }
}
