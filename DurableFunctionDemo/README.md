# Durable Functions Demo — Parallel Data Fetching

A second project for the workshop, showing **Azure Durable Functions**: stateful
*workflows* built on top of stateless functions. It covers exactly three things:

1. **A parallel data-fetching workflow** (fan-out / fan-in).
2. **The orchestrator function** that coordinates it.
3. **Robust retry policies** for flaky dependencies.

> **Stateless vs. Durable in one line:** a plain HTTP function forgets everything
> after each call. A Durable *orchestration* remembers its progress in storage, so
> a multi-step workflow can run for minutes or days, survive restarts, and resume
> exactly where it stopped.

---

## The three function *types* (this is the mental model)

| Type | Attribute | Role | May do I/O? |
|------|-----------|------|-------------|
| **Client / Starter** | `[DurableClient]` on a normal trigger | Starts orchestrations, queries status | Yes |
| **Orchestrator** | `[OrchestrationTrigger]` | The "conductor" — decides what runs and in what order. **Must be deterministic.** | **No** |
| **Activity** | `[ActivityTrigger]` | Does the actual work (API/DB calls). Retried on failure. | Yes |

Files: [HttpStartFunctions.cs](HttpStartFunctions.cs) · [OrchestratorFunctions.cs](OrchestratorFunctions.cs) · [ActivityFunctions.cs](ActivityFunctions.cs)

---

## The workflow (real use case: assemble a customer dashboard)

One dashboard needs data from three independent sources. Fetching them **in
parallel** means total time ≈ the *slowest* source, not the *sum*.

```
                       ┌────────────► GetProfileAsync ─────────┐
  StartDashboard ──►  Orchestrator ─► GetOrdersAsync (retries) ─┼─► Task.WhenAll ─► CustomerDashboard
    (HTTP 202)         (fan-out)  └──► GetRecommendationsAsync ─┘     (fan-in)
```

- **Fan-out:** the orchestrator calls all three activities *without awaiting* → they run concurrently.
- **Fan-in:** `await Task.WhenAll(...)` waits for all, then merges the results.
- `GetOrdersAsync` is deliberately **flaky** (fails twice, succeeds on the 3rd try)
  to demonstrate the retry policy.

---

## Robust retry policy

Defined in the orchestrator and attached to the flaky activity:

```csharp
var retryPolicy = new RetryPolicy(
    maxNumberOfAttempts: 4,                       // total tries before giving up
    firstRetryInterval: TimeSpan.FromSeconds(2),  // wait before the 1st retry
    backoffCoefficient: 2.0,                       // exponential: 2s, 4s, 8s, ...
    maxRetryInterval: TimeSpan.FromSeconds(30),    // cap so waits don't explode
    retryTimeout: TimeSpan.FromMinutes(2));        // overall deadline
context.CallActivityAsync<OrdersData>(nameof(GetOrdersAsync), userId,
    TaskOptions.FromRetryPolicy(retryPolicy));
```

**Why these knobs:** exponential backoff stops you from hammering a struggling
dependency; the cap keeps individual waits sane; the attempt count + timeout
guarantee the workflow can’t retry forever. Durable performs the waits as
**durable timers** — it isn’t blocking a thread; the orchestration is unloaded and
rescheduled, so waiting costs nothing.

Verified in the console log — backoff between attempts:
`attempt 1 fail → +2s → attempt 2 fail → +4s → attempt 3 succeeds`.

---

## Run it locally

Durable **requires a storage backend**. Locally that’s **Azurite** (the Azure
Storage emulator).

```bash
# Terminal 1 — start Azurite (installed with Azure Functions / VS, or via npm)
npx azurite --silent --location ./__azurite

# Terminal 2 — start this app
cd DurableFunctionDemo
func start --port 7219
```

Then drive it (or use [Workshop.http](Workshop.http)):

```bash
# Start the workflow -> returns an instanceId
ID=$(curl -s -X POST http://localhost:7219/api/dashboard/user-777 | grep -o '"instanceId":"[^"]*"' | cut -d'"' -f4)

# Poll until Completed (watch Terminal 2 for the retry attempts)
curl http://localhost:7219/api/dashboard/status/$ID
```

---

## Anticipated participant questions

- **Why can’t the orchestrator call an API directly?** It runs many times
  (“replay”) to rebuild state, so it must be deterministic. Any I/O, clock, GUID,
  or randomness goes in an **activity**; the orchestrator only calls activities.
- **Isn’t polling clunky?** It’s the honest async model — workflows can outlive an
  HTTP request. In Azure the built-in status endpoint and the Durable dashboard
  give you the same thing.
- **Where is the state stored?** In the storage account (Azurite locally) under the
  task hub `DemoTaskHub` (see `host.json`) — as queues, tables, and history.
- **Does the retry block a thread while waiting?** No. Durable timers persist the
  wait; nothing is running during the backoff interval.
- **Fan-out vs. sequential?** Not awaiting each call immediately is what makes them
  parallel. `await` one-by-one and you get sequential execution instead.

## Going to Azure (beyond local)

- Swap Azurite for a real Storage account (or the Netherite / MSSQL backend).
- Replace the simulated activities with real HTTP/DB calls.
- The retry policy and orchestrator code stay exactly the same.
