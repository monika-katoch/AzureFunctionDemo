using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace HelloDurable;

// =============================================================================
// The simplest possible Durable Functions example: ONE orchestration.
// It shows the three roles every Durable app has — Client, Orchestrator, Activity.
// The orchestrator just calls the activity 3 times in a row ("function chaining").
// =============================================================================

public static class HelloOrchestration
{
    // 1) CLIENT — a normal HTTP function that STARTS the orchestration.
    //    GET http://localhost:7220/api/start
    [Function("StartHello")]
    public static async Task<IActionResult> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "start")] HttpRequest req,
        [DurableClient] DurableTaskClient client)
    {
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync("HelloOrchestrator");
        // Return the built-in status URLs. Open "statusQueryGetUri" to see the result.
        return new OkObjectResult(client.CreateHttpManagementPayload(instanceId));
    }

    // 2) ORCHESTRATOR — the workflow. Calls the activity three times, in order,
    //    and collects the results. (No I/O here — orchestrators must be deterministic.)
    [Function("HelloOrchestrator")]
    public static async Task<List<string>> Orchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        // blocking a thread; the orchestration is unloaded and rescheduled.
        var retryPolicy = new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(2),
            backoffCoefficient: 2.0,
            maxRetryInterval: TimeSpan.FromSeconds(30),
            retryTimeout: TimeSpan.FromMinutes(2));

        var retryOptions = TaskOptions.FromRetryPolicy(retryPolicy);

        var results = new List<string>
        {
            await context.CallActivityAsync<string>("SayHello", "Tokyo",retryOptions),
            await context.CallActivityAsync<string>("SayHello", "London",retryOptions),
            await context.CallActivityAsync<string>("SayHello", "Seattle",retryOptions)
        };
        return results;
    }

    // 3) ACTIVITY — the unit of actual work.
    [Function("SayHello")]
    public static string SayHello([ActivityTrigger] string city)
    {
        Random r = new Random();
        if(r.Next(10) < 4)
        {
            throw new Exception();
        }
        return $"Hello, {city}!";
    }
}
