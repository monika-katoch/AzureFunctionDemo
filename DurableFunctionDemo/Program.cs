using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// =============================================================================
// STARTUP — identical shape to a normal Functions app. Durable Functions needs
// NO special registration here: the Durable extension is wired up automatically
// by the [DurableClient]/[OrchestrationTrigger]/[ActivityTrigger] attributes plus
// the host.json "durableTask" section.
// =============================================================================

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
