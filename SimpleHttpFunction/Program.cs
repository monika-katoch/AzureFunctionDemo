using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleHttpFunction.Common.Middleware;
using SimpleHttpFunction.Learning;
using SimpleHttpFunction.Orders.Repository;
using SimpleHttpFunction.Orders.Services;

// =============================================================================
// APPLICATION STARTUP — runs ONCE when the host process boots (a "cold start"),
// not per request. Here we build the DI container and the middleware pipeline
// that every request will flow through.
// =============================================================================

var builder = FunctionsApplication.CreateBuilder(args);

// Enable the ASP.NET Core integration so we can use HttpRequest / IActionResult
// (richer and more familiar than the lower-level HttpRequestData model).
builder.ConfigureFunctionsWebApplication();

// -----------------------------------------------------------------------------
// MIDDLEWARE PIPELINE (order matters — it's an onion, outermost first).
//   1. ExceptionHandling  -> outermost, so it can catch anything below it.
//   2. ApiKeyAuthentication -> gate /api/orders before the function runs.
// -----------------------------------------------------------------------------
builder.UseMiddleware<ExceptionHandlingMiddleware>();
builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();

// -----------------------------------------------------------------------------
// SERVICE REGISTRATION (Dependency Injection).
// Lifetimes explained:
//   Singleton -> one instance for the whole app. Use for stateless, thread-safe
//                services and (here) our in-memory store that must persist.
//   Scoped    -> one instance per function invocation (per request).
//   Transient -> a new instance every time it's requested.
// -----------------------------------------------------------------------------

// Learning track: the simple demo service from S5.
builder.Services.AddSingleton<IGreetingService, GreetingService>();

// Enterprise track: repository is a SINGLETON so the in-memory data survives
// across requests (a real DB client would also typically be a singleton).
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

// The service holds no per-request state -> singleton is fine and cheapest.
builder.Services.AddSingleton<IOrderService, OrderService>();

// Application Insights (telemetry/logging). Harmless locally; lights up in Azure.
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
